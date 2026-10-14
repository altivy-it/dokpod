using Dokpod.Agent.Contracts.V1;
using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.ControlPlane.Application.Metrics;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Hosts;
using Dokpod.Domain.Metrics;
using Google.Protobuf.WellKnownTypes;
using Xunit;

namespace Dokpod.ControlPlane.Application.Tests.Metrics;

public sealed class MetricsServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 13, 12, 0, 10, TimeSpan.Zero);
    private static readonly Guid Env1 = Guid.Parse("00000000-0000-4000-8000-000000000001");
    private static readonly Guid Env2 = Guid.Parse("00000000-0000-4000-8000-000000000002");

    private sealed class Fixture
    {
        public Clock Clock { get; } = new(T0);
        public MetricsOptions Options { get; } = new();
        public Store Store { get; } = new();
        public LatestMetricsCache Latest { get; }
        public ContainerMetricsBuffer Buffer { get; }
        public MetricsIngestionService Ingestion { get; }
        public MetricsQueryService Query { get; }

        public Fixture()
        {
            Latest = new LatestMetricsCache(Clock, Options);
            Buffer = new ContainerMetricsBuffer(Clock);
            Ingestion = new MetricsIngestionService(Latest, Buffer, new HostMinuteAggregator(), Store, Clock);
            Query = new MetricsQueryService(Latest, Buffer, Store, Options, Clock);
        }
    }

    private static ResourceSampleBatch Batch(DateTimeOffset at, double? hostCpu, ulong? used, ulong? total, params ContainerResourceSample[] containers)
    {
        var host = new HostResourceSample();
        if (hostCpu is { } cpu) host.CpuPercent = cpu;
        if (used is { } u) host.MemoryUsedBytes = u;
        if (total is { } t) host.MemoryTotalBytes = t;
        var batch = new ResourceSampleBatch { SampledAt = Timestamp.FromDateTimeOffset(at), Host = host };
        batch.Containers.AddRange(containers);
        return batch;
    }

    [Fact]
    public async Task Ingest_KeepsLatestAndExpiresAfterThreeIntervals()
    {
        var f = new Fixture();
        await f.Ingestion.IngestAsync(Env1, Batch(T0, 40, 8UL << 30, 16UL << 30,
            new ContainerResourceSample { ContainerId = "c1", CpuPercentOfHost = 12.5, CpuPercentOfLimit = 50, MemoryUsedBytes = 256 << 20, MemoryLimitBytes = 512 << 20 }), TestContext.Current.CancellationToken);

        Assert.Equal(40, f.Latest.GetHost(Env1)!.CpuPercent);
        Assert.Equal(50, f.Query.GetContainerResources(Env1, "c1")!.CpuPercentOfLimit);

        f.Clock.Advance(f.Options.LatestExpiry + TimeSpan.FromSeconds(1));
        Assert.Null(f.Latest.GetHost(Env1));
        Assert.Null(f.Query.GetContainerResources(Env1, "c1"));
    }

    [Fact]
    public async Task Ingest_ReplacesSkewedAgentClockWithReceptionTime()
    {
        var f = new Fixture();
        await f.Ingestion.IngestAsync(Env1, Batch(T0.AddHours(-3), 10, null, null), TestContext.Current.CancellationToken);

        Assert.Equal(T0, f.Latest.GetHost(Env1)!.SampledAtUtc);
    }

    [Fact]
    public async Task Ingest_PersistsOnlyClosedHostMinutesAndNeverContainers()
    {
        var f = new Fixture();
        var c = new ContainerResourceSample { ContainerId = "c1", CpuPercentOfHost = 10, MemoryUsedBytes = 1 };
        await f.Ingestion.IngestAsync(Env1, Batch(T0, 20, 4, 10, c), TestContext.Current.CancellationToken);
        f.Clock.Advance(TimeSpan.FromSeconds(15));
        await f.Ingestion.IngestAsync(Env1, Batch(T0.AddSeconds(15), 40, 6, 10, c), TestContext.Current.CancellationToken);
        Assert.Empty(f.Store.Upserts);

        f.Clock.Advance(TimeSpan.FromSeconds(50)); // próximo minuto
        await f.Ingestion.IngestAsync(Env1, Batch(T0.AddSeconds(65), 90, 9, 10, c), TestContext.Current.CancellationToken);

        var row = Assert.Single(f.Store.Upserts);
        Assert.Equal(new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero), row.MinuteUtc);
        Assert.Equal(30, row.CpuPercent);
        Assert.Equal(6, row.MemoryUsedBytes);
        Assert.Equal(2, row.SampleCount);

        f.Clock.Advance(TimeSpan.FromMinutes(2));
        await f.Ingestion.FlushAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, f.Store.Upserts.Count);
        Assert.Equal(90, f.Store.Upserts[1].CpuPercent);
    }

    [Fact]
    public async Task ContainerSeries_IsMinuteAveragedWithGapsAsNullAndMemoryAsPercent()
    {
        var f = new Fixture();
        for (var i = 0; i < 4; i++)
        {
            if (i == 2) { f.Clock.Advance(TimeSpan.FromMinutes(1)); continue; } // minuto sem amostra
            await f.Ingestion.IngestAsync(Env1, Batch(f.Clock.GetUtcNow(), null, null, null,
                new ContainerResourceSample { ContainerId = "c1", CpuPercentOfHost = 10 * (i + 1), MemoryUsedBytes = 128 << 20, MemoryLimitBytes = 512 << 20 }), TestContext.Current.CancellationToken);
            f.Clock.Advance(TimeSpan.FromMinutes(1));
        }

        var series = f.Query.GetContainer(Env1, "c1", new MetricsWindow(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(1))).Series;

        Assert.Equal(30, series.X.Count);
        Assert.Equal([10, 20, null, 40, null], series.Cpu.TakeLast(5));
        Assert.Equal(25, series.Memory[^2]);
        Assert.Null(series.Memory[^1]);
    }

    [Fact]
    public async Task Stage_WeightsCurrentCpuByCoresAndBuildsSeriesFromStore()
    {
        var f = new Fixture();
        await f.Ingestion.IngestAsync(Env1, Batch(T0, 40, 32UL << 30, 64UL << 30), TestContext.Current.CancellationToken);
        await f.Ingestion.IngestAsync(Env2, Batch(T0, 80, 12UL << 30, 16UL << 30), TestContext.Current.CancellationToken);
        var minute = new DateTimeOffset(2026, 10, 13, 11, 0, 0, TimeSpan.Zero);
        f.Store.Rows.AddRange([
            new HostMinuteSample(Env1, minute, 40, 32L << 30, 64L << 30, 4),
            new HostMinuteSample(Env2, minute, 80, 12L << 30, 16L << 30, 4),
        ]);

        var result = await f.Query.GetStageAsync([Host(Env1, 16), Host(Env2, 4), Host(Guid.NewGuid(), 8)], new MetricsWindow(TimeSpan.FromHours(12), TimeSpan.FromHours(1)), TestContext.Current.CancellationToken);

        Assert.Equal(48, result.Current.CpuPercent);
        Assert.Equal(55, result.Current.MemoryPercent);
        Assert.Equal(2, result.Current.HostsSampled);
        Assert.Equal(3, result.Current.HostsTotal);
        Assert.Equal(12, result.Series.X.Count);
        var index = result.Series.X.ToList().IndexOf(minute);
        Assert.Equal(48, result.Series.Cpu[index]);
        Assert.Null(result.Series.Cpu[0]);
        Assert.Equal(80, result.OperationalLimitPercent);
    }

    private static HostView Host(Guid id, int cpus) => new(
        EnvironmentRegistration.Create(id, $"h-{id:N}"[..10], "h.local", true, ["environment:read"], EnvironmentStage.Production),
        new AgentDescriptor(id, "docker", "27.3.1", "1.47", false, "linux", "24.04", "amd64", "0.9.0", [], "h", cpus, 1L << 34, T0),
        null, true, HostSituation.UpToDate, null);

    private sealed class Clock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset now = start;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan by) => now += by;
    }

    private sealed class Store : IHostMetricsStore
    {
        public List<HostMinuteSample> Upserts { get; } = [];
        public List<HostMinuteSample> Rows { get; } = [];

        public Task UpsertAsync(IReadOnlyCollection<HostMinuteSample> samples, CancellationToken cancellationToken)
        {
            Upserts.AddRange(samples);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<HostMinuteSample>> GetSeriesAsync(IReadOnlyCollection<Guid> environmentIds, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<HostMinuteSample>>(Rows.Where(r => environmentIds.Contains(r.EnvironmentId) && r.MinuteUtc >= fromUtc && r.MinuteUtc < toUtc).ToArray());

        public Task<long> ApplyRetentionAsync(TimeSpan retention, CancellationToken cancellationToken) => Task.FromResult(0L);
    }
}
