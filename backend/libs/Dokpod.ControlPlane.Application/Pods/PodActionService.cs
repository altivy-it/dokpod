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
using Dokpod.Domain.Pods;

namespace Dokpod.ControlPlane.Application.Pods;

public enum PodActionOutcome
{
    Accepted,
    Duplicate,
    ConflictingPayload,
    InvalidId,
    NotFound,
    AgentPod,
    JustificationRequired,
    JustificationInvalid,
    AuthorizationDenied,
    AuthorizationUnavailable,
    EnvironmentNotFound,
    EnvironmentUnavailable,
    AgentOffline,
    NotSupported,
}

public sealed record PodActionResult(PodActionOutcome Outcome, Guid CommandId, string? FailureCode);

/// <summary>
/// Ação sobre o pod inteiro (spec 021 D-004): scope de container da ação no host, pod conhecido e fora do "pod do agente"
/// para parar, pausar e reiniciar, capability do agente, justificativa para parar e pausar e fila de comandos durável.
/// </summary>
public sealed class PodActionService(
    IEnvironmentAuthorizationDecider authorizationDecider,
    IEnvironmentRegistrationStore registrationStore,
    IAgentSessionStore sessionStore,
    AgentCommandQueueService queueService,
    Dokpod.ControlPlane.Application.Hosts.IAgentHostStore hostStore,
    IPodInventoryStore pods,
    IActionJustificationStore justifications,
    TimeProvider timeProvider)
{
    public async Task<PodActionResult> ExecuteAsync(
        Guid environmentId,
        Guid commandId,
        string podId,
        PodActionKind action,
        DateTimeOffset deadlineUtc,
        string? justification,
        AuthenticatedActor actor,
        string accessToken,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (environmentId == Guid.Empty || commandId == Guid.Empty || correlationId == Guid.Empty ||
            string.IsNullOrWhiteSpace(accessToken) || deadlineUtc.Offset != TimeSpan.Zero || !Enum.IsDefined(action))
        {
            throw new ArgumentException("Pod action request is invalid.");
        }

        if (!PodNames.IsValidId(podId)) return new PodActionResult(PodActionOutcome.InvalidId, commandId, "pod_invalid");
        var scope = PodNames.ScopeFor(action);
        EnvironmentAuthorizationDecision authorization;
        try
        {
            authorization = await authorizationDecider.DecideAsync(
                $"urn:dokpod:environment:{environmentId:D}", scope, actor, accessToken, correlationId, cancellationToken).ConfigureAwait(false);
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
            return new PodActionResult(
                authorization.Outcome == AuthorizationDecisionOutcome.Indeterminate ? PodActionOutcome.AuthorizationUnavailable : PodActionOutcome.AuthorizationDenied,
                commandId,
                authorization.FailureCode ?? "authorization_denied");
        }

        var registration = await registrationStore.GetAsync(environmentId, cancellationToken).ConfigureAwait(false);
        if (registration is null) return new PodActionResult(PodActionOutcome.EnvironmentNotFound, commandId, "environment_not_found");
        var access = EnvironmentAccessDecision.Evaluate(registration, scope);
        if (!access.Allowed) return new PodActionResult(PodActionOutcome.EnvironmentUnavailable, commandId, access.Reason);

        var known = await pods.GetAsync(environmentId, podId, cancellationToken).ConfigureAwait(false);
        if (known is null || known.Pod.PodId != podId) return new PodActionResult(PodActionOutcome.NotFound, commandId, PodActionCodes.NotFound);
        // FR-005: o pod do agente não é parado, pausado nem reiniciado (o agente confere de novo).
        if (known.Pod.AgentPod && PodNames.RefusedForAgentPod(action)) return new PodActionResult(PodActionOutcome.AgentPod, commandId, PodActionCodes.AgentPod);

        var trimmed = justification?.Trim();
        string? masked = null;
        if (PodNames.RequiresJustification(action))
        {
            if (string.IsNullOrEmpty(trimmed))
            {
                var policies = await justifications.GetPoliciesAsync(cancellationToken).ConfigureAwait(false);
                if (registration.Stage is { } stage && policies.TryGetValue(stage, out var required) && required)
                {
                    return new PodActionResult(PodActionOutcome.JustificationRequired, commandId, "justification_required");
                }
            }
            else if (!ActionJustifications.IsValidLength(trimmed))
            {
                return new PodActionResult(PodActionOutcome.JustificationInvalid, commandId, "justification_invalid");
            }
            else
            {
                masked = JustificationMasker.Mask(trimmed);
            }
        }

        var session = await sessionStore.FindActiveAsync(environmentId, cancellationToken).ConfigureAwait(false);
        if (session is null || session.FencingToken > long.MaxValue) return new PodActionResult(PodActionOutcome.AgentOffline, commandId, "agent_disconnected");
        var descriptors = await hostStore.GetDescriptorsAsync([environmentId], cancellationToken).ConfigureAwait(false);
        if (!descriptors.TryGetValue(environmentId, out var descriptor) || !descriptor.Capabilities.Contains("PodActions"))
        {
            return new PodActionResult(PodActionOutcome.NotSupported, commandId, "not_supported");
        }

        var parameters = new PodActionParameters(known.Pod.PodId, known.Pod.Name, action);
        var command = new AgentCommand(
            environmentId,
            commandId,
            AgentCommandKind.PodAction,
            string.Empty,
            string.Empty,
            AgentCommandKinds.PayloadHash(AgentCommandKind.PodAction, string.Empty, string.Empty, parameters),
            deadlineUtc,
            (long)session.FencingToken)
        {
            Parameters = parameters,
        };
        var details = new Dictionary<string, string> { ["name"] = known.Pod.Name, ["podId"] = known.Pod.PodId };
        if (masked is not null) details["justification"] = masked;
        var auditEvent = AuditEvent.Create(
                Guid.NewGuid(), timeProvider.GetUtcNow(), correlationId, AuditActorKind.User, actor.Subject, PodNames.AuditAction(action), environmentId, AuditOutcome.Succeeded, commandId: commandId)
            .WithActorName(actor.DisplayName)
            .WithSubject(known.Pod.Name, details);
        var result = await queueService.EnqueueAsync(command, auditEvent, cancellationToken).ConfigureAwait(false);
        return new PodActionResult(
            result switch
            {
                AgentCommandEnqueueResult.Created => PodActionOutcome.Accepted,
                AgentCommandEnqueueResult.Duplicate => PodActionOutcome.Duplicate,
                _ => PodActionOutcome.ConflictingPayload,
            },
            commandId,
            result == AgentCommandEnqueueResult.ConflictingPayload ? "command_id_conflict" : null);
    }
}
