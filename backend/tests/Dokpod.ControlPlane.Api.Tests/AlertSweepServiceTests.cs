using Dokpod.ControlPlane.Application.Agents;
using Dokpod.ControlPlane.Application.Alerts;
using Dokpod.ControlPlane.Application.Environments;
using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.ControlPlane.Application.Inventory;
using Dokpod.ControlPlane.Application.Metrics;
using Dokpod.ControlPlane.Application.Realtime;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.Domain.Alerts;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests;

/// <summary>Spec 016 T010: varredura com relógio controlado sobre stores de alertas em PostgreSQL real.</summary>
[Collection(AlertGovernancePostgresCollection.Name)]
public sealed class AlertSweepServiceTests
{
    private static readonly Guid DiedRule = Guid.Parse("01600000-0000-4000-8000-000000000001");
    private static readonly Guid AgentDisconnectedRule = Guid.Parse("01600000-0000-4000-8000-000000000005");
    private static readonly Guid HostCpuRule = Guid.Parse("01600000-0000-4000-8000-000000000006");

    [Fact]
    public async Task HostCpuAboveLimit_OpensOnlyAfterTheRuleDurationAndResolvesWhenItDrops()
    {
        await using var harness = Harness.CreateOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var host = harness.AddHost(connected: true);
        try
        {
            harness.Cpu(host, 95);
            await harness.Sweep(ct);
            harness.Clock.Advance(TimeSpan.FromMinutes(4));
            harness.Cpu(host, 96);
            await harness.Sweep(ct);
            Assert.Null(await harness.Active(host, HostCpuRule, ct));

            harness.Clock.Advance(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(1));
            harness.Cpu(host, 97);
            var opened = await harness.Sweep(ct);
            var alert = await harness.Active(host, HostCpuRule, ct);
            Assert.Equal(1, opened.Opened);
            Assert.NotNull(alert);
            Assert.Contains("97%", alert.ConditionText);

            // Sem amostra recente (agente parou de publicar) nada muda.
            harness.Clock.Advance(TimeSpan.FromMinutes(2));
            await harness.Sweep(ct);
            Assert.NotNull(await harness.Active(host, HostCpuRule, ct));

            harness.Cpu(host, 40);
            await harness.Sweep(ct);
            var resolved = (await harness.Store.GetAsync(alert.AlertId, ct))!;
            Assert.Equal(AlertState.Resolved, resolved.State);
            Assert.Equal(AlertResolutionReasons.ConditionCleared, resolved.ResolutionReason);
        }
        finally
        {
            await harness.Cleanup(ct);
        }
    }

    [Fact]
    public async Task DisconnectedAgent_OpensAfterTwoMinutesAndResolvesOnReconnection()
    {
        await using var harness = Harness.CreateOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var host = harness.AddHost(connected: false);
        try
        {
            await harness.Sweep(ct);
            harness.Clock.Advance(TimeSpan.FromSeconds(119));
            await harness.Sweep(ct);
            Assert.Null(await harness.Active(host, AgentDisconnectedRule, ct));

            harness.Clock.Advance(TimeSpan.FromSeconds(2));
            await harness.Sweep(ct);
            var alert = await harness.Active(host, AgentDisconnectedRule, ct);
            Assert.NotNull(alert);
            Assert.Null(alert.ContainerId);

            harness.Sessions.Connect(host);
            await harness.Sweep(ct);
            Assert.Equal(AlertState.Resolved, (await harness.Store.GetAsync(alert.AlertId, ct))!.State);
        }
        finally
        {
            await harness.Cleanup(ct);
        }
    }

    [Fact]
    public async Task MissingContainer_IsResolvedAsRemovedOnlyAfterTheInventoryGrace()
    {
        await using var harness = Harness.CreateOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var host = harness.AddHost(connected: true);
        try
        {
            var rule = (await harness.Store.GetRuleAsync(DiedRule, ct))!;
            var (alert, _) = await harness.Store.OpenOrOccurAsync(
                new AlertOccurrence(rule, host, "rapido", "rapido", "Container rapido encerrado com erro (código 3)", harness.Clock.GetUtcNow(), null), ct);

            harness.Clock.Advance(TimeSpan.FromMinutes(1));
            await harness.Sweep(ct);
            Assert.Equal(AlertState.Open, (await harness.Store.GetAsync(alert.AlertId, ct))!.State);

            harness.Clock.Advance(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(1));
            await harness.Sweep(ct);
            var resolved = (await harness.Store.GetAsync(alert.AlertId, ct))!;
            Assert.Equal(AlertState.Resolved, resolved.State);
            Assert.Equal(AlertResolutionReasons.ContainerRemoved, resolved.ResolutionReason);
        }
        finally
        {
            await harness.Cleanup(ct);
        }
    }

