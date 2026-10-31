using Dokpod.Agent.Contracts.V1;
using Dokpod.ControlPlane.Api.Agents;
using Dokpod.ControlPlane.Application.Agents;
using Dokpod.ControlPlane.Application.Events;
using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.ControlPlane.Application.Realtime;
using Dokpod.Domain.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests.Agents;

/// <summary>
/// Spec 005 T016: no restart da API com 100 agentes, o PostgreSQL recusou conexões (53300) e o registro do
/// início de algumas sessões falhou; os hosts ficaram desconectados no console com o agente ativo.
/// </summary>
public sealed class AgentHostActivityTests
{
    [Fact]
    public async Task Failed_session_start_registration_is_retried_on_the_next_heartbeat()
    {
        var ct = TestContext.Current.CancellationToken;
        var environment = Guid.NewGuid();
        var store = new FlakyHostStore(failuresLeft: 1);
        var events = new RecordingEventStore();
        var activity = Create(store, events);

        await activity.OnSessionStartedAsync(new AgentSession(environment, Guid.NewGuid(), 1), Hello(), ct);
        Assert.Null(store.Connected);

        await activity.OnHeartbeatAsync(environment, 1, ct);
        Assert.True(store.Connected);
        Assert.Equal(1, store.DescriptorWrites);
        Assert.Single(events.Events, item => item.Type == OperationalEventTypes.AgentConnected);

        await activity.OnHeartbeatAsync(environment, 2, ct);
        Assert.Equal(1, store.DescriptorWrites);
        Assert.Single(events.Events, item => item.Type == OperationalEventTypes.AgentConnected);
    }

    [Fact]
    public async Task Successful_session_start_is_not_repeated_on_heartbeats()
    {
        var ct = TestContext.Current.CancellationToken;
        var environment = Guid.NewGuid();
        var store = new FlakyHostStore(failuresLeft: 0);
        var activity = Create(store, new RecordingEventStore());

        await activity.OnSessionStartedAsync(new AgentSession(environment, Guid.NewGuid(), 1), Hello(), ct);
        await activity.OnHeartbeatAsync(environment, 1, ct);

        Assert.True(store.Connected);
        Assert.Equal(1, store.DescriptorWrites);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ended_session_leaves_the_connected_agents_metric_even_when_replaced(bool replaced)
    {
        var ct = TestContext.Current.CancellationToken;
        var liveness = new AgentLivenessTracker(TimeProvider.System);
        using var metrics = new ControlPlaneMetrics(liveness);
        long connected = 0;
        using var listener = new System.Diagnostics.Metrics.MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (ReferenceEquals(instrument.Meter, metrics.Meter) && instrument.Name == "dokpod.agents.connected") l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => Interlocked.Add(ref connected, value));
        listener.Start();
        var session = new AgentSession(Guid.NewGuid(), Guid.NewGuid(), 1);
        var activity = Create(new FlakyHostStore(failuresLeft: 0), new RecordingEventStore(), metrics);

        await activity.OnSessionStartedAsync(session, Hello(), ct);
        Assert.Equal(1, Interlocked.Read(ref connected));

        await activity.OnSessionEndedAsync(session, replaced);
        Assert.Equal(0, Interlocked.Read(ref connected));
    }

    internal static AgentHostActivity Create(
        FlakyHostStore store,
        RecordingEventStore events,
        ControlPlaneMetrics? metrics = null,
        Dokpod.ControlPlane.Application.Licensing.IAgentCapacityObserver? capacityObserver = null)
    {
        var notifier = new NullControlPlaneNotifier();
        return new AgentHostActivity(
            store,
            new AgentLivenessTracker(TimeProvider.System),
            new OperationalEventService(events, notifier, TimeProvider.System),
            null!,
            notifier,
            new EngineSupportOptions(),
            TimeProvider.System,
            NullLogger<AgentHostActivity>.Instance,
            metrics,
            capacityObserver);
    }

    internal static AgentHello Hello() => new()
    {
        Engine = EngineKind.Docker,
        EngineVersion = "28.0.0",
        HostName = "carga-001",
        AgentVersion = "1.0.0",
    };

    internal sealed class FlakyHostStore(int failuresLeft) : IAgentHostStore
    {
        public bool? Connected { get; private set; }
        public int DescriptorWrites { get; private set; }

        public Task UpsertDescriptorAsync(AgentDescriptor descriptor, CancellationToken cancellationToken)
        {
            if (failuresLeft-- > 0) throw new InvalidOperationException("53300: sorry, too many clients already");
            DescriptorWrites++;
            return Task.CompletedTask;
        }

        public Task<bool?> SetConnectionAsync(Guid environmentId, bool connected, DateTimeOffset atUtc, CancellationToken cancellationToken)
        {
            var previous = Connected;
            Connected = connected;
            return Task.FromResult(previous);
        }

        public Task TouchLastSeenAsync(Guid environmentId, DateTimeOffset atUtc, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyDictionary<Guid, AgentDescriptor>> GetDescriptorsAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<Guid, AgentConnectionState>> GetConnectionsAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>> ListConnectedAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    internal sealed class RecordingEventStore : IOperationalEventStore
    {
        public List<OperationalEvent> Events { get; } = [];

        public Task<int> AppendAsync(IReadOnlyCollection<OperationalEvent> events, CancellationToken cancellationToken)
        {
            Events.AddRange(events);
            return Task.FromResult(events.Count);
        }

        public Task<OperationalEventPage> ListAsync(OperationalEventQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<int> CountAttentionAsync(IReadOnlyCollection<Guid> environmentIds, DateTimeOffset sinceUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<long> ApplyRetentionAsync(TimeSpan retention, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
