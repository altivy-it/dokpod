using Dokpod.ControlPlane.Application.Commands;
using Dokpod.ControlPlane.Application.Pods;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Pods;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests;

/// <summary>Spec 021 em PostgreSQL real: projeção de pods, situação, busca, filtro por container e comando com falha parcial.</summary>
[Collection(AgentCommandsPostgresCollection.Name)]
public sealed class PodsIntegrationTests
{
    internal static PodInventoryItem Item(string id, string name, string status, bool agent = false, params (string Id, string Name, string Status)[] members) =>
        new(id, name, status, null, new Dictionary<string, string> { ["io.podman.compose.project"] = name }, ["rede"], null,
            members.Select(m => new PodMemberItem(m.Id, m.Name, m.Status, 0, false)).ToArray(), agent, false);

    [Fact]
    public async Task Store_ReplacesByRevisionSummarizesByStatusAndFindsPodsByContainer()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var (podman, docker) = (Guid.NewGuid(), Guid.NewGuid());
        var store = new PostgresPodInventoryStore(context);
        var now = DateTimeOffset.UtcNow;
        var web = new string('a', 64);
        try
        {
            await CreateEnvironmentAsync(context, podman, ct);
            await CreateEnvironmentAsync(context, docker, ct);
            var first = new PodSnapshot(podman, 10, true, now,
            [
                Item(new string('1', 64), "pod-loja", "Degraded", false, (web, "web", "running"), (new string('b', 64), "worker", "exited")),
                Item(new string('2', 64), "pod_dokpod", "Running", true, (new string('c', 64), "agent", "running")),
                Item(new string('3', 64), "pod-parado", "Exited"),
            ]);
            Assert.True(await store.ReplaceSnapshotAsync(first, ct));
            Assert.True(await store.ReplaceSnapshotAsync(new PodSnapshot(docker, 1, false, now, []), ct));
            Assert.False(await store.ReplaceSnapshotAsync(first with { Revision = 9, Pods = [] }, ct));

            Task<PodSearchResult> Search(string[]? statuses = null, string? container = null, string? text = null) =>
                store.SearchAsync(new PodSearchQuery([podman, docker], statuses ?? [], container, text, PodSort.NameAsc, 0, 50), ct);

            var all = await Search();
            Assert.Equal(3, all.Summary.Total);
            Assert.Equal((1, 1, 1), (all.Summary.ByStatus["degraded"], all.Summary.ByStatus["running"], all.Summary.ByStatus["exited"]));
            Assert.Equal(["pod-loja"], (await Search(statuses: ["degraded"])).Rows.Select(r => r.Pod.Name));
            Assert.Equal(["pod-loja"], (await Search(container: web)).Rows.Select(r => r.Pod.Name));
            Assert.Equal(["pod-loja"], (await Search(text: "worker")).Rows.Select(r => r.Pod.Name));
            var loja = all.Rows.Single(r => r.Pod.Name == "pod-loja").Pod;
            Assert.Equal((2, 1), (loja.Containers.Count, loja.RunningContainers));
            Assert.True((await store.GetAsync(podman, "pod_dokpod", ct))!.Pod.AgentPod);
            Assert.Equal(
                [new PodProjectionState(podman, true), new PodProjectionState(docker, false)],
                (await store.ListProjectionsAsync([podman, docker], ct)).OrderBy(p => p.PodsSupported ? 0 : 1));
        }
        finally
        {
            await CleanupAsync(context, [podman, docker], ct);
        }
    }

    [Fact]
    public async Task CommandStore_PersistsPodActionAndFailedContainers()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var env = Guid.NewGuid();
        try
        {
            await CreateEnvironmentAsync(context, env, ct);
            var store = new PostgresAgentCommandStore(context);
            var parameters = new PodActionParameters(new string('1', 64), "pod-loja", PodActionKind.Restart);
            var command = new AgentCommand(env, Guid.NewGuid(), AgentCommandKind.PodAction, string.Empty, string.Empty,
                AgentCommandKinds.PayloadHash(AgentCommandKind.PodAction, string.Empty, string.Empty, parameters),
                DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds()), 1)
            {
                Parameters = parameters,
            };
            var now = DateTimeOffset.UtcNow;

            Assert.Equal(AgentCommandEnqueueResult.Created, await store.EnqueueAsync(new PersistedAgentCommand(command, ControlPlaneCommandState.Pending, now, now), ct));
            var claimed = Assert.Single(await store.ClaimDispatchableAsync(env, 7, now.AddSeconds(1), ct));
            Assert.Equal(PodActionKind.Restart, Assert.IsType<PodActionParameters>(claimed.Command.Parameters).Action);

            var update = new AgentCommandStatusUpdate(env, command.CommandId, ControlPlaneCommandState.Failed, PodActionCodes.PartialFailure, null, now.AddSeconds(2))
            {
                Output = new AgentCommandOutput { FailedContainers = ["worker", "cache"] },
            };
            Assert.Equal(AgentCommandStatusUpdateResult.Applied, await store.ApplyStatusAsync(update, ct));
            var status = (await store.GetAsync(env, command.CommandId, ct))!;
            Assert.Equal(["worker", "cache"], status.Output!.FailedContainers);
            Assert.Equal(("pod-loja", "restart"), (status.PodName, status.PodAction));
        }
        finally
        {
            await CleanupAsync(context, [env], ct);
        }
    }

    internal static Task CreateEnvironmentAsync(ControlPlaneDbContext context, Guid id, CancellationToken ct) =>
        new PostgresEnvironmentRegistrationStore(context).CreateAsync(
            EnvironmentRegistration.Create(id, $"pod-{id:N}"[..20], "srv-pod", true, EnvironmentResourceScopes.Supported), ct);

    internal static async Task CleanupAsync(ControlPlaneDbContext context, Guid[] ids, CancellationToken ct)
    {
        await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dokpod.agent_commands WHERE environment_id = ANY ({ids})", ct);
        await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dokpod.inventory_pod_projections WHERE environment_id = ANY ({ids})", ct);
        await context.EnvironmentRegistrations.Where(item => ids.Contains(item.EnvironmentId)).ExecuteDeleteAsync(ct);
    }

    internal static ControlPlaneDbContext CreateContextOrSkip()
    {
        var connectionString = Environment.GetEnvironmentVariable("DOKPOD_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) Assert.Skip("DOKPOD_TEST_POSTGRES_CONNECTION is not configured.");
        return new ControlPlaneDbContext(new DbContextOptionsBuilder<ControlPlaneDbContext>().UseNpgsql(connectionString).Options);
    }
}

