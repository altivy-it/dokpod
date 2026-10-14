using Dokpod.Agent.Application.Engines;
using Dokpod.Agent.Application.Metrics;
using Dokpod.Agent.Contracts.V1;
using Dokpod.Domain.Commands;
using Google.Protobuf.Reflection;
using Xunit;

namespace Dokpod.Agent.Application.Tests.Metrics;

public sealed class ResourceSamplerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task FirstSampleHasMemoryOnlyAndSecondAddsCpuAndRates()
    {
        var clock = new Clock(Start);
        var engine = new StatsEngine();
        engine.Stats["a"] = new EngineContainerStats(1_000, 100_000, 4, 50 << 20, 512 << 20, 0, 0, 0, 0);
        var sampler = new ResourceSampler(engine, new Probe(), clock);

        var first = (await sampler.SampleAsync([new SampleTarget("a", 2)], TestContext.Current.CancellationToken)).Single().Containers.Single();
        Assert.Equal(50UL << 20, first.MemoryUsedBytes);
        Assert.Equal(512UL << 20, first.MemoryLimitBytes);
        Assert.False(first.HasCpuPercentOfHost);
        Assert.False(first.HasNetRxBps);

        clock.Advance(TimeSpan.FromSeconds(15));
        engine.Stats["a"] = new EngineContainerStats(4_000, 112_000, 4, 60 << 20, 512 << 20, 15_360, 30_720, 0, 1_500);
        var second = (await sampler.SampleAsync([new SampleTarget("a", 2)], TestContext.Current.CancellationToken)).Single().Containers.Single();
        Assert.Equal(25, second.CpuPercentOfHost);   // 3.000 / 12.000
        Assert.Equal(50, second.CpuPercentOfLimit);  // 1 núcleo de 2
        Assert.Equal(2, second.CpuLimitCores);
        Assert.Equal(1_024UL, second.NetRxBps);
        Assert.Equal(2_048UL, second.NetTxBps);
        Assert.Equal(100UL, second.BlkWriteBps);
    }

    [Fact]
    public async Task ContainersAboveCapacityRotateAndAreCountedAsDropped()
    {
        var engine = new StatsEngine();
        var targets = Enumerable.Range(0, 7).Select(i => new SampleTarget($"c{i}", null)).ToArray();
        foreach (var t in targets) engine.Stats[t.ContainerId] = new EngineContainerStats(1, 1, 1, 1, null, 0, 0, 0, 0);
        var sampler = new ResourceSampler(engine, new Probe(), new Clock(Start), maxContainersPerMessage: 2, maxMessagesPerInterval: 2);

        var cycle1 = await sampler.SampleAsync(targets, TestContext.Current.CancellationToken);
        var cycle2 = await sampler.SampleAsync(targets, TestContext.Current.CancellationToken);

        Assert.Equal(2, cycle1.Count);
        Assert.All(cycle1, b => Assert.True(b.Containers.Count <= 2));
        Assert.Equal(3U, cycle1[0].DroppedCount);
        Assert.Equal(["c0", "c1", "c2", "c3"], cycle1.SelectMany(b => b.Containers).Select(c => c.ContainerId));
        Assert.Equal(["c4", "c5", "c6", "c0"], cycle2.SelectMany(b => b.Containers).Select(c => c.ContainerId));
        Assert.NotNull(cycle1[0].Host);
        Assert.Null(cycle1[1].Host);
    }

    [Fact]
    public async Task HostCpuNeedsTwoReadingsAndVirtualizedProcIsUnavailable()
    {
        var probe = new Probe { Reading = new HostResourceReading(1_000, 400, 16UL << 30, 4UL << 30) };
        var sampler = new ResourceSampler(new StatsEngine(), probe, new Clock(Start)) { EngineMemoryTotalBytes = 16L << 30 };

        var first = (await sampler.SampleAsync([], TestContext.Current.CancellationToken)).Single().Host;
        Assert.False(first.HasCpuPercent);
        Assert.Equal(12UL << 30, first.MemoryUsedBytes);

        probe.Reading = new HostResourceReading(2_000, 650, 16UL << 30, 4UL << 30);
        Assert.Equal(75, (await sampler.SampleAsync([], TestContext.Current.CancellationToken)).Single().Host.CpuPercent);

        sampler.EngineMemoryTotalBytes = 64L << 30; // `/proc` mostra 16 GiB, engine 64 GiB: lxcfs
        var virtualized = (await sampler.SampleAsync([], TestContext.Current.CancellationToken)).Single().Host;
        Assert.False(virtualized.HasCpuPercent);
        Assert.False(virtualized.HasMemoryUsedBytes);
    }

    [Fact]
    public async Task StatsFailureSkipsOnlyThatContainerAndStoppedContainersArePruned()
    {
        var engine = new StatsEngine();
        engine.Stats["ok"] = new EngineContainerStats(1, 1, 1, 1, null, 0, 0, 0, 0);
        var sampler = new ResourceSampler(engine, new Probe(), new Clock(Start));

        var batch = (await sampler.SampleAsync([new SampleTarget("ok", null), new SampleTarget("gone", null)], TestContext.Current.CancellationToken)).Single();

        Assert.Equal(["ok"], batch.Containers.Select(c => c.ContainerId));
    }

    [Fact]
    public void Payload_HasNoTextFieldBesidesTheContainerId()
    {
        // SC-006: nenhum campo pode transportar processo, argumento, variável ou label.
        static IEnumerable<FieldDescriptor> Strings(MessageDescriptor descriptor) =>
            descriptor.Fields.InFieldNumberOrder().Where(f => f.FieldType is FieldType.String or FieldType.Bytes);

        Assert.Empty(Strings(ResourceSampleBatch.Descriptor));
        Assert.Empty(Strings(HostResourceSample.Descriptor));
        Assert.Equal(["container_id"], Strings(ContainerResourceSample.Descriptor).Select(f => f.Name));
        Assert.DoesNotContain(ContainerResourceSample.Descriptor.Fields.InFieldNumberOrder(), f => f.IsMap || f.FieldType == FieldType.Message);
    }

    private sealed class Clock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset now = start;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan by) => now += by;
    }

    private sealed class Probe : IHostResourceProbe
    {
        public HostResourceReading? Reading { get; set; }
        public HostResourceReading? Read() => Reading;
    }

    private sealed class StatsEngine : IContainerEngine
    {
        public Dictionary<string, EngineContainerStats> Stats { get; } = new(StringComparer.Ordinal);

        public Task<EngineContainerStats?> GetContainerStatsAsync(string containerId, CancellationToken cancellationToken) =>
            Task.FromResult(Stats.TryGetValue(containerId, out var stats) ? stats : null);

        public Task<EngineDescriptor> InspectAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<EngineContainer>> ListContainersAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EngineContainer?> InspectContainerAsync(string containerId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ContainerMutationResult> ExecuteAsync(AgentCommandKind commandKind, string containerId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
