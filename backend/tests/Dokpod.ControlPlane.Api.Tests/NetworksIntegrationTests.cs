using Dokpod.ControlPlane.Application.Commands;
using Dokpod.ControlPlane.Application.Networks;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Environments;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests;

/// <summary>Spec 019 em PostgreSQL real: projeção de redes, busca por sub-rede, resumo, detalhe por nome e comando de remoção.</summary>
[Collection(AgentCommandsPostgresCollection.Name)]
public sealed class NetworksIntegrationTests
{
    internal static readonly IReadOnlyDictionary<string, string> NoLabels = new Dictionary<string, string>();

    internal static NetworkInventoryItem Item(string id, string name, string subnet, bool system = false, string scope = "local", IReadOnlyDictionary<string, string>? labels = null, params NetworkConnection[] containers) =>
        new(id, name, "bridge", scope, false, false, false, system, null, [new NetworkSubnet(subnet, null)], labels ?? NoLabels, containers, false);

    [Fact]
    public async Task Store_ReplacesSnapshotsByRevisionAndSearchesBySubnetLabelAndContainer()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        var store = new PostgresNetworkInventoryStore(context);
        var now = DateTimeOffset.UtcNow;
        try
        {
            await CreateEnvironmentAsync(context, a, ct);
            await CreateEnvironmentAsync(context, b, ct);
            var first = new NetworkSnapshot(a, 10, now,
            [
                Item(new string('1', 64), "rede-pedidos", "10.89.77.0/24", labels: new Dictionary<string, string> { ["com.docker.compose.project"] = "loja" },
                    containers: [new NetworkConnection(new string('a', 64), "api-pedidos", "10.89.77.2", null), new NetworkConnection(new string('b', 64), "worker", null, null)]),
                Item(new string('2', 64), "orfa", "10.89.78.0/24"),
                Item(new string('3', 64), "bridge", "172.17.0.0/16", system: true),
            ]);
            Assert.Equal(NetworkSnapshotOutcome.Accepted, await store.ReplaceSnapshotAsync(first, ct));
            Assert.Equal(NetworkSnapshotOutcome.Accepted, await store.ReplaceSnapshotAsync(new NetworkSnapshot(b, 5, now, [Item(new string('4', 64), "orfa", "10.89.77.0/24")]), ct));
            Assert.Equal(NetworkSnapshotOutcome.Stale, await store.ReplaceSnapshotAsync(first with { Revision = 9, Networks = [] }, ct));

            Task<NetworkSearchResult> Search(bool unused = false, bool? system = null, string? text = null, string[]? drivers = null) =>
                store.SearchAsync(new NetworkSearchQuery([a, b], unused, system, drivers ?? [], text, NetworkSort.NameAsc, 0, 50), ct);

            var all = await Search();
            Assert.Equal(4, all.Summary.Total);
            Assert.Equal(2, all.Summary.Unused); // a rede do sistema não conta como "sem uso"
            Assert.Equal(["orfa", "orfa"], (await Search(unused: true)).Rows.Select(r => r.Network.Name));
            Assert.Equal("bridge", Assert.Single((await Search(system: true)).Rows).Network.Name);
            Assert.Equal(3, (await Search(system: false)).Rows.Count);
            Assert.Equal(2, (await Search(text: "10.89.77")).Rows.Count);                                   // sub-redes sobrepostas em hosts diferentes
            Assert.Equal("rede-pedidos", Assert.Single((await Search(text: "worker")).Rows).Network.Name);  // container parado conectado
            Assert.Equal("rede-pedidos", Assert.Single((await Search(text: "loja")).Rows).Network.Name);    // label
            Assert.Equal("orfa", Assert.Single((await Search(text: "2222")).Rows).Network.Name);            // prefixo do identificador
            Assert.Empty((await Search(text: "%")).Rows);
            Assert.Empty((await Search(drivers: ["macvlan"])).Rows);

            var byName = (await store.GetAsync(a, "rede-pedidos", ct))!;
            Assert.Equal(new string('1', 64), byName.Network.NetworkId);
            Assert.Equal(["api-pedidos", "worker"], byName.Network.Containers.Select(c => c.ContainerName));
            Assert.Null(byName.Network.Containers[1].IPv4Address);
            Assert.Equal("loja", byName.Network.Labels["com.docker.compose.project"]);
            Assert.Equal("orfa", (await store.GetAsync(a, new string('2', 64), ct))!.Network.Name);
            Assert.Null(await store.GetAsync(b, "rede-pedidos", ct));
            Assert.Equal([a], await store.ListProjectedAsync([a, Guid.NewGuid()], ct));
        }
        finally
        {
            await CleanupAsync(context, [a, b], ct);
        }
    }

    [Fact]
    public async Task CommandStore_PersistsRemoveNetworkAsHostCommandWithParameters()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var env = Guid.NewGuid();
        try
        {
            await CreateEnvironmentAsync(context, env, ct);
            var store = new PostgresAgentCommandStore(context);
            var parameters = new RemoveNetworkParameters(new string('2', 64), "orfa");
            var command = new AgentCommand(env, Guid.NewGuid(), AgentCommandKind.RemoveNetwork, string.Empty, string.Empty,
                AgentCommandKinds.PayloadHash(AgentCommandKind.RemoveNetwork, string.Empty, string.Empty, parameters),
                DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds()), 1)
            {
                Parameters = parameters,
            };
            var now = DateTimeOffset.UtcNow;

            Assert.Equal(AgentCommandEnqueueResult.Created, await store.EnqueueAsync(new PersistedAgentCommand(command, ControlPlaneCommandState.Pending, now, now), ct));
            var claimed = Assert.Single(await store.ClaimDispatchableAsync(env, 7, now.AddSeconds(1), ct));
            var stored = Assert.IsType<RemoveNetworkParameters>(claimed.Command.Parameters);
            Assert.Equal((new string('2', 64), "orfa"), (stored.NetworkId, stored.Name));
            Assert.Equal(command.PayloadHash, claimed.Command.PayloadHash);
        }
        finally
        {
            await CleanupAsync(context, [env], ct);
        }
    }

    internal static Task CreateEnvironmentAsync(ControlPlaneDbContext context, Guid id, CancellationToken ct) =>
        new PostgresEnvironmentRegistrationStore(context).CreateAsync(
            EnvironmentRegistration.Create(id, $"net-{id:N}"[..20], "srv-net", true, [EnvironmentResourceScopes.Read, EnvironmentResourceScopes.DeleteNetwork]), ct);

    internal static async Task CleanupAsync(ControlPlaneDbContext context, Guid[] ids, CancellationToken ct)
    {
        await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dokpod.agent_commands WHERE environment_id = ANY ({ids})", ct);
        await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dokpod.inventory_network_projections WHERE environment_id = ANY ({ids})", ct);
        await context.EnvironmentRegistrations.Where(item => ids.Contains(item.EnvironmentId)).ExecuteDeleteAsync(ct);
    }

    internal static ControlPlaneDbContext CreateContextOrSkip()
    {
        var connectionString = Environment.GetEnvironmentVariable("DOKPOD_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) Assert.Skip("DOKPOD_TEST_POSTGRES_CONNECTION is not configured.");
        return new ControlPlaneDbContext(new DbContextOptionsBuilder<ControlPlaneDbContext>().UseNpgsql(connectionString).Options);
    }
}

