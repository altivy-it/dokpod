using System.Text.Json;
using ContainerQueryKind = Dokpod.Agent.Contracts.V1.ContainerQueryKind;
using Dokpod.ControlPlane.Application.Agents;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Commands;
using Dokpod.ControlPlane.Application.Environments;
using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.ControlPlane.Application.Images;
using Dokpod.ControlPlane.Application.Queries;
using Dokpod.Domain.Auditing;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Containers;
using Dokpod.Domain.Environments;

namespace Dokpod.ControlPlane.Application.Containers;

public enum ContainerCreationOutcome
{
    Accepted,
    Duplicate,
    ConflictingPayload,
    InvalidSpec,
    RegistryNotAllowed,
    AuthorizationDenied,
    AuthorizationUnavailable,
    EnvironmentNotFound,
    EnvironmentUnavailable,
    AgentOffline,
    NotSupported,
}

public sealed record ContainerCreationResult(
    ContainerCreationOutcome Outcome,
    Guid CommandId,
    string? FailureCode,
    IReadOnlyList<FieldError> Errors,
    string? EquivalentCommand = null);

public sealed record HostObjectName(string Name, string? Kind = null, string? Driver = null);

public sealed record ContainerCreateOptions(
    string Engine,
    string EngineVersion,
    bool? Rootless,
    IReadOnlyList<HostObjectName> Secrets,
    IReadOnlyList<HostObjectName> Networks,
    IReadOnlyList<HostObjectName> Volumes,
    IReadOnlyList<string> AllowedRegistries,
    bool BindMountsAllowed,
    bool Partial);

public sealed record ContainerCreateOptionsResult(ContainerCreationOutcome Outcome, string? FailureCode, ContainerCreateOptions? Options);

