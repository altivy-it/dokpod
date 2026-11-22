using Dokpod.ControlPlane.Application.Auditing;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.ChangeWindows;
using Dokpod.ControlPlane.Application.Commands;
using Dokpod.Domain.Auditing;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Environments;

namespace Dokpod.ControlPlane.Application.Tests.ChangeWindows;

/// <summary>
/// Guard da CE para os testes dos serviços de ação (spec 026 T008): sem janela, ele libera sem consultar a fila, o
/// Keycloak nem a trilha, então as dependências falham se forem chamadas.
/// </summary>
internal static class ChangeWindowTestGuard
{
    public static ChangeWindowGuard Unrestricted() =>
        new(new NoChangeWindows(), new UnusedCommandStore(), new UnusedDecider(), new UnusedAuditWriter(), TimeProvider.System);

    /// <summary>Liberação de um comando sem janela, para os testes que chamam a fila diretamente.</summary>
    public static ChangeWindowGuard.Clearance ClearanceFor(AgentCommand command) =>
        Unrestricted().EvaluateCommandAsync(
                EnvironmentRegistration.Create(command.EnvironmentId, "env", "host", true, [EnvironmentResourceScopes.Read]),
                command.Kind,
                command.CommandId,
                "container.start",
                null,
                AuthenticatedActor.FromSubject("user-1"),
                "token",
                Guid.NewGuid(),
                null,
                CancellationToken.None)
            .GetAwaiter()
            .GetResult()
            .Clearance!;

    private sealed class UnusedDecider : IEnvironmentAuthorizationDecider
    {
        public Task<EnvironmentAuthorizationDecision> DecideAsync(string resource, string scope, AuthenticatedActor actor, string accessToken, Guid correlationId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Sem janela, o guard não consulta o Keycloak.");
    }

    private sealed class UnusedAuditWriter : IAuditEventWriter
    {
        public Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Sem janela, o guard não grava auditoria.");
    }

    private sealed class UnusedCommandStore : IAgentCommandStore
    {
        public Task<AgentCommandStatusSnapshot?> GetAsync(Guid environmentId, Guid commandId, CancellationToken cancellationToken) => throw Unused();
        public Task<AgentCommandEnqueueResult> EnqueueAsync(PersistedAgentCommand command, CancellationToken cancellationToken) => throw Unused();
        public Task<AgentCommandEnqueueResult> EnqueueAuditedAsync(PersistedAgentCommand command, AuditEvent auditEvent, CancellationToken cancellationToken) => throw Unused();
        public Task<AgentCommandStatusUpdateResult> ApplyStatusAsync(AgentCommandStatusUpdate update, CancellationToken cancellationToken) => throw Unused();
        public Task<AgentCommandStatusUpdateResult> ApplyStatusAuditedAsync(AgentCommandStatusUpdate update, AuditEvent auditEvent, CancellationToken cancellationToken) => throw Unused();
        public Task<int> ExpireNonTerminalAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken) => throw Unused();
        public Task<IReadOnlyList<PersistedAgentCommand>> ClaimDispatchableAsync(Guid environmentId, long activeFencingToken, DateTimeOffset nowUtc, CancellationToken cancellationToken) => throw Unused();

        private static InvalidOperationException Unused() => new("Sem janela, o guard não lê a fila.");
    }
}
