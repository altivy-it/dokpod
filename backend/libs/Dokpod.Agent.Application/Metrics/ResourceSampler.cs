using Dokpod.Agent.Application.Engines;
using Dokpod.Agent.Contracts.V1;
using Dokpod.Domain.Metrics;
using Google.Protobuf.WellKnownTypes;

namespace Dokpod.Agent.Application.Metrics;

/// <summary>Container em execução a amostrar, com o limite de CPU conhecido pelo inventário.</summary>
public sealed record SampleTarget(string ContainerId, double? CpuLimitCores);

/// <summary>Leitura bruta do host: jiffies totais/ociosos de `/proc/stat` e memória de `/proc/meminfo`.</summary>
public sealed record HostResourceReading(ulong CpuTotal, ulong CpuIdle, ulong MemoryTotalBytes, ulong MemoryAvailableBytes);

public interface IHostResourceProbe
{
    /// <summary>Nulo quando a plataforma não oferece a leitura (ex.: Windows).</summary>
    HostResourceReading? Read();
}

/// <summary>
/// Amostragem de recursos (spec 009 FR-001/FR-002, D-001, D-003, D-005): uma amostra por intervalo,
/// sem média prévia, no máximo 500 containers por mensagem e 4 mensagens por ciclo; acima disso os
/// containers são amostrados em rodízio e o excedente é informado em `dropped_count`. A primeira
/// amostra de cada container só traz memória (CPU e taxas precisam de dois instantes). Nenhum campo
/// carrega nome de processo, argumento, variável ou label.
/// </summary>
public sealed class ResourceSampler(
    IContainerEngine engine,
    IHostResourceProbe hostProbe,
    TimeProvider timeProvider,
    int maxContainersPerMessage = 500,
    int maxMessagesPerInterval = 4,
    int maxConcurrency = 8)
{
    /// <summary>Divergência máxima entre `/proc/meminfo` e `/info` antes de considerar `/proc` virtualizado.</summary>
    public const double VirtualizationTolerance = 0.05;

    private readonly Dictionary<string, (EngineContainerStats Stats, DateTimeOffset At)> previous = new(StringComparer.Ordinal);
    private HostResourceReading? previousHost;
    private int rotation;

    /// <summary>Memória total informada pelo engine (`/info`), usada para detectar `/proc` virtualizado.</summary>
    public long? EngineMemoryTotalBytes { get; set; }

    public async Task<IReadOnlyList<ResourceSampleBatch>> SampleAsync(IReadOnlyList<SampleTarget> running, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var capacity = maxContainersPerMessage * maxMessagesPerInterval;
        var selected = Rotate(running, capacity);
        var dropped = running.Count - selected.Count;

        // As leituras do engine são paralelas; o cálculo usa o estado anterior (dicionário não concorrente) e roda
        // em sequência depois, para que respostas simultâneas não corrompam esse estado.
        var readings = new EngineContainerStats?[selected.Count];
        using (var gate = new SemaphoreSlim(maxConcurrency))
        {
            await Task.WhenAll(selected.Select(async (target, index) =>
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    readings[index] = await engine.GetContainerStatsAsync(target.ContainerId, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is HttpRequestException or IOException or System.Text.Json.JsonException or InvalidDataException)
                {
                    readings[index] = null;
                }
                finally
                {
                    gate.Release();
                }
            })).ConfigureAwait(false);
        }

        var samples = new ContainerResourceSample?[selected.Count];
        for (var index = 0; index < selected.Count; index++)
        {
            if (readings[index] is { } stats) samples[index] = BuildContainerSample(selected[index], stats, now);
        }

        var runningIds = running.Select(t => t.ContainerId).ToHashSet(StringComparer.Ordinal);
        foreach (var gone in previous.Keys.Where(id => !runningIds.Contains(id)).ToArray()) previous.Remove(gone);

        var containers = samples.Where(s => s is not null).Select(s => s!).ToArray();
        var batches = new List<ResourceSampleBatch>();
        var sampledAt = Timestamp.FromDateTimeOffset(now);
        for (var start = 0; start == 0 || start < containers.Length; start += maxContainersPerMessage)
        {
            var batch = new ResourceSampleBatch { SampledAt = sampledAt };
            if (start == 0)
            {
                batch.Host = BuildHostSample();
                batch.DroppedCount = (uint)Math.Max(0, dropped);
            }

            batch.Containers.AddRange(containers.Skip(start).Take(maxContainersPerMessage));
            batches.Add(batch);
            if (containers.Length == 0) break;
        }

        return batches;
    }

    private List<SampleTarget> Rotate(IReadOnlyList<SampleTarget> running, int capacity)
    {
        if (running.Count <= capacity)
        {
            rotation = 0;
            return [.. running];
        }

        var start = rotation % running.Count;
        rotation = (start + capacity) % running.Count;
        return Enumerable.Range(0, capacity).Select(i => running[(start + i) % running.Count]).ToList();
    }

    private ContainerResourceSample BuildContainerSample(SampleTarget target, EngineContainerStats stats, DateTimeOffset now)
    {
        var sample = new ContainerResourceSample { ContainerId = target.ContainerId, MemoryUsedBytes = stats.MemoryUsedBytes };
        if (stats.MemoryLimitBytes is { } limit) sample.MemoryLimitBytes = limit;
        if (target.CpuLimitCores is > 0) sample.CpuLimitCores = target.CpuLimitCores.Value;

        if (previous.TryGetValue(target.ContainerId, out var last))
        {
            var elapsed = now - last.At;
            // Preferir o instante de leitura do engine: as leituras são paralelas e não coincidem com `now`.
            var cpuElapsed = stats.ReadAt is { } readAt && last.Stats.ReadAt is { } lastReadAt && readAt > lastReadAt ? readAt - lastReadAt : elapsed;
            var cpu = CpuUsageCalculator.Container(
                new ContainerCpuCounters(last.Stats.CpuTotalNanos, last.Stats.OnlineCpus),
                new ContainerCpuCounters(stats.CpuTotalNanos, stats.OnlineCpus),
                cpuElapsed,
                target.CpuLimitCores);
            if (cpu is { } usage)
            {
                sample.CpuPercentOfHost = usage.PercentOfHost;
                sample.CpuPercentOfLimit = usage.PercentOfLimit;
            }

            if (CpuUsageCalculator.Rate(last.Stats.NetRxBytes, stats.NetRxBytes, elapsed) is { } rx) sample.NetRxBps = rx;
            if (CpuUsageCalculator.Rate(last.Stats.NetTxBytes, stats.NetTxBytes, elapsed) is { } tx) sample.NetTxBps = tx;
            if (CpuUsageCalculator.Rate(last.Stats.BlockReadBytes, stats.BlockReadBytes, elapsed) is { } read) sample.BlkReadBps = read;
            if (CpuUsageCalculator.Rate(last.Stats.BlockWriteBytes, stats.BlockWriteBytes, elapsed) is { } write) sample.BlkWriteBps = write;
        }

        previous[target.ContainerId] = (stats, now);
        return sample;
    }

    private HostResourceSample BuildHostSample()
    {
        var sample = new HostResourceSample();
        var reading = hostProbe.Read();
        if (reading is null) return sample;

        if (EngineMemoryTotalBytes is > 0 && reading.MemoryTotalBytes > 0)
        {
            var divergence = Math.Abs((double)reading.MemoryTotalBytes - EngineMemoryTotalBytes.Value) / EngineMemoryTotalBytes.Value;
            if (divergence > VirtualizationTolerance)
            {
                // `/proc` virtualizado (lxcfs ou similar): valores não refletem o host; "indisponível".
                previousHost = null;
                return sample;
            }
        }

        if (reading.MemoryTotalBytes > 0)
        {
            sample.MemoryTotalBytes = reading.MemoryTotalBytes;
            sample.MemoryUsedBytes = reading.MemoryTotalBytes - Math.Min(reading.MemoryAvailableBytes, reading.MemoryTotalBytes);
        }

        if (previousHost is not null &&
            CpuUsageCalculator.Host(previousHost.CpuTotal, previousHost.CpuIdle, reading.CpuTotal, reading.CpuIdle) is { } cpu)
        {
            sample.CpuPercent = cpu;
        }

        previousHost = reading;
        return sample;
    }
}
