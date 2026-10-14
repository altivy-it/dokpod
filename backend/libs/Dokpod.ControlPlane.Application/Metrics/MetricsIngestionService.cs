using Dokpod.Agent.Contracts.V1;

namespace Dokpod.ControlPlane.Application.Metrics;

/// <summary>
/// Recebe `ResourceSampleBatch` (spec 009 T010): atualiza o último valor, a série de container em memória
/// e agrega o host por minuto, gravando no PostgreSQL somente minutos de host fechados. Horário do agente
/// com divergência acima de 5 min é substituído pelo horário de recebimento.
/// </summary>
public sealed class MetricsIngestionService(
    LatestMetricsCache latest,
    ContainerMetricsBuffer containers,
    HostMinuteAggregator aggregator,
    IHostMetricsStore store,
    TimeProvider timeProvider)
{
    public static readonly TimeSpan ClockSkewThreshold = TimeSpan.FromMinutes(5);
    public const int MaxContainersPerBatch = 500;

    public async Task IngestAsync(Guid environmentId, ResourceSampleBatch batch, CancellationToken cancellationToken)
    {
        var received = timeProvider.GetUtcNow();
        var sampledAt = batch.SampledAt is null ? received : batch.SampledAt.ToDateTimeOffset();
        if ((received - sampledAt).Duration() > ClockSkewThreshold) sampledAt = received;

        if (batch.Host is { } host)
        {
            var hostSample = new HostMetricsSample(
                host.HasCpuPercent && double.IsFinite(host.CpuPercent) ? Math.Clamp(host.CpuPercent, 0, 100) : null,
                host.HasMemoryUsedBytes ? ToLong(host.MemoryUsedBytes) : null,
                host.HasMemoryTotalBytes ? ToLong(host.MemoryTotalBytes) : null,
                sampledAt);
            latest.SetHost(environmentId, hostSample);
            if (aggregator.Add(environmentId, hostSample) is { } closed)
            {
                await store.UpsertAsync([closed], cancellationToken).ConfigureAwait(false);
            }
        }

        foreach (var container in batch.Containers.Take(MaxContainersPerBatch))
        {
            if (string.IsNullOrEmpty(container.ContainerId) || container.ContainerId.Length > 128) continue;
            var sample = new ContainerMetricsSample(
                container.HasCpuPercentOfHost && double.IsFinite(container.CpuPercentOfHost) ? Math.Clamp(container.CpuPercentOfHost, 0, 100) : null,
                container.HasCpuPercentOfLimit && double.IsFinite(container.CpuPercentOfLimit) ? Math.Max(0, container.CpuPercentOfLimit) : null,
                container.HasCpuLimitCores && container.CpuLimitCores > 0 ? container.CpuLimitCores : null,
                container.HasMemoryUsedBytes ? ToLong(container.MemoryUsedBytes) : null,
                container.HasMemoryLimitBytes ? ToLong(container.MemoryLimitBytes) : null,
                container.HasNetRxBps ? ToLong(container.NetRxBps) : null,
                container.HasNetTxBps ? ToLong(container.NetTxBps) : null,
                container.HasBlkReadBps ? ToLong(container.BlkReadBps) : null,
                container.HasBlkWriteBps ? ToLong(container.BlkWriteBps) : null,
                sampledAt);
            latest.SetContainer(environmentId, container.ContainerId, sample);
            var memoryReference = sample.MemoryLimitBytes ?? latest.GetHost(environmentId)?.MemoryTotalBytes;
            double? memoryPercent = sample.MemoryUsedBytes is { } used && memoryReference is > 0
                ? Math.Min(100, Math.Round((double)used / memoryReference.Value * 100, 2))
                : null;
            containers.Add(environmentId, container.ContainerId, sampledAt, sample.CpuPercentOfLimit is { } ofLimit ? Math.Min(100, ofLimit) : sample.CpuPercentOfHost, memoryPercent);
        }
    }

    /// <summary>Grava minutos de host pendentes de agentes que pararam de enviar (manutenção periódica).</summary>
    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        var closed = aggregator.FlushBefore(timeProvider.GetUtcNow());
        if (closed.Count > 0) await store.UpsertAsync(closed, cancellationToken).ConfigureAwait(false);
        containers.Sweep();
    }

    private static long ToLong(ulong value) => value > long.MaxValue ? long.MaxValue : (long)value;
}
