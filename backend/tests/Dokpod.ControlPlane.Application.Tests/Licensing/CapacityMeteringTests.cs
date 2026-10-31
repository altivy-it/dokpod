using Dokpod.ControlPlane.Application.Licensing;
using Xunit;

namespace Dokpod.ControlPlane.Application.Tests.Licensing;

/// <summary>Spec 031 T032 (SC-004): linhas do cenário 3 do quickstart, com relógio e hosts sintéticos.</summary>
public sealed class CapacityMeteringTests
{
    private static readonly DateTimeOffset Now = new(2026, 12, 1, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Today = new(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Band16_With16_17_32_IsCurrent5()
    {
        var input = new Builder()
            .Host("a", Today.AddDays(-5), 16)
            .Host("b", Today.AddDays(-5), 17)
            .Host("c", Today.AddDays(-5), 32)
            .Build();

        var snapshot = CapacityMeter.Compute(input, 16, Now);

        Assert.Equal((5, 5), (snapshot.Current, snapshot.WindowPeak));
    }

    [Fact]
    public void PeaksAtDifferentHours_UseTheSimultaneousTotalNotTheSumOfPeaks()
    {
        var builder = new Builder();
        var x = builder.HostId("x");
        var y = builder.HostId("y");
        var input = builder
            .Host("x", Today.AddHours(10), 32)
            .Observe(x, Today.AddHours(12), 16)
            .Host("y", Today.AddHours(10), 16)
            .Observe(y, Today.AddHours(15), 48)
            .Build();

        var snapshot = CapacityMeter.Compute(input, 16, Now);

        Assert.Equal(4, snapshot.WindowPeak);
        Assert.Equal(Today.AddHours(15), snapshot.WindowPeakAtUtc);
        Assert.Equal(4, snapshot.Current);
    }

    [Fact]
    public void HostWithDockerAndPodman_CountsOnceWithTheLargerCapacity()
    {
        var builder = new Builder();
        var input = builder.Host("dual", Today.AddDays(-1), 32).SecondEnvironment("dual", Today.AddDays(-1), 32).Build();

        var snapshot = CapacityMeter.Compute(input, 16, Now);

        Assert.Equal(2, snapshot.Current);
        Assert.Single(snapshot.Hosts);
    }

    [Fact]
    public void AgentWithoutCpuSource_IsUnknownAndKeepsTheLastTrustedValue()
    {
        var builder = new Builder();
        var host = builder.HostId("old");
        var input = builder.Host("old", Today.AddDays(-3), 32).Observe(host, Today.AddDays(-1), null, CapacitySource.Unknown).Build();

        var snapshot = CapacityMeter.Compute(input, 16, Now);

        Assert.Equal(1, snapshot.UnknownHosts);
        Assert.Equal(2, snapshot.Current);
        Assert.Equal(CapacitySource.Unknown, snapshot.Hosts.Single().Source);
        Assert.Equal(32, snapshot.Hosts.Single().Vcpus);
    }

    [Fact]
    public void ProcessFallbackCount_NeverEntersTheTotal()
    {
        var builder = new Builder();
        var input = builder.Host("fallback", Today.AddDays(-1), null, CapacitySource.ProcessFallback).Build();

        var snapshot = CapacityMeter.Compute(input, 16, Now);

        Assert.Equal((0, 1), (snapshot.Current, snapshot.UnknownHosts));
    }

    [Fact]
    public void HostRemovedTenDaysAgo_StillCountsInTheWindowPeak()
    {
        var builder = new Builder();
        var input = builder
            .Host("gone", Today.AddDays(-20), 64)
            .Remove("gone", Today.AddDays(-10))
            .Host("stay", Today.AddDays(-20), 16)
            .Build();

        var snapshot = CapacityMeter.Compute(input, 16, Now);

        Assert.Equal(5, snapshot.WindowPeak);
        Assert.Equal(1, snapshot.Current);
    }

    [Fact]
    public void BandChange_RecomputesFromRawData()
    {
        var input = new Builder()
            .Host("a", Today.AddDays(-5), 16)
            .Host("b", Today.AddDays(-5), 17)
            .Host("c", Today.AddDays(-5), 32)
            .Build();

        Assert.Equal(5, CapacityMeter.Compute(input, 16, Now).Current);
        Assert.Equal(3, CapacityMeter.Compute(input, 32, Now).Current);
    }

    [Theory]
    [InlineData(32, 1)]
    [InlineData(33, 2)]
    [InlineData(64, 2)]
    [InlineData(65, 3)]
    public void Band32_RoundsEachHostUp(int vcpus, int expected)
    {
        var input = new Builder().Host("h", Today.AddDays(-1), vcpus).Build();

        Assert.Equal(expected, CapacityMeter.Compute(input, 32, Now).Current);
    }

    [Fact]
    public void DisconnectedHost_KeepsTheAnchorFromBeforeTheWindow()
    {
        var input = new Builder().Host("quiet", Now.AddDays(-60), 48).Build();

        var snapshot = CapacityMeter.Compute(input, 16, Now);

        Assert.Equal((3, 3), (snapshot.Current, snapshot.WindowPeak));
    }

    [Fact]
    public void EnvironmentMovedToAnotherHost_StopsCountingOnTheOldOne()
    {
        var builder = new Builder();
        var input = builder.Host("first", Today.AddDays(-5), 32).Move("first", "second", Today.AddDays(-2)).Build();

        var snapshot = CapacityMeter.Compute(input, 16, Now);

        Assert.Equal(2, snapshot.Current);
        Assert.Equal(2, snapshot.WindowPeak);
    }

    private sealed class Builder
    {
        private readonly Dictionary<string, Guid> hosts = [];
        private readonly Dictionary<string, Guid> environments = [];
        private readonly List<LicensingHostRecord> hostRecords = [];
        private readonly List<LicensingHostLink> links = [];
        private readonly List<CapacityObservation> observations = [];

        public Guid HostId(string name) => hosts.TryGetValue(name, out var id) ? id : hosts[name] = Guid.NewGuid();

        public Builder Host(string name, DateTimeOffset at, int? vcpus, string source = CapacitySource.Engine)
        {
            var host = HostId(name);
            var environment = environments[name] = Guid.NewGuid();
            hostRecords.Add(new LicensingHostRecord(host, name, at, null));
            links.Add(new LicensingHostLink(host, environment, at, null));
            observations.Add(new CapacityObservation(host, at, vcpus, source, environment));
            return this;
        }

        public Builder SecondEnvironment(string name, DateTimeOffset at, int vcpus)
        {
            var host = HostId(name);
            var environment = Guid.NewGuid();
            links.Add(new LicensingHostLink(host, environment, at, null));
            observations.Add(new CapacityObservation(host, at, vcpus, CapacitySource.Engine, environment));
            return this;
        }

        public Builder Observe(Guid host, DateTimeOffset at, int? vcpus, string source = CapacitySource.Engine)
        {
            var environment = links.First(link => link.HostId == host).EnvironmentId;
            observations.Add(new CapacityObservation(host, at, vcpus, source, environment));
            return this;
        }

        public Builder Remove(string name, DateTimeOffset at)
        {
            var host = HostId(name);
            var index = hostRecords.FindIndex(record => record.HostId == host);
            hostRecords[index] = hostRecords[index] with { RemovedAtUtc = at };
            for (var i = 0; i < links.Count; i++)
            {
                if (links[i].HostId == host && links[i].UnlinkedAtUtc is null) links[i] = links[i] with { UnlinkedAtUtc = at };
            }

            return this;
        }

        public Builder Move(string from, string to, DateTimeOffset at)
        {
            var environment = environments[from];
            var source = HostId(from);
            var target = HostId(to);
            var index = links.FindIndex(link => link.HostId == source && link.EnvironmentId == environment);
            links[index] = links[index] with { UnlinkedAtUtc = at };
            hostRecords.Add(new LicensingHostRecord(target, to, at, null));
            links.Add(new LicensingHostLink(target, environment, at, null));
            return this;
        }

        public MeteringInput Build() => new(hostRecords, links, observations);
    }
}
