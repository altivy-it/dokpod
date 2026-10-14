using Dokpod.ControlPlane.Application.Commands;
using Dokpod.ControlPlane.Application.Auditing;
using Dokpod.Domain.Auditing;
using Dokpod.Domain.Commands;
using Microsoft.EntityFrameworkCore;

namespace Dokpod.ControlPlane.Infrastructure;

public sealed class PostgresAgentCommandStore(
    ControlPlaneDbContext dbContext,
    IAuditEventWriter auditEventWriter,
    ICommandParameterProtector? protector = null) : IAgentCommandStore
{
    private static readonly System.Text.Json.JsonSerializerOptions ParametersJson = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private const int MaximumDispatchBatchSize = 100;
    private const int MaximumExpirationBatchSize = 100;

    public PostgresAgentCommandStore(ControlPlaneDbContext dbContext)
        : this(dbContext, new PostgresAuditEventWriter(dbContext))
    {
    }

    public async Task<AgentCommandStatusSnapshot?> GetAsync(
        Guid environmentId,
        Guid commandId,
        CancellationToken cancellationToken)
    {
        if (environmentId == Guid.Empty || commandId == Guid.Empty)
        {
            throw new ArgumentException("Command identity is required.");
        }

        var row = await dbContext.AgentCommands
            .AsNoTracking()
            .Where(command =>
                command.EnvironmentId == environmentId &&
                command.CommandId == commandId)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        var parameters = AgentCommandKinds.IsHostTarget(row.Kind)
            ? await LoadParametersAsync(environmentId, [commandId], cancellationToken).ConfigureAwait(false)
            : new Dictionary<Guid, AgentCommandParameters>();
        return new AgentCommandStatusSnapshot(
            row.EnvironmentId,
            row.CommandId,
            row.Kind,
            row.ContainerId ?? string.Empty,
            row.ExpectedContainerRevision ?? string.Empty,
            row.State,
            row.FailureCode,
            row.ObservedContainerRevision,
            row.DeadlineUtc,
            row.CreatedAtUtc,
            row.UpdatedAtUtc,
            row.CompletedAtUtc)
        {
            Output = row.ResolvedImageDigest is null && row.ImageSizeBytes is null && row.CreatedContainerId is null
                ? null
                : new AgentCommandOutput(row.ResolvedImageDigest, row.ImageSizeBytes, row.CreatedContainerId),
            ImageReference = parameters.GetValueOrDefault(commandId) switch
            {
                PullImageParameters pull => pull.Reference.Canonical,
                CreateContainerParameters create => create.Spec.Image.Canonical,
                _ => null,
            },
            ContainerName = parameters.GetValueOrDefault(commandId) is CreateContainerParameters created ? created.Spec.Name : null,
        };
    }

    public async Task<AgentCommandEnqueueResult> EnqueueAsync(
        PersistedAgentCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var agentCommand = command.Command;
        var kind = (short)agentCommand.Kind;
        var state = (short)command.State;
        var hostTarget = AgentCommandKinds.IsHostTarget(agentCommand.Kind);
        string? containerId = hostTarget ? null : agentCommand.ContainerId;
        string? expectedRevision = hostTarget ? null : agentCommand.ExpectedContainerRevision;
        var inserted = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO dokpod.agent_commands (
                environment_id,
                command_id,
                kind,
                container_id,
                expected_container_revision,
                payload_hash,
                deadline_utc,
                fencing_token,
                state,
                created_at_utc,
                updated_at_utc)
            VALUES (
                {agentCommand.EnvironmentId},
                {agentCommand.CommandId},
                {kind},
                {containerId},
                {expectedRevision},
                {agentCommand.PayloadHash},
                {agentCommand.DeadlineUtc},
                {agentCommand.FencingToken},
                {state},
                {command.CreatedAtUtc},
                {command.UpdatedAtUtc})
            ON CONFLICT (environment_id, command_id, created_at_utc) DO NOTHING
            """, cancellationToken).ConfigureAwait(false);

        if (inserted == 1)
        {
            if (agentCommand.Parameters is { } parameters)
            {
                dbContext.AgentCommandParameters.Add(new AgentCommandParametersEntity
                {
                    EnvironmentId = agentCommand.EnvironmentId,
                    CommandId = agentCommand.CommandId,
                    CreatedAtUtc = command.CreatedAtUtc,
                    Kind = ParametersKind(parameters),
                    Parameters = SerializeParameters(parameters),
                    ProtectedValues = ProtectValues(parameters),
                });
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                dbContext.ChangeTracker.Clear();
            }

            return AgentCommandEnqueueResult.Created;
        }

        var persisted = await dbContext.AgentCommands
            .AsNoTracking()
            .SingleAsync(existing =>
                existing.EnvironmentId == agentCommand.EnvironmentId &&
                existing.CommandId == agentCommand.CommandId,
                cancellationToken)
            .ConfigureAwait(false);

        var sameEnvelope = persisted.Kind == agentCommand.Kind &&
            string.Equals(persisted.ContainerId ?? string.Empty, agentCommand.ContainerId, StringComparison.Ordinal) &&
            string.Equals(
                persisted.ExpectedContainerRevision ?? string.Empty,
                agentCommand.ExpectedContainerRevision,
                StringComparison.Ordinal) &&
            string.Equals(persisted.PayloadHash, agentCommand.PayloadHash, StringComparison.Ordinal) &&
            persisted.DeadlineUtc == agentCommand.DeadlineUtc;

        return sameEnvelope
            ? AgentCommandEnqueueResult.Duplicate
            : AgentCommandEnqueueResult.ConflictingPayload;
    }

    public async Task<AgentCommandEnqueueResult> EnqueueAuditedAsync(
        PersistedAgentCommand command,
        AuditEvent auditEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        ValidateAuditEvent(
            auditEvent,
            command.Command.EnvironmentId,
            command.Command.CommandId,
            GetCommandAction(command.Command.Kind));
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        var result = await EnqueueAsync(command, cancellationToken).ConfigureAwait(false);
        if (result == AgentCommandEnqueueResult.Created)
        {
            await auditEventWriter.AppendAsync(auditEvent, cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task<AgentCommandStatusUpdateResult> ApplyStatusAsync(
        AgentCommandStatusUpdate update,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);

        var terminal = update.State is ControlPlaneCommandState.Succeeded or
            ControlPlaneCommandState.Failed or
            ControlPlaneCommandState.Indeterminate;
        var reconcilesExpiredCommand = update.State is ControlPlaneCommandState.Succeeded or
            ControlPlaneCommandState.Failed;
        var completedAtUtc = terminal ? update.UpdatedAtUtc : (DateTimeOffset?)null;
        var updated = await dbContext.AgentCommands
            .Where(command =>
                command.EnvironmentId == update.EnvironmentId &&
                command.CommandId == update.CommandId &&
                ((command.State <= ControlPlaneCommandState.Accepted &&
                    command.State < update.State) ||
                 (reconcilesExpiredCommand &&
                    command.State == ControlPlaneCommandState.Indeterminate &&
                    command.FailureCode == "expired_command")))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(command => command.State, update.State)
                    .SetProperty(command => command.FailureCode, update.FailureCode)
                    .SetProperty(command => command.ObservedContainerRevision, update.ObservedContainerRevision)
                    .SetProperty(command => command.CompletedAtUtc, completedAtUtc)
                    .SetProperty(command => command.ResolvedImageDigest, update.Output == null ? null : update.Output.ResolvedImageDigest)
                    .SetProperty(command => command.ImageSizeBytes, update.Output == null ? null : update.Output.ImageSizeBytes)
                    .SetProperty(command => command.CreatedContainerId, update.Output == null ? null : update.Output.CreatedContainerId)
                    .SetProperty(
                        command => command.UpdatedAtUtc,
                        command => command.UpdatedAtUtc > update.UpdatedAtUtc
                            ? command.UpdatedAtUtc
                            : update.UpdatedAtUtc),
                cancellationToken)
            .ConfigureAwait(false);
        if (updated == 1)
        {
            if (terminal)
            {
                // FR-004: valores protegidos saem do armazenamento assim que o comando termina.
                await PurgeProtectedValuesAsync(update.EnvironmentId, update.CommandId, update.UpdatedAtUtc, cancellationToken).ConfigureAwait(false);
            }

            return AgentCommandStatusUpdateResult.Applied;
        }

        var existing = await dbContext.AgentCommands
            .AsNoTracking()
            .SingleOrDefaultAsync(
                command =>
                    command.EnvironmentId == update.EnvironmentId &&
                    command.CommandId == update.CommandId,
                cancellationToken)
            .ConfigureAwait(false);
        if (existing is null)
        {
            return AgentCommandStatusUpdateResult.NotFound;
        }

        var sameStatus = existing.State == update.State &&
            string.Equals(existing.FailureCode, update.FailureCode, StringComparison.Ordinal) &&
            string.Equals(
                existing.ObservedContainerRevision,
                update.ObservedContainerRevision,
                StringComparison.Ordinal);
        return sameStatus && (!terminal || existing.CompletedAtUtc == completedAtUtc)
                ? AgentCommandStatusUpdateResult.Duplicate
                : AgentCommandStatusUpdateResult.InvalidTransition;
    }

    public async Task<AgentCommandStatusUpdateResult> ApplyStatusAuditedAsync(
        AgentCommandStatusUpdate update,
        AuditEvent auditEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        ValidateAuditEvent(
            auditEvent,
            update.EnvironmentId,
            update.CommandId,
            "container.command.result");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        var result = await ApplyStatusAsync(update, cancellationToken).ConfigureAwait(false);
        if (result == AgentCommandStatusUpdateResult.Applied)
        {
            await auditEventWriter.AppendAsync(auditEvent, cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task<IReadOnlyList<PersistedAgentCommand>> ClaimDispatchableAsync(
        Guid environmentId,
        long activeFencingToken,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (environmentId == Guid.Empty)
        {
            throw new ArgumentException("Environment ID is required.", nameof(environmentId));
        }

        if (activeFencingToken <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(activeFencingToken));
        }

        if (nowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Current time must be UTC.", nameof(nowUtc));
        }

        await ExpireNonTerminalAsync(nowUtc, cancellationToken).ConfigureAwait(false);

        var candidates = await dbContext.AgentCommands
            .AsNoTracking()
            .Where(command =>
                command.EnvironmentId == environmentId &&
                command.State <= ControlPlaneCommandState.Accepted &&
                command.DeadlineUtc > nowUtc &&
                command.LastDispatchFencingToken != activeFencingToken)
            .OrderBy(command => command.CreatedAtUtc)
            .ThenBy(command => command.CommandId)
            .Select(command => new
            {
                command.CommandId,
                command.LastDispatchFencingToken,
            })
            .Take(MaximumDispatchBatchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (candidates.Count == 0)
        {
            return [];
        }

        var claimedIds = new List<Guid>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var updated = await dbContext.AgentCommands
                .Where(command =>
                    command.EnvironmentId == environmentId &&
                    command.CommandId == candidate.CommandId &&
                    command.State <= ControlPlaneCommandState.Accepted &&
                    command.DeadlineUtc > nowUtc &&
                    command.LastDispatchFencingToken == candidate.LastDispatchFencingToken)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(command => command.LastDispatchFencingToken, activeFencingToken)
                        .SetProperty(command => command.UpdatedAtUtc, nowUtc),
                    cancellationToken)
                .ConfigureAwait(false);
            if (updated == 1)
            {
                claimedIds.Add(candidate.CommandId);
            }
        }

        if (claimedIds.Count == 0)
        {
            return [];
        }

        var claimed = await dbContext.AgentCommands
            .AsNoTracking()
            .Where(command =>
                command.EnvironmentId == environmentId &&
                claimedIds.Contains(command.CommandId) &&
                command.State <= ControlPlaneCommandState.Accepted &&
                command.LastDispatchFencingToken == activeFencingToken)
            .OrderBy(command => command.CreatedAtUtc)
            .ThenBy(command => command.CommandId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var hostCommandIds = claimed.Where(command => AgentCommandKinds.IsHostTarget(command.Kind)).Select(command => command.CommandId).ToArray();
        var parameters = hostCommandIds.Length == 0
            ? new Dictionary<Guid, AgentCommandParameters>()
            : await LoadParametersAsync(environmentId, hostCommandIds, cancellationToken).ConfigureAwait(false);
        return claimed.Select(command => new PersistedAgentCommand(
                new Dokpod.Domain.Commands.AgentCommand(
                    command.EnvironmentId,
                    command.CommandId,
                    command.Kind,
                    command.ContainerId ?? string.Empty,
                    command.ExpectedContainerRevision ?? string.Empty,
                    command.PayloadHash,
                    command.DeadlineUtc,
                    activeFencingToken)
                {
                    Parameters = parameters.GetValueOrDefault(command.CommandId),
                },
                command.State,
                command.CreatedAtUtc,
                command.UpdatedAtUtc))
            .ToArray();
    }

    public async Task<int> ExpireNonTerminalAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (nowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Current time must be UTC.", nameof(nowUtc));
        }

        var totalExpired = 0;
        while (true)
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            var candidates = await dbContext.AgentCommands
                .AsNoTracking()
                .Where(command =>
                    command.State <= ControlPlaneCommandState.Accepted &&
                    command.DeadlineUtc <= nowUtc)
                .OrderBy(command => command.DeadlineUtc)
                .ThenBy(command => command.CommandId)
                .Select(command => new ExpirationCandidate(
                    command.EnvironmentId,
                    command.CommandId,
                    command.State,
                    command.LastDispatchFencingToken))
                .Take(MaximumExpirationBatchSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var expiredInBatch = 0;
            foreach (var candidate in candidates)
            {
                var state = candidate.State == ControlPlaneCommandState.Pending &&
                    candidate.LastDispatchFencingToken is null
                        ? ControlPlaneCommandState.Failed
                        : ControlPlaneCommandState.Indeterminate;
                var updated = await dbContext.AgentCommands
                    .Where(command =>
                        command.EnvironmentId == candidate.EnvironmentId &&
                        command.CommandId == candidate.CommandId &&
                        command.State == candidate.State &&
                        command.LastDispatchFencingToken == candidate.LastDispatchFencingToken &&
                        command.DeadlineUtc <= nowUtc)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(command => command.State, state)
                            .SetProperty(command => command.FailureCode, "expired_command")
                            .SetProperty(command => command.CompletedAtUtc, nowUtc)
                            .SetProperty(command => command.UpdatedAtUtc, nowUtc),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (updated == 0)
                {
                    continue;
                }

                var outcome = state == ControlPlaneCommandState.Failed
                    ? AuditOutcome.Failed
                    : AuditOutcome.Indeterminate;
                var auditEvent = AuditEvent.Create(
                    Guid.NewGuid(),
                    nowUtc,
                    Guid.NewGuid(),
                    AuditActorKind.System,
                    "control-plane",
                    "container.command.result",
                    candidate.EnvironmentId,
                    outcome,
                    "expired_command",
                    candidate.CommandId);
                await auditEventWriter.AppendAsync(auditEvent, cancellationToken).ConfigureAwait(false);
                expiredInBatch++;
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            totalExpired += expiredInBatch;
            if (candidates.Count < MaximumExpirationBatchSize || expiredInBatch == 0)
            {
                return totalExpired;
            }
        }
    }

    public async Task<int> PurgeTerminalAsync(DateTimeOffset cutoffUtc, int batchSize, CancellationToken cancellationToken)
    {
        if (cutoffUtc.Offset != TimeSpan.Zero || batchSize is < 1 or > 50_000)
        {
            throw new ArgumentException("Retention cutoff must be UTC and batch size within limits.");
        }

        // Somente Succeeded/Failed: Indeterminate exige reconciliação antes (runbook de comandos).
        // Parâmetros saem por ON DELETE CASCADE; a chave de idempotência é liberada pelo gatilho existente.
        var total = 0;
        while (true)
        {
            var deleted = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                WITH doomed AS (
                    SELECT environment_id, command_id, created_at_utc
                    FROM dokpod.agent_commands
                    WHERE created_at_utc < {cutoffUtc} AND state IN (3, 4)
                    LIMIT {batchSize})
                DELETE FROM dokpod.agent_commands command
                USING doomed
                WHERE command.environment_id = doomed.environment_id
                  AND command.command_id = doomed.command_id
                  AND command.created_at_utc = doomed.created_at_utc
                """, cancellationToken).ConfigureAwait(false);
            total += deleted;
            if (deleted < batchSize)
            {
                return total;
            }
        }
    }

    /// <summary>Zera valores protegidos de comandos terminais (inclusive expirados); idempotente.</summary>
    public async Task<int> PurgeProtectedValuesAsync(CancellationToken cancellationToken) =>
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dokpod.agent_command_parameters p
            SET protected_values = NULL, purged_at = {DateTimeOffset.UtcNow}
            FROM dokpod.agent_commands c
            WHERE p.protected_values IS NOT NULL
              AND c.environment_id = p.environment_id AND c.command_id = p.command_id AND c.created_at_utc = p.created_at_utc
              AND c.state IN (3, 4, 5)
            """, cancellationToken).ConfigureAwait(false);

    private Task<int> PurgeProtectedValuesAsync(Guid environmentId, Guid commandId, DateTimeOffset atUtc, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dokpod.agent_command_parameters
            SET protected_values = NULL, purged_at = {atUtc}
            WHERE environment_id = {environmentId} AND command_id = {commandId} AND protected_values IS NOT NULL
            """, cancellationToken);

    private byte[]? ProtectValues(AgentCommandParameters parameters)
    {
        if (parameters is not CreateContainerParameters create)
        {
            return null;
        }

        var values = create.Spec.LiteralValues();
        if (values.Count == 0)
        {
            return null;
        }

        if (protector is null)
        {
            throw new InvalidOperationException("Command parameter protection is not configured.");
        }

        return protector.Protect(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(values));
    }

    private Dictionary<string, string>? UnprotectValues(AgentCommandParametersEntity row)
    {
        if (row.ProtectedValues is null || row.PurgedAt is not null || protector is null)
        {
            return null;
        }

        var plaintext = protector.TryUnprotect(row.ProtectedValues);
        return plaintext is null ? null : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(plaintext);
    }

    private async Task<Dictionary<Guid, AgentCommandParameters>> LoadParametersAsync(
        Guid environmentId,
        Guid[] commandIds,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.AgentCommandParameters
            .AsNoTracking()
            .Where(parameters => parameters.EnvironmentId == environmentId && commandIds.Contains(parameters.CommandId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var result = new Dictionary<Guid, AgentCommandParameters>();
        foreach (var row in rows)
        {
            var parameters = DeserializeParameters(row.Kind, row.Parameters);
            if (parameters is CreateContainerParameters create && UnprotectValues(row) is { } values)
            {
                // Valores só voltam à memória para a reentrega ao agente; purgados ou sem chave → spec sem valores.
                parameters = new CreateContainerParameters(create.Spec.WithValues(values));
            }

            if (parameters is not null)
            {
                result[row.CommandId] = parameters;
            }
        }

        return result;
    }

    private static string ParametersKind(AgentCommandParameters parameters) => parameters switch
    {
        PullImageParameters => "pull_image",
        CreateContainerParameters => "create_container",
        _ => throw new ArgumentOutOfRangeException(nameof(parameters)),
    };

    private static string SerializeParameters(AgentCommandParameters parameters) => parameters switch
    {
        PullImageParameters pull => System.Text.Json.JsonSerializer.Serialize(new { reference = pull.Reference.Canonical }),
        CreateContainerParameters create => System.Text.Json.JsonSerializer.Serialize(
            Dokpod.ControlPlane.Application.Containers.ContainerCreationRequest.FromSpec(create.Spec.WithoutValues()),
            ParametersJson),
        _ => throw new ArgumentOutOfRangeException(nameof(parameters)),
    };

    private static AgentCommandParameters? DeserializeParameters(string kind, string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return kind switch
        {
            "pull_image" when document.RootElement.TryGetProperty("reference", out var reference) &&
                Dokpod.Domain.Images.ImageReference.TryParse(reference.GetString(), out var parsed) => new PullImageParameters(parsed),
            "create_container" when System.Text.Json.JsonSerializer.Deserialize<Dokpod.ControlPlane.Application.Containers.ContainerCreationRequest>(json, ParametersJson)?.ToSpec().Spec is { } spec =>
                new CreateContainerParameters(spec.WithoutValues()),
            _ => null,
        };
    }

    private sealed record ExpirationCandidate(
        Guid EnvironmentId,
        Guid CommandId,
        ControlPlaneCommandState State,
        long? LastDispatchFencingToken);

    private static void ValidateAuditEvent(
        AuditEvent auditEvent,
        Guid environmentId,
        Guid commandId,
        string action)
    {
        if (auditEvent.EnvironmentId != environmentId ||
            auditEvent.CommandId != commandId ||
            !string.Equals(auditEvent.Action, action, StringComparison.Ordinal))
        {
            throw new ArgumentException("Audit event does not match the command mutation.", nameof(auditEvent));
        }
    }

    private static string GetCommandAction(AgentCommandKind kind) =>
        kind switch
        {
            AgentCommandKind.StartContainer => "container.start",
            AgentCommandKind.StopContainer => "container.stop",
            AgentCommandKind.RestartContainer => "container.restart",
            AgentCommandKind.DeleteContainer => "container.delete",
            AgentCommandKind.PauseContainer => "container.pause",
            AgentCommandKind.UnpauseContainer => "container.unpause",
            AgentCommandKind.PullImage => "image.pull",
            AgentCommandKind.CreateContainer => "container.create.requested",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
}