/// <summary>Spec 019 SC-005: 2.016 redes em 56 hosts, primeira página em até 2 s no p95 (coleção de desempenho).</summary>
[Collection(PostgresPerformanceCollection.Name)]
public sealed class NetworkLoadTests
{
    [Fact]
    public async Task FirstPage_With2000NetworksAcross56Hosts_RespondsWithinTwoSecondsAtP95()
    {
        await using var context = NetworksIntegrationTests.CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var hosts = Enumerable.Range(0, 56).Select(_ => Guid.NewGuid()).ToArray();
        var store = new PostgresNetworkInventoryStore(context);
        try
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var (host, index) in hosts.Select((h, i) => (h, i)))
            {
                await NetworksIntegrationTests.CreateEnvironmentAsync(context, host, ct);
                var networks = Enumerable.Range(0, 36).Select(n => NetworksIntegrationTests.Item(
                    $"{index:x2}{n:x2}{new string('c', 60)}",
                    n == 0 ? "bridge" : $"rede-{index}-{n}",
                    $"10.{index}.{n}.0/24",
                    system: n == 0,
                    labels: new Dictionary<string, string> { ["app"] = $"app-{n % 7}" },
                    containers: n % 2 == 0 ? [] : [new NetworkConnection(new string('a', 64), $"svc-{index}-{n}", $"10.{index}.{n}.2", null)])).ToArray();
                await store.ReplaceSnapshotAsync(new NetworkSnapshot(host, 1, now, networks), ct);
            }

            await context.Database.ExecuteSqlRawAsync("ANALYZE dokpod.inventory_networks", ct);
            var queries = new[]
            {
                new NetworkSearchQuery(hosts, false, null, [], null, NetworkSort.NameAsc, 0, 50),
                new NetworkSearchQuery(hosts, true, null, [], null, NetworkSort.NameAsc, 0, 50),
                new NetworkSearchQuery(hosts, false, false, ["bridge"], "10.12.", NetworkSort.CreatedAtDesc, 0, 50),
                new NetworkSearchQuery(hosts, false, null, [], "svc-10", NetworkSort.Environment, 0, 50),
                new NetworkSearchQuery(hosts.Take(5).ToArray(), true, null, [], null, NetworkSort.NameDesc, 0, 50),
            };
            var timings = new List<double>();
            for (var round = 0; round < 8; round++)
            {
                foreach (var query in queries)
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    var result = await store.SearchAsync(query, ct);
                    timings.Add(watch.Elapsed.TotalMilliseconds);
                    if (query.EnvironmentIds.Count == 56 && !query.UnusedOnly && query.Text is null) Assert.Equal(2016, result.Summary.Total);
                }
            }

            timings.Sort();
            var p95 = timings[(int)Math.Ceiling(timings.Count * 0.95) - 1];
            TestContext.Current.SendDiagnosticMessage($"SC-005 primeira página p95={p95:0} ms (máx {timings[^1]:0} ms) com 2.016 redes");
            Assert.True(p95 < 2000, $"p95 {p95:0} ms");
        }
        finally
        {
            await NetworksIntegrationTests.CleanupAsync(context, hosts, ct);
        }
    }
}
