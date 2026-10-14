using Dokpod.ControlPlane.Application.Auditing;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Environments;
using Dokpod.ControlPlane.Application.Realtime;
using Dokpod.Domain.Auditing;
using Dokpod.Domain.Environments;

namespace Dokpod.ControlPlane.Application.Hosts;

public enum StageChangeOutcome
{
    Changed,
    Unchanged,
    NotFound,
    Denied,
    Unavailable,
}

public sealed record StageChangeResult(StageChangeOutcome Outcome, EnvironmentStage? Previous, string? FailureCode);

/// <summary>
/// Altera o estágio de um ambiente (spec 008 FR-002/T015). Autoriza antes de consultar o ambiente
/// (não revela existência), audita negações e, somente após a mudança efetiva, audita sucesso com o
/// estágio anterior e o novo. Mesmo estágio é idempotente e não audita.
/// </summary>
public sealed class EnvironmentStageService(
    IEnvironmentRegistrationStore registrationStore,
    IEnvironmentStageStore stageStore,
    IEnvironmentAuthorizationDecider authorizationDecider,
    IAuditEventWriter auditEventWriter,
    IControlPlaneNotifier notifier)
{
    public const string AuditAction = "environment.stage_changed";
    private const string NoStage = "none";

    public async Task<StageChangeResult> ChangeAsync(
        Guid environmentId,
        EnvironmentStage? stage,
        AuthenticatedActor actor,
        string accessToken,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        EnvironmentAuthorizationDecision decision;
        try
        {
            decision = await authorizationDecider.DecideAsync(
                    $"urn:dokpod:environment:{environmentId:D}", EnvironmentResourceScopes.Manage, actor, accessToken, correlationId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            decision = new EnvironmentAuthorizationDecision(AuthorizationDecisionOutcome.Indeterminate, "authorization_unavailable");
        }

        if (!decision.Allowed)
        {
            var indeterminate = decision.Outcome != AuthorizationDecisionOutcome.Denied;
            var failureCode = decision.FailureCode ?? (indeterminate ? "authorization_unavailable" : "authorization_denied");
            await AuditAsync(environmentId, actor, correlationId, indeterminate ? AuditOutcome.Indeterminate : AuditOutcome.Denied, failureCode, null, cancellationToken)
                .ConfigureAwait(false);
            return new StageChangeResult(indeterminate ? StageChangeOutcome.Unavailable : StageChangeOutcome.Denied, null, failureCode);
        }

        var current = await registrationStore.GetAsync(environmentId, cancellationToken).ConfigureAwait(false);
        if (current is null) return new StageChangeResult(StageChangeOutcome.NotFound, null, "environment_not_found");
        if (current.Stage == stage) return new StageChangeResult(StageChangeOutcome.Unchanged, stage, null);

        var (found, previous) = await stageStore.SetStageAsync(environmentId, stage, cancellationToken).ConfigureAwait(false);
        if (!found) return new StageChangeResult(StageChangeOutcome.NotFound, null, "environment_not_found");

        await AuditAsync(
                environmentId,
                actor,
                correlationId,
                AuditOutcome.Succeeded,
                null,
                new Dictionary<string, string>
                {
                    ["previousStage"] = previous is null ? NoStage : EnvironmentStages.ToValue(previous.Value),
                    ["stage"] = stage is null ? NoStage : EnvironmentStages.ToValue(stage.Value),
                },
                cancellationToken)
            .ConfigureAwait(false);
        await notifier.NotifyAsync(environmentId, InvalidationKinds.Host, cancellationToken).ConfigureAwait(false);
        return new StageChangeResult(StageChangeOutcome.Changed, previous, null);
    }

    private Task AuditAsync(
        Guid environmentId,
        AuthenticatedActor actor,
        Guid correlationId,
        AuditOutcome outcome,
        string? failureCode,
        IReadOnlyDictionary<string, string>? details,
        CancellationToken cancellationToken)
    {
        var auditEvent = AuditEvent.Create(
            Guid.NewGuid(), DateTimeOffset.UtcNow, correlationId, AuditActorKind.User, actor.Subject, AuditAction, environmentId, outcome, failureCode);
        if (details is not null) auditEvent = auditEvent.WithSubject(environmentId.ToString("D"), details);
        return auditEventWriter.AppendAsync(auditEvent, cancellationToken);
    }
}
