using Dokpod.ControlPlane.Application.Commands;
using Dokpod.ControlPlane.Application.Volumes;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Environments;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests;

/// <summary>Spec 018 em PostgreSQL real: projeção de volumes, busca, resumo e comando de remoção com parâmetro.</summary>
[Collection(AgentCommandsPostgresCollection.Name)]
public sealed class VolumesIntegrationTests
{
    internal static readonly IReadOnlyDictionary<string, string> NoLabels = new Dictionary<string, string>();

    [Fact]
    public async Task Store_ReplacesSnapshotsByRevisionAndSearchesWithSummary()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        var store = new PostgresVolumeInventoryStore(context);
        var now = DateTimeOffset.UtcNow;
        try
        {
            await CreateEnvironmentAsync(context, a, ct);
            await CreateEnvironmentAsync(context, b, ct);
            var anonymous = new string('f', 64);
            var first = new VolumeSnapshot(a, 10, now,
            [
                new VolumeInventoryItem("dados-pedidos", "local", "local", now.AddDays(-2), new Dictionary<string, string> { ["com.docker.compose.project"] = "loja" },
                    [new VolumeMount(new string('a', 64), "api-pedidos", "/srv/dados", false), new VolumeMount(new string('b', 64), "backup", "/backup", true)], false),
                new VolumeInventoryItem("orfao", "local", "local", now.AddDays(-1), NoLabels, [], false),
                new VolumeInventoryItem(anonymous, "local", "local", null, NoLabels, [], false),
            ]);
            Assert.Equal(VolumeSnapshotOutcome.Accepted, await store.ReplaceSnapshotAsync(first, ct));
            Assert.Equal(VolumeSnapshotOutcome.Accepted, await store.ReplaceSnapshotAsync(new VolumeSnapshot(b, 5, now, [new VolumeInventoryItem("orfao", "local", "local", null, NoLabels, [], false)]), ct));
            Assert.Equal(VolumeSnapshotOutcome.Stale, await store.ReplaceSnapshotAsync(first with { Revision = 9, Volumes = [] }, ct));

            Task<VolumeSearchResult> Search(bool unused = false, bool? anon = null, string? text = null, Guid[]? hosts = null) =>
                store.SearchAsync(new VolumeSearchQuery(hosts ?? [a, b], unused, anon, text, VolumeSort.NameAsc, 0, 50), ct);

            var all = await Search();
            Assert.Equal(4, all.Summary.Total);
            Assert.Equal(3, all.Summary.Unused);
            Assert.Equal(["dados-pedidos", "orfao", "orfao"], (await Search(anon: false)).Rows.Select(r => r.Volume.Name));
            Assert.Equal(3, (await Search(unused: true)).Rows.Count);
            Assert.Equal(anonymous, Assert.Single((await Search(anon: true)).Rows).Volume.Name);
            Assert.Equal("dados-pedidos", Assert.Single((await Search(text: "backup")).Rows).Volume.Name);  // container que monta
            Assert.Equal("dados-pedidos", Assert.Single((await Search(text: "loja")).Rows).Volume.Name);    // label
            Assert.Equal(2, (await Search(text: "orf")).Rows.Count);
            Assert.Empty((await Search(text: "%")).Rows);                                                     // curinga escapado

            var detail = (await store.GetAsync(a, "dados-pedidos", ct))!;
            Assert.Equal(["api-pedidos", "backup"], detail.Volume.Mounts.Select(m => m.ContainerName).Order());
            Assert.True(detail.Volume.Mounts.Single(m => m.ContainerName == "backup").ReadOnly);
            Assert.Equal("loja", detail.Volume.Labels["com.docker.compose.project"]);
            Assert.Null(await store.GetAsync(b, "dados-pedidos", ct));
            Assert.Equal((ulong)10, await store.GetRevisionAsync(a, ct));

            var unprojected = Guid.NewGuid();
            Assert.Equal([a], await store.ListProjectedAsync([a, unprojected], ct));
            Assert.Equal(VolumeSnapshotOutcome.Accepted, await store.ReplaceSnapshotAsync(first with { Revision = 11, Volumes = [] }, ct));
            Assert.Equal(1, (await Search()).Summary.Total);
        }
        finally
        {
            await CleanupAsync(context, [a, b], ct);
        }
    }

    [Fact]
    public async Task CommandStore_PersistsRemoveVolumeAsHostCommandWithParameters()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var env = Guid.NewGuid();
        try
        {
            await CreateEnvironmentAsync(context, env, ct);
            var store = new PostgresAgentCommandStore(context);
            var parameters = new RemoveVolumeParameters("orfao");
            var command = new AgentCommand(env, Guid.NewGuid(), AgentCommandKind.RemoveVolume, string.Empty, string.Empty,
                AgentCommandKinds.PayloadHash(AgentCommandKind.RemoveVolume, string.Empty, string.Empty, parameters),
                DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds()), 1)
            {
                Parameters = parameters,
            };
            var now = DateTimeOffset.UtcNow;

            Assert.Equal(AgentCommandEnqueueResult.Created, await store.EnqueueAsync(new PersistedAgentCommand(command, ControlPlaneCommandState.Pending, now, now), ct));
            var claimed = Assert.Single(await store.ClaimDispatchableAsync(env, 7, now.AddSeconds(1), ct));
            Assert.Equal(AgentCommandKind.RemoveVolume, claimed.Command.Kind);
            Assert.Equal("orfao", Assert.IsType<RemoveVolumeParameters>(claimed.Command.Parameters).Name);
            Assert.Equal(command.PayloadHash, claimed.Command.PayloadHash);
        }
        finally
        {
            await CleanupAsync(context, [env], ct);
        }
    }

    internal static Task CreateEnvironmentAsync(ControlPlaneDbContext context, Guid id, CancellationToken ct) =>
        new PostgresEnvironmentRegistrationStore(context).CreateAsync(
            EnvironmentRegistration.Create(id, $"vol-{id:N}"[..20], "srv-vol", true, [EnvironmentResourceScopes.Read, EnvironmentResourceScopes.DeleteVolume]), ct);

    internal static async Task CleanupAsync(ControlPlaneDbContext context, Guid[] ids, CancellationToken ct)
    {
        await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dokpod.agent_commands WHERE environment_id = ANY ({ids})", ct);
        await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dokpod.inventory_volume_projections WHERE environment_id = ANY ({ids})", ct);
        await context.EnvironmentRegistrations.Where(item => ids.Contains(item.EnvironmentId)).ExecuteDeleteAsync(ct);
    }

    internal static ControlPlaneDbContext CreateContextOrSkip()
    {
        var connectionString = Environment.GetEnvironmentVariable("DOKPOD_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) Assert.Skip("DOKPOD_TEST_POSTGRES_CONNECTION is not configured.");
        return new ControlPlaneDbContext(new DbContextOptionsBuilder<ControlPlaneDbContext>().UseNpgsql(connectionString).Options);
    }
}

