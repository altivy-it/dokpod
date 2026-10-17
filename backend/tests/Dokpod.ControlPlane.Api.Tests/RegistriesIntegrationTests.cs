using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.ControlPlane.Application.Images;
using Dokpod.ControlPlane.Application.Registries;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Hosts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests;

/// <summary>Spec 020 em PostgreSQL real: configuração de registros por host, registros de origem das imagens e política.</summary>
[Collection(AgentCommandsPostgresCollection.Name)]
public sealed class RegistriesIntegrationTests
{
    internal static HostView Host(EnvironmentRegistration registration, params string[] capabilities) => new(
        registration,
        new AgentDescriptor(registration.EnvironmentId, "docker", "28.5.2", "1.47", false, "linux", "6", "amd64", "0.1.0",
            capabilities.Length == 0 ? ["ImageInventory", "RegistryInventory"] : capabilities, "host", 2, 1024, DateTimeOffset.UtcNow),
        null, true, HostSituation.UpToDate, null);

    [Fact]
    public async Task Stores_ReplaceConfigurationByRevisionAndFeedTheCatalogWithImageOrigins()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var env = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        try
        {
            var registration = await CreateEnvironmentAsync(context, env, EnvironmentStage.Staging, ct);
            var store = new PostgresRegistryInventoryStore(context);
            var first = new RegistryInventorySnapshot(env, 10, true, now,
            [
                new RegistryHostConfig("docker.io", ["mirror.empresa.com"], null, false, false, true, false),
                new RegistryHostConfig("mirror.empresa.com", [], "docker.io", false, false, false, false),
                new RegistryHostConfig("registry.empresa.com", [], null, true, false, false, true),
            ]);
            Assert.True(await store.ReplaceAsync(first, ct));
            Assert.False(await store.ReplaceAsync(first with { Revision = 9, Registries = [] }, ct));
            await new PostgresImageInventoryStore(context).ReplaceSnapshotAsync(new ImageSnapshot(env, 5, now,
            [
                new ImageInventoryItem("sha256:" + new string('a', 64), ["nginx:1.27"], [], 100, null, null, null, false),
                new ImageInventoryItem("sha256:" + new string('b', 64), ["registry.empresa.com/loja/api:3"], [], 50, null, null, null, false),
            ]), ct);

            var rows = await store.ListAsync([env], ct);
            Assert.Equal(["docker.io", "mirror.empresa.com", "registry.empresa.com"], rows.Select(r => r.Config.Registry).Order());
            Assert.Equal(["mirror.empresa.com"], rows.Single(r => r.Config.Registry == "docker.io").Config.Mirrors);
            Assert.Equal([env], await store.ListConfigAvailableAsync([env, Guid.NewGuid()], ct));

            var catalog = new RegistryCatalog(store, new ImagePullPolicy());
            var page = RegistryCatalog.Page(await catalog.EntriesAsync([Host(registration)], ct), new RegistryQuery([], false, false, null, RegistrySort.RegistryAsc, 0, 50));
            var hub = page.Rows.Single(r => r.Registry == "docker.io");
            Assert.Equal((1, 100L), (hub.Images, hub.SizeBytes));
            Assert.Equal(["credential", "images", "insecure"], page.Rows.Single(r => r.Registry == "registry.empresa.com").Origins);
            Assert.Equal(1, page.Summary.Insecure);

            // Spec 023: agente anterior (sem RegistryInventory nem ImageInventory) não traz a projeção gravada por uma versão mais nova.
            Assert.Empty(await catalog.EntriesAsync([Host(registration, "Inventory")], ct));

            Assert.True(await store.ReplaceAsync(first with { Revision = 11, ConfigAvailable = false, Registries = [] }, ct));
            Assert.Empty(await store.ListConfigAvailableAsync([env], ct));
            Assert.Empty(await store.ListAsync([env], ct));
        }
        finally
        {
            await CleanupAsync(context, [env], ct);
        }
    }

    [Fact]
    public async Task PolicyStore_SavesAndListsPerStage()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var store = new PostgresRegistryPolicyStore(context);
        var before = await store.ListAsync(ct);
        try
        {
            var at = new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
            await store.SaveAsync(new StoredRegistryPolicy(EnvironmentStage.Staging, ["quay.io"], "admin", at), ct);
            await store.SaveAsync(new StoredRegistryPolicy(EnvironmentStage.Staging, ["quay.io", "registry.empresa.com"], "admin2", at.AddMinutes(1)), ct);
            await store.SaveAsync(new StoredRegistryPolicy(null, [], "admin", at), ct);

            var stored = await store.ListAsync(ct);
            var staging = stored.Single(p => p.Stage == EnvironmentStage.Staging);
            Assert.Equal(["quay.io", "registry.empresa.com"], staging.Registries);
            Assert.Equal(("admin2", at.AddMinutes(1)), (staging.UpdatedBy, staging.UpdatedAt));
            Assert.Empty(stored.Single(p => p.Stage is null).Registries);
        }
        finally
        {
            // Restaura a política do banco de testes como estava antes do teste.
            await context.Database.ExecuteSqlRawAsync("DELETE FROM dokpod.registry_policies", ct);
            foreach (var policy in before) await store.SaveAsync(policy, ct);
        }
    }

    internal static async Task<EnvironmentRegistration> CreateEnvironmentAsync(ControlPlaneDbContext context, Guid id, EnvironmentStage stage, CancellationToken ct)
    {
        var registration = EnvironmentRegistration.Create(id, $"reg-{id:N}"[..20], "srv-reg", true, [EnvironmentResourceScopes.Read], stage);
        await new PostgresEnvironmentRegistrationStore(context).CreateAsync(registration, ct);
        return registration;
    }

    internal static async Task CleanupAsync(ControlPlaneDbContext context, Guid[] ids, CancellationToken ct)
    {
        await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dokpod.inventory_registry_projections WHERE environment_id = ANY ({ids})", ct);
        await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dokpod.inventory_image_projections WHERE environment_id = ANY ({ids})", ct);
        await context.EnvironmentRegistrations.Where(item => ids.Contains(item.EnvironmentId)).ExecuteDeleteAsync(ct);
    }

    internal static ControlPlaneDbContext CreateContextOrSkip()
    {
        var connectionString = Environment.GetEnvironmentVariable("DOKPOD_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) Assert.Skip("DOKPOD_TEST_POSTGRES_CONNECTION is not configured.");
        return new ControlPlaneDbContext(new DbContextOptionsBuilder<ControlPlaneDbContext>().UseNpgsql(connectionString).Options);
    }
}

