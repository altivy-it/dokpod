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
using Dokpod.Domain.Networks;

namespace Dokpod.ControlPlane.Application.Networks;

public enum NetworkRemovalOutcome
{
    Accepted,
    Duplicate,
    ConflictingPayload,
    InvalidId,
    NotFound,
    InUse,
    System,
    NotLocal,
    JustificationRequired,
    JustificationInvalid,
    AuthorizationDenied,
    AuthorizationUnavailable,
    EnvironmentNotFound,
    EnvironmentUnavailable,
    AgentOffline,
    NotSupported,
}

public sealed record NetworkRemovalResult(NetworkRemovalOutcome Outcome, Guid CommandId, string? FailureCode);

/// <summary>
/// Remoção de rede sem uso (spec 019 D-004): `network:delete`, rede conhecida, local, fora do sistema e sem conexões na
/// projeção, capability do agente, justificativa conforme a política do estágio (spec 016) e fila de comandos durável.
/// O agente confere de novo antes de remover e nunca desconecta containers.
/// </summary>
public sealed class NetworkRemovalService(
    IEnvironmentAuthorizationDecider authorizationDecider,
    IEnvironmentRegistrationStore registrationStore,
    IAgentSessionStore sessionStore,
    AgentCommandQueueService queueService,
    Dokpod.ControlPlane.Application.Hosts.IAgentHostStore hostStore,
    INetworkInventoryStore networks,
    IActionJustificationStore justifications,
    TimeProvider timeProvider)
{
    public async Task<NetworkRemovalResult> RemoveAsync(
        Guid environmentId,
        Guid commandId,
        string networkId,
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
            throw new ArgumentException("Network removal request is invalid.");
        }

        if (!NetworkNames.IsValidId(networkId)) return new NetworkRemovalResult(NetworkRemovalOutcome.InvalidId, commandId, "network_invalid");

        EnvironmentAuthorizationDecision authorization;
        try
        {
            authorization = await authorizationDecider.DecideAsync(
                $"urn:dokpod:environment:{environmentId:D}", EnvironmentResourceScopes.DeleteNetwork, actor, accessToken, correlationId, cancellationToken).ConfigureAwait(false);
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
            return new NetworkRemovalResult(
                authorization.Outcome == AuthorizationDecisionOutcome.Indeterminate ? NetworkRemovalOutcome.AuthorizationUnavailable : NetworkRemovalOutcome.AuthorizationDenied,
                commandId,
                authorization.FailureCode ?? "authorization_denied");
        }

        var registration = await registrationStore.GetAsync(environmentId, cancellationToken).ConfigureAwait(false);
        if (registration is null) return new NetworkRemovalResult(NetworkRemovalOutcome.EnvironmentNotFound, commandId, "environment_not_found");
        var access = EnvironmentAccessDecision.Evaluate(registration, EnvironmentResourceScopes.DeleteNetwork);
        if (!access.Allowed) return new NetworkRemovalResult(NetworkRemovalOutcome.EnvironmentUnavailable, commandId, access.Reason);

        // A projeção evita enfileirar o que já se sabe recusado; a decisão final é do agente (D-004).
        var known = await networks.GetAsync(environmentId, networkId, cancellationToken).ConfigureAwait(false);
        if (known is null) return new NetworkRemovalResult(NetworkRemovalOutcome.NotFound, commandId, NetworkRemovalCodes.NotFound);
        var network = known.Network;
        if (network.System) return new NetworkRemovalResult(NetworkRemovalOutcome.System, commandId, NetworkRemovalCodes.System);
        if (!network.Local) return new NetworkRemovalResult(NetworkRemovalOutcome.NotLocal, commandId, NetworkRemovalCodes.NotLocal);
        if (network.InUse) return new NetworkRemovalResult(NetworkRemovalOutcome.InUse, commandId, NetworkRemovalCodes.InUse);

        var trimmed = justification?.Trim();
        string? masked = null;
        if (string.IsNullOrEmpty(trimmed))
        {
            var policies = await justifications.GetPoliciesAsync(cancellationToken).ConfigureAwait(false);
            if (registration.Stage is { } stage && policies.TryGetValue(stage, out var required) && required)
            {
                return new NetworkRemovalResult(NetworkRemovalOutcome.JustificationRequired, commandId, "justification_required");
            }
        }
        else if (!ActionJustifications.IsValidLength(trimmed))
        {
            return new NetworkRemovalResult(NetworkRemovalOutcome.JustificationInvalid, commandId, "justification_invalid");
        }
        else
        {
            masked = JustificationMasker.Mask(trimmed);
        }

        var session = await sessionStore.FindActiveAsync(environmentId, cancellationToken).ConfigureAwait(false);
        if (session is null || session.FencingToken > long.MaxValue) return new NetworkRemovalResult(NetworkRemovalOutcome.AgentOffline, commandId, "agent_disconnected");
        var descriptors = await hostStore.GetDescriptorsAsync([environmentId], cancellationToken).ConfigureAwait(false);
        if (!descriptors.TryGetValue(environmentId, out var descriptor) || !descriptor.Capabilities.Contains("NetworkRemove"))
        {
            return new NetworkRemovalResult(NetworkRemovalOutcome.NotSupported, commandId, "not_supported");
        }

        var parameters = new RemoveNetworkParameters(network.NetworkId, network.Name);
        var command = new AgentCommand(
            environmentId,
            commandId,
            AgentCommandKind.RemoveNetwork,
            string.Empty,
            string.Empty,
            AgentCommandKinds.PayloadHash(AgentCommandKind.RemoveNetwork, string.Empty, string.Empty, parameters),
            deadlineUtc,
            (long)session.FencingToken)
        {
            Parameters = parameters,
        };
        var details = new Dictionary<string, string> { ["name"] = network.Name, ["networkId"] = network.NetworkId };
        if (masked is not null) details["justification"] = masked;
        var auditEvent = AuditEvent.Create(
                Guid.NewGuid(), timeProvider.GetUtcNow(), correlationId, AuditActorKind.User, actor.Subject, "network.remove", environmentId, AuditOutcome.Succeeded, commandId: commandId)
            .WithActorName(actor.DisplayName)
            .WithSubject(network.Name, details);
        var result = await queueService.EnqueueAsync(command, auditEvent, cancellationToken).ConfigureAwait(false);
        return new NetworkRemovalResult(
            result switch
            {
                AgentCommandEnqueueResult.Created => NetworkRemovalOutcome.Accepted,
                AgentCommandEnqueueResult.Duplicate => NetworkRemovalOutcome.Duplicate,
                _ => NetworkRemovalOutcome.ConflictingPayload,
            },
            commandId,
            result == AgentCommandEnqueueResult.ConflictingPayload ? "command_id_conflict" : null);
    }
}
