using Dokpod.Domain.Metrics;
using Xunit;

namespace Dokpod.Domain.Tests.Metrics;

public sealed class MetricsDomainTests
{
    [Theory]
    // 4 CPUs, container usou 1 núcleo inteiro no intervalo (25% do host); sem limite, referência = host.
    [InlineData(1_000, 4_000, 4, null, 25, 25)]
    // limite de 2 núcleos: 1 núcleo = 50% do limite.
    [InlineData(1_000, 4_000, 4, 2.0, 25, 50)]
    // limite de 0,5 núcleo excedido momentaneamente: 200% do limite.
    [InlineData(1_000, 4_000, 4, 0.5, 25, 200)]
    public void Container_ComputesShareOfHostAndOfLimit(long containerDelta, long systemDelta, int cpus, double? limit, double host, double ofLimit)
    {
        var previous = new ContainerCpuCounters(10_000, 100_000, cpus);
        var current = new ContainerCpuCounters(10_000 + (ulong)containerDelta, 100_000 + (ulong)systemDelta, cpus);

        var usage = CpuUsageCalculator.Container(previous, current, limit);

        Assert.Equal(host, usage!.Value.PercentOfHost);
        Assert.Equal(ofLimit, usage.Value.PercentOfLimit);
    }

    [Theory]
    [InlineData(500UL, 200_000UL)]   // contador do container reiniciou
    [InlineData(20_000UL, 100_000UL)] // sistema não avançou
    public void Container_ResetCountersAreGapsNotZeroOrSpikes(ulong containerNow, ulong systemNow)
    {
        Assert.Null(CpuUsageCalculator.Container(new(10_000, 100_000, 4), new(containerNow, systemNow, 4), null));
    }

    [Fact]
    public void Host_ComputesBusyShareAndRejectsResets()
    {
        Assert.Equal(75, CpuUsageCalculator.Host(1_000, 400, 2_000, 650));
        Assert.Null(CpuUsageCalculator.Host(2_000, 650, 1_000, 400));
        Assert.Null(CpuUsageCalculator.Host(1_000, 400, 1_000, 400));
    }

    [Fact]
    public void Rate_IsBytesPerSecondAndGapOnReset()
    {
        Assert.Equal(1_024UL, CpuUsageCalculator.Rate(0, 15_360, TimeSpan.FromSeconds(15)));
        Assert.Null(CpuUsageCalculator.Rate(15_360, 0, TimeSpan.FromSeconds(15)));
        Assert.Null(CpuUsageCalculator.Rate(0, 1, TimeSpan.Zero));
    }

    [Theory]
    [InlineData("12h", "1h", true, 12)]
    [InlineData("24h", "1m", true, 1440)]
    [InlineData("30m", "1m", true, 30)]
    [InlineData("1h", "15m", true, 4)]
    [InlineData("12h", "1m", true, 720)]
    [InlineData("6h", "1h", true, 6)]
    [InlineData("1h", "1h", true, 1)]
    [InlineData("30m", "1h", false, 0)]
    [InlineData("2d", "1h", false, 0)]
    [InlineData("12h", "2m", false, 0)]
    public void Window_AcceptsKnownCombinationsUpTo1440Points(string window, string step, bool ok, int points)
    {
        Assert.Equal(ok, MetricsPolicy.TryParse(window, step, TimeSpan.FromHours(24), out var parsed));
        if (ok) Assert.Equal(points, parsed.Points);
    }

    [Fact]
    public void Window_RespectsMaximumWindowOfTheSeries()
    {
        Assert.True(MetricsPolicy.TryParse("30m", "1m", MetricsPolicy.ContainerSeriesWindow, out _));
        Assert.False(MetricsPolicy.TryParse("1h", "1m", MetricsPolicy.ContainerSeriesWindow, out _));
    }

    [Fact]
    public void Aggregate_WeightsCpuByCoresAndIgnoresHostsWithoutSample()
    {
        var (cpu, memory, sampled) = MetricsPolicy.Aggregate(
        [
            (40, 16, 32L << 30, 64L << 30),
            (80, 4, 12L << 30, 16L << 30),
            (null, 8, null, null),
        ]);

        Assert.Equal(48, cpu);      // (40×16 + 80×4) / 20
        Assert.Equal(55, memory);   // 44 GiB / 80 GiB
        Assert.Equal(2, sampled);
    }

    [Fact]
    public void Aggregate_WithoutSamplesIsUnavailable()
    {
        var (cpu, memory, sampled) = MetricsPolicy.Aggregate([(null, 8, null, null)]);
        Assert.Null(cpu);
        Assert.Null(memory);
        Assert.Equal(0, sampled);
    }
}