/// <summary>Spec 018 SC-005: 5.040 volumes em 56 hosts, primeira página em até 2 s no p95 (coleção de desempenho).</summary>
[Collection(PostgresPerformanceCollection.Name)]
public sealed class VolumeLoadTests
{
    [Fact]
    public async Task FirstPage_With5000VolumesAcross56Hosts_RespondsWithinTwoSecondsAtP95()
    {
        await using var context = VolumesIntegrationTests.CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var hosts = Enumerable.Range(0, 56).Select(_ => Guid.NewGuid()).ToArray();
        var store = new PostgresVolumeInventoryStore(context);
        try
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var (host, index) in hosts.Select((h, i) => (h, i)))
            {
                await VolumesIntegrationTests.CreateEnvironmentAsync(context, host, ct);
                var volumes = Enumerable.Range(0, 90).Select(v => new VolumeInventoryItem(
                    v % 3 == 0 ? $"{v:x2}{new string('c', 62)}" : $"dados-{index}-{v}",
                    "local", "local", now.AddMinutes(-v), new Dictionary<string, string> { ["app"] = $"app-{v % 7}" },
                    v % 2 == 0 ? [] : [new VolumeMount(new string('a', 64), $"svc-{index}-{v}", "/dados", false)], false)).ToArray();
                await store.ReplaceSnapshotAsync(new VolumeSnapshot(host, 1, now, volumes), ct);
            }

            await context.Database.ExecuteSqlRawAsync("ANALYZE dokpod.inventory_volumes", ct);
            var queries = new[]
            {
                new VolumeSearchQuery(hosts, false, null, null, VolumeSort.NameAsc, 0, 50),
                new VolumeSearchQuery(hosts, true, null, null, VolumeSort.NameAsc, 0, 50),
                new VolumeSearchQuery(hosts, false, false, "app-3", VolumeSort.CreatedAtDesc, 0, 50),
                new VolumeSearchQuery(hosts, false, null, "svc-10", VolumeSort.Environment, 0, 50),
                new VolumeSearchQuery(hosts.Take(5).ToArray(), true, true, null, VolumeSort.NameDesc, 0, 50),
            };
            var timings = new List<double>();
            for (var round = 0; round < 8; round++)
            {
                foreach (var query in queries)
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    var result = await store.SearchAsync(query, ct);
                    timings.Add(watch.Elapsed.TotalMilliseconds);
                    if (query.EnvironmentIds.Count == 56 && !query.UnusedOnly && query.Text is null) Assert.Equal(5040, result.Summary.Total);
                }
            }

            timings.Sort();
            var p95 = timings[(int)Math.Ceiling(timings.Count * 0.95) - 1];
            TestContext.Current.SendDiagnosticMessage($"SC-005 primeira página p95={p95:0} ms (máx {timings[^1]:0} ms) com 5.040 volumes");
            Assert.True(p95 < 2000, $"p95 {p95:0} ms");
        }
        finally
        {
            await VolumesIntegrationTests.CleanupAsync(context, hosts, ct);
        }
    }
}
