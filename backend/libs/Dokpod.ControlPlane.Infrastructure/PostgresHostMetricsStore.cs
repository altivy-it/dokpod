using Dokpod.ControlPlane.Application.Metrics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Dokpod.ControlPlane.Infrastructure;

/// <summary>Série de 1 minuto por host (spec 009); upsert por (host, minuto) e retenção pela rotina genérica da 008.</summary>
public sealed class PostgresHostMetricsStore(ControlPlaneDbContext dbContext) : IHostMetricsStore
{
    public async Task UpsertAsync(IReadOnlyCollection<HostMinuteSample> samples, CancellationToken cancellationToken)
    {
        foreach (var sample in samples)
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO dokpod.host_resource_samples (environment_id, minute, cpu_percent, memory_used_bytes, memory_total_bytes, sample_count)
                VALUES ({sample.EnvironmentId}, {sample.MinuteUtc}, {(float?)sample.CpuPercent}, {sample.MemoryUsedBytes}, {sample.MemoryTotalBytes}, {(short)Math.Clamp(sample.SampleCount, 1, short.MaxValue)})
                ON CONFLICT (environment_id, minute) DO UPDATE SET
                    cpu_percent = COALESCE(EXCLUDED.cpu_percent, dokpod.host_resource_samples.cpu_percent),
                    memory_used_bytes = COALESCE(EXCLUDED.memory_used_bytes, dokpod.host_resource_samples.memory_used_bytes),
                    memory_total_bytes = COALESCE(EXCLUDED.memory_total_bytes, dokpod.host_resource_samples.memory_total_bytes),
                    sample_count = LEAST(32767, dokpod.host_resource_samples.sample_count + EXCLUDED.sample_count)
                """, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<HostMinuteSample>> GetSeriesAsync(
        IReadOnlyCollection<Guid> environmentIds,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        if (environmentIds.Count == 0) return [];
        var ids = environmentIds.ToArray();
        var rows = await dbContext.Database
            .SqlQuery<Row>($"""
                SELECT environment_id AS "EnvironmentId", minute AS "Minute", cpu_percent AS "CpuPercent",
                       memory_used_bytes AS "MemoryUsedBytes", memory_total_bytes AS "MemoryTotalBytes", sample_count AS "SampleCount"
                FROM dokpod.host_resource_samples
                WHERE environment_id = ANY ({ids}) AND minute >= {fromUtc} AND minute < {toUtc}
                ORDER BY minute
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(r => new HostMinuteSample(r.EnvironmentId, r.Minute, r.CpuPercent, r.MemoryUsedBytes, r.MemoryTotalBytes, r.SampleCount)).ToArray();
    }

    public async Task<long> ApplyRetentionAsync(TimeSpan retention, CancellationToken cancellationToken)
    {
        var result = await dbContext.Database
            .SqlQueryRaw<long>(
                "SELECT dokpod.dokpod_apply_temporal_retention('dokpod.host_resource_samples'::regclass, 'minute', @retention) AS \"Value\"",
                new NpgsqlParameter("retention", NpgsqlDbType.Interval) { Value = retention })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return result.SingleOrDefault();
    }

    private sealed class Row
    {
        public Guid EnvironmentId { get; init; }
        public DateTimeOffset Minute { get; init; }
        public float? CpuPercent { get; init; }
        public long? MemoryUsedBytes { get; init; }
        public long? MemoryTotalBytes { get; init; }
        public short SampleCount { get; init; }
    }
}
