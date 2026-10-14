using Dokpod.ControlPlane.Application.Agents;
using Dokpod.ControlPlane.Application.Events;
using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.ControlPlane.Application.Overview;
using Dokpod.ControlPlane.Application.Realtime;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Events;
using Dokpod.Domain.Hosts;
using Xunit;

namespace Dokpod.ControlPlane.Application.Tests.Hosts;

public sealed class HostAndOverviewTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    private static Guid Id(int n) => Guid.Parse($"00000000-0000-4000-8000-{n:D12}");

    private static EnvironmentRegistration Registration(int n, EnvironmentStage? stage, bool enabled = true) =>
        EnvironmentRegistration.Create(Id(n), $"srv-{n}", $"srv-{n}.local", enabled, ["environment:read"], stage);

    private static AgentDescriptor Descriptor(int n, string engine, string version) =>
        new(Id(n), engine, version, "1.47", engine == "podman", "Ubuntu", "24.04", "x86_64", "0.9.0", [], $"srv-{n}", 8, 1L << 34, Now);

    [Fact]
    public async Task Describe_EvaluatesSituationFromSessionLivenessAndMinimumVersion()
    {
        var clock = new ManualClock(Now);
        var liveness = new AgentLivenessTracker(clock);
        var hosts = new HostStore(
            Descriptor(1, "docker", "27.3.1"),
            Descriptor(2, "docker", "26.1.4"),
            Descriptor(3, "podman", "5.2.3"),
            Descriptor(4, "podman", "5.2.3"));
        var sessions = new SessionStore(Id(1), Id(2), Id(3));
        var service = new HostQueryService(hosts, new StatisticsStore(), sessions, liveness, new EngineSupportOptions());

        // host 3 diverge (revisão relatada > projetada) por mais que a tolerância
        liveness.RecordProjected(Id(3), 5);
        liveness.RecordHeartbeat(Id(3), 9);
        clock.Advance(AgentLivenessTracker.DivergenceTolerance + TimeSpan.FromSeconds(1));

        var views = await service.DescribeAsync(
            [Registration(1, EnvironmentStage.Production), Registration(2, EnvironmentStage.Production), Registration(3, null), Registration(4, null), Registration(5, null, enabled: false)],
            TestContext.Current.CancellationToken);

        Assert.Equal(
            [HostSituation.UpToDate, HostSituation.UpdateRequired, HostSituation.Stale, HostSituation.Disconnected, HostSituation.Disabled],
            views.Select(v => v.Situation));
    }

    [Fact]
    public void Liveness_StableHostWithoutDeltasStaysUpToDateAndPersistsLastSeenAtMostOncePerMinute()
    {
        var clock = new ManualClock(Now);
        var liveness = new AgentLivenessTracker(clock);
        liveness.RecordProjected(Id(1), 7);

        Assert.True(liveness.RecordHeartbeat(Id(1), 7));
        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.False(liveness.RecordHeartbeat(Id(1), 7));
        clock.Advance(TimeSpan.FromMinutes(30));
        Assert.True(liveness.RecordHeartbeat(Id(1), 7));
        Assert.False(liveness.IsDiverging(Id(1)));

        liveness.RecordHeartbeat(Id(1), 8);
        clock.Advance(AgentLivenessTracker.DivergenceTolerance - TimeSpan.FromSeconds(1));
        Assert.False(liveness.IsDiverging(Id(1)));
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.True(liveness.IsDiverging(Id(1)));
        liveness.RecordProjected(Id(1), 8);
        Assert.False(liveness.IsDiverging(Id(1)));
    }

    [Fact]
    public void Filter_CombinesStageEngineSituationAndText()
    {
        var views = new[]
        {
            new HostView(Registration(1, EnvironmentStage.Production), Descriptor(1, "docker", "27.3.1"), null, true, HostSituation.UpToDate, null),
            new HostView(Registration(2, EnvironmentStage.Production), Descriptor(2, "podman", "5.2.3"), null, true, HostSituation.UpToDate, null),
            new HostView(Registration(3, EnvironmentStage.Staging), Descriptor(3, "podman", "5.2.3"), null, false, HostSituation.Disconnected, null),
        };

        Assert.Equal([Id(2)], HostQueryService.Filter(views, new HostFilter(EnvironmentStage.Production, "podman")).Select(v => v.Registration.EnvironmentId));
        Assert.Equal([Id(3)], HostQueryService.Filter(views, new HostFilter(Situation: HostSituation.Disconnected)).Select(v => v.Registration.EnvironmentId));
        Assert.Equal([Id(1)], HostQueryService.Filter(views, new HostFilter(Query: "SRV-1")).Select(v => v.Registration.EnvironmentId));
        Assert.Equal(3, HostQueryService.Filter(views, new HostFilter()).Count);
    }

    [Fact]
    public async Task Overview_AggregatesOnlyEnabledHostsOfTheStage()
    {
        var events = new EventStore { Attention = 4 };
        var service = new OverviewQueryService(events, new ManualClock(Now));
        var stats = (int n, int running, int failed) => new InventoryStatistics(
            Id(n), new Dictionary<string, int> { ["running"] = running, ["failed"] = failed }, Now.AddMinutes(-n), 1);
        var hosts = new[]
        {
            new HostView(Registration(1, EnvironmentStage.Production), Descriptor(1, "docker", "27.3.1"), null, true, HostSituation.UpToDate, stats(1, 10, 1)),
            new HostView(Registration(2, EnvironmentStage.Production), Descriptor(2, "docker", "27.3.1"), null, true, HostSituation.UpToDate, stats(2, 5, 0)),
            new HostView(Registration(3, EnvironmentStage.Production), Descriptor(3, "podman", "5.2.3"), null, false, HostSituation.Disconnected, stats(3, 2, 2)),
            new HostView(Registration(4, EnvironmentStage.Staging), Descriptor(4, "docker", "26.1.4"), null, true, HostSituation.UpdateRequired, stats(4, 99, 0)),
            new HostView(Registration(5, EnvironmentStage.Production, enabled: false), Descriptor(5, "docker", "27.3.1"), null, true, HostSituation.Disabled, stats(5, 50, 0)),
        };

        var summary = await service.SummarizeAsync(EnvironmentStage.Production, hosts, TestContext.Current.CancellationToken);

        Assert.Equal(3, summary.HostsTotal);
        Assert.Equal(2, summary.HostsBySituation["up_to_date"]);
        Assert.Equal(1, summary.HostsBySituation["disconnected"]);
        Assert.Equal(20, summary.ContainersTotal);
        Assert.Equal(17, summary.ContainersByDisplayStatus["running"]);
        Assert.Equal(3, summary.ContainersByDisplayStatus["failed"]);
        var docker = Assert.Single(summary.Engines, e => e.Engine == "docker");
        Assert.Equal(2, docker.Hosts);
        Assert.Equal(16, docker.Containers);
        Assert.Equal([new EngineVersionCount("27.3.1", 2)], docker.Versions);
        Assert.Equal(Now.AddMinutes(-3), summary.ObservedAtUtc);
        Assert.Equal(4, summary.AttentionEvents24h);
        Assert.Equal([Id(1), Id(2), Id(3)], events.AttentionScope.Order());
        Assert.Equal(Now.AddHours(-24), events.AttentionSince);
    }

    [Fact]
    public async Task Overview_WithoutHostsSkipsEventQuery()
    {
        var events = new EventStore();
        var summary = await new OverviewQueryService(events, new ManualClock(Now)).SummarizeAsync(null, [], TestContext.Current.CancellationToken);

        Assert.Equal(0, summary.HostsTotal);
        Assert.Null(summary.ObservedAtUtc);
        Assert.Empty(events.AttentionScope);
    }

    [Fact]
    public async Task Events_NotifyOnlyWhenSomethingWasWritten()
    {
        var notifier = new Notifier();
        var store = new EventStore { Written = 0 };
        var service = new OperationalEventService(store, notifier, new ManualClock(Now));

        await service.RecordAsync(Id(1), OperationalEventTypes.AgentConnected, null, null, null, null, TestContext.Current.CancellationToken);
        Assert.Empty(notifier.Kinds);

        store.Written = 1;
        await service.RecordAsync(Id(1), OperationalEventTypes.AgentDisconnected, null, null, null, null, TestContext.Current.CancellationToken);
        Assert.Equal([InvalidationKinds.Events], notifier.Kinds);
        Assert.Equal(OperationalEventSeverities.Warning, store.Appended.Last().Severity);
    }

    private sealed class ManualClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset now = start;

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan by) => now += by;
    }

    private sealed class HostStore(params AgentDescriptor[] descriptors) : IAgentHostStore
    {
        public Task<IReadOnlyDictionary<Guid, AgentDescriptor>> GetDescriptorsAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, AgentDescriptor>>(descriptors.ToDictionary(d => d.EnvironmentId));

        public Task<IReadOnlyDictionary<Guid, AgentConnectionState>> GetConnectionsAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, AgentConnectionState>>(new Dictionary<Guid, AgentConnectionState>());

        public Task UpsertDescriptorAsync(AgentDescriptor descriptor, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool?> SetConnectionAsync(Guid environmentId, bool connected, DateTimeOffset atUtc, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task TouchLastSeenAsync(Guid environmentId, DateTimeOffset atUtc, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>> ListConnectedAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StatisticsStore : IInventoryStatisticsStore
    {
        public Task<IReadOnlyDictionary<Guid, InventoryStatistics>> GetStatisticsAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, InventoryStatistics>>(new Dictionary<Guid, InventoryStatistics>());
    }

    private sealed class SessionStore(params Guid[] active) : IAgentSessionStore
    {
        public ValueTask<AgentSession?> FindActiveAsync(Guid environmentId, CancellationToken cancellationToken) =>
            ValueTask.FromResult(active.Contains(environmentId) ? new AgentSession(environmentId, Guid.NewGuid(), 1) : null);

        public ValueTask<AgentSession> ActivateAsync(Guid environmentId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask InvalidateEnvironmentAsync(Guid environmentId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public bool IsActive(AgentSession session) => true;

        public Task WaitUntilInactiveAsync(AgentSession session, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask DeactivateAsync(AgentSession session, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class EventStore : IOperationalEventStore
    {
        public int Attention { get; init; }
        public int Written { get; set; } = 1;
        public List<OperationalEvent> Appended { get; } = [];
        public Guid[] AttentionScope { get; private set; } = [];
        public DateTimeOffset AttentionSince { get; private set; }

        public Task<int> AppendAsync(IReadOnlyCollection<OperationalEvent> events, CancellationToken cancellationToken)
        {
            Appended.AddRange(events);
            return Task.FromResult(Written);
        }

        public Task<int> CountAttentionAsync(IReadOnlyCollection<Guid> environmentIds, DateTimeOffset sinceUtc, CancellationToken cancellationToken)
        {
            AttentionScope = [.. environmentIds];
            AttentionSince = sinceUtc;
            return Task.FromResult(Attention);
        }

        public Task<OperationalEventPage> ListAsync(OperationalEventQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<long> ApplyRetentionAsync(TimeSpan retention, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class Notifier : IControlPlaneNotifier
    {
        public List<string> Kinds { get; } = [];

        public Task NotifyAsync(Guid environmentId, string kind, CancellationToken cancellationToken)
        {
            Kinds.Add(kind);
            return Task.CompletedTask;
        }
    }
}