/// <summary>Spec 020 SC-004: 200 registros em 56 hosts (2.240 imagens e 224 configurações), primeira página em até 2 s no p95.</summary>
[Collection(PostgresPerformanceCollection.Name)]
public sealed class RegistryLoadTests
{
    [Fact]
    public async Task FirstPage_With200RegistriesAcross56Hosts_RespondsWithinTwoSecondsAtP95()
    {
        await using var context = RegistriesIntegrationTests.CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var ids = Enumerable.Range(0, 56).Select(_ => Guid.NewGuid()).ToArray();
        var store = new PostgresRegistryInventoryStore(context);
        var images = new PostgresImageInventoryStore(context);
        var hosts = new List<HostView>();
        try
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var (id, h) in ids.Select((id, h) => (id, h)))
            {
                var stage = (EnvironmentStage)(h % 3);
                hosts.Add(RegistriesIntegrationTests.Host(await RegistriesIntegrationTests.CreateEnvironmentAsync(context, id, stage, ct)));
                await images.ReplaceSnapshotAsync(new ImageSnapshot(id, 1, now, Enumerable.Range(0, 40).Select(i => new ImageInventoryItem(
                    $"sha256:{h:x2}{i:x2}{new string('c', 60)}", [$"reg-{(h * 4 + i) % 200}.empresa.com/app/img-{i}:1"], [], 1_000_000 + i, null, null, null, false)).ToArray()), ct);
                await store.ReplaceAsync(new RegistryInventorySnapshot(id, 1, true, now, Enumerable.Range(0, 4).Select(k =>
                    new RegistryHostConfig($"reg-{(h * 4 + k) % 200}.empresa.com", [], null, k == 0, false, k == 1, k == 2)).ToArray()), ct);
            }

            var catalog = new RegistryCatalog(store, new ImagePullPolicy { AllowedRegistriesByStage = new(StringComparer.OrdinalIgnoreCase) { ["production"] = ["reg-0.empresa.com"] } });
            var queries = new[]
            {
                new RegistryQuery([], false, false, null, RegistrySort.RegistryAsc, 0, 50),
                new RegistryQuery([], true, false, null, RegistrySort.ImagesDesc, 0, 50),
                new RegistryQuery([], false, true, null, RegistrySort.HostsDesc, 0, 50),
                new RegistryQuery([RegistryOrigins.Credential], false, false, "reg-1", RegistrySort.RegistryAsc, 0, 50),
            };
            var timings = new List<double>();
            for (var round = 0; round < 8; round++)
            {
                foreach (var query in queries)
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    var page = RegistryCatalog.Page(await catalog.EntriesAsync(hosts, ct), query);
                    timings.Add(watch.Elapsed.TotalMilliseconds);
                    if (query.Origins.Count == 0 && !query.InsecureOnly && !query.OutsidePolicyOnly) Assert.Equal(200, page.Summary.Total);
                }
            }

            timings.Sort();
            var p95 = timings[(int)Math.Ceiling(timings.Count * 0.95) - 1];
            TestContext.Current.SendDiagnosticMessage($"SC-004 primeira página p95={p95:0} ms (máx {timings[^1]:0} ms) com 200 registros");
            Assert.True(p95 < 2000, $"p95 {p95:0} ms");
        }
        finally
        {
            await RegistriesIntegrationTests.CleanupAsync(context, ids, ct);
        }
    }
}
