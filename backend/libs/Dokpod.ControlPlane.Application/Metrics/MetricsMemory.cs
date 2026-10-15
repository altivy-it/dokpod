using System.Collections.Concurrent;

namespace Dokpod.ControlPlane.Application.Metrics;

/// <summary>Último valor por host e por container (spec 009 FR-003); expira sem amostras recentes.</summary>
public sealed class LatestMetricsCache(TimeProvider timeProvider, MetricsOptions options)
{
    private readonly ConcurrentDictionary<Guid, HostMetricsSample> hosts = new();
    private readonly ConcurrentDictionary<(Guid, string), ContainerMetricsSample> containers = new();

    public void SetHost(Guid environmentId, HostMetricsSample sample) => hosts[environmentId] = sample;

    public void SetContainer(Guid environmentId, string containerId, ContainerMetricsSample sample) => containers[(environmentId, containerId)] = sample;

    public HostMetricsSample? GetHost(Guid environmentId) =>
        hosts.TryGetValue(environmentId, out var sample) && Fresh(sample.SampledAtUtc) ? sample : null;

    public ContainerMetricsSample? GetContainer(Guid environmentId, string containerId) =>
        containers.TryGetValue((environmentId, containerId), out var sample) && Fresh(sample.SampledAtUtc) ? sample : null;

    /// <summary>Containers do host com amostra recente e seus ids (spec 016: regras de limite por container).</summary>
    public IReadOnlyList<(string ContainerId, ContainerMetricsSample Sample)> GetContainersWithIds(Guid environmentId) =>
        containers.Where(entry => entry.Key.Item1 == environmentId && Fresh(entry.Value.SampledAtUtc)).Select(entry => (entry.Key.Item2, entry.Value)).ToArray();

    /// <summary>Containers do host com amostra recente (spec 015: agregado do estágio pelos containers).</summary>
    public IReadOnlyList<ContainerMetricsSample> GetContainers(Guid environmentId) =>
        containers.Where(entry => entry.Key.Item1 == environmentId && Fresh(entry.Value.SampledAtUtc)).Select(entry => entry.Value).ToArray();

    /// <summary>Remove containers que saíram do inventário do host (chamado na ingestão).</summary>
    public void RetainContainers(Guid environmentId, IReadOnlySet<string> sampledIds)
    {
        foreach (var key in containers.Keys)
        {
            if (key.Item1 == environmentId && !sampledIds.Contains(key.Item2) && !Fresh(containers[key].SampledAtUtc))
            {
                containers.TryRemove(key, out _);
            }
        }
    }

    public void ForgetHost(Guid environmentId)
    {
        hosts.TryRemove(environmentId, out _);
        foreach (var key in containers.Keys.Where(k => k.Item1 == environmentId)) containers.TryRemove(key, out _);
    }

    private bool Fresh(DateTimeOffset sampledAt) => timeProvider.GetUtcNow() - sampledAt <= options.LatestExpiry;
}

public sealed record SeriesPoint(DateTimeOffset MinuteUtc, double? Cpu, double? Memory);

