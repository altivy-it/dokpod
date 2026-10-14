using Dokpod.ControlPlane.Application.Agents;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Commands;
using Dokpod.ControlPlane.Application.Environments;
using Dokpod.Domain.Auditing;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Images;

namespace Dokpod.ControlPlane.Application.Images;

/// <summary>Registros permitidos por estágio (`Images:AllowedRegistriesByStage`); lista vazia = qualquer registro.</summary>
public sealed class ImagePullPolicy
{
    public Dictionary<string, string[]> AllowedRegistriesByStage { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public bool Allows(EnvironmentStage? stage, ImageReference reference)
    {
        var key = stage is null ? "none" : EnvironmentStages.ToValue(stage.Value);
        if (!AllowedRegistriesByStage.TryGetValue(key, out var registries) || registries.Length == 0)
        {
            return true;
        }

        return registries.Any(registry => string.Equals(registry.Trim(), reference.Registry, StringComparison.OrdinalIgnoreCase));
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