    [Fact]
    public async Task SilenceWindow_SilencesCoveredAlertsAndReturnsThemToOpenWhenItEnds()
    {
        await using var harness = Harness.CreateOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var host = harness.AddHost(connected: true);
        var silence = new AlertSilence(Guid.NewGuid(), new AlertScope(null, host, "lote-*"), null, harness.Clock.GetUtcNow(), harness.Clock.GetUtcNow().AddMinutes(10), null, "Janela de carga do lote", "ana");
        try
        {
            var rule = (await harness.Store.GetRuleAsync(DiedRule, ct))!;
            harness.Containers.Set(host, new ContainerInventory("lote-1", "lote-1", "img", "exited", "r1", harness.Clock.GetUtcNow()) { ExitCode = 1 });
            var (alert, _) = await harness.Store.OpenOrOccurAsync(new AlertOccurrence(rule, host, "lote-1", "lote-1", "Container lote-1 encerrado com erro (código 1)", harness.Clock.GetUtcNow(), null), ct);
            await harness.Store.CreateSilenceAsync(silence, ct);

            var first = await harness.Sweep(ct);
            Assert.Equal(1, first.Silenced);
            Assert.Equal(AlertState.Silenced, (await harness.Store.GetAsync(alert.AlertId, ct))!.State);

            harness.Clock.Advance(TimeSpan.FromMinutes(11));
            var second = await harness.Sweep(ct);
            Assert.Equal(1, second.Unsilenced);
            Assert.Equal(AlertState.Open, (await harness.Store.GetAsync(alert.AlertId, ct))!.State);
            var timeline = await harness.Store.GetTimelineAsync(alert.AlertId, ct);
            Assert.Equal([AlertTimelineKinds.Silenced, AlertTimelineKinds.Unsilenced], timeline.Select(t => t.Kind).Where(k => k is AlertTimelineKinds.Silenced or AlertTimelineKinds.Unsilenced));
        }
        finally
        {
            await harness.Context.Database.ExecuteSqlRawAsync("DELETE FROM dokpod.alert_silences WHERE silence_id = @id", [new NpgsqlParameter("id", silence.SilenceId)], ct);
            await harness.Cleanup(ct);
        }
    }

    [Fact]
    public async Task IntentionalStop_EndsWhenTheContainerRunsAgain()
    {
        await using var harness = Harness.CreateOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var host = harness.AddHost(connected: true);
        try
        {
            var stoppedAt = harness.Clock.GetUtcNow();
            await harness.Justifications.RecordAsync(new ContainerActionJustification(Guid.NewGuid(), Guid.NewGuid(), host, "web", "web", JustifiedAction.Stop, "ana", "Manutenção planejada", stoppedAt, null), ct);
            harness.Containers.Set(host, new ContainerInventory("web", "web", "img", "exited", "r1", stoppedAt.AddSeconds(5)) { ExitCode = 0 });
            harness.Clock.Advance(TimeSpan.FromMinutes(1));
            Assert.Equal(0, (await harness.Sweep(ct)).JustificationsEnded);
            Assert.Single(await harness.Justifications.GetCurrentAsync([(host, "web")], ct));

            harness.Containers.Set(host, new ContainerInventory("web", "web", "img", "running", "r2", harness.Clock.GetUtcNow()));
            Assert.Equal(1, (await harness.Sweep(ct)).JustificationsEnded);
            Assert.Empty(await harness.Justifications.GetCurrentAsync([(host, "web")], ct));
        }
        finally
        {
            await harness.Cleanup(ct);
        }
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly List<EnvironmentRegistration> registrations = [];

        private Harness(ControlPlaneDbContext context)
        {
            Context = context;
            Store = new PostgresAlertStore(context);
            Justifications = new PostgresActionJustificationStore(context);
            var hosts = new HostQueryService(new NoHostStore(), new NoHostStore(), Sessions, new AgentLivenessTracker(Clock), new EngineSupportOptions());
            Metrics = new LatestMetricsCache(Clock, new MetricsOptions());
            Service = new AlertSweepService(Store, Justifications, new Registrations(registrations), hosts, Containers, Metrics, new AlertConditionState(), new NullControlPlaneNotifier(), Clock);
        }

        public ControlPlaneDbContext Context { get; }
        public PostgresAlertStore Store { get; }
        public PostgresActionJustificationStore Justifications { get; }
        public ManualClock Clock { get; } = new(DateTimeOffset.UtcNow);
        public FakeSessions Sessions { get; } = new();
        public FakeContainers Containers { get; } = new();
        public LatestMetricsCache Metrics { get; }
        public AlertSweepService Service { get; }

        public static Harness CreateOrSkip()
        {
            var connectionString = Environment.GetEnvironmentVariable("DOKPOD_TEST_POSTGRES_CONNECTION");
            if (string.IsNullOrWhiteSpace(connectionString)) Assert.Skip("DOKPOD_TEST_POSTGRES_CONNECTION is not configured.");
            return new Harness(new ControlPlaneDbContext(new DbContextOptionsBuilder<ControlPlaneDbContext>().UseNpgsql(connectionString).Options));
        }

        public Guid AddHost(bool connected)
        {
            var id = Guid.NewGuid();
            registrations.Add(EnvironmentRegistration.Create(id, $"host-{registrations.Count}", "srv", true, EnvironmentResourceScopes.Supported, EnvironmentStage.Development));
            if (connected) Sessions.Connect(id);
            return id;
        }

        public void Cpu(Guid host, double percent) =>
            Metrics.SetHost(host, new HostMetricsSample(percent, 1L << 30, 16L << 30, Clock.GetUtcNow()));

        public Task<AlertSweepResult> Sweep(CancellationToken ct) => Service.RunOnceAsync(ct);

        public async Task<AlertRecord?> Active(Guid host, Guid ruleId, CancellationToken ct) =>
            (await Store.ListActiveAsync(ct)).FirstOrDefault(a => a.EnvironmentId == host && a.RuleId == ruleId);

        public async Task Cleanup(CancellationToken ct)
        {
            var ids = registrations.Select(r => r.EnvironmentId).ToArray();
            await Context.Database.ExecuteSqlRawAsync("DELETE FROM dokpod.alerts WHERE environment_id = ANY(@ids)", [new NpgsqlParameter("ids", ids)], ct);
            await Context.Database.ExecuteSqlRawAsync("DELETE FROM dokpod.container_action_justifications WHERE environment_id = ANY(@ids)", [new NpgsqlParameter("ids", ids)], ct);
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    private sealed class ManualClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset now = start;

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan by) => now += by;
    }