/// <summary>
/// Série de 30 minutos por container somente em memória (spec 009 D-002 A′); nunca persistida.
/// Pontos de 1 minuto com média incremental de CPU % e memória % (do limite ou, sem limite, do host);
/// container sem amostra há 30 min é descartado.
/// </summary>
public sealed class ContainerMetricsBuffer(TimeProvider timeProvider)
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(30);

    private readonly ConcurrentDictionary<(Guid, string), Series> series = new();

    private sealed class Series
    {
        public readonly SortedDictionary<DateTimeOffset, (double CpuSum, int CpuCount, double MemSum, int MemCount)> Minutes = new();
        public DateTimeOffset LastSampleUtc;
    }

    public void Add(Guid environmentId, string containerId, DateTimeOffset sampledAtUtc, double? cpuPercent, double? memoryPercent)
    {
        var entry = series.GetOrAdd((environmentId, containerId), _ => new Series());
        var minute = Truncate(sampledAtUtc);
        lock (entry)
        {
            entry.Minutes.TryGetValue(minute, out var point);
            if (cpuPercent is { } cpu) point = (point.CpuSum + cpu, point.CpuCount + 1, point.MemSum, point.MemCount);
            if (memoryPercent is { } memory) point = (point.CpuSum, point.CpuCount, point.MemSum + memory, point.MemCount + 1);
            entry.Minutes[minute] = point;
            entry.LastSampleUtc = sampledAtUtc > entry.LastSampleUtc ? sampledAtUtc : entry.LastSampleUtc;
            Trim(entry);
        }

        if (series.Count > 0 && Random.Shared.Next(64) == 0) Sweep();
    }

    /// <summary>Pontos por minuto da janela (lacunas como `null`), do mais antigo para o mais recente.</summary>
    public IReadOnlyList<SeriesPoint> Get(Guid environmentId, string containerId, TimeSpan window, TimeSpan step)
    {
        var end = Align(Truncate(timeProvider.GetUtcNow()), step);
        var start = end - (window.Ticks / step.Ticks - 1) * step;
        var buckets = new List<SeriesPoint>();
        series.TryGetValue((environmentId, containerId), out var entry);
        for (var bucket = start; bucket <= end; bucket += step)
        {
            double cpuSum = 0, memSum = 0;
            int cpuCount = 0, memCount = 0;
            if (entry is not null)
            {
                lock (entry)
                {
                    foreach (var (minute, point) in entry.Minutes)
                    {
                        if (minute < bucket || minute >= bucket + step) continue;
                        cpuSum += point.CpuSum;
                        cpuCount += point.CpuCount;
                        memSum += point.MemSum;
                        memCount += point.MemCount;
                    }
                }
            }

            buckets.Add(new SeriesPoint(bucket, cpuCount > 0 ? Math.Round(cpuSum / cpuCount, 2) : null, memCount > 0 ? Math.Round(memSum / memCount, 2) : null));
        }

        return buckets;
    }

    public int Count => series.Count;

    public void Sweep()
    {
        var cutoff = timeProvider.GetUtcNow() - Window;
        foreach (var (key, entry) in series)
        {
            if (entry.LastSampleUtc < cutoff) series.TryRemove(key, out _);
        }
    }

    private void Trim(Series entry)
    {
        var cutoff = Truncate(timeProvider.GetUtcNow()) - Window;
        while (entry.Minutes.Count > 0 && entry.Minutes.Keys.First() <= cutoff) entry.Minutes.Remove(entry.Minutes.Keys.First());
    }

    internal static DateTimeOffset Truncate(DateTimeOffset value) =>
        new(value.UtcTicks - value.UtcTicks % TimeSpan.TicksPerMinute, TimeSpan.Zero);

    internal static DateTimeOffset Align(DateTimeOffset value, TimeSpan step) =>
        new(value.UtcTicks - value.UtcTicks % step.Ticks, TimeSpan.Zero);
}

/// <summary>Agrega amostras de host por minuto em memória; minutos fechados são gravados por upsert.</summary>
public sealed class HostMinuteAggregator
{
    private readonly ConcurrentDictionary<Guid, Pending> pending = new();

    private sealed class Pending
    {
        public DateTimeOffset Minute;
        public double CpuSum;
        public int CpuCount;
        public long? MemoryUsed;
        public long? MemoryTotal;
        public int Samples;
    }

    /// <summary>Acumula a amostra; devolve o minuto anterior quando ele fecha.</summary>
    public HostMinuteSample? Add(Guid environmentId, HostMetricsSample sample)
    {
        var minute = ContainerMetricsBuffer.Truncate(sample.SampledAtUtc);
        var entry = pending.GetOrAdd(environmentId, _ => new Pending { Minute = minute });
        lock (entry)
        {
            HostMinuteSample? closed = null;
            if (minute > entry.Minute)
            {
                closed = entry.Samples > 0 ? Close(environmentId, entry) : null;
                entry.Minute = minute;
                entry.CpuSum = 0;
                entry.CpuCount = 0;
                entry.MemoryUsed = null;
                entry.MemoryTotal = null;
                entry.Samples = 0;
            }
            else if (minute < entry.Minute)
            {
                return null; // amostra atrasada de um minuto já gravado
            }

            if (sample.CpuPercent is { } cpu)
            {
                entry.CpuSum += cpu;
                entry.CpuCount++;
            }

            if (sample.MemoryUsedBytes is not null) entry.MemoryUsed = sample.MemoryUsedBytes;
            if (sample.MemoryTotalBytes is not null) entry.MemoryTotal = sample.MemoryTotalBytes;
            entry.Samples++;
            return closed;
        }
    }

    /// <summary>Fecha minutos pendentes anteriores ao minuto atual (host que parou de enviar).</summary>
    public IReadOnlyList<HostMinuteSample> FlushBefore(DateTimeOffset nowUtc)
    {
        var current = ContainerMetricsBuffer.Truncate(nowUtc);
        var closed = new List<HostMinuteSample>();
        foreach (var (environmentId, entry) in pending)
        {
            lock (entry)
            {
                if (entry.Minute >= current || entry.Samples == 0) continue;
                closed.Add(Close(environmentId, entry));
                entry.Samples = 0;
                entry.CpuCount = 0;
                entry.CpuSum = 0;
            }
        }

        return closed;
    }

    private static HostMinuteSample Close(Guid environmentId, Pending entry) => new(
        environmentId,
        entry.Minute,
        entry.CpuCount > 0 ? Math.Round(entry.CpuSum / entry.CpuCount, 2) : null,
        entry.MemoryUsed,
        entry.MemoryTotal,
        entry.Samples);
}
