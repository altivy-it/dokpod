using Dokpod.ControlPlane.Application.Auditing;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.ChangeWindows;
using Dokpod.ControlPlane.Application.Commands;
using Dokpod.Domain.Auditing;
using Dokpod.Domain.ChangeWindows;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Environments;
using Xunit;

namespace Dokpod.ControlPlane.Application.Tests.ChangeWindows;

/// <summary>
/// Spec 026 T008: a fila só enfileira com a liberação do guard, do mesmo ambiente e tipo, dentro do prazo da janela;
/// no <c>Replay</c> ela nunca cria, e com a exceção grava os dois eventos juntos.
/// </summary>
public sealed class ChangeWindowQueueTests
{
    // Terça, 24/11/2026, 23:00 em São Paulo: dentro da faixa 22:00-06:00, que fecha às 09:00Z de quarta.
    private static readonly DateTimeOffset InsideWindow = new(2026, 11, 25, 2, 0, 0, TimeSpan.Zero);
    // Terça, 15:00 local: fora da janela.
    private static readonly DateTimeOffset OutsideWindow = new(2026, 11, 24, 18, 0, 0, TimeSpan.Zero);
    private static readonly Guid EnvironmentId = Guid.Parse("4b0f3b8e-7d0a-4c8e-9f38-2b8e9a1c0d11");
    private static readonly string Container = new('a', 64);

    [Fact]
    public async Task Enqueue_WithClearanceOfAnotherEnvironmentOrKind_IsRejected()
    {
        var fixture = new Fixture(InsideWindow);
        var command = fixture.Command(InsideWindow.AddMinutes(10));
        var clearance = (await fixture.EvaluateAsync(command)).Clearance!;
        var queue = fixture.Queue();

        await Assert.ThrowsAsync<ArgumentException>(() => queue.EnqueueAsync(
            command with { EnvironmentId = Guid.NewGuid() }, fixture.Audit(command), clearance, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => queue.EnqueueAsync(
            command with { Kind = AgentCommandKind.StopContainer }, fixture.Audit(command), clearance, TestContext.Current.CancellationToken));
        Assert.Null(fixture.Store.Enqueued);
    }

    [Fact]
    public async Task OpenWindow_LimitsTheDeadlineToTheClosingPlusGrace()
    {
        var fixture = new Fixture(InsideWindow);
        var command = fixture.Command(InsideWindow.AddDays(3));
        var decision = await fixture.EvaluateAsync(command);
        var clearance = decision.Clearance!;

        Assert.Equal(ChangeWindowClearanceMode.Open, clearance.Mode);
        Assert.Equal(new DateTimeOffset(2026, 11, 25, 9, 5, 0, TimeSpan.Zero), clearance.MaxDeadlineUtc);
        Assert.Equal(clearance.MaxDeadlineUtc, clearance.LimitDeadline(command.DeadlineUtc));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Queue().EnqueueAsync(
            command, fixture.Audit(command), clearance, TestContext.Current.CancellationToken));

        var limited = command with { DeadlineUtc = clearance.LimitDeadline(command.DeadlineUtc) };
        var result = await fixture.Queue().EnqueueAsync(limited, fixture.Audit(limited), clearance, TestContext.Current.CancellationToken);
        Assert.Equal(AgentCommandEnqueueResult.Created, result);
        Assert.Equal(clearance.MaxDeadlineUtc, fixture.Store.Enqueued!.Command.DeadlineUtc);
    }

    [Fact]
    public async Task ClosedWindow_WithoutOverride_IsRefusedWithTheNextOpeningAndAudited()
    {
        var fixture = new Fixture(OutsideWindow);
        var command = fixture.Command(OutsideWindow.AddMinutes(10));

        var decision = await fixture.EvaluateAsync(command);

        Assert.False(decision.Allowed);
        Assert.Equal(ChangeWindowGuard.WindowClosed, decision.FailureCode);
        Assert.Equal(new DateTimeOffset(2026, 11, 25, 1, 0, 0, TimeSpan.Zero), decision.NextWindowStartsAt);
        var denied = Assert.Single(fixture.AuditWriter.Events);
        Assert.Equal("change_window.denied", denied.Action);
        Assert.Equal(AuditOutcome.Denied, denied.Outcome);
        Assert.Equal(0, fixture.Decider.Calls);
    }

