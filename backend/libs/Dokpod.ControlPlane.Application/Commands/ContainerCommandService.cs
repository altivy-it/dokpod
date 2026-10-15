using System.Security.Cryptography;
using System.Text;
using Dokpod.ControlPlane.Application.Agents;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Environments;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Auditing;
using Dokpod.Domain.Environments;

namespace Dokpod.ControlPlane.Application.Commands;

public enum ContainerCommandSubmissionOutcome
{
    Accepted,
    Duplicate,
    ConflictingPayload,
    AuthorizationDenied,
    AuthorizationUnavailable,
    EnvironmentNotFound,
    EnvironmentUnavailable,
    AgentOffline,
    JustificationRequired,
    JustificationInvalid,
}

public sealed record ContainerCommandSubmissionResult(
    ContainerCommandSubmissionOutcome Outcome,
    Guid CommandId,
    string? FailureCode);

public sealed class ContainerCommandService(
    IEnvironmentAuthorizationDecider authorizationDecider,
    IEnvironmentRegistrationStore registrationStore,
    IAgentSessionStore sessionStore,
    AgentCommandQueueService queueService,
    Alerts.IActionJustificationStore? justifications = null,
    Inventory.IContainerSearchStore? containers = null,
    TimeProvider? timeProvider = null)
{
    public async Task<ContainerCommandSubmissionResult> SubmitAsync(
        Guid environmentId,
        Guid commandId,
        AgentCommandKind kind,
        string containerId,
        string expectedContainerRevision,
        DateTimeOffset deadlineUtc,
        AuthenticatedActor actor,
        string accessToken,
        Guid correlationId,
        CancellationToken cancellationToken,
        string? justification = null)
    {
        ValidateRequest(
            environmentId,
            commandId,
            kind,
            containerId,
            expectedContainerRevision,
            deadlineUtc,
            actor,
            accessToken,
            correlationId);

        var requiredScope = ScopeFor(kind);
        var authorization = await AuthorizeAsync(
            environmentId,
            requiredScope,
            actor,
            accessToken,
            correlationId,
            cancellationToken).ConfigureAwait(false);
        if (!authorization.Allowed)
        {
            return new ContainerCommandSubmissionResult(
                authorization.Outcome == AuthorizationDecisionOutcome.Indeterminate
                    ? ContainerCommandSubmissionOutcome.AuthorizationUnavailable
                    : ContainerCommandSubmissionOutcome.AuthorizationDenied,
                commandId,
                authorization.FailureCode);
        }

        var registration = await registrationStore.GetAsync(environmentId, cancellationToken)
            .ConfigureAwait(false);
        if (registration is null)
        {
            return new ContainerCommandSubmissionResult(
                ContainerCommandSubmissionOutcome.EnvironmentNotFound,
                commandId,
                "environment_not_found");
        }

        var environmentAccess = EnvironmentAccessDecision.Evaluate(registration, requiredScope);
        if (!environmentAccess.Allowed)
        {
            return new ContainerCommandSubmissionResult(
                ContainerCommandSubmissionOutcome.EnvironmentUnavailable,
                commandId,
                environmentAccess.Reason);
        }

        // Spec 016: parar, pausar e remover exigem justificativa onde a política do estágio manda (FR-015/FR-017).
        var justifiedAction = JustifiedActionFor(kind);
        string? maskedJustification = null;
        if (justifiedAction is not null && justifications is not null)
        {
            var trimmed = justification?.Trim();
            var policies = await justifications.GetPoliciesAsync(cancellationToken).ConfigureAwait(false);
            var required = registration.Stage is { } stage && policies.TryGetValue(stage, out var value) && value;
            if (string.IsNullOrEmpty(trimmed))
            {
                if (required)
                {
                    return new ContainerCommandSubmissionResult(ContainerCommandSubmissionOutcome.JustificationRequired, commandId, "justification_required");
                }
            }
            else if (!Dokpod.Domain.Alerts.ActionJustifications.IsValidLength(trimmed))
            {
                return new ContainerCommandSubmissionResult(ContainerCommandSubmissionOutcome.JustificationInvalid, commandId, "justification_invalid");
            }
            else
            {
                maskedJustification = Terminal.JustificationMasker.Mask(trimmed);
            }
        }

        var session = await sessionStore.FindActiveAsync(environmentId, cancellationToken)
            .ConfigureAwait(false);
        if (session is null || session.FencingToken > long.MaxValue)
        {
            return new ContainerCommandSubmissionResult(
                ContainerCommandSubmissionOutcome.AgentOffline,
                commandId,
                "agent_offline");
        }

        var command = new AgentCommand(
            environmentId,
            commandId,
            kind,
            containerId,
            expectedContainerRevision,
            ComputePayloadHash(kind, containerId, expectedContainerRevision),
            deadlineUtc,
            (long)session.FencingToken);
        var auditEvent = AuditEvent.Create(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            correlationId,
            AuditActorKind.User,
            actor.Subject,
            AuditActionFor(kind),
            environmentId,
            AuditOutcome.Succeeded,
            commandId: commandId);
        if (maskedJustification is not null)
        {
            auditEvent = auditEvent.WithSubject(containerId, new Dictionary<string, string> { ["justification"] = maskedJustification });
        }

        var enqueueResult = await queueService.EnqueueAsync(command, auditEvent, cancellationToken)
            .ConfigureAwait(false);
        if (maskedJustification is not null && justifiedAction is { } action && enqueueResult != AgentCommandEnqueueResult.ConflictingPayload)
        {
            // Depois de Created ou Duplicate: nova tentativa com a mesma chave completa o registro (D-005).
            var name = containers is null ? null : (await containers.GetAsync(environmentId, containerId, cancellationToken).ConfigureAwait(false))?.Name;
            await justifications!.RecordAsync(
                new Alerts.ContainerActionJustification(Guid.NewGuid(), commandId, environmentId, containerId, name, action, actor.Label, maskedJustification, (timeProvider ?? TimeProvider.System).GetUtcNow(), null),
                cancellationToken).ConfigureAwait(false);
        }

        return new ContainerCommandSubmissionResult(
            enqueueResult switch
            {
                AgentCommandEnqueueResult.Created => ContainerCommandSubmissionOutcome.Accepted,
                AgentCommandEnqueueResult.Duplicate => ContainerCommandSubmissionOutcome.Duplicate,
                AgentCommandEnqueueResult.ConflictingPayload => ContainerCommandSubmissionOutcome.ConflictingPayload,
                _ => throw new ArgumentOutOfRangeException(nameof(enqueueResult)),
            },
            commandId,
            enqueueResult == AgentCommandEnqueueResult.ConflictingPayload
                ? "command_id_conflict"
                : null);
    }

    private async Task<EnvironmentAuthorizationDecision> AuthorizeAsync(
        Guid environmentId,
        string requiredScope,
        AuthenticatedActor actor,
        string accessToken,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var authorization = await authorizationDecider.DecideAsync(
                $"urn:dokpod:environment:{environmentId:D}",
                requiredScope,
                actor,
                accessToken,
                correlationId,
                cancellationToken).ConfigureAwait(false);
            return authorization.Allowed || !string.IsNullOrWhiteSpace(authorization.FailureCode)
                ? authorization
                : new EnvironmentAuthorizationDecision(
                    AuthorizationDecisionOutcome.Indeterminate,
                    "authorization_invalid");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return new EnvironmentAuthorizationDecision(
                AuthorizationDecisionOutcome.Indeterminate,
                "authorization_unavailable");
        }
    }

    private static string ScopeFor(AgentCommandKind kind) => kind switch
    {
        AgentCommandKind.StartContainer => EnvironmentResourceScopes.StartContainer,
        AgentCommandKind.StopContainer => EnvironmentResourceScopes.StopContainer,
        AgentCommandKind.RestartContainer => EnvironmentResourceScopes.RestartContainer,
        AgentCommandKind.DeleteContainer => EnvironmentResourceScopes.DeleteContainer,
        AgentCommandKind.PauseContainer or AgentCommandKind.UnpauseContainer => EnvironmentResourceScopes.PauseContainer,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static string AuditActionFor(AgentCommandKind kind) => kind switch
    {
        AgentCommandKind.StartContainer => "container.start",
        AgentCommandKind.StopContainer => "container.stop",
        AgentCommandKind.RestartContainer => "container.restart",
        AgentCommandKind.DeleteContainer => "container.delete",
        AgentCommandKind.PauseContainer => "container.pause",
        AgentCommandKind.UnpauseContainer => "container.unpause",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static string ComputePayloadHash(
        AgentCommandKind kind,
        string containerId,
        string expectedContainerRevision) =>
        AgentCommandKinds.PayloadHash(kind, containerId, expectedContainerRevision, null);

    private static Dokpod.Domain.Alerts.JustifiedAction? JustifiedActionFor(AgentCommandKind kind) => kind switch
    {
        AgentCommandKind.StopContainer => Dokpod.Domain.Alerts.JustifiedAction.Stop,
        AgentCommandKind.PauseContainer => Dokpod.Domain.Alerts.JustifiedAction.Pause,
        AgentCommandKind.DeleteContainer => Dokpod.Domain.Alerts.JustifiedAction.Delete,
        _ => null,
    };

    private static void ValidateRequest(
        Guid environmentId,
        Guid commandId,
        AgentCommandKind kind,
        string containerId,
        string expectedContainerRevision,
        DateTimeOffset deadlineUtc,
        AuthenticatedActor actor,
        string accessToken,
        Guid correlationId)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (environmentId == Guid.Empty || commandId == Guid.Empty || correlationId == Guid.Empty ||
            string.IsNullOrWhiteSpace(accessToken) ||
            kind is < AgentCommandKind.StartContainer or > AgentCommandKind.UnpauseContainer ||
            containerId?.Length != 64 || !containerId.All(Uri.IsHexDigit) ||
            string.IsNullOrWhiteSpace(expectedContainerRevision) ||
            expectedContainerRevision.Length > 255 ||
            deadlineUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Container command request is invalid.");
        }
    }
}