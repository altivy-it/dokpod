using Dokpod.ControlPlane.Application.Auditing;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Environments;
using Dokpod.ControlPlane.Application.Licensing;
using Dokpod.Domain.Auditing;
using Dokpod.Domain.Environments;

namespace Dokpod.ControlPlane.Application.ImageUpdates;

public enum ImageUpdateCheckOutcome
{
    Accepted,
    AuthorizationDenied,
    LicenseDenied,
    RateLimited,
    AgentUnsupported,
    EnvironmentDisconnected,
    EnvironmentNotFound,
}

public sealed record ImageUpdateCheckServiceResult(
    ImageUpdateCheckOutcome Outcome, string? Code = null, DateTimeOffset? RequestedAt = null, DateTimeOffset? NextDueAt = null, DateTimeOffset? RetryAt = null);

/// <summary>
/// "Verificar agora" (spec 025 T016, D-006): autoriza <c>image:pull</c> no host pelo Keycloak (indisponível nega),
/// confere o direito comercial pelo gate, adianta o vencimento do host para agora no máximo uma vez por minuto e audita
/// <c>image.update_check_requested</c>. Não muda engine nem inventário, por isso não pede chave idempotente.
/// </summary>
[BusinessCapability(LicenseImageUpdateEntitlement.Capability)]
public sealed class ImageUpdateCheckService(
    IEnvironmentAuthorizationDecider authorizationDecider,
    IEnvironmentRegistrationStore registrations,
    IBusinessCapabilityGate gate,
    IImageUpdateStore store,
    IImageUpdateAgentChannel agents,
    IAuditEventWriter auditWriter,
    TimeProvider timeProvider)
{
    public const string AuditAction = "image.update_check_requested";

    public async Task<ImageUpdateCheckServiceResult> RequestAsync(
        Guid environmentId, AuthenticatedActor actor, string accessToken, Guid correlationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var registration = await registrations.GetAsync(environmentId, cancellationToken).ConfigureAwait(false);
        if (registration is null) return new(ImageUpdateCheckOutcome.EnvironmentNotFound, "environment_not_found");

        EnvironmentAuthorizationDecision authorization;
        try
        {
            authorization = await authorizationDecider.DecideAsync(
                $"urn:dokpod:environment:{environmentId:D}", EnvironmentResourceScopes.PullImage, actor, accessToken, correlationId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            authorization = new EnvironmentAuthorizationDecision(AuthorizationDecisionOutcome.Indeterminate, "authorization_unavailable");
        }

        // Sem autorização, o host não é revelado (404); o Keycloak indisponível também nega (falha fechada).
        if (!authorization.Allowed) return new(ImageUpdateCheckOutcome.AuthorizationDenied, "authorization_denied");

        var now = timeProvider.GetUtcNow();
        var decision = await gate.EnsureAsync(LicenseImageUpdateEntitlement.Capability, cancellationToken).ConfigureAwait(false);
        if (!decision.Allowed)
        {
            await AuditAsync(environmentId, actor, correlationId, now, AuditOutcome.Denied, decision.DenialCode, cancellationToken).ConfigureAwait(false);
            return new(ImageUpdateCheckOutcome.LicenseDenied, decision.DenialCode);
        }

        if (!agents.IsConnected(environmentId)) return await RefusedAsync(ImageUpdateCheckOutcome.EnvironmentDisconnected, "environment_disconnected").ConfigureAwait(false);
        if (!agents.Supports(environmentId)) return await RefusedAsync(ImageUpdateCheckOutcome.AgentUnsupported, "agent_unsupported").ConfigureAwait(false);

        await store.EnsureScheduledAsync([environmentId], now, cancellationToken).ConfigureAwait(false);
        var request = await store.RequestNowAsync(environmentId, now, manual: true, cancellationToken).ConfigureAwait(false);
        if (request.Outcome == ImageUpdateManualOutcome.RateLimited)
        {
            await AuditAsync(environmentId, actor, correlationId, now, AuditOutcome.Denied, "rate_limited", cancellationToken).ConfigureAwait(false);
            return new(ImageUpdateCheckOutcome.RateLimited, "rate_limited", RetryAt: request.RetryAtUtc);
        }

        await AuditAsync(environmentId, actor, correlationId, now, AuditOutcome.Succeeded, null, cancellationToken).ConfigureAwait(false);
        return new(ImageUpdateCheckOutcome.Accepted, RequestedAt: now, NextDueAt: request.NextDueAtUtc);

        async Task<ImageUpdateCheckServiceResult> RefusedAsync(ImageUpdateCheckOutcome outcome, string code)
        {
            await AuditAsync(environmentId, actor, correlationId, now, AuditOutcome.Failed, code, cancellationToken).ConfigureAwait(false);
            return new(outcome, code);
        }
    }

    private Task AuditAsync(Guid environmentId, AuthenticatedActor actor, Guid correlationId, DateTimeOffset now, AuditOutcome outcome, string? failureCode, CancellationToken cancellationToken) =>
        auditWriter.AppendAsync(
            AuditEvent.Create(Guid.NewGuid(), now, correlationId, AuditActorKind.User, actor.Subject, AuditAction, environmentId, outcome, failureCode)
                .WithActorName(actor.DisplayName),
            cancellationToken);
}
