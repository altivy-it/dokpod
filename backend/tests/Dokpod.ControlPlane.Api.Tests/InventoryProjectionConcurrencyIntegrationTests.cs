using Xunit;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Dokpod.ControlPlane.Api.Tests;

/// <summary>
/// Spec 005, achado A2: na reconexão de muitos agentes, a projeção em SERIALIZABLE gerava 40001 entre ambientes
/// diferentes. Com o lock por ambiente, publicações de ambientes distintos não colidem e as do mesmo ambiente
/// continuam ordenadas pela revisão.
/// </summary>
public sealed class InventoryProjectionConcurrencyIntegrationTests
{
    [Fact]
    public async Task ManyAgentsPublishingAtOnce_AllSnapshotsAndDeltasAreProjected()
    {
        var connectionString = ConnectionStringOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var ids = Enumerable.Range(0, 40).Select(_ => Guid.NewGuid()).ToArray();
        await using var setup = CreateContext(connectionString);
        try
        {
            await RegisterAsync(setup, ids, ct);
            var now = DateTimeOffset.UtcNow;

            var outcomes = await Task.WhenAll(ids.Select(async id =>
            {
                await using var context = CreateContext(connectionString);
                var store = new PostgresInventoryProjectionStore(context);
                var snapshot = await store.ReplaceSnapshotAsync(new InventorySnapshot(id, 1, Containers(id, 20, now)), ct);
                var delta = await store.ApplyDeltaAsync(id, new InventoryDelta(1, 2,
                [
                    new InventoryChange(InventoryChangeKind.Updated, $"{id:N}{0:D4}", Container(id, 0, now, "Exited")),
                ]), ct);
                return (snapshot.Outcome, delta.Outcome);
            }));

            Assert.All(outcomes, outcome =>
            {
                Assert.Equal(InventoryReconciliationOutcome.Accepted, outcome.Item1);
                Assert.Equal(InventoryReconciliationOutcome.Accepted, outcome.Item2);
            });
            var projections = await setup.InventoryProjections.AsNoTracking()
                .Where(item => ids.Contains(item.EnvironmentId))
                .ToListAsync(ct);
            Assert.Equal(ids.Length, projections.Count);
            Assert.All(projections, projection => Assert.Equal(2, projection.Revision));
            Assert.Equal(ids.Length * 20, await setup.InventoryContainers.CountAsync(item => ids.Contains(item.EnvironmentId), ct));
        }
        finally
        {
            await CleanupAsync(setup, ids, ct);
        }
    }

    [Fact]
    public async Task SameEnvironmentRacingSnapshots_KeepsTheNewestRevision()
    {
        var connectionString = ConnectionStringOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        await using var setup = CreateContext(connectionString);
        try
        {
            await RegisterAsync(setup, [id], ct);
            var now = DateTimeOffset.UtcNow;

            await Task.WhenAll(Enumerable.Range(1, 10).Select(async revision =>
            {
                await using var context = CreateContext(connectionString);
                await new PostgresInventoryProjectionStore(context).ReplaceSnapshotAsync(
                    new InventorySnapshot(id, (ulong)revision, Containers(id, revision, now)), ct);
            }));

            var projection = await setup.InventoryProjections.AsNoTracking().SingleAsync(item => item.EnvironmentId == id, ct);
            Assert.Equal(10, projection.Revision);
            Assert.Equal(10, await setup.InventoryContainers.CountAsync(item => item.EnvironmentId == id, ct));
        }
        finally
        {
            await CleanupAsync(setup, [id], ct);
        }
    }

    private static Dictionary<string, ContainerInventory> Containers(Guid id, int count, DateTimeOffset now) =>
        Enumerable.Range(0, count).ToDictionary(index => $"{id:N}{index:D4}", index => Container(id, index, now, "Running"));

    private static ContainerInventory Container(Guid id, int index, DateTimeOffset now, string state) =>
        new($"{id:N}{index:D4}", $"c-{index}", "img", state, $"r{index}-{state}", now);

    private static async Task RegisterAsync(ControlPlaneDbContext context, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var registrations = new PostgresEnvironmentRegistrationStore(context);
        for (var i = 0; i < ids.Count; i++)
        {
            await registrations.CreateAsync(
                EnvironmentRegistration.Create(ids[i], $"conc-{i:D2}-{ids[i]:N}"[..24], $"conc-{i}.local", true, [EnvironmentResourceScopes.Read], EnvironmentStage.Production),
                ct);
        }
    }

    private static async Task CleanupAsync(ControlPlaneDbContext context, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        await context.InventoryProjections.Where(item => ids.Contains(item.EnvironmentId)).ExecuteDeleteAsync(ct);
        await context.EnvironmentRegistrations.Where(item => ids.Contains(item.EnvironmentId)).ExecuteDeleteAsync(ct);
    }

    private static string ConnectionStringOrSkip()
    {
        var connectionString = Environment.GetEnvironmentVariable("DOKPOD_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("DOKPOD_TEST_POSTGRES_CONNECTION is not configured.");
        }

        return connectionString;
    }

    private static ControlPlaneDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<ControlPlaneDbContext>().UseNpgsql(connectionString).Options);
}
