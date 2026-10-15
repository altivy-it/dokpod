using System.Data.Common;
using Dokpod.ControlPlane.Application.Alerts;
using Dokpod.Domain.Alerts;
using Dokpod.Domain.Environments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Dokpod.ControlPlane.Infrastructure;

/// <summary>Política por estágio e justificativas de parar, pausar e remover (spec 016 FR-014 a FR-018).</summary>
public sealed class PostgresActionJustificationStore(ControlPlaneDbContext dbContext) : IActionJustificationStore
{
    private const string Columns = "justification_id, command_id, environment_id, container_id, container_name, action, actor, reason, created_at_utc, ended_at_utc";

    public async Task<IReadOnlyDictionary<EnvironmentStage, bool>> GetPoliciesAsync(CancellationToken cancellationToken)
    {
        var rows = await QueryAsync("SELECT stage, required FROM dokpod.action_justification_policies", [], reader => (Stage: reader.GetString(0), Required: reader.GetBoolean(1)), cancellationToken).ConfigureAwait(false);
        var policies = Enum.GetValues<EnvironmentStage>().ToDictionary(stage => stage, _ => false);
        foreach (var (stage, required) in rows)
        {
            if (EnvironmentStages.TryParse(stage, out var parsed)) policies[parsed] = required;
        }

        return policies;
    }

    public Task SetPolicyAsync(EnvironmentStage stage, bool required, string actor, DateTimeOffset at, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO dokpod.action_justification_policies (stage, required, updated_by, updated_at_utc) VALUES (@stage, @required, @actor, @at)
            ON CONFLICT (stage) DO UPDATE SET required = EXCLUDED.required, updated_by = EXCLUDED.updated_by, updated_at_utc = EXCLUDED.updated_at_utc
            """,
            [P("stage", EnvironmentStages.ToValue(stage)), P("required", required), P("actor", actor), P("at", at)],
            cancellationToken);

    public Task RecordAsync(ContainerActionJustification justification, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO dokpod.container_action_justifications (justification_id, command_id, environment_id, container_id, container_name, action, actor, reason, created_at_utc)
            VALUES (@id, @command, @environment, @container, @name, @action, @actor, @reason, @at)
            ON CONFLICT (command_id) DO NOTHING
            """,
            [
                P("id", justification.JustificationId), P("command", justification.CommandId), P("environment", justification.EnvironmentId),
                P("container", justification.ContainerId), P("name", justification.ContainerName), P("action", ActionJustifications.Value(justification.Action)),
                P("actor", justification.Actor), P("reason", justification.Reason), P("at", justification.CreatedAtUtc),
            ],
            cancellationToken);

    public async Task<IReadOnlyDictionary<(Guid EnvironmentId, string ContainerId), ContainerActionJustification>> GetCurrentAsync(
        IReadOnlyCollection<(Guid EnvironmentId, string ContainerId)> containers,
        CancellationToken cancellationToken)
    {
        if (containers.Count == 0) return new Dictionary<(Guid, string), ContainerActionJustification>();
        var rows = await QueryAsync(
            $"""
            SELECT DISTINCT ON (environment_id, container_id) {Columns}
            FROM dokpod.container_action_justifications
            WHERE ended_at_utc IS NULL AND action IN ('stop', 'pause')
                AND (environment_id, container_id) IN (SELECT * FROM unnest(@environments::uuid[], @containers::text[]))
            ORDER BY environment_id, container_id, created_at_utc DESC
            """,
            [P("environments", containers.Select(c => c.EnvironmentId).ToArray()), P("containers", containers.Select(c => c.ContainerId).ToArray())],
            Read,
            cancellationToken).ConfigureAwait(false);
        return rows.ToDictionary(row => (row.EnvironmentId, row.ContainerId));
    }

    public async Task<IReadOnlyList<ContainerActionJustification>> ListAsync(
        Guid environmentId,
        string? containerId,
        DateTimeOffset? sinceUtc,
        DateTimeOffset? untilUtc,
        JustificationCursor? after,
        int limit,
        CancellationToken cancellationToken) =>
        await QueryAsync(
            $"""
            SELECT {Columns} FROM dokpod.container_action_justifications
            WHERE environment_id = @environment
                AND (@container::text IS NULL OR container_id = @container)
                AND (@since::timestamptz IS NULL OR created_at_utc >= @since)
                AND (@until::timestamptz IS NULL OR created_at_utc < @until)
                AND (@afterAt::timestamptz IS NULL OR (created_at_utc, justification_id) < (@afterAt, @afterId))
            ORDER BY created_at_utc DESC, justification_id DESC
            LIMIT @limit
            """,
            [
                P("environment", environmentId), P("container", containerId), P("since", sinceUtc), P("until", untilUtc),
                P("afterAt", after?.At), P("afterId", after?.JustificationId ?? Guid.Empty), P("limit", limit),
            ],
            Read,
            cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<ContainerActionJustification>> ListOpenAsync(CancellationToken cancellationToken) =>
        await QueryAsync($"SELECT {Columns} FROM dokpod.container_action_justifications WHERE ended_at_utc IS NULL AND action IN ('stop', 'pause')", [], Read, cancellationToken).ConfigureAwait(false);

    public Task EndAsync(Guid justificationId, DateTimeOffset at, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE dokpod.container_action_justifications SET ended_at_utc = @at WHERE justification_id = @id AND ended_at_utc IS NULL",
            [P("id", justificationId), P("at", at)],
            cancellationToken);

    public async Task<bool> HasRecentAsync(Guid environmentId, string containerId, DateTimeOffset sinceUtc, CancellationToken cancellationToken) =>
        (await QueryAsync(
            """
            SELECT EXISTS (SELECT 1 FROM dokpod.container_action_justifications
                WHERE environment_id = @environment AND container_id = @container
                    AND ((ended_at_utc IS NULL AND action IN ('stop', 'pause')) OR created_at_utc >= @since))
            """,
            [P("environment", environmentId), P("container", containerId), P("since", sinceUtc)],
            reader => reader.GetBoolean(0),
            cancellationToken).ConfigureAwait(false))[0];

    private static ContainerActionJustification Read(DbDataReader reader)
    {
        ActionJustifications.TryParse(reader.GetString(5), out var action);
        return new ContainerActionJustification(
            reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4), action,
            reader.GetString(6), reader.GetString(7), reader.GetFieldValue<DateTimeOffset>(8), reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9));
    }

    private static NpgsqlParameter P(string name, object? value) => new(name, value ?? DBNull.Value);

    private async Task<List<T>> QueryAsync<T>(string sql, IReadOnlyList<NpgsqlParameter> parameters, Func<DbDataReader, T> read, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var opened = connection.State != System.Data.ConnectionState.Open;
        if (opened) await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
            foreach (var parameter in parameters) command.Parameters.Add(parameter.Clone());
            var results = new List<T>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) results.Add(read(reader));
            return results;
        }
        finally
        {
            if (opened) await connection.CloseAsync().ConfigureAwait(false);
        }
    }
}
