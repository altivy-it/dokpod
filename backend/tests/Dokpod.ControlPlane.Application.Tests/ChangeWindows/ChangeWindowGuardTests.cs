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
/// Spec 026 T021: todo <see cref="AgentCommandKind"/> (iteração sobre o enum, para um tipo novo entrar sozinho) e o
/// terminal são recusados fora da janela e liberados dentro; host sem estágio, estágio sem janela e a CE liberam.
/// </summary>
public sealed class ChangeWindowGuardTests
{
    // Seg-sex 22:00-06:00 em São Paulo. Terça, 24/11/2026: 15:00 local está fora; 23:00 local está dentro.
    private static readonly DateTimeOffset Outside = new(2026, 11, 24, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Inside = new(2026, 11, 25, 2, 0, 0, TimeSpan.Zero);
    private static readonly Guid EnvironmentId = Guid.Parse("6f6a2a43-5c7e-4c55-9a0a-1d3a6c8e7b21");

    public static TheoryData<AgentCommandKind> Kinds()
    {
        var data = new TheoryData<AgentCommandKind>();
        foreach (var kind in Enum.GetValues<AgentCommandKind>().Where(kind => kind != AgentCommandKind.Unknown))
        {
            data.Add(kind);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task EveryCommandKind_IsRefusedOutsideTheWindow_AndAllowedInside(AgentCommandKind kind)
    {
        var outside = new Fixture(Outside);
        var refused = await outside.EvaluateAsync(kind);
        Assert.False(refused.Allowed);
        Assert.Equal(ChangeWindowGuard.WindowClosed, refused.FailureCode);
        Assert.Equal(new DateTimeOffset(2026, 11, 25, 1, 0, 0, TimeSpan.Zero), refused.NextWindowStartsAt);
        Assert.Equal("America/Sao_Paulo", refused.TimeZone);
        Assert.Equal("change_window.denied", Assert.Single(outside.Audit.Events).Action);

        var inside = new Fixture(Inside);
        var allowed = await inside.EvaluateAsync(kind);
        Assert.Equal(ChangeWindowClearanceMode.Open, allowed.Clearance!.Mode);
        Assert.Equal(kind, allowed.Clearance.Kind);
        Assert.Empty(inside.Audit.Events);
    }

    [Fact]
    public async Task Terminal_IsRefusedOutsideTheWindow_AndAllowedInside()
    {
        var outside = new Fixture(Outside);
        var refused = await outside.Guard.EvaluateTerminalAsync(outside.Registration(EnvironmentStage.Production), new string('a', 64), Actor, "token", Guid.NewGuid(), null, TestContext.Current.CancellationToken);
        Assert.Equal(ChangeWindowGuard.WindowClosed, refused.FailureCode);
        Assert.Equal("container.exec.requested", Assert.Single(outside.Audit.Events).Details!["action"]);

        var inside = new Fixture(Inside);
        Assert.True((await inside.Guard.EvaluateTerminalAsync(inside.Registration(EnvironmentStage.Production), new string('a', 64), Actor, "token", Guid.NewGuid(), null, TestContext.Current.CancellationToken)).Allowed);
    }

    [Fact]
    public async Task HostWithoutStage_AndStageWithoutWindow_AreAllowedWithoutReadingTheQueue()
    {
        var fixture = new Fixture(Outside);

        var withoutStage = await fixture.EvaluateAsync(AgentCommandKind.StopContainer, stage: null);
        var withoutWindow = await fixture.EvaluateAsync(AgentCommandKind.StopContainer, EnvironmentStage.Staging);

        Assert.Equal(ChangeWindowClearanceMode.NoWindow, withoutStage.Clearance!.Mode);
        Assert.Equal(ChangeWindowClearanceMode.NoWindow, withoutWindow.Clearance!.Mode);
        Assert.Equal(0, fixture.Commands.Reads);
        Assert.Empty(fixture.Audit.Events);
    }

    [Fact]
    public async Task CommunityEdition_AllowsEverything()
    {
        var guard = ChangeWindowTestGuard.Unrestricted();
        foreach (var kind in Enum.GetValues<AgentCommandKind>().Where(kind => kind != AgentCommandKind.Unknown))
        {
            var decision = await guard.EvaluateCommandAsync(
                EnvironmentRegistration.Create(EnvironmentId, "prod-1", "srv-1", true, [EnvironmentResourceScopes.Read], EnvironmentStage.Production),
                kind, Guid.NewGuid(), "container.stop", null, Actor, "token", Guid.NewGuid(), null, TestContext.Current.CancellationToken);
            Assert.Equal(ChangeWindowClearanceMode.NoWindow, decision.Clearance!.Mode);
        }
    }

    private static AuthenticatedActor Actor => AuthenticatedActor.FromSubject("user-1");

    private sealed class Fixture(DateTimeOffset now)
    {
        public RecordingCommands Commands { get; } = new();
        public RecordingAudit Audit { get; } = new();

        public ChangeWindowGuard Guard => new(new Schedule(), Commands, new DenyingDecider(), Audit, new FixedTime(now));

        public EnvironmentRegistration Registration(EnvironmentStage? stage) =>
            EnvironmentRegistration.Create(EnvironmentId, "prod-1", "srv-1", true, [EnvironmentResourceScopes.Read], stage);

        public Task<ChangeWindowDecision> EvaluateAsync(AgentCommandKind kind, EnvironmentStage? stage = EnvironmentStage.Production) =>
            Guard.EvaluateCommandAsync(Registration(stage), kind, Guid.NewGuid(), "container.stop", new string('a', 64), Actor, "token", Guid.NewGuid(), null, TestContext.Current.CancellationToken);
    }

    private sealed class Schedule : IChangeWindowSchedule
    {
        private static readonly ChangeWindow Production = new(
            EnvironmentStage.Production,
            "America/Sao_Paulo",
            [new ChangeWindowRange(new HashSet<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }, new TimeOnly(22, 0), new TimeOnly(6, 0))],
            1,
            "Ana",
            Inside);

        public Task<ChangeWindow?> GetAsync(EnvironmentStage stage, CancellationToken cancellationToken) =>
            Task.FromResult(stage == EnvironmentStage.Production ? Production : null);

        public Task<IReadOnlyList<ChangeWindow>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ChangeWindow>>([Production]);
    }

    /// <summary>Sem pedido de exceção, o guard nunca consulta o Keycloak.</summary>
    private sealed class DenyingDecider : IEnvironmentAuthorizationDecider
    {
        public Task<EnvironmentAuthorizationDecision> DecideAsync(string resource, string scope, AuthenticatedActor actor, string accessToken, Guid correlationId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("O guard não deveria consultar o Keycloak sem pedido de exceção.");
    }

    private sealed class RecordingAudit : IAuditEventWriter
    {
        public List<AuditEvent> Events { get; } = [];

        public Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingCommands : IAgentCommandStore
    {
        public int Reads { get; private set; }

        public Task<AgentCommandStatusSnapshot?> GetAsync(Guid environmentId, Guid commandId, CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult<AgentCommandStatusSnapshot?>(null);
        }

        public Task<AgentCommandEnqueueResult> EnqueueAsync(PersistedAgentCommand command, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AgentCommandEnqueueResult> EnqueueAuditedAsync(PersistedAgentCommand command, AuditEvent auditEvent, CancellationToken cancellationToken) => throw new NotSupportedException();
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
