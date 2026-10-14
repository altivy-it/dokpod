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
