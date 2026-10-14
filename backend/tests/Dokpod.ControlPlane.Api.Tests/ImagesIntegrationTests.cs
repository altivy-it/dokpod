using Dokpod.ControlPlane.Application.Commands;
using Dokpod.ControlPlane.Application.Images;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Images;
using Dokpod.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests;

/// <summary>Spec 012 em PostgreSQL real: inventário de imagens, comandos com alvo de host, parâmetros e retenção.</summary>
[Collection(AgentCommandsPostgresCollection.Name)]
public sealed class ImagesIntegrationTests
{
    private static string Id(char c) => "sha256:" + new string(c, 64);

    [Fact]
    public async Task ImageStore_ReplacesSnapshotsAndComputesUsageSummaryAndLookup()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var env = Guid.NewGuid();
        var legacy = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        try
        {
            await CreateEnvironmentAsync(context, env, ct);
            await CreateEnvironmentAsync(context, legacy, ct);
            await new PostgresInventoryProjectionStore(context).ReplaceSnapshotAsync(new InventorySnapshot(env, 10, new Dictionary<string, ContainerInventory>
            {
                ["c1"] = new("c1", "web", "nginx:1.27", "running", "r", now) { ImageId = Id('a') },
            }), ct);
            await new PostgresInventoryProjectionStore(context).ReplaceSnapshotAsync(new InventorySnapshot(legacy, 10, new Dictionary<string, ContainerInventory>
            {
                ["c2"] = new("c2", "old", "redis:7", "running", "r", now),
            }), ct);
            var store = new PostgresImageInventoryStore(context);
            var digest = "sha256:" + new string('d', 64);

            Assert.Equal(ImageSnapshotOutcome.Accepted, await store.ReplaceSnapshotAsync(new ImageSnapshot(env, 5, now, [
                new ImageInventoryItem(Id('a'), ["nginx:1.27"], [$"nginx@{digest}"], 190_000_000, now.AddDays(-3), "amd64", "linux", false),
                new ImageInventoryItem(Id('b'), ["registry.empresa.com/pedidos/api:3.14.2"], [], 420_000_000, now.AddDays(-1), null, null, false),
                new ImageInventoryItem(Id('c'), [], [], 10_000, null, null, null, false),
            ]), ct));
            Assert.Equal(ImageSnapshotOutcome.Stale, await store.ReplaceSnapshotAsync(new ImageSnapshot(env, 4, now, []), ct));
            Assert.Equal(5UL, await store.GetRevisionAsync(env, ct));

            var all = await store.SearchAsync(new ImageSearchQuery([env], false, null, ImageSort.NameAsc, 0, 2), ct);
            Assert.Equal(["docker.io/library/nginx:1.27", "registry.empresa.com/pedidos/api:3.14.2"], all.Rows.Select(r => r.Image.RepoTags[0]));
            Assert.True(all.HasMore);
            Assert.Equal(1, all.Rows[0].Containers);
            Assert.Equal(new ImageSummary(3, 2, 610_010_000, false), all.Summary);

            var unused = await store.SearchAsync(new ImageSearchQuery([env], true, null, ImageSort.SizeDesc, 0, 10), ct);
            Assert.Equal([Id('b'), Id('c')], unused.Rows.Select(r => r.Image.ImageId));
            var text = await store.SearchAsync(new ImageSearchQuery([env], false, "pedidos", ImageSort.NameAsc, 0, 10), ct);
            Assert.Equal(Id('b'), Assert.Single(text.Rows).Image.ImageId);
            var wildcard = await store.SearchAsync(new ImageSearchQuery([env], false, "p_dido", ImageSort.NameAsc, 0, 10), ct);
            Assert.Empty(wildcard.Rows);

            // Host sem inventário de imagens ou com container sem image_id (agente N-1) torna o resumo parcial.
            Assert.True((await store.SummarizeAsync([env, legacy], ct)).Partial);

            Assert.True(ImageReference.TryParse("nginx:1.27", out var tag));
            var found = await store.LookupAsync(env, tag, ct);
            Assert.True(found.Present);
            Assert.Equal(Id('a'), found.ImageId);
            Assert.Equal(digest, found.Digest);
            Assert.True(ImageReference.TryParse($"docker.io/library/nginx@{digest}", out var pinned));
            Assert.True((await store.LookupAsync(env, pinned, ct)).Present);
            Assert.True(ImageReference.TryParse("nginx:1.28", out var missing));
            Assert.False((await store.LookupAsync(env, missing, ct)).Present);
        }
        finally
        {
            await CleanupAsync(context, [env, legacy], ct);
        }
    }

    [Fact]
    public async Task CommandStore_PersistsHostTargetedPullWithParametersAndOutput()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var env = Guid.NewGuid();
        try
        {
            await CreateEnvironmentAsync(context, env, ct);
            var store = new PostgresAgentCommandStore(context);
            var command = Pull(env, Guid.NewGuid(), "nginx:1.27");
            var now = DateTimeOffset.UtcNow;

            Assert.Equal(AgentCommandEnqueueResult.Created, await store.EnqueueAsync(new PersistedAgentCommand(command, ControlPlaneCommandState.Pending, now, now), ct));
            Assert.Equal(AgentCommandEnqueueResult.Duplicate, await store.EnqueueAsync(new PersistedAgentCommand(command, ControlPlaneCommandState.Pending, now, now), ct));
            var different = Pull(env, command.CommandId, "nginx:1.28") with { DeadlineUtc = command.DeadlineUtc };
            Assert.Equal(AgentCommandEnqueueResult.ConflictingPayload, await store.EnqueueAsync(new PersistedAgentCommand(different, ControlPlaneCommandState.Pending, now, now), ct));

            var claimed = Assert.Single(await store.ClaimDispatchableAsync(env, 7, now.AddSeconds(1), ct));
            Assert.Equal("docker.io/library/nginx:1.27", Assert.IsType<PullImageParameters>(claimed.Command.Parameters).Reference.Canonical);
            Assert.Equal(string.Empty, claimed.Command.ContainerId);

            var update = new AgentCommandStatusUpdate(env, command.CommandId, ControlPlaneCommandState.Succeeded, null, null, now.AddSeconds(2))
            {
                Output = new AgentCommandOutput("sha256:" + new string('e', 64), 1234),
            };
            Assert.Equal(AgentCommandStatusUpdateResult.Applied, await store.ApplyStatusAsync(update, ct));
            var status = await store.GetAsync(env, command.CommandId, ct);
            Assert.Equal(1234, status!.Output!.ImageSizeBytes);
            Assert.Equal("docker.io/library/nginx:1.27", status.ImageReference);
            Assert.Equal(string.Empty, status.ContainerId);

            // CHECK por tipo: comando de container sem alvo é recusado pelo banco.
            var invalid = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO dokpod.agent_commands (environment_id, command_id, kind, container_id, expected_container_revision,
                    payload_hash, deadline_utc, fencing_token, state, created_at_utc, updated_at_utc)
                VALUES ({env}, {Guid.NewGuid()}, 1, NULL, NULL, {new string('A', 64)}, {now.AddMinutes(1)}, 1, 0, {now}, {now})
                """, ct));
            Assert.Equal("23514", invalid.SqlState);
        }
        finally
        {
            await CleanupAsync(context, [env], ct);
        }
    }

    [Fact]
    public async Task Retention_RemovesOldTerminalCommandsAndParametersButKeepsNonTerminal()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var env = Guid.NewGuid();
        try
        {
            await CreateEnvironmentAsync(context, env, ct);
            var store = new PostgresAgentCommandStore(context);
            var old = DateTimeOffset.UtcNow.AddDays(-100);
            var terminal = Pull(env, Guid.NewGuid(), "nginx:1") with { DeadlineUtc = old.AddMinutes(5) };
            var pending = Pull(env, Guid.NewGuid(), "nginx:2") with { DeadlineUtc = old.AddMinutes(5) };
            var recent = Pull(env, Guid.NewGuid(), "nginx:3");
            await store.EnqueueAsync(new PersistedAgentCommand(terminal, ControlPlaneCommandState.Failed, old, old), ct);
            await store.EnqueueAsync(new PersistedAgentCommand(pending, ControlPlaneCommandState.Accepted, old, old), ct);
            await store.EnqueueAsync(new PersistedAgentCommand(recent, ControlPlaneCommandState.Succeeded, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow), ct);

            var purged = await store.PurgeTerminalAsync(DateTimeOffset.UtcNow.AddDays(-90), 1, ct);

            Assert.True(purged >= 1);
            Assert.Null(await store.GetAsync(env, terminal.CommandId, ct));
            Assert.NotNull(await store.GetAsync(env, pending.CommandId, ct));
            Assert.NotNull(await store.GetAsync(env, recent.CommandId, ct));
            Assert.Equal(2, await context.AgentCommandParameters.CountAsync(p => p.EnvironmentId == env, ct));
            var indeterminate = Pull(env, Guid.NewGuid(), "nginx:4") with { DeadlineUtc = old.AddMinutes(5) };
            await store.EnqueueAsync(new PersistedAgentCommand(indeterminate, ControlPlaneCommandState.Indeterminate, old, old), ct);
            await store.PurgeTerminalAsync(DateTimeOffset.UtcNow.AddDays(-90), 100, ct);
            Assert.NotNull(await store.GetAsync(env, indeterminate.CommandId, ct));
        }
        finally
        {
            await CleanupAsync(context, [env], ct);
        }
    }

    [Fact]
    public async Task Schema_HasMonthlyParameterPartitions()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var partition = $"dokpod.agent_command_parameters_{DateTime.UtcNow.AddMonths(1):yyyy_MM}";

        var found = await context.Database.SqlQuery<string>($"SELECT coalesce(to_regclass({partition})::text, '') AS \"Value\"").SingleAsync(ct);

        Assert.False(string.IsNullOrEmpty(found), $"partição {partition} ausente");
    }

    private static AgentCommand Pull(Guid env, Guid commandId, string reference)
    {
        Assert.True(ImageReference.TryParse(reference, out var parsed));
        var parameters = new PullImageParameters(parsed);
        return new AgentCommand(env, commandId, AgentCommandKind.PullImage, string.Empty, string.Empty,
            AgentCommandKinds.PayloadHash(AgentCommandKind.PullImage, string.Empty, string.Empty, parameters),
            DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeMilliseconds()), 1)
        {
            Parameters = parameters,
        };
    }

    private static Task CreateEnvironmentAsync(ControlPlaneDbContext context, Guid id, CancellationToken ct) =>
        new PostgresEnvironmentRegistrationStore(context).CreateAsync(
            EnvironmentRegistration.Create(id, $"img-{id:N}"[..20], "srv-img", true, [EnvironmentResourceScopes.Read, EnvironmentResourceScopes.PullImage]), ct);

    private static async Task CleanupAsync(ControlPlaneDbContext context, Guid[] ids, CancellationToken ct)
    {
        await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dokpod.agent_commands WHERE environment_id = ANY ({ids})", ct);
        await context.InventoryProjections.Where(item => ids.Contains(item.EnvironmentId)).ExecuteDeleteAsync(ct);
        await context.EnvironmentRegistrations.Where(item => ids.Contains(item.EnvironmentId)).ExecuteDeleteAsync(ct);
    }

    private static ControlPlaneDbContext CreateContextOrSkip()
    {
        var connectionString = Environment.GetEnvironmentVariable("DOKPOD_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) Assert.Skip("DOKPOD_TEST_POSTGRES_CONNECTION is not configured.");
        return new ControlPlaneDbContext(new DbContextOptionsBuilder<ControlPlaneDbContext>().UseNpgsql(connectionString).Options);
    }
}
