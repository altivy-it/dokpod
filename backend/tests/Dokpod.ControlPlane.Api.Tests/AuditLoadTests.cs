using Dokpod.ControlPlane.Application.Auditing;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests;

/// <summary>Spec 017 SC-003: carga de 1 milhão de eventos, isolada na coleção de desempenho (transação desfeita).</summary>
[Collection(PostgresPerformanceCollection.Name)]
public sealed class AuditLoadTests
{
    private static DateTimeOffset Now => AuditConsoleIntegrationTests.Now;

    [Fact]
    public async Task FirstPage_WithOneMillionEventsAcross56Hosts_RespondsWithinTwoSecondsAtP95()
    {
        // SC-003: 1 milhão de eventos em 56 hosts nos últimos 30 dias; combinações de filtros na primeira página.
        await using var context = AuditConsoleIntegrationTests.CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        var hosts = Enumerable.Range(0, 56).Select(_ => Guid.NewGuid()).ToArray();
        context.Database.SetCommandTimeout(TimeSpan.FromMinutes(10)); // só a carga; as consultas medidas ficam bem abaixo
        await AuditConsoleIntegrationTests.BulkInsertAsync(context, hosts, 1_000_000, TimeSpan.FromDays(30), ct);
        await context.Database.ExecuteSqlRawAsync("ANALYZE dokpod.audit_events", ct);
        var reader = new PostgresAuditEventReader(context);
        var queries = new[]
        {
            AuditConsoleIntegrationTests.Query(hosts),
            AuditConsoleIntegrationTests.Query(hosts) with { SinceUtc = Now.AddDays(-30), UntilUtc = Now },
            AuditConsoleIntegrationTests.Query(hosts) with { SinceUtc = Now.AddDays(-30), UntilUtc = Now, Outcomes = [AuditOutcome.Denied] },
            AuditConsoleIntegrationTests.Query(hosts) with { SinceUtc = Now.AddDays(-30), UntilUtc = Now, Actions = AuditCategories.Actions["terminal"] },
            AuditConsoleIntegrationTests.Query(hosts) with { SinceUtc = Now.AddDays(-30), UntilUtc = Now, Actor = "user-7" },
            AuditConsoleIntegrationTests.Query(hosts) with { SinceUtc = Now.AddDays(-30), UntilUtc = Now, Text = "subject-42" },
            AuditConsoleIntegrationTests.Query(hosts.Take(3).ToArray()) with { SinceUtc = Now.AddDays(-7), UntilUtc = Now },
        };

        var timings = new List<double>();
        for (var round = 0; round < 5; round++)
        {
            foreach (var query in queries)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                await reader.ListAsync(query, ct);
                timings.Add(watch.Elapsed.TotalMilliseconds);
            }
        }

        timings.Sort();
        var p95 = timings[(int)Math.Ceiling(timings.Count * 0.95) - 1];
        TestContext.Current.SendDiagnosticMessage($"SC-003 primeira página p95={p95:0} ms (máx {timings[^1]:0} ms) com 1.000.000 eventos");
        Assert.True(p95 < 2000, $"p95 {p95:0} ms");
    }
}