/// <summary>
/// Criação de container (spec 013 T012/T013): allowlist validada antes de qualquer efeito, `container:create`
/// por ambiente, registros permitidos por estágio (012), capability 14, cifragem dos valores (no store) e
/// auditoria com o comando equivalente redigido.
/// </summary>
public sealed class ContainerCreationService(
    IEnvironmentAuthorizationDecider authorizationDecider,
    IEnvironmentRegistrationStore registrationStore,
    IAgentSessionStore sessionStore,
    AgentCommandQueueService queueService,
    IAgentHostStore hostStore,
    ImagePullPolicy registryPolicy,
    AgentQueryDispatcher queries,
    TimeProvider timeProvider)
{
    public const string Capability = "ContainerCreate";

    /// <summary>Prazo padrão: prazo de pull da 012 (15 min) + 2 min para create/start.</summary>
    public static readonly TimeSpan DefaultDeadline = TimeSpan.FromMinutes(17);
    public static readonly TimeSpan MaxDeadline = TimeSpan.FromMinutes(30);

    public async Task<ContainerCreationResult> CreateAsync(
        Guid environmentId,
        Guid commandId,
        ContainerCreationRequest request,
        AuthenticatedActor actor,
        string accessToken,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actor);
        if (environmentId == Guid.Empty || commandId == Guid.Empty || correlationId == Guid.Empty || string.IsNullOrWhiteSpace(accessToken))
        {
            throw new ArgumentException("Container creation request is invalid.");
        }

        var now = timeProvider.GetUtcNow();
        var deadline = request.DeadlineUtc?.ToUniversalTime() ?? now + DefaultDeadline;
        var (spec, shapeErrors) = request.ToSpec();
        var errors = new List<FieldError>(shapeErrors);
        if (deadline <= now || deadline > now + MaxDeadline) errors.Add(new FieldError("deadlineUtc", "out_of_range"));
        if (spec is not null) errors.AddRange(spec.Validate());
        if (errors.Count > 0 || spec is null)
        {
            return new ContainerCreationResult(ContainerCreationOutcome.InvalidSpec, commandId, "invalid_spec", errors);
        }

        var authorization = await AuthorizeAsync(environmentId, actor, accessToken, correlationId, cancellationToken).ConfigureAwait(false);
        if (!authorization.Allowed)
        {
            return new ContainerCreationResult(
                authorization.Outcome == AuthorizationDecisionOutcome.Indeterminate ? ContainerCreationOutcome.AuthorizationUnavailable : ContainerCreationOutcome.AuthorizationDenied,
                commandId,
                authorization.FailureCode ?? "authorization_denied",
                []);
        }

        var registration = await registrationStore.GetAsync(environmentId, cancellationToken).ConfigureAwait(false);
        if (registration is null)
        {
            return new ContainerCreationResult(ContainerCreationOutcome.EnvironmentNotFound, commandId, "environment_not_found", []);
        }

        var access = EnvironmentAccessDecision.Evaluate(registration, EnvironmentResourceScopes.CreateContainer);
        if (!access.Allowed)
        {
            return new ContainerCreationResult(ContainerCreationOutcome.EnvironmentUnavailable, commandId, access.Reason, []);
        }

        if (!registryPolicy.Allows(registration.Stage, spec.Image))
        {
            return new ContainerCreationResult(ContainerCreationOutcome.RegistryNotAllowed, commandId, "registry_not_allowed", [new FieldError("image", "registry_not_allowed")]);
        }

        var session = await sessionStore.FindActiveAsync(environmentId, cancellationToken).ConfigureAwait(false);
        if (session is null || session.FencingToken > long.MaxValue)
        {
            return new ContainerCreationResult(ContainerCreationOutcome.AgentOffline, commandId, "agent_disconnected", []);
        }

        var descriptors = await hostStore.GetDescriptorsAsync([environmentId], cancellationToken).ConfigureAwait(false);
        if (!descriptors.TryGetValue(environmentId, out var descriptor) || !descriptor.Capabilities.Contains(Capability))
        {
            return new ContainerCreationResult(ContainerCreationOutcome.NotSupported, commandId, "not_supported", []);
        }

        var engineErrors = spec.Validate(descriptor.EngineKind);
        if (engineErrors.Count > 0)
        {
            return new ContainerCreationResult(ContainerCreationOutcome.InvalidSpec, commandId, "invalid_spec", engineErrors);
        }

        var parameters = new CreateContainerParameters(spec);
        var command = new AgentCommand(
            environmentId,
            commandId,
            AgentCommandKind.CreateContainer,
            string.Empty,
            string.Empty,
            AgentCommandKinds.PayloadHash(AgentCommandKind.CreateContainer, string.Empty, string.Empty, parameters),
            deadline,
            (long)session.FencingToken)
        {
            Parameters = parameters,
        };
        var equivalent = EquivalentCommandRenderer.Render(spec, descriptor.EngineKind);
        var auditEvent = AuditEvent.Create(
                Guid.NewGuid(),
                now,
                correlationId,
                AuditActorKind.User,
                actor.Subject,
                "container.create.requested",
                environmentId,
                AuditOutcome.Succeeded,
                commandId: commandId)
            .WithSubject(spec.Name, new Dictionary<string, string>
            {
                ["command"] = equivalent.Length > AuditEvent.MaxCommandDetailLength ? equivalent[..(AuditEvent.MaxCommandDetailLength - 1)] + "…" : equivalent,
                ["image"] = spec.Image.Canonical.Length > 128 ? spec.Image.Canonical[..127] + "…" : spec.Image.Canonical,
                ["engine"] = descriptor.EngineKind,
            });
        var result = await queueService.EnqueueAsync(command, auditEvent, cancellationToken).ConfigureAwait(false);
        return new ContainerCreationResult(
            result switch
            {
                AgentCommandEnqueueResult.Created => ContainerCreationOutcome.Accepted,
                AgentCommandEnqueueResult.Duplicate => ContainerCreationOutcome.Duplicate,
                _ => ContainerCreationOutcome.ConflictingPayload,
            },
            commandId,
            result == AgentCommandEnqueueResult.ConflictingPayload ? "command_id_conflict" : null,
            [],
            equivalent);
    }

    /// <summary>Dados do formulário (FR-006): engine, segredos, redes e volumes do host por consulta sob demanda (011).</summary>
    public async Task<ContainerCreateOptionsResult> GetOptionsAsync(
        Guid environmentId,
        AuthenticatedActor actor,
        string accessToken,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var authorization = await AuthorizeAsync(environmentId, actor, accessToken, correlationId, cancellationToken).ConfigureAwait(false);
        if (!authorization.Allowed)
        {
            return new ContainerCreateOptionsResult(
                authorization.Outcome == AuthorizationDecisionOutcome.Indeterminate ? ContainerCreationOutcome.AuthorizationUnavailable : ContainerCreationOutcome.AuthorizationDenied,
                authorization.FailureCode ?? "authorization_denied",
                null);
        }

        var registration = await registrationStore.GetAsync(environmentId, cancellationToken).ConfigureAwait(false);
        if (registration is null) return new ContainerCreateOptionsResult(ContainerCreationOutcome.EnvironmentNotFound, "environment_not_found", null);
        var descriptors = await hostStore.GetDescriptorsAsync([environmentId], cancellationToken).ConfigureAwait(false);
        if (!queries.IsConnected(environmentId)) return new ContainerCreateOptionsResult(ContainerCreationOutcome.AgentOffline, "agent_disconnected", null);
        if (!descriptors.TryGetValue(environmentId, out var descriptor) || !descriptor.Capabilities.Contains(Capability))
        {
            return new ContainerCreateOptionsResult(ContainerCreationOutcome.NotSupported, "not_supported", null);
        }

        var secrets = queries.QueryAsync(environmentId, ContainerQueryKind.HostSecrets, string.Empty, null, cancellationToken);
        var networks = queries.QueryAsync(environmentId, ContainerQueryKind.HostNetworks, string.Empty, null, cancellationToken);
        var volumes = queries.QueryAsync(environmentId, ContainerQueryKind.HostVolumes, string.Empty, null, cancellationToken);
        await Task.WhenAll(secrets, networks, volumes).ConfigureAwait(false);
        var volumeJson = Parse(volumes.Result);
        var stage = registration.Stage is null ? "none" : EnvironmentStages.ToValue(registration.Stage.Value);
        return new ContainerCreateOptionsResult(
            ContainerCreationOutcome.Accepted,
            null,
            new ContainerCreateOptions(
                descriptor.EngineKind,
                descriptor.EngineVersion,
                descriptor.EngineRootless,
                Names(Parse(secrets.Result), "secrets"),
                Names(Parse(networks.Result), "networks"),
                Names(volumeJson, "volumes"),
                registryPolicy.AllowedRegistriesByStage.TryGetValue(stage, out var allowed) ? allowed : [],
                volumeJson is { } v && v.TryGetProperty("bindMountsAllowed", out var bind) && bind.ValueKind == JsonValueKind.True,
                new[] { secrets.Result, networks.Result, volumes.Result }.Any(r => r.Kind != QueryOutcomeKind.Ok)));
    }

    private static JsonElement? Parse(QueryOutcome outcome)
    {
        if (outcome.Kind != QueryOutcomeKind.Ok || outcome.Json.Length == 0) return null;
        try
        {
            using var document = JsonDocument.Parse(outcome.Json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<HostObjectName> Names(JsonElement? root, string property)
    {
        if (root is not { } element || !element.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array) return [];
        return array.EnumerateArray()
            .Take(500)
            .Select(item => new HostObjectName(
                item.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty,
                item.TryGetProperty("kind", out var kind) ? kind.GetString() : null,
                item.TryGetProperty("driver", out var driver) ? driver.GetString() : null))
            .Where(item => item.Name.Length is > 0 and <= 128)
            .ToArray();
    }

    private async Task<EnvironmentAuthorizationDecision> AuthorizeAsync(
        Guid environmentId,
        AuthenticatedActor actor,
        string accessToken,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var decision = await authorizationDecider.DecideAsync(
                $"urn:dokpod:environment:{environmentId:D}",
                EnvironmentResourceScopes.CreateContainer,
                actor,
                accessToken,
                correlationId,
                cancellationToken).ConfigureAwait(false);
            return decision.Allowed || !string.IsNullOrWhiteSpace(decision.FailureCode)
                ? decision
                : new EnvironmentAuthorizationDecision(AuthorizationDecisionOutcome.Indeterminate, "authorization_invalid");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return new EnvironmentAuthorizationDecision(AuthorizationDecisionOutcome.Indeterminate, "authorization_unavailable");
        }
    }
}