    private sealed class Registrations(List<EnvironmentRegistration> registrations) : IEnvironmentRegistrationStore
    {
        public Task<EnvironmentRegistrationPage> ListAsync(Guid? afterEnvironmentId, int limit, CancellationToken cancellationToken) =>
            Task.FromResult(new EnvironmentRegistrationPage(afterEnvironmentId is null ? registrations.ToArray() : [], null));

        public Task<EnvironmentRegistrationStoreResult> CreateAsync(EnvironmentRegistration registration, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<EnvironmentRegistration?> GetAsync(Guid environmentId, CancellationToken cancellationToken) =>
            Task.FromResult(registrations.FirstOrDefault(r => r.EnvironmentId == environmentId));
    }

    private sealed class FakeSessions : IAgentSessionStore
    {
        private readonly Dictionary<Guid, AgentSession> sessions = [];

        public void Connect(Guid environmentId) => sessions[environmentId] = new AgentSession(environmentId, Guid.NewGuid(), 1);

        public ValueTask<AgentSession?> FindActiveAsync(Guid environmentId, CancellationToken cancellationToken) =>
            ValueTask.FromResult(sessions.GetValueOrDefault(environmentId));

        public ValueTask<AgentSession> ActivateAsync(Guid environmentId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask InvalidateEnvironmentAsync(Guid environmentId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public bool IsActive(AgentSession session) => sessions.ContainsValue(session);
        public Task WaitUntilInactiveAsync(AgentSession session, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask DeactivateAsync(AgentSession session, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeContainers : IContainerSearchStore
    {
        private readonly Dictionary<(Guid, string), ContainerInventory> items = [];

        public void Set(Guid environmentId, ContainerInventory container) => items[(environmentId, container.ContainerId)] = container;

        public Task<ContainerInventory?> GetAsync(Guid environmentId, string containerId, CancellationToken cancellationToken) =>
            Task.FromResult(items.GetValueOrDefault((environmentId, containerId)));

        public Task<ContainerSearchResult> SearchAsync(ContainerSearchQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class NoHostStore : IAgentHostStore, IInventoryStatisticsStore
    {
        public Task<IReadOnlyDictionary<Guid, AgentDescriptor>> GetDescriptorsAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, AgentDescriptor>>(new Dictionary<Guid, AgentDescriptor>());

        public Task<IReadOnlyDictionary<Guid, AgentConnectionState>> GetConnectionsAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, AgentConnectionState>>(new Dictionary<Guid, AgentConnectionState>());

        public Task<IReadOnlyDictionary<Guid, InventoryStatistics>> GetStatisticsAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, InventoryStatistics>>(new Dictionary<Guid, InventoryStatistics>());

        public Task UpsertDescriptorAsync(AgentDescriptor descriptor, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool?> SetConnectionAsync(Guid environmentId, bool connected, DateTimeOffset atUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task TouchLastSeenAsync(Guid environmentId, DateTimeOffset atUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> ListConnectedAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
