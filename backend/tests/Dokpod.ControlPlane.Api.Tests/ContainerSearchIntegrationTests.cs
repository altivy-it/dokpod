using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.ControlPlane.Application.Inventory;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Hosts;
using Dokpod.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests;

/// <summary>Spec 010: busca multi-host com filtros, keyset estável e resumo por situação.</summary>
public sealed class ContainerSearchIntegrationTests
{
    [Fact]
    public async Task SearchStore_FiltersPagesAndSummarizesAcrossHosts()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var registrations = new PostgresEnvironmentRegistrationStore(context);
        var inventory = new PostgresInventoryProjectionStore(context);
        var now = DateTimeOffset.UtcNow;
        try
        {
            await registrations.CreateAsync(EnvironmentRegistration.Create(first, $"a-{first:N}"[..18], "srv-a", true, [EnvironmentResourceScopes.Read]), ct);
            await registrations.CreateAsync(EnvironmentRegistration.Create(second, $"b-{second:N}"[..18], "srv-b", true, [EnvironmentResourceScopes.Read]), ct);
            await inventory.ReplaceSnapshotAsync(Snapshot(first, now, ("api-pedidos", "Running", null), ("gateway-01", "Running", null), ("worker", "Exited", 137)), ct);
            await inventory.ReplaceSnapshotAsync(Snapshot(second, now, ("api-pedidos", "Paused", null), ("redis_cache", "Running", null)), ct);
            var store = new PostgresContainerSearchStore(context);

            var page1 = await store.SearchAsync(new ContainerSearchQuery([first, second], [], null, ContainerSort.NameAsc, null, 2), ct);
            Assert.Equal(["api-pedidos", "api-pedidos"], page1.Rows.Select(r => r.Container.Name));
            Assert.NotNull(page1.Next);
            var page2 = await store.SearchAsync(new ContainerSearchQuery([first, second], [], null, ContainerSort.NameAsc, page1.Next, 2), ct);
            var page3 = await store.SearchAsync(new ContainerSearchQuery([first, second], [], null, ContainerSort.NameAsc, page2.Next, 2), ct);
            Assert.Equal(["gateway-01", "redis_cache", "worker"], page2.Rows.Concat(page3.Rows).Select(r => r.Container.Name));
            Assert.Null(page3.Next);
            Assert.Equal(5, page1.SummaryByDisplayStatus.Values.Sum());
            Assert.Equal(1, page1.SummaryByDisplayStatus["failed"]);
            Assert.Equal(1, page1.SummaryByDisplayStatus["paused"]);

            var failed = await store.SearchAsync(new ContainerSearchQuery([first, second], ["failed"], null, ContainerSort.NameAsc, null, 10), ct);
            Assert.Equal("worker", Assert.Single(failed.Rows).Container.Name);
            Assert.Equal(5, failed.SummaryByDisplayStatus.Values.Sum());

            var underscore = await store.SearchAsync(new ContainerSearchQuery([first, second], [], "s_c", ContainerSort.NameAsc, null, 10), ct);
            Assert.Equal("redis_cache", Assert.Single(underscore.Rows).Container.Name);
            var wildcard = await store.SearchAsync(new ContainerSearchQuery([first, second], [], "a_i", ContainerSort.NameAsc, null, 10), ct);
            Assert.Empty(wildcard.Rows);
            var byPort = await store.SearchAsync(new ContainerSearchQuery([first], [], "8080", ContainerSort.NameAsc, null, 10), ct);
            Assert.Equal(3, byPort.Rows.Count);
            var onlySecond = await store.SearchAsync(new ContainerSearchQuery([second], [], "api", ContainerSort.NameDesc, null, 10), ct);
            Assert.Equal(second, Assert.Single(onlySecond.Rows).EnvironmentId);
        }
        finally
        {
            await context.InventoryProjections.Where(item => item.EnvironmentId == first || item.EnvironmentId == second).ExecuteDeleteAsync(ct);
            await context.EnvironmentRegistrations.Where(item => item.EnvironmentId == first || item.EnvironmentId == second).ExecuteDeleteAsync(ct);
        }
    }

    [Fact]
    public void AllowedActions_IntersectScopeCapabilityStateAndConnection()
    {
        var registration = EnvironmentRegistration.Create(
            Guid.NewGuid(), "srv", "srv", true,
            [EnvironmentResourceScopes.Read, EnvironmentResourceScopes.StopContainer, EnvironmentResourceScopes.PauseContainer, EnvironmentResourceScopes.DeleteContainer]);
        var descriptor = new AgentDescriptor(registration.EnvironmentId, "docker", "27.3.1", "1.47", false, "linux", "6", "amd64", "0.2.0",
            ["Inventory", "ContainerStop", "ContainerDelete", "ContainerPause", "ContainerUnpause"], "srv", 4, 1, DateTimeOffset.UtcNow);
        var connected = new HostView(registration, descriptor, null, true, HostSituation.UpToDate, null);
        var running = new ContainerInventory("a", "api", "img", "Running", "a:1", DateTimeOffset.UtcNow);
        var paused = running with { State = "Paused" };
        var exited = running with { State = "Exited" };

        Assert.Equal(["stop", "pause"], ContainerSearchService.AllowedActions(connected, running));
        Assert.Equal(["stop", "unpause"], ContainerSearchService.AllowedActions(connected, paused));
        Assert.Equal(["delete"], ContainerSearchService.AllowedActions(connected, exited));
        Assert.Empty(ContainerSearchService.AllowedActions(connected with { Connected = false, Situation = HostSituation.Disconnected }, running));
        Assert.Equal(["stop"], ContainerSearchService.AllowedActions(connected with { Descriptor = descriptor with { Capabilities = ["ContainerStop"] } }, running));
    }

    private static InventorySnapshot Snapshot(Guid environmentId, DateTimeOffset now, params (string Name, string State, int? Exit)[] containers) =>
        new(environmentId, (ulong)now.ToUnixTimeMilliseconds(), containers.ToDictionary(
            c => $"{environmentId:N}-{c.Name}",
            c => new ContainerInventory($"{environmentId:N}-{c.Name}", c.Name, $"registry/{c.Name}:1", c.State, "r", now)
            {
                ExitCode = c.Exit,
                Ports = [new ContainerPortBinding("0.0.0.0", 8080, 80, "tcp")],
            }));

    private static ControlPlaneDbContext CreateContextOrSkip()
    {
        var connectionString = Environment.GetEnvironmentVariable("DOKPOD_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) Assert.Skip("DOKPOD_TEST_POSTGRES_CONNECTION is not configured.");
        return new ControlPlaneDbContext(new DbContextOptionsBuilder<ControlPlaneDbContext>().UseNpgsql(connectionString).Options);
    }
}
