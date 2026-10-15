using Dokpod.ControlPlane.Application.Agents;
using Dokpod.ControlPlane.Application.Alerts;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Commands;
using Dokpod.ControlPlane.Application.Environments;
using Dokpod.ControlPlane.Application.Terminal;
using Dokpod.Domain.Alerts;
using Dokpod.Domain.Auditing;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Volumes;

namespace Dokpod.ControlPlane.Application.Volumes;

public enum VolumeRemovalOutcome
{
    Accepted,
    Duplicate,
    ConflictingPayload,
    InvalidName,
    NotFound,
    InUse,
    JustificationRequired,
    JustificationInvalid,
    AuthorizationDenied,
    AuthorizationUnavailable,
    EnvironmentNotFound,
    EnvironmentUnavailable,
    AgentOffline,
    NotSupported,
}

public sealed record VolumeRemovalResult(VolumeRemovalOutcome Outcome, Guid CommandId, string? FailureCode);

/// <summary>
/// Remoção de volume sem uso (spec 018 D-005): `volume:delete`, volume conhecido e sem montagens na projeção,
/// capability do agente, justificativa conforme a política do estágio (spec 016) e fila de comandos durável.
/// O agente confere o uso de novo antes de remover e nunca força.
/// </summary>
public sealed class VolumeRemovalService(
    IEnvironmentAuthorizationDecider authorizationDecider,
    IEnvironmentRegistrationStore registrationStore,
    IAgentSessionStore sessionStore,
    AgentCommandQueueService queueService,
    Dokpod.ControlPlane.Application.Hosts.IAgentHostStore hostStore,
    IVolumeInventoryStore volumes,
    IActionJustificationStore justifications,
    TimeProvider timeProvider)
{
    public async Task<VolumeRemovalResult> RemoveAsync(
        Guid environmentId,
        Guid commandId,
        string name,
        DateTimeOffset deadlineUtc,
        string? justification,
        AuthenticatedActor actor,
        string accessToken,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (environmentId == Guid.Empty || commandId == Guid.Empty || correlationId == Guid.Empty ||
            string.IsNullOrWhiteSpace(accessToken) || deadlineUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Volume removal request is invalid.");
        }

        if (!VolumeNames.IsValid(name)) return new VolumeRemovalResult(VolumeRemovalOutcome.InvalidName, commandId, "volume_name_invalid");

        EnvironmentAuthorizationDecision authorization;
        try
        {
            authorization = await authorizationDecider.DecideAsync(
                $"urn:dokpod:environment:{environmentId:D}", EnvironmentResourceScopes.DeleteVolume, actor, accessToken, correlationId, cancellationToken).ConfigureAwait(false);
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
            return new VolumeRemovalResult(
                authorization.Outcome == AuthorizationDecisionOutcome.Indeterminate ? VolumeRemovalOutcome.AuthorizationUnavailable : VolumeRemovalOutcome.AuthorizationDenied,
                commandId,
                authorization.FailureCode ?? "authorization_denied");
        }

        var registration = await registrationStore.GetAsync(environmentId, cancellationToken).ConfigureAwait(false);
        if (registration is null) return new VolumeRemovalResult(VolumeRemovalOutcome.EnvironmentNotFound, commandId, "environment_not_found");
        var access = EnvironmentAccessDecision.Evaluate(registration, EnvironmentResourceScopes.DeleteVolume);
        if (!access.Allowed) return new VolumeRemovalResult(VolumeRemovalOutcome.EnvironmentUnavailable, commandId, access.Reason);

        // A projeção evita enfileirar o que já se sabe em uso; a decisão final é do agente (D-005).
        var known = await volumes.GetAsync(environmentId, name, cancellationToken).ConfigureAwait(false);
        if (known is null) return new VolumeRemovalResult(VolumeRemovalOutcome.NotFound, commandId, VolumeRemovalCodes.NotFound);
        if (known.Volume.Mounts.Count > 0) return new VolumeRemovalResult(VolumeRemovalOutcome.InUse, commandId, VolumeRemovalCodes.InUse);

        var trimmed = justification?.Trim();
        string? masked = null;
        if (string.IsNullOrEmpty(trimmed))
        {
            var policies = await justifications.GetPoliciesAsync(cancellationToken).ConfigureAwait(false);
            if (registration.Stage is { } stage && policies.TryGetValue(stage, out var required) && required)
            {
                return new VolumeRemovalResult(VolumeRemovalOutcome.JustificationRequired, commandId, "justification_required");
            }
        }
        else if (!ActionJustifications.IsValidLength(trimmed))
        {
            return new VolumeRemovalResult(VolumeRemovalOutcome.JustificationInvalid, commandId, "justification_invalid");
        }
        else
        {
            masked = JustificationMasker.Mask(trimmed);
        }

        var session = await sessionStore.FindActiveAsync(environmentId, cancellationToken).ConfigureAwait(false);
        if (session is null || session.FencingToken > long.MaxValue) return new VolumeRemovalResult(VolumeRemovalOutcome.AgentOffline, commandId, "agent_disconnected");
        var descriptors = await hostStore.GetDescriptorsAsync([environmentId], cancellationToken).ConfigureAwait(false);
        if (!descriptors.TryGetValue(environmentId, out var descriptor) || !descriptor.Capabilities.Contains("VolumeRemove"))
        {
            return new VolumeRemovalResult(VolumeRemovalOutcome.NotSupported, commandId, "not_supported");
        }

        var parameters = new RemoveVolumeParameters(name);
        var command = new AgentCommand(
            environmentId,
            commandId,
            AgentCommandKind.RemoveVolume,
            string.Empty,
            string.Empty,
            AgentCommandKinds.PayloadHash(AgentCommandKind.RemoveVolume, string.Empty, string.Empty, parameters),
            deadlineUtc,
            (long)session.FencingToken)
        {
            Parameters = parameters,
        };
        var details = new Dictionary<string, string> { ["name"] = name };
        if (masked is not null) details["justification"] = masked;
        var auditEvent = AuditEvent.Create(
                Guid.NewGuid(), timeProvider.GetUtcNow(), correlationId, AuditActorKind.User, actor.Subject, "volume.remove", environmentId, AuditOutcome.Succeeded, commandId: commandId)
            .WithActorName(actor.DisplayName)
            .WithSubject(name, details);
        var result = await queueService.EnqueueAsync(command, auditEvent, cancellationToken).ConfigureAwait(false);
        return new VolumeRemovalResult(
            result switch
            {
                AgentCommandEnqueueResult.Created => VolumeRemovalOutcome.Accepted,
                AgentCommandEnqueueResult.Duplicate => VolumeRemovalOutcome.Duplicate,
                _ => VolumeRemovalOutcome.ConflictingPayload,
            },
            commandId,
            result == AgentCommandEnqueueResult.ConflictingPayload ? "command_id_conflict" : null);
    }
}
