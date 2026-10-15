using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.Domain.Metrics;

namespace Dokpod.ControlPlane.Application.Metrics;

public sealed record StageMetricsCurrent(double? CpuPercent, double? MemoryPercent, int HostsSampled, int HostsTotal, DateTimeOffset? SampledAtUtc);

public sealed record MetricsSeries(IReadOnlyList<DateTimeOffset> X, IReadOnlyList<double?> Cpu, IReadOnlyList<double?> Memory);

public sealed record StageMetrics(StageMetricsCurrent Current, MetricsSeries Series, double OperationalLimitPercent);

public sealed record HostMetricsView(Guid EnvironmentId, double? CpuPercent, long? MemoryUsedBytes, long? MemoryTotalBytes, DateTimeOffset? SampledAtUtc);

public sealed record ContainerMetrics(ContainerMetricsSample? Current, MetricsSeries Series, double OperationalLimitPercent);

/// <summary>Consultas de métricas (spec 009 FR-005/FR-006): sempre sobre hosts já autorizados pelo chamador.</summary>
public sealed class MetricsQueryService(
    LatestMetricsCache latest,
    ContainerMetricsBuffer containers,
    IHostMetricsStore store,
    MetricsOptions options,
    TimeProvider timeProvider)
{
    public double OperationalLimitPercent => options.OperationalLimitPercent;

    public async Task<StageMetrics> GetStageAsync(IReadOnlyList<HostView> hosts, MetricsWindow window, CancellationToken cancellationToken)
    {
        var enabled = hosts.Where(h => h.Registration.Enabled).ToArray();
        // Spec 015 D-005: o valor atual soma o consumo dos containers com amostra recente sobre a capacidade do
        // host (amostra do host ou, na falta, o descritor); a série continua sendo a do host.
        var currentSamples = enabled.Select(h => (Host: h, Containers: latest.GetContainers(h.Registration.EnvironmentId))).ToArray();
        var (cpu, memory, sampled) = MetricsPolicy.Aggregate(currentSamples.Select(x => ContainersOnHost(x.Host, x.Containers)));
        var sampledAt = currentSamples.SelectMany(x => x.Containers).Select(c => (DateTimeOffset?)c.SampledAtUtc).Max();

        // Exatamente `janela ÷ passo` baldes, terminando no balde que contém o minuto atual.
        var end = ContainerMetricsBuffer.Align(ContainerMetricsBuffer.Truncate(timeProvider.GetUtcNow()), window.Step);
        var start = end - (window.Points - 1) * window.Step;
        var rows = enabled.Length == 0
            ? []
            : await store.GetSeriesAsync(enabled.Select(h => h.Registration.EnvironmentId).ToArray(), start, end + window.Step, cancellationToken).ConfigureAwait(false);
        var cpus = enabled.ToDictionary(h => h.Registration.EnvironmentId, h => h.Descriptor?.CpuCount);
        var x = new List<DateTimeOffset>();
        var cpuSeries = new List<double?>();
        var memorySeries = new List<double?>();
        for (var bucket = start; bucket <= end; bucket += window.Step)
        {
            var inBucket = rows.Where(r => r.MinuteUtc >= bucket && r.MinuteUtc < bucket + window.Step)
                .GroupBy(r => r.EnvironmentId)
                .Select(g => (
                    Cpu: g.Where(r => r.CpuPercent is not null).Select(r => r.CpuPercent!.Value).DefaultIfEmpty(double.NaN).Average(),
                    Cpus: cpus.GetValueOrDefault(g.Key),
                    Used: g.Where(r => r.MemoryUsedBytes is not null).Select(r => (double)r.MemoryUsedBytes!.Value).DefaultIfEmpty(double.NaN).Average(),
                    Total: g.Where(r => r.MemoryTotalBytes is not null).Select(r => (double)r.MemoryTotalBytes!.Value).DefaultIfEmpty(double.NaN).Average()))
                .Select(v => (
                    double.IsNaN(v.Cpu) ? (double?)null : v.Cpu,
                    v.Cpus,
                    double.IsNaN(v.Used) ? (long?)null : (long)v.Used,
                    double.IsNaN(v.Total) ? (long?)null : (long)v.Total));
            var (bucketCpu, bucketMemory, _) = MetricsPolicy.Aggregate(inBucket);
            x.Add(bucket);
            cpuSeries.Add(bucketCpu);
            memorySeries.Add(bucketMemory);
        }

        return new StageMetrics(
            new StageMetricsCurrent(cpu, memory, sampled, enabled.Length, sampledAt),
            new MetricsSeries(x, cpuSeries, memorySeries),
            options.OperationalLimitPercent);
    }

    private (double? CpuPercent, int? Cpus, long? MemoryUsedBytes, long? MemoryTotalBytes) ContainersOnHost(
        HostView host,
        IReadOnlyList<ContainerMetricsSample> containers)
    {
        if (containers.Count == 0) return (null, host.Descriptor?.CpuCount, null, null);
        var cpus = containers.Where(c => c.CpuPercentOfHost is not null).Select(c => c.CpuPercentOfHost!.Value).ToArray();
        var memory = containers.Where(c => c.MemoryUsedBytes is not null).Select(c => c.MemoryUsedBytes!.Value).ToArray();
        var total = latest.GetHost(host.Registration.EnvironmentId)?.MemoryTotalBytes ?? host.Descriptor?.MemoryBytes;
        return (
            cpus.Length == 0 ? null : Math.Min(100, cpus.Sum()),
            host.Descriptor?.CpuCount,
            memory.Length == 0 ? null : memory.Sum(),
            total);
    }

    public IReadOnlyList<HostMetricsView> GetHosts(IReadOnlyList<HostView> hosts) =>
        hosts.Select(h =>
        {
            var sample = latest.GetHost(h.Registration.EnvironmentId);
            return new HostMetricsView(h.Registration.EnvironmentId, sample?.CpuPercent, sample?.MemoryUsedBytes, sample?.MemoryTotalBytes, sample?.SampledAtUtc);
        }).ToArray();

    public ContainerMetrics GetContainer(Guid environmentId, string containerId, MetricsWindow window)
    {
        var points = containers.Get(environmentId, containerId, window.Window, window.Step);
        return new ContainerMetrics(
            latest.GetContainer(environmentId, containerId),
            new MetricsSeries(points.Select(p => p.MinuteUtc).ToArray(), points.Select(p => p.Cpu).ToArray(), points.Select(p => p.Memory).ToArray()),
            options.OperationalLimitPercent);
    }

    /// <summary>Recursos atuais de um container para listas (spec 010) e detalhe (spec 011); nulo sem amostra.</summary>
    public ContainerMetricsSample? GetContainerResources(Guid environmentId, string containerId) => latest.GetContainer(environmentId, containerId);
}
