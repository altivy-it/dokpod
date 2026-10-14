using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Environments;
using Dokpod.ControlPlane.Application.Realtime;
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

/// <summary>Altera o estágio de um ambiente (spec 008 FR-002); mesmo estágio é idempotente e não audita.</summary>
public sealed class EnvironmentStageService(
    IEnvironmentRegistrationStore registrationStore,
    IEnvironmentStageStore stageStore,
    EnvironmentAccessService accessService,
    IEnvironmentAuthorizationDecider authorizationDecider,
    IControlPlaneNotifier notifier)
{
    public async Task<StageChangeResult> ChangeAsync(
        Guid environmentId,
        EnvironmentStage? stage,
        AuthenticatedActor actor,
        string accessToken,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var current = await registrationStore.GetAsync(environmentId, cancellationToken).ConfigureAwait(false);
        if (current is not null && current.Stage == stage)
        {
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
                return new StageChangeResult(StageChangeOutcome.Unavailable, null, "authorization_unavailable");
            }

            return decision.Allowed
                ? new StageChangeResult(StageChangeOutcome.Unchanged, stage, null)
                : new StageChangeResult(
                    decision.Outcome == AuthorizationDecisionOutcome.Indeterminate ? StageChangeOutcome.Unavailable : StageChangeOutcome.Denied,
                    null,
                    decision.FailureCode ?? "authorization_denied");
        }

        var access = await accessService.ChangeStageAsync(environmentId, actor, accessToken, correlationId, cancellationToken)
            .ConfigureAwait(false);
        if (!access.Allowed)
        {
            return new StageChangeResult(
                access.Outcome == AuthorizationDecisionOutcome.Indeterminate ? StageChangeOutcome.Unavailable : StageChangeOutcome.Denied,
                null,
                access.FailureCode);
        }

        var (found, previous) = await stageStore.SetStageAsync(environmentId, stage, cancellationToken).ConfigureAwait(false);
        if (!found) return new StageChangeResult(StageChangeOutcome.NotFound, null, "environment_not_found");
        await notifier.NotifyAsync(environmentId, InvalidationKinds.Host, cancellationToken).ConfigureAwait(false);
        return new StageChangeResult(StageChangeOutcome.Changed, previous, null);
    }
}