    [Fact]
    public async Task Replay_AfterTheWindowCloses_ReturnsDuplicateWithoutCreating()
    {
        var fixture = new Fixture(OutsideWindow);
        // Aceito dentro da janela, com o prazo encurtado para o fechamento mais a folga.
        var command = fixture.Command(OutsideWindow.AddDays(2));
        fixture.Store.Existing = Snapshot(command, OutsideWindow.AddMinutes(30));

        var decision = await fixture.EvaluateAsync(command);
        var result = await fixture.Queue().EnqueueAsync(command, fixture.Audit(command), decision.Clearance!, TestContext.Current.CancellationToken);

        Assert.Equal(ChangeWindowClearanceMode.Replay, decision.Clearance!.Mode);
        Assert.Equal(AgentCommandEnqueueResult.Duplicate, result);
        Assert.Null(fixture.Store.Enqueued);
        Assert.Empty(fixture.AuditWriter.Events);
    }

    [Fact]
    public async Task Replay_WithAnotherPayload_IsAConflict()
    {
        var fixture = new Fixture(OutsideWindow);
        var command = fixture.Command(OutsideWindow.AddMinutes(10));
        fixture.Store.Existing = Snapshot(command, command.DeadlineUtc) with { PayloadHash = new string('B', 64) };

        var decision = await fixture.EvaluateAsync(command);
        var result = await fixture.Queue().EnqueueAsync(command, fixture.Audit(command), decision.Clearance!, TestContext.Current.CancellationToken);

        Assert.Equal(AgentCommandEnqueueResult.ConflictingPayload, result);
        Assert.Null(fixture.Store.Enqueued);
    }

    [Fact]
    public async Task Override_IsRecordedWithTheCommandInOneWrite()
    {
        var fixture = new Fixture(OutsideWindow, AuthorizationDecisionOutcome.Allowed);
        var command = fixture.Command(OutsideWindow.AddDays(2));

        var decision = await fixture.EvaluateAsync(command, new ChangeWindowOverrideRequest("Incidente INC-123: container travado"));
        var result = await fixture.Queue().EnqueueAsync(command, fixture.Audit(command), decision.Clearance!, TestContext.Current.CancellationToken);

        Assert.Equal(ChangeWindowClearanceMode.Override, decision.Clearance!.Mode);
        Assert.Null(decision.Clearance.MaxDeadlineUtc);
        Assert.Equal(AgentCommandEnqueueResult.Created, result);
        Assert.Equal(["container.restart", "change_window.override"], fixture.Store.EnqueuedEvents!.Select(audit => audit.Action));
        Assert.Equal(command.CommandId, fixture.Store.EnqueuedEvents![1].CommandId);
        Assert.Equal(EnvironmentResourceScopes.ChangeOverride, fixture.Decider.LastScope);
        Assert.Empty(fixture.AuditWriter.Events);
    }

    private static AgentCommandStatusSnapshot Snapshot(AgentCommand command, DateTimeOffset deadline) =>
        new(command.EnvironmentId, command.CommandId, command.Kind, command.ContainerId, command.ExpectedContainerRevision,
            ControlPlaneCommandState.Pending, null, null, deadline, InsideWindow, InsideWindow, null)
        {
            PayloadHash = command.PayloadHash,
        };

    private sealed class Fixture(DateTimeOffset now, AuthorizationDecisionOutcome overrideDecision = AuthorizationDecisionOutcome.Denied)
    {
        public RecordingStore Store { get; } = new();
        public RecordingDecider Decider { get; } = new(overrideDecision);
        public RecordingAuditWriter AuditWriter { get; } = new();
        private readonly TimeProvider time = new FixedTime(now);

        private ChangeWindowGuard Guard() => new(new Schedule(), Store, Decider, AuditWriter, time);

        public AgentCommandQueueService Queue() => new(Store, new Delivery(), time);

        public AgentCommand Command(DateTimeOffset deadline) =>
            new(EnvironmentId, Guid.NewGuid(), AgentCommandKind.RestartContainer, Container, "rev-1", new string('A', 64), deadline, 3);

