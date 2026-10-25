using System.Diagnostics.Metrics;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Hosts;
using Xunit;

namespace Dokpod.ControlPlane.Application.Tests.Hosts;

/// <summary>Spec 005, gate de observabilidade: métricas do plano de controle (ADR 2026-0001 e 2026-0002).</summary>
public sealed class ControlPlaneMetricsTests
{
    [Fact]
    public void Connected_agents_are_counted_by_engine_and_version()
    {
        using var metrics = new ControlPlaneMetrics(new AgentLivenessTracker(TimeProvider.System));
        using var recorded = Recorded.For(metrics);

        metrics.AgentConnected("docker", "1.4.0");
        metrics.AgentConnected("podman", "1.4.0");
        metrics.AgentDisconnected("docker", "1.4.0");

        Assert.Equal(1, recorded.Sum("dokpod.agents.connected", ("agent.engine", "podman")));
        Assert.Equal(0, recorded.Sum("dokpod.agents.connected", ("agent.engine", "docker")));
    }

    [Fact]
    public void Command_completion_records_state_failure_and_duration()
    {
        using var metrics = new ControlPlaneMetrics(new AgentLivenessTracker(TimeProvider.System));
        using var recorded = Recorded.For(metrics);

        metrics.CommandCompleted("RestartContainer", "succeeded", null, TimeSpan.FromMilliseconds(300));
        metrics.CommandCompleted("RestartContainer", "failed", "stale_target", TimeSpan.FromMilliseconds(120));
        metrics.CommandCompleted("unknown", "succeeded", null, null);

        Assert.Equal(3, recorded.Sum("dokpod.commands.completed"));
        Assert.Equal(1, recorded.Sum("dokpod.commands.completed", ("command.failure_code", "stale_target")));
        Assert.Equal(2, recorded.Count("dokpod.command.duration"));
    }

    [Fact]
    public void Authorization_decisions_are_tagged_by_outcome()
    {
        using var metrics = new ControlPlaneMetrics(new AgentLivenessTracker(TimeProvider.System));
        using var recorded = Recorded.For(metrics);

        metrics.AuthorizationDecided(AuthorizationDecisionOutcome.Allowed, null, TimeSpan.FromMilliseconds(8));
        metrics.AuthorizationDecided(AuthorizationDecisionOutcome.Indeterminate, "authorization_unavailable", TimeSpan.FromSeconds(15));

        Assert.Equal(1, recorded.Sum("dokpod.authorization.decisions", ("authorization.outcome", "indeterminate")));
        Assert.Equal(2, recorded.Count("dokpod.authorization.duration"));
    }

    [Fact]
    public void Inventory_lag_and_heartbeat_age_come_from_the_liveness_tracker()
    {
        var time = new ManualTime(new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero));
        var liveness = new AgentLivenessTracker(time);
        using var metrics = new ControlPlaneMetrics(liveness);
        using var recorded = Recorded.For(metrics);
        var synced = Guid.NewGuid();
        var lagging = Guid.NewGuid();

        liveness.RecordHeartbeat(synced, 5);
        liveness.RecordProjected(synced, 5);
        liveness.RecordHeartbeat(lagging, 9);
        liveness.RecordProjected(lagging, 7);
        time.Advance(TimeSpan.FromSeconds(40));
        liveness.RecordHeartbeat(synced, 5);
        recorded.Collect();

        Assert.Equal(40, recorded.Last("dokpod.inventory.lag"));
        Assert.Equal(40, recorded.Last("dokpod.agents.heartbeat.age"));
    }

    private sealed class ManualTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan by) => now += by;
    }

    private sealed class Recorded : IDisposable
    {
        private readonly MeterListener listener = new();
        private readonly List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)> values = [];

        private Recorded(ControlPlaneMetrics metrics)
        {
            listener.InstrumentPublished = (instrument, l) =>
            {
                if (ReferenceEquals(instrument.Meter, metrics.Meter)) l.EnableMeasurementEvents(instrument);
            };
            listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Add(instrument, value, tags));
            listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Add(instrument, value, tags));
            listener.Start();
        }

        public static Recorded For(ControlPlaneMetrics metrics) => new(metrics);

        public void Collect() => listener.RecordObservableInstruments();

        public double Sum(string name, params (string Key, string Value)[] filter) =>
            Matching(name, filter).Sum(item => item.Value);

        public int Count(string name) => Matching(name, []).Count();

        public double Last(string name) => Matching(name, []).Last().Value;

        public void Dispose() => listener.Dispose();

        private IEnumerable<(string Name, double Value, KeyValuePair<string, object?>[] Tags)> Matching(string name, (string Key, string Value)[] filter)
        {
            lock (values)
            {
                return values
                    .Where(item => item.Name == name && filter.All(f => item.Tags.Any(t => t.Key == f.Key && Equals(t.Value, f.Value))))
                    .ToArray();
            }
        }

        private void Add(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            lock (values) values.Add((instrument.Name, value, tags.ToArray()));
        }
    }
}
