using Xunit;
using Dokpod.ControlPlane.Application.Events;
using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Events;
using Dokpod.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Dokpod.ControlPlane.Api.Tests;

/// <summary>Spec 008 em PostgreSQL real: estágio, descritor, conexão, estatísticas e eventos.</summary>
public sealed class ConsoleOverviewIntegrationTests
{
    [Fact]
    public async Task HostStore_PersistsStageDescriptorConnectionAndStatistics()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var environmentId = Guid.NewGuid();
        var registrations = new PostgresEnvironmentRegistrationStore(context);
        try
        {
            await registrations.CreateAsync(
                EnvironmentRegistration.Create(environmentId, $"srv-{environmentId:N}"[..20], "srv-prod-01", true, [EnvironmentResourceScopes.Read], EnvironmentStage.Staging),
                ct);
            Assert.Equal(EnvironmentStage.Staging, (await registrations.GetAsync(environmentId, ct))!.Stage);

            var hosts = new PostgresAgentHostStore(context);
            var (found, previous) = await hosts.SetStageAsync(environmentId, EnvironmentStage.Production, ct);
            Assert.True(found);
            Assert.Equal(EnvironmentStage.Staging, previous);

            await hosts.UpsertDescriptorAsync(new AgentDescriptor(
                environmentId, "podman", "5.2.3", "1.41", true, "linux", "6.8", "amd64", "0.2.0",
                ["Inventory", "EngineEvents"], "srv-prod-01", 8, 16L * 1024 * 1024 * 1024, DateTimeOffset.UtcNow), ct);
            Assert.Null(await hosts.SetConnectionAsync(environmentId, true, DateTimeOffset.UtcNow, ct));
            Assert.True(await hosts.SetConnectionAsync(environmentId, false, DateTimeOffset.UtcNow, ct));
            var descriptor = (await hosts.GetDescriptorsAsync([environmentId], ct))[environmentId];
            Assert.Equal("podman", descriptor.EngineKind);
            Assert.Contains("EngineEvents", descriptor.Capabilities);
            Assert.False((await hosts.GetConnectionsAsync([environmentId], ct))[environmentId].Connected);

            var now = DateTimeOffset.UtcNow;
            var inventory = new PostgresInventoryProjectionStore(context);
            var snapshot = new InventorySnapshot(environmentId, 7, new Dictionary<string, ContainerInventory>
            {
                ["a"] = new("a", "api", "img", "Running", "a:1", now) { Health = ContainerHealth.Unhealthy, Ports = [new ContainerPortBinding("0.0.0.0", 8080, 80, "tcp")] },
                ["b"] = new("b", "worker", "img", "Exited", "b:1", now) { ExitCode = 0, OomKilled = false },
                ["c"] = new("c", "job", "img", "Exited", "c:1", now) { ExitCode = 137 },
            });
            Assert.Equal(InventoryReconciliationOutcome.Accepted, (await inventory.ReplaceSnapshotAsync(snapshot, ct)).Outcome);
            var page = await inventory.GetPageAsync(environmentId, null, 10, ct);
            var api = page!.Containers.Single(c => c.ContainerId == "a");
            Assert.Equal(ContainerDisplayStatus.Failed, api.DisplayStatus);
            Assert.Equal(8080, Assert.Single(api.Ports).HostPort);

            var statistics = (await hosts.GetStatisticsAsync([environmentId], ct))[environmentId];
            Assert.Equal(3, statistics.Total);
            Assert.Equal(2, statistics.ByDisplayStatus["failed"]);
            Assert.Equal(1, statistics.ByDisplayStatus["stopped"]);
        }
        finally
        {
            await context.InventoryProjections.Where(item => item.EnvironmentId == environmentId).ExecuteDeleteAsync(ct);
            await context.EnvironmentRegistrations.Where(item => item.EnvironmentId == environmentId).ExecuteDeleteAsync(ct);
        }
    }

    [Fact]
    public async Task EventStore_DeduplicatesBySourceKeyPagesAndCountsAttention()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var environmentId = Guid.NewGuid();
        var store = new PostgresOperationalEventStore(context);
        var now = DateTimeOffset.UtcNow;
        try
        {
            var died = OperationalEvent.Create(environmentId, OperationalEventTypes.ContainerDied, now.AddMinutes(-3), now, "container", "x", "worker", null, 1, "x:1:die");
            var duplicate = OperationalEvent.Create(environmentId, OperationalEventTypes.ContainerDied, now.AddMinutes(-3), now, "container", "x", "worker", null, 1, "x:1:die");
            var created = OperationalEvent.Create(environmentId, OperationalEventTypes.ContainerCreated, now.AddMinutes(-2), now, "container", "y", "api", null);
            var connected = OperationalEvent.Create(environmentId, OperationalEventTypes.AgentConnected, now.AddMinutes(-1), now, "host", null, null, null);

            Assert.Equal(3, await store.AppendAsync([died, created, connected], ct));
            Assert.Equal(0, await store.AppendAsync([duplicate], ct));

            var first = await store.ListAsync(new OperationalEventQuery([environmentId], null, now.AddHours(-1), null, null, 2), ct);
            Assert.Equal([OperationalEventTypes.AgentConnected, OperationalEventTypes.ContainerCreated], first.Events.Select(e => e.Type));
            Assert.NotNull(first.Next);
            var second = await store.ListAsync(new OperationalEventQuery([environmentId], null, now.AddHours(-1), null, first.Next, 2), ct);
            Assert.Equal(OperationalEventTypes.ContainerDied, Assert.Single(second.Events).Type);
            Assert.Null(second.Next);

            Assert.Equal(1, await store.CountAttentionAsync([environmentId], now.AddHours(-24), ct));
            var errors = await store.ListAsync(new OperationalEventQuery([environmentId], OperationalEventSeverities.Error, now.AddHours(-1), null, null, 10), ct);
            Assert.Single(errors.Events);
        }
        finally
        {
            await context.EnvironmentEvents.Where(item => item.EnvironmentId == environmentId).ExecuteDeleteAsync(ct);
        }
    }

    [Fact]
    public async Task Retention_RemovesOnlyExpiredEventsAndRefusesOtherTables()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var environmentId = Guid.NewGuid();
        var store = new PostgresOperationalEventStore(context);
        var now = DateTimeOffset.UtcNow;
        try
        {
            var old = OperationalEvent.Create(environmentId, OperationalEventTypes.ContainerDied, now.AddDays(-120), now, "container", "x", "old", null);
            var recent = OperationalEvent.Create(environmentId, OperationalEventTypes.ContainerDied, now.AddDays(-2), now, "container", "y", "recent", null);
            Assert.Equal(2, await store.AppendAsync([old, recent], ct));

            Assert.True(await store.ApplyRetentionAsync(TimeSpan.FromDays(90), ct) >= 0);

            var remaining = await context.EnvironmentEvents.Where(e => e.EnvironmentId == environmentId).Select(e => e.SubjectName).ToListAsync(ct);
            Assert.Equal(["recent"], remaining);

            foreach (var (table, column) in new[] { ("dokpod.audit_events", "occurred_at_utc"), ("dokpod.environment_events", "event_id") })
            {
                var error = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => context.Database
                    .SqlQuery<long>($"SELECT dokpod.dokpod_apply_temporal_retention({table}::text::regclass, {column}, interval '30 days') AS \"Value\"")
                    .ToListAsync(ct));
                Assert.Contains("not eligible for temporal retention", error.MessageText);
            }
        }
        finally
        {
            await context.EnvironmentEvents.Where(item => item.EnvironmentId == environmentId).ExecuteDeleteAsync(ct);
        }
    }

    [Fact]
    public async Task Partitions_EnvironmentEventsHaveMonthlyPartitionsCreatedAhead()
    {
        await using var context = CreateContextOrSkip();
        var nextMonth = DateTime.UtcNow.AddMonths(1);
        var partition = $"dokpod.environment_events_{nextMonth:yyyy_MM}";

        var found = await context.Database
            .SqlQuery<string>($"SELECT coalesce(to_regclass({partition})::text, '') AS \"Value\"")
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrEmpty(found), $"partição {partition} ausente");
    }

    [Fact]
    public async Task Overview_With56HostsAnd1120ContainersAnswersWithinBudget()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var ids = Enumerable.Range(0, 56).Select(_ => Guid.NewGuid()).ToArray();
        var registrations = new PostgresEnvironmentRegistrationStore(context);
        var hosts = new PostgresAgentHostStore(context);
        var inventory = new PostgresInventoryProjectionStore(context);
        var now = DateTimeOffset.UtcNow;
        try
        {
            for (var i = 0; i < ids.Length; i++)
            {
                var id = ids[i];
                await registrations.CreateAsync(
                    EnvironmentRegistration.Create(id, $"perf-{i:D2}-{id:N}"[..24], $"perf-{i}.local", true, [EnvironmentResourceScopes.Read], i % 2 == 0 ? EnvironmentStage.Production : EnvironmentStage.Staging),
                    ct);
                await hosts.UpsertDescriptorAsync(new AgentDescriptor(
                    id, i % 3 == 0 ? "podman" : "docker", i % 3 == 0 ? "5.2.3" : "27.3.1", "1.47", false, "linux", "24.04", "amd64", "0.9.0", [], $"perf-{i}", 8, 1L << 34, now), ct);
                var containers = Enumerable.Range(0, 20).ToDictionary(
                    c => $"{id:N}{c:D4}",
                    c => new ContainerInventory($"{id:N}{c:D4}", $"c-{c}", "img", c % 5 == 0 ? "Exited" : "Running", $"r{c}", now) { ExitCode = c % 10 == 0 ? 1 : 0 });
                await inventory.ReplaceSnapshotAsync(new InventorySnapshot(id, 1, containers), ct);
            }

            var events = new PostgresOperationalEventStore(context);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var authorized = new List<EnvironmentRegistration>();
            foreach (var id in ids) authorized.Add((await registrations.GetAsync(id, ct))!);
            var hostQuery = new HostQueryService(
                hosts, hosts, new Dokpod.ControlPlane.Api.Agents.InMemoryAgentSessionStore(), new AgentLivenessTracker(TimeProvider.System), new EngineSupportOptions());
            var views = await hostQuery.DescribeAsync(authorized, ct);
            var summary = await new Dokpod.ControlPlane.Application.Overview.OverviewQueryService(events, TimeProvider.System).SummarizeAsync(null, views, ct);
            stopwatch.Stop();

            Assert.Equal(56, summary.HostsTotal);
            Assert.Equal(1120, summary.ContainersTotal);
            Assert.Equal(56 * 4, summary.ContainersByDisplayStatus["stopped"] + summary.ContainersByDisplayStatus["failed"]);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"overview em {stopwatch.ElapsedMilliseconds} ms");
        }
        finally
        {
            await context.InventoryProjections.Where(item => ids.Contains(item.EnvironmentId)).ExecuteDeleteAsync(ct);
            await context.EnvironmentRegistrations.Where(item => ids.Contains(item.EnvironmentId)).ExecuteDeleteAsync(ct);
        }
    }

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
