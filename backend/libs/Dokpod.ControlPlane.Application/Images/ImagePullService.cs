using Dokpod.ControlPlane.Application.Agents;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Commands;
using Dokpod.ControlPlane.Application.Environments;
using Dokpod.Domain.Auditing;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Images;

namespace Dokpod.ControlPlane.Application.Images;

/// <summary>
/// Registros permitidos por estágio; lista vazia = qualquer registro. A configuração `Images:AllowedRegistriesByStage`
/// é o valor inicial e a política editada no console (spec 020) a sobrepõe por estágio.
/// </summary>
public sealed class ImagePullPolicy
{
    private IReadOnlyDictionary<string, string[]> overrides = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string[]> AllowedRegistriesByStage { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Lista efetiva do estágio (`none` = sem estágio); vazia = qualquer registro.</summary>
    public string[] AllowedFor(string stageKey) =>
        Volatile.Read(ref overrides).TryGetValue(stageKey, out var stored) ? stored
        : AllowedRegistriesByStage.TryGetValue(stageKey, out var configured) ? configured.Select(r => r.Trim()).Where(r => r.Length > 0).ToArray()
        : [];

    /// <summary>Verdadeiro quando o estágio tem lista própria salva no console.</summary>
    public bool IsOverridden(string stageKey) => Volatile.Read(ref overrides).ContainsKey(stageKey);

    /// <summary>Troca as listas salvas no console (recarga periódica e alteração local).</summary>
    public void ApplyOverrides(IReadOnlyDictionary<string, string[]> stored) =>
        Volatile.Write(ref overrides, new Dictionary<string, string[]>(stored, StringComparer.OrdinalIgnoreCase));

    public bool Allows(EnvironmentStage? stage, ImageReference reference) => Allows(stage, reference.Registry);

    public bool Allows(EnvironmentStage? stage, string registry)
    {
        var registries = AllowedFor(stage is null ? "none" : EnvironmentStages.ToValue(stage.Value));
        return registries.Length == 0 || registries.Any(allowed => string.Equals(allowed.Trim(), registry, StringComparison.OrdinalIgnoreCase));
    }
}

public enum ImagePullOutcome
{
    Accepted,
    Duplicate,
    ConflictingPayload,
    InvalidReference,
    RegistryNotAllowed,
    AuthorizationDenied,
    AuthorizationUnavailable,
    EnvironmentNotFound,
    EnvironmentUnavailable,
    AgentOffline,
    NotSupported,
}

public sealed record ImagePullResult(ImagePullOutcome Outcome, Guid CommandId, string? FailureCode, string? Reference = null);

/// <summary>Pull por host (spec 012 T009): autoriza `image:pull`, aplica a política de registros e reutiliza a fila de comandos.</summary>
public sealed class ImagePullService(
    IEnvironmentAuthorizationDecider authorizationDecider,
    IEnvironmentRegistrationStore registrationStore,
    IAgentSessionStore sessionStore,
    AgentCommandQueueService queueService,
    Dokpod.ControlPlane.Application.Hosts.IAgentHostStore hostStore,
    ImagePullPolicy policy)
{
    public async Task<ImagePullResult> PullAsync(
        Guid environmentId,
        Guid commandId,
        string reference,
        DateTimeOffset deadlineUtc,
        AuthenticatedActor actor,
        string accessToken,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (environmentId == Guid.Empty || commandId == Guid.Empty || correlationId == Guid.Empty ||
            string.IsNullOrWhiteSpace(accessToken) || deadlineUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Image pull request is invalid.");
        }

        if (!ImageReference.TryParse(reference, out var parsed))
        {
            return new ImagePullResult(ImagePullOutcome.InvalidReference, commandId, "invalid_reference");
        }

        EnvironmentAuthorizationDecision authorization;
        try
        {
            authorization = await authorizationDecider.DecideAsync(
                $"urn:dokpod:environment:{environmentId:D}",
                EnvironmentResourceScopes.PullImage,
                actor,
                accessToken,
                correlationId,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            authorization = new EnvironmentAuthorizationDecision(AuthorizationDecisionOutcome.Indeterminate, "authorization_unavailable");
        }

        if (!authorization.Allowed)
        {
            return new ImagePullResult(
                authorization.Outcome == AuthorizationDecisionOutcome.Indeterminate
                    ? ImagePullOutcome.AuthorizationUnavailable
                    : ImagePullOutcome.AuthorizationDenied,
                commandId,
                authorization.FailureCode ?? "authorization_denied");
        }

        var registration = await registrationStore.GetAsync(environmentId, cancellationToken).ConfigureAwait(false);
        if (registration is null)
        {
            return new ImagePullResult(ImagePullOutcome.EnvironmentNotFound, commandId, "environment_not_found");
        }

        var access = EnvironmentAccessDecision.Evaluate(registration, EnvironmentResourceScopes.PullImage);
        if (!access.Allowed)
        {
            return new ImagePullResult(ImagePullOutcome.EnvironmentUnavailable, commandId, access.Reason);
        }

        if (!policy.Allows(registration.Stage, parsed))
        {
            return new ImagePullResult(ImagePullOutcome.RegistryNotAllowed, commandId, "registry_not_allowed", parsed.Canonical);
        }

        var session = await sessionStore.FindActiveAsync(environmentId, cancellationToken).ConfigureAwait(false);
        if (session is null || session.FencingToken > long.MaxValue)
        {
            return new ImagePullResult(ImagePullOutcome.AgentOffline, commandId, "agent_disconnected");
        }

        var descriptors = await hostStore.GetDescriptorsAsync([environmentId], cancellationToken).ConfigureAwait(false);
        if (!descriptors.TryGetValue(environmentId, out var descriptor) || !descriptor.Capabilities.Contains("ImagePull"))
        {
            return new ImagePullResult(ImagePullOutcome.NotSupported, commandId, "not_supported");
        }

        var parameters = new PullImageParameters(parsed);
        var command = new AgentCommand(
            environmentId,
            commandId,
            AgentCommandKind.PullImage,
            string.Empty,
            string.Empty,
            AgentCommandKinds.PayloadHash(AgentCommandKind.PullImage, string.Empty, string.Empty, parameters),
            deadlineUtc,
            (long)session.FencingToken)
        {
            Parameters = parameters,
        };
        var auditEvent = AuditEvent.Create(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            correlationId,
            AuditActorKind.User,
            actor.Subject,
            "image.pull",
            environmentId,
            AuditOutcome.Succeeded,
            commandId: commandId);
        var result = await queueService.EnqueueAsync(command, auditEvent, cancellationToken).ConfigureAwait(false);
        return new ImagePullResult(
            result switch
            {
                AgentCommandEnqueueResult.Created => ImagePullOutcome.Accepted,
                AgentCommandEnqueueResult.Duplicate => ImagePullOutcome.Duplicate,
                _ => ImagePullOutcome.ConflictingPayload,
            },
            commandId,
            result == AgentCommandEnqueueResult.ConflictingPayload ? "command_id_conflict" : null,
            parsed.Canonical);
    }
}
