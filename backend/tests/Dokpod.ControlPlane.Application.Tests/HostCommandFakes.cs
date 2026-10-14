using Dokpod.ControlPlane.Application.Agents;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Commands;
using Dokpod.ControlPlane.Application.Environments;
using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.Domain.Auditing;
using Dokpod.Domain.Environments;

namespace Dokpod.ControlPlane.Application.Tests;

// Fakes compartilhados pelos testes de comandos com alvo de host (specs 012 e 013).
internal sealed class Decider(AuthorizationDecisionOutcome outcome) : IEnvironmentAuthorizationDecider
{
    public string? Scope { get; private set; }

    public Task<EnvironmentAuthorizationDecision> DecideAsync(string resource, string scope, AuthenticatedActor actor, string accessToken, Guid correlationId, CancellationToken cancellationToken)
    {
        Scope = scope;
        return Task.FromResult(new EnvironmentAuthorizationDecision(outcome, outcome == AuthorizationDecisionOutcome.Allowed ? null : "access_denied"));
    }
}

internal sealed class Registrations(EnvironmentStage? stage) : IEnvironmentRegistrationStore
{
    public Task<EnvironmentRegistrationPage> ListAsync(Guid? afterEnvironmentId, int limit, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<EnvironmentRegistrationStoreResult> CreateAsync(EnvironmentRegistration registration, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<EnvironmentRegistration?> GetAsync(Guid environmentId, CancellationToken cancellationToken) =>
        Task.FromResult<EnvironmentRegistration?>(EnvironmentRegistration.Create(environmentId, "lab", "lab-host", true, EnvironmentResourceScopes.Supported, stage));
}

internal sealed class Sessions(AgentSession? session) : IAgentSessionStore
{
    public ValueTask<AgentSession> ActivateAsync(Guid environmentId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public ValueTask<AgentSession?> FindActiveAsync(Guid environmentId, CancellationToken cancellationToken) => ValueTask.FromResult(session);
    public ValueTask InvalidateEnvironmentAsync(Guid environmentId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public bool IsActive(AgentSession candidate) => candidate == session;
    public Task WaitUntilInactiveAsync(AgentSession candidate, CancellationToken cancellationToken) => throw new NotSupportedException();
    public ValueTask DeactivateAsync(AgentSession candidate, CancellationToken cancellationToken) => throw new NotSupportedException();
}

internal sealed class FakeHostStore(string[] capabilities, Guid EnvironmentId, string Engine = "docker") : IAgentHostStore
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    public Task UpsertDescriptorAsync(AgentDescriptor descriptor, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool?> SetConnectionAsync(Guid environmentId, bool connected, DateTimeOffset atUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task TouchLastSeenAsync(Guid environmentId, DateTimeOffset atUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<IReadOnlyDictionary<Guid, AgentDescriptor>> GetDescriptorsAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, AgentDescriptor>>(new Dictionary<Guid, AgentDescriptor>
        {
            [EnvironmentId] = new(EnvironmentId, Engine, "27.3.1", "1.47", false, "linux", "6", "amd64", "1.0", capabilities, "lab", 4, 1L << 33, Now),
        });
    public Task<IReadOnlyDictionary<Guid, AgentConnectionState>> GetConnectionsAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<IReadOnlyList<Guid>> ListConnectedAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
}

internal sealed class Store(AgentCommandEnqueueResult result) : IAgentCommandStore
{
    public PersistedAgentCommand? Command { get; private set; }
    public AuditEvent? AuditEvent { get; private set; }

    public Task<AgentCommandStatusSnapshot?> GetAsync(Guid environmentId, Guid commandId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<AgentCommandEnqueueResult> EnqueueAsync(PersistedAgentCommand command, CancellationToken cancellationToken)
    {
        Command = command;
        return Task.FromResult(result);
    }

    public Task<AgentCommandEnqueueResult> EnqueueAuditedAsync(PersistedAgentCommand command, AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        AuditEvent = auditEvent;
        return EnqueueAsync(command, cancellationToken);
    }

    public Task<AgentCommandStatusUpdateResult> ApplyStatusAsync(AgentCommandStatusUpdate update, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<AgentCommandStatusUpdateResult> ApplyStatusAuditedAsync(AgentCommandStatusUpdate update, AuditEvent auditEvent, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<int> ExpireNonTerminalAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<IReadOnlyList<PersistedAgentCommand>> ClaimDispatchableAsync(Guid environmentId, long activeFencingToken, DateTimeOffset nowUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
}

internal sealed class Delivery : IAgentCommandDeliveryQueue
{
    public ValueTask EnqueueAsync(PersistedAgentCommand command, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    public ValueTask<PersistedAgentCommand> DequeueAsync(Guid environmentId, CancellationToken cancellationToken) => throw new NotSupportedException();
}
