using Dokpod.ControlPlane.Api.Agents;
using Dokpod.ControlPlane.Api.Commands;
using Dokpod.ControlPlane.Api.Endpoints;
using Dokpod.ControlPlane.Api.Tests.Licensing;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.ChangeWindows;
using Dokpod.ControlPlane.Application.Commands;
using Dokpod.ControlPlane.Application.Containers;
using Dokpod.ControlPlane.Application.Images;
using Dokpod.ControlPlane.Application.Networks;
using Dokpod.ControlPlane.Application.Pods;
using Dokpod.ControlPlane.Application.Queries;
using Dokpod.ControlPlane.Application.Volumes;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.ControlPlane.Infrastructure.ChangeWindows;
using Dokpod.Domain.ChangeWindows;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Pods;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
using static Dokpod.ControlPlane.Api.Tests.Licensing.LicensingPostgres;

namespace Dokpod.ControlPlane.Api.Tests.ChangeWindows;

/// <summary>
/// Spec 026 T022: a janela aplicada pelos serviços reais de ação sobre PostgreSQL real, com o relógio do servidor
/// fixado. Fora da janela, nenhuma linha nova em <c>agent_commands</c> e um <c>change_window.denied</c>; dentro, o prazo
/// encurtado (L1); depois do fechamento, a repetição do mesmo comando é duplicada, não <c>409</c> (D-010).
/// </summary>
[Trait("Category", "Integration")]
[Collection(LicensingPostgresCollection.Name)]
public sealed class ChangeWindowEnforcementTests
{
    // Seg-sex 22:00-06:00 em São Paulo. Terça 24/11/2026 15:00 local está fora; 23:00 local está dentro.
    private static readonly DateTimeOffset Outside = new(2026, 11, 24, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Inside = new(2026, 11, 25, 2, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset NextOpening = new(2026, 11, 25, 1, 0, 0, TimeSpan.Zero);
    private static readonly string Container = new('c', 64);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task OutsideTheWindow_EveryActionIsRefusedWith409_WithoutCommandAndWithAudit()
    {
        await using var fixture = await Fixture.CreateAsync(Outside);
        // L4: o prazo vem do cliente e cai dentro da janela; só o relógio do servidor decide.
        var clientInsideWindow = Inside.AddMinutes(30);

        var outcomes = new List<(string Route, ChangeWindowDecision? Decision)>
        {
            ("container", (await fixture.Commands.SubmitAsync(fixture.Environment, Guid.NewGuid(), AgentCommandKind.StopContainer, Container, "rev-1", clientInsideWindow, Actor, "token", Guid.NewGuid(), Ct)).ChangeWindow),
            ("create", (await fixture.Creation.CreateAsync(fixture.Environment, Guid.NewGuid(), Creation(), Actor, "token", Guid.NewGuid(), Ct)).ChangeWindow),
            ("pull", (await fixture.Pull.PullAsync(fixture.Environment, Guid.NewGuid(), "docker.io/library/nginx:1.28", clientInsideWindow, Actor, "token", Guid.NewGuid(), Ct)).ChangeWindow),
            ("volume", (await fixture.Volumes.RemoveAsync(fixture.Environment, Guid.NewGuid(), "orfao", clientInsideWindow, null, Actor, "token", Guid.NewGuid(), Ct)).ChangeWindow),
            ("network", (await fixture.Networks.RemoveAsync(fixture.Environment, Guid.NewGuid(), new string('d', 64), clientInsideWindow, null, Actor, "token", Guid.NewGuid(), Ct)).ChangeWindow),
            ("pod", (await fixture.Pods.ExecuteAsync(fixture.Environment, Guid.NewGuid(), new string('e', 64), PodActionKind.Stop, clientInsideWindow, null, Actor, "token", Guid.NewGuid(), Ct)).ChangeWindow),
        };

        foreach (var (route, decision) in outcomes)
        {
            Assert.True(decision is { Allowed: false }, route);
            var problem = Assert.IsType<ProblemHttpResult>(ChangeWindowProblems.Denied(new DefaultHttpContext(), decision));
            Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
            Assert.Equal(ChangeWindowGuard.WindowClosed, problem.ProblemDetails.Extensions["code"]);
            Assert.Equal(NextOpening, problem.ProblemDetails.Extensions["nextWindowStartsAt"]);
            Assert.Equal("America/Sao_Paulo", problem.ProblemDetails.Extensions["timeZone"]);
        }

        Assert.Equal(0L, await fixture.CommandCountAsync());
        Assert.Equal(6L, await fixture.AuditCountAsync("change_window.denied"));
    }

    [Fact]
    public async Task WithoutTheActionScope_The403OfTheActionWins_EvenForAnExistingCommandId()
    {
        await using var fixture = await Fixture.CreateAsync(Inside);
        var commandId = Guid.NewGuid();
        Assert.Equal(ContainerCommandSubmissionOutcome.Accepted, (await fixture.Commands.SubmitAsync(fixture.Environment, commandId, AgentCommandKind.RestartContainer, Container, "rev-1", Inside.AddMinutes(10), Actor, "token", Guid.NewGuid(), Ct)).Outcome);

        fixture.Clock.Now = Outside;
        fixture.Decider.Outcome = AuthorizationDecisionOutcome.Denied;
        var denied = await fixture.Commands.SubmitAsync(fixture.Environment, commandId, AgentCommandKind.RestartContainer, Container, "rev-1", Inside.AddMinutes(10), Actor, "token", Guid.NewGuid(), Ct);

        Assert.Equal(ContainerCommandSubmissionOutcome.AuthorizationDenied, denied.Outcome);
        Assert.Equal(0L, await fixture.AuditCountAsync("change_window.denied"));
    }

    [Fact]
    public async Task InsideTheWindow_ALongDeadlineIsShortenedToTheClosingPlusFiveMinutes()
    {
        await using var fixture = await Fixture.CreateAsync(Inside);
        var commandId = Guid.NewGuid();

        var accepted = await fixture.Commands.SubmitAsync(fixture.Environment, commandId, AgentCommandKind.RestartContainer, Container, "rev-1", Inside.AddDays(3), Actor, "token", Guid.NewGuid(), Ct);

        Assert.Equal(ContainerCommandSubmissionOutcome.Accepted, accepted.Outcome);
        var stored = await new PostgresAgentCommandStore(fixture.Context).GetAsync(fixture.Environment, commandId, Ct);
        Assert.Equal(new DateTimeOffset(2026, 11, 25, 9, 5, 0, TimeSpan.Zero), stored!.DeadlineUtc);
    }

    [Fact]
    public async Task AfterTheWindowCloses_TheAcceptedCommandStaysPending_AndItsRetryIsADuplicate()
    {
        await using var fixture = await Fixture.CreateAsync(Inside);
        var commandId = Guid.NewGuid();
        Assert.Equal(ContainerCommandSubmissionOutcome.Accepted, (await fixture.Commands.SubmitAsync(fixture.Environment, commandId, AgentCommandKind.RestartContainer, Container, "rev-1", Inside.AddDays(1), Actor, "token", Guid.NewGuid(), Ct)).Outcome);

        fixture.Clock.Now = Outside.AddDays(1);
        var retry = await fixture.Commands.SubmitAsync(fixture.Environment, commandId, AgentCommandKind.RestartContainer, Container, "rev-1", Inside.AddDays(1), Actor, "token", Guid.NewGuid(), Ct);
        var conflicting = await fixture.Commands.SubmitAsync(fixture.Environment, commandId, AgentCommandKind.RestartContainer, Container, "rev-2", Inside.AddDays(1), Actor, "token", Guid.NewGuid(), Ct);

        Assert.Equal(ContainerCommandSubmissionOutcome.Duplicate, retry.Outcome);
        Assert.Equal(ContainerCommandSubmissionOutcome.ConflictingPayload, conflicting.Outcome);
        Assert.Equal(1L, await fixture.CommandCountAsync());
        Assert.Equal(ControlPlaneCommandState.Pending, (await new PostgresAgentCommandStore(fixture.Context).GetAsync(fixture.Environment, commandId, Ct))!.State);
        Assert.Equal(0L, await fixture.AuditCountAsync("change_window.denied"));
    }

    private static AuthenticatedActor Actor => AuthenticatedActor.FromSubject("user-026") with { DisplayName = "Maria" };

    private static ContainerCreationRequest Creation() =>
        new("docker.io/library/nginx:1.28", "web-026", null, null, null, null, null, null, null, null, false, null);

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(string connectionString, DateTimeOffset now)
        {
            ConnectionString = connectionString;
            Context = CreateContext(connectionString);
            Clock = new MutableClock(now);
            var registrations = new PostgresEnvironmentRegistrationStore(Context);
            var sessions = new InMemoryAgentSessionStore();
            var commandStore = new PostgresAgentCommandStore(Context);
            var audit = new PostgresAuditEventWriter(Context);
            var queue = new AgentCommandQueueService(commandStore, new InMemoryAgentCommandDeliveryQueue(), Clock);
            var guard = new ChangeWindowGuard(new ChangeWindowStore(Context), commandStore, Decider, audit, Clock);
            var hosts = new PostgresAgentHostStore(Context);
            var justifications = new PostgresActionJustificationStore(Context);
            Sessions = sessions;
            Commands = new ContainerCommandService(Decider, registrations, sessions, queue, guard, justifications, new PostgresContainerSearchStore(Context), Clock);
            Creation = new ContainerCreationService(Decider, registrations, sessions, queue, guard, hosts, new ImagePullPolicy(), new AgentQueryDispatcher(Clock), Clock);
            Pull = new ImagePullService(Decider, registrations, sessions, queue, guard, hosts, new ImagePullPolicy());
            Volumes = new VolumeRemovalService(Decider, registrations, sessions, queue, guard, hosts, new PostgresVolumeInventoryStore(Context), justifications, Clock);
            Networks = new NetworkRemovalService(Decider, registrations, sessions, queue, guard, hosts, new PostgresNetworkInventoryStore(Context), justifications, Clock);
            Pods = new PodActionService(Decider, registrations, sessions, queue, guard, hosts, new PostgresPodInventoryStore(Context), justifications, Clock);
        }

        public string ConnectionString { get; }
        public ControlPlaneDbContext Context { get; }
        public MutableClock Clock { get; }
        public SwitchableDecider Decider { get; } = new();
        public InMemoryAgentSessionStore Sessions { get; }
        public Guid Environment { get; } = Guid.NewGuid();
        public ContainerCommandService Commands { get; }
        public ContainerCreationService Creation { get; }
        public ImagePullService Pull { get; }
        public VolumeRemovalService Volumes { get; }
        public NetworkRemovalService Networks { get; }
        public PodActionService Pods { get; }

        public static async Task<Fixture> CreateAsync(DateTimeOffset now)
        {
            var fixture = new Fixture(ConnectionStringOrSkip(), now);
            await using var connection = await OpenAsync(fixture.ConnectionString, Ct);
            await ExecuteAsync(connection, null, "DELETE FROM dokpod.change_windows", Ct);
            await new PostgresEnvironmentRegistrationStore(fixture.Context).CreateAsync(
                EnvironmentRegistration.Create(fixture.Environment, $"cw-{fixture.Environment:N}"[..20], "srv-cw", true, EnvironmentResourceScopes.Supported, EnvironmentStage.Production), Ct);
            Assert.Equal(ChangeWindowWriteOutcome.Saved, (await new ChangeWindowStore(fixture.Context).SaveAsync(
                new ChangeWindow(
                    EnvironmentStage.Production,
                    "America/Sao_Paulo",
                    [new ChangeWindowRange(new HashSet<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }, new TimeOnly(22, 0), new TimeOnly(6, 0))],
                    0,
                    "Ana",
                    DateTimeOffset.UtcNow),
                null,
                Ct)).Outcome);
            await fixture.Sessions.ActivateAsync(fixture.Environment, Ct);
            return fixture;
        }

        public async Task<long> CommandCountAsync()
        {
            await using var connection = await OpenAsync(ConnectionString, Ct);
            return await ScalarAsync<long>(connection, null, "SELECT count(*) FROM dokpod.agent_commands WHERE environment_id = @id", Ct, new NpgsqlParameter("id", Environment));
        }

        public async Task<long> AuditCountAsync(string action)
        {
            await using var connection = await OpenAsync(ConnectionString, Ct);
            return await ScalarAsync<long>(
                connection, null, "SELECT count(*) FROM dokpod.audit_events WHERE environment_id = @id AND action = @action", Ct,
                new NpgsqlParameter("id", Environment), new NpgsqlParameter("action", action));
        }

        public async ValueTask DisposeAsync()
        {
            await using (var connection = await OpenAsync(ConnectionString, Ct))
            {
                await ExecuteAsync(connection, null, "DELETE FROM dokpod.change_windows", Ct);
                await ExecuteAsync(connection, null, "DELETE FROM dokpod.agent_commands WHERE environment_id = @id", Ct, new NpgsqlParameter("id", Environment));
            }

            await Context.EnvironmentRegistrations.Where(item => item.EnvironmentId == Environment).ExecuteDeleteAsync(Ct);
            await Context.DisposeAsync();
        }
    }

    private sealed class SwitchableDecider : IEnvironmentAuthorizationDecider
    {
        public AuthorizationDecisionOutcome Outcome { get; set; } = AuthorizationDecisionOutcome.Allowed;

        public Task<EnvironmentAuthorizationDecision> DecideAsync(string resource, string scope, AuthenticatedActor actor, string accessToken, Guid correlationId, CancellationToken cancellationToken) =>
            Task.FromResult(new EnvironmentAuthorizationDecision(Outcome, Outcome == AuthorizationDecisionOutcome.Allowed ? null : "denied"));
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
