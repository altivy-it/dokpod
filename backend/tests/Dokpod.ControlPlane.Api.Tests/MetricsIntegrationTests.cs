using Dokpod.ControlPlane.Application.Metrics;
using Dokpod.ControlPlane.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests;

/// <summary>Spec 009 em PostgreSQL real: série de host, retenção e ausência de persistência de containers.</summary>
public sealed class MetricsIntegrationTests
{
    [Fact]
    public async Task HostStore_UpsertsMinutesAndQueriesTheWindow()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var store = new PostgresHostMetricsStore(context);
        var minute = Truncate(DateTimeOffset.UtcNow).AddMinutes(-5);
        try
        {
            await store.UpsertAsync([new HostMinuteSample(id, minute, 40, 8L << 30, 16L << 30, 2)], ct);
            await store.UpsertAsync([new HostMinuteSample(id, minute, null, 9L << 30, 16L << 30, 1)], ct);
            await store.UpsertAsync([new HostMinuteSample(id, minute.AddMinutes(1), 60, null, null, 4)], ct);

            var rows = await store.GetSeriesAsync([id], minute, minute.AddMinutes(2), ct);

            Assert.Equal(2, rows.Count);
            Assert.Equal(40, rows[0].CpuPercent!.Value, 3);      // CPU nula não apaga o valor anterior
            Assert.Equal(9L << 30, rows[0].MemoryUsedBytes);
            Assert.Equal(3, rows[0].SampleCount);
            Assert.Empty(await store.GetSeriesAsync([Guid.NewGuid()], minute, minute.AddMinutes(2), ct));
        }
        finally
        {
            await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dokpod.host_resource_samples WHERE environment_id = {id}", ct);
        }
    }

    [Fact]
    public async Task Retention_RemovesExpiredHostMinutesOnly()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var store = new PostgresHostMetricsStore(context);
        var now = Truncate(DateTimeOffset.UtcNow);
        try
        {
            await store.UpsertAsync([new HostMinuteSample(id, now.AddHours(-60), 10, null, null, 1), new HostMinuteSample(id, now.AddHours(-1), 20, null, null, 1)], ct);

            await store.ApplyRetentionAsync(TimeSpan.FromHours(48), ct);

            var rows = await store.GetSeriesAsync([id], now.AddDays(-7), now.AddMinutes(1), ct);
            Assert.Equal(20, Assert.Single(rows).CpuPercent!.Value, 3);
        }
        finally
        {
            await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dokpod.host_resource_samples WHERE environment_id = {id}", ct);
        }
    }

    [Fact]
    public async Task Schema_HasMonthlyHostPartitionsAndNoContainerMetricsTable()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var partition = $"dokpod.host_resource_samples_{DateTime.UtcNow.AddMonths(1):yyyy_MM}";

        var found = await context.Database.SqlQuery<string>($"SELECT coalesce(to_regclass({partition})::text, '') AS \"Value\"").SingleAsync(ct);
        var containerTables = await context.Database
            .SqlQuery<string>($"SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'dokpod' AND table_name LIKE '%container%' AND (table_name LIKE '%metric%' OR table_name LIKE '%sample%' OR table_name LIKE '%resource%')")
            .ToListAsync(ct);

        Assert.False(string.IsNullOrEmpty(found), $"partição {partition} ausente");
        Assert.Empty(containerTables);
    }

    [Fact]
    public async Task StageSeries_For56HostsOver12HoursAnswersWithin500Milliseconds()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var ids = Enumerable.Range(0, 56).Select(_ => Guid.NewGuid()).ToArray();
        var store = new PostgresHostMetricsStore(context);
        var end = Truncate(DateTimeOffset.UtcNow);
        try
        {
            var values = string.Join(",", ids.SelectMany(id => Enumerable.Range(0, 720).Select(m =>
                $"('{id}', '{end.AddMinutes(-m):O}', {m % 100}, {m}, 1000, 4)")));
#pragma warning disable EF1002 // valores gerados pelo próprio teste
            await context.Database.ExecuteSqlRawAsync(
                $"INSERT INTO dokpod.host_resource_samples (environment_id, minute, cpu_percent, memory_used_bytes, memory_total_bytes, sample_count) VALUES {values}", ct);
#pragma warning restore EF1002

            var durations = new List<double>();
            for (var i = 0; i < 20; i++)
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var rows = await store.GetSeriesAsync(ids, end.AddHours(-12), end.AddMinutes(1), ct);
                stopwatch.Stop();
                Assert.Equal(56 * 720, rows.Count);
                durations.Add(stopwatch.Elapsed.TotalMilliseconds);
            }

            durations.Sort();
            var p95 = durations[(int)Math.Ceiling(durations.Count * 0.95) - 1];
            Assert.True(p95 <= 500, $"p95 {p95:F0} ms");
        }
        finally
        {
            await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dokpod.host_resource_samples WHERE environment_id = ANY ({ids})", ct);
        }
    }

    private static DateTimeOffset Truncate(DateTimeOffset value) => new(value.UtcTicks - value.UtcTicks % TimeSpan.TicksPerMinute, TimeSpan.Zero);

    private static ControlPlaneDbContext CreateContextOrSkip()
    {
        var connectionString = Environment.GetEnvironmentVariable("DOKPOD_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("DOKPOD_TEST_POSTGRES_CONNECTION is not configured.");
        }

        return new ControlPlaneDbContext(new DbContextOptionsBuilder<ControlPlaneDbContext>().UseNpgsql(connectionString).Options);
    }
}
