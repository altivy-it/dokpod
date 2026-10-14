namespace Dokpod.ControlPlane.Application.Metrics;

public sealed class MetricsOptions
{
    public const string SectionName = "Metrics";

    public double OperationalLimitPercent { get; set; } = Dokpod.Domain.Metrics.MetricsPolicy.DefaultOperationalLimitPercent;

    /// <summary>Retenção da série de host no PostgreSQL (padrão 48 h, mínimo 12 h).</summary>
    public TimeSpan HostRetention { get; set; } = TimeSpan.FromHours(48);

    /// <summary>Último valor expira após 3 intervalos de amostragem sem novidade.</summary>
    public TimeSpan LatestExpiry { get; set; } = TimeSpan.FromSeconds(45);

    public bool IsValid() =>
        OperationalLimitPercent is > 0 and <= 100 &&
        HostRetention >= TimeSpan.FromHours(12) && HostRetention <= TimeSpan.FromDays(31) &&
        LatestExpiry >= TimeSpan.FromSeconds(15) && LatestExpiry <= TimeSpan.FromMinutes(5);
}

public sealed record HostMetricsSample(double? CpuPercent, long? MemoryUsedBytes, long? MemoryTotalBytes, DateTimeOffset SampledAtUtc);

public sealed record ContainerMetricsSample(
    double? CpuPercentOfHost,
    double? CpuPercentOfLimit,
    double? CpuLimitCores,
    long? MemoryUsedBytes,
    long? MemoryLimitBytes,
    long? NetRxBps,
    long? NetTxBps,
    long? BlockReadBps,
    long? BlockWriteBps,
    DateTimeOffset SampledAtUtc);

/// <summary>Minuto agregado de um host, gravado no PostgreSQL (spec 009 D-002 A′).</summary>
public sealed record HostMinuteSample(
    Guid EnvironmentId,
    DateTimeOffset MinuteUtc,
    double? CpuPercent,
    long? MemoryUsedBytes,
    long? MemoryTotalBytes,
    int SampleCount);

public interface IHostMetricsStore
{
    Task UpsertAsync(IReadOnlyCollection<HostMinuteSample> samples, CancellationToken cancellationToken);

    Task<IReadOnlyList<HostMinuteSample>> GetSeriesAsync(
        IReadOnlyCollection<Guid> environmentIds,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);

    Task<long> ApplyRetentionAsync(TimeSpan retention, CancellationToken cancellationToken);
}