        public AuditEvent Audit(AgentCommand command) =>
            AuditEvent.Create(Guid.NewGuid(), now, Guid.NewGuid(), AuditActorKind.User, "user-1", "container.restart", command.EnvironmentId, AuditOutcome.Succeeded, commandId: command.CommandId);

        public Task<ChangeWindowDecision> EvaluateAsync(AgentCommand command, ChangeWindowOverrideRequest? overrideRequest = null) =>
            Guard().EvaluateCommandAsync(
                EnvironmentRegistration.Create(EnvironmentId, "prod-1", "srv-1", true, [EnvironmentResourceScopes.Read, EnvironmentResourceScopes.RestartContainer, EnvironmentResourceScopes.ChangeOverride], EnvironmentStage.Production),
                command.Kind, command.CommandId, "container.restart", command.ContainerId, AuthenticatedActor.FromSubject("user-1"), "token", Guid.NewGuid(), overrideRequest,
                TestContext.Current.CancellationToken);
    }

    private sealed class Schedule : IChangeWindowSchedule
    {
        private static readonly ChangeWindow Window = new(
            EnvironmentStage.Production,
            "America/Sao_Paulo",
            [new ChangeWindowRange(new HashSet<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }, new TimeOnly(22, 0), new TimeOnly(6, 0))],
            1,
            "Ana",
            InsideWindow);

        public Task<ChangeWindow?> GetAsync(EnvironmentStage stage, CancellationToken cancellationToken) =>
            Task.FromResult<ChangeWindow?>(stage == EnvironmentStage.Production ? Window : null);

        public Task<IReadOnlyList<ChangeWindow>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ChangeWindow>>([Window]);
    }

    private sealed class RecordingDecider(AuthorizationDecisionOutcome outcome) : IEnvironmentAuthorizationDecider
    {
        public int Calls { get; private set; }
        public string? LastScope { get; private set; }

        public Task<EnvironmentAuthorizationDecision> DecideAsync(string resource, string scope, AuthenticatedActor actor, string accessToken, Guid correlationId, CancellationToken cancellationToken)
        {
            Calls++;
            LastScope = scope;
            return Task.FromResult(new EnvironmentAuthorizationDecision(outcome, outcome == AuthorizationDecisionOutcome.Allowed ? null : "denied"));
        }
    }

    private sealed class RecordingAuditWriter : IAuditEventWriter
    {
        public List<AuditEvent> Events { get; } = [];

        public Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingStore : IAgentCommandStore
    {
        public AgentCommandStatusSnapshot? Existing { get; set; }
        public PersistedAgentCommand? Enqueued { get; private set; }
        public IReadOnlyList<AuditEvent>? EnqueuedEvents { get; private set; }

        public Task<AgentCommandStatusSnapshot?> GetAsync(Guid environmentId, Guid commandId, CancellationToken cancellationToken) =>
            Task.FromResult(Existing is { } existing && existing.CommandId == commandId ? existing : null);

        public Task<AgentCommandEnqueueResult> EnqueueAsync(PersistedAgentCommand command, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AgentCommandEnqueueResult> EnqueueAuditedAsync(PersistedAgentCommand command, AuditEvent auditEvent, CancellationToken cancellationToken) =>
            EnqueueAuditedAsync(command, [auditEvent], cancellationToken);

        public Task<AgentCommandEnqueueResult> EnqueueAuditedAsync(PersistedAgentCommand command, IReadOnlyList<AuditEvent> auditEvents, CancellationToken cancellationToken)
        {
            Enqueued = command;
            EnqueuedEvents = auditEvents;
            return Task.FromResult(AgentCommandEnqueueResult.Created);
        }

        public Task<AgentCommandStatusUpdateResult> ApplyStatusAsync(AgentCommandStatusUpdate update, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AgentCommandStatusUpdateResult> ApplyStatusAuditedAsync(AgentCommandStatusUpdate update, AuditEvent auditEvent, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> ExpireNonTerminalAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<PersistedAgentCommand>> ClaimDispatchableAsync(Guid environmentId, long activeFencingToken, DateTimeOffset nowUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