/// <summary>Spec 021 SC-005: 1.008 pods em 56 hosts, primeira página em até 2 s no p95.</summary>
[Collection(PostgresPerformanceCollection.Name)]
public sealed class PodLoadTests
{
    [Fact]
    public async Task FirstPage_With1000PodsAcross56Hosts_RespondsWithinTwoSecondsAtP95()
    {
        await using var context = PodsIntegrationTests.CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var hosts = Enumerable.Range(0, 56).Select(_ => Guid.NewGuid()).ToArray();
        var store = new PostgresPodInventoryStore(context);
        string[] statuses = ["Running", "Degraded", "Exited", "Paused", "Created"];
        try
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var (host, h) in hosts.Select((host, h) => (host, h)))
            {
                await PodsIntegrationTests.CreateEnvironmentAsync(context, host, ct);
                var pods = Enumerable.Range(0, 18).Select(p => PodsIntegrationTests.Item(
                    $"{h:x2}{p:x2}{new string('d', 60)}", $"pod-{h}-{p}", statuses[p % statuses.Length], false,
                    Enumerable.Range(0, 4).Select(c => ($"{h:x2}{p:x2}{c:x2}{new string('e', 58)}", $"svc-{h}-{p}-{c}", c == 0 ? "running" : "exited")).ToArray())).ToArray();
                await store.ReplaceSnapshotAsync(new PodSnapshot(host, 1, true, now, pods), ct);
            }

            await context.Database.ExecuteSqlRawAsync("ANALYZE dokpod.inventory_pods", ct);
            var queries = new[]
            {
                new PodSearchQuery(hosts, [], null, null, PodSort.NameAsc, 0, 50),
                new PodSearchQuery(hosts, ["degraded"], null, null, PodSort.Environment, 0, 50),
                new PodSearchQuery(hosts, [], null, "svc-10", PodSort.CreatedAtDesc, 0, 50),
                new PodSearchQuery(hosts, [], $"0a03{"02"}{new string('e', 58)}", null, PodSort.NameAsc, 0, 50),
            };
            var timings = new List<double>();
            for (var round = 0; round < 8; round++)
            {
                foreach (var query in queries)
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    var result = await store.SearchAsync(query, ct);
                    timings.Add(watch.Elapsed.TotalMilliseconds);
                    if (query.Statuses.Count == 0 && query.Text is null && query.ContainerId is null) Assert.Equal(1008, result.Summary.Total);
                    if (query.ContainerId is not null) Assert.Single(result.Rows);
                }
            }

            timings.Sort();
            var p95 = timings[(int)Math.Ceiling(timings.Count * 0.95) - 1];
            TestContext.Current.SendDiagnosticMessage($"SC-005 primeira página p95={p95:0} ms (máx {timings[^1]:0} ms) com 1.008 pods");
            Assert.True(p95 < 2000, $"p95 {p95:0} ms");
        }
        finally
        {
            await PodsIntegrationTests.CleanupAsync(context, hosts, ct);
        }
    }
}
