using Dokpod.ControlPlane.Application.Auditing;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.Domain.Auditing;
using Dokpod.Domain.Environments;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests;

/// <summary>
/// Spec 017: gravação com nome do ator, leitura filtrada e autorização da consulta em PostgreSQL real.
/// A trilha é append-only (gatilho rejeita DELETE): cada teste roda numa transação desfeita ao final.
/// </summary>
[Collection(AlertGovernancePostgresCollection.Name)]
public sealed class AuditConsoleIntegrationTests
{
    internal static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public async Task Writer_StoresTheActorNameOfTheRequestUserOnly()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        var writer = new PostgresAuditEventWriter(context, new AuditEventMetrics(), new Names("sub-ana", "ana"));
        var environment = Guid.NewGuid();
        var mine = Event(environment, "sub-ana", "environment.stage_changed", Now);
        var other = Event(environment, "sub-bruno", "environment.stage_changed", Now);
        var agent = AuditEvent.Create(Guid.NewGuid(), Now, Guid.NewGuid(), AuditActorKind.Agent, "sub-ana", "container.command.result", environment, AuditOutcome.Succeeded, null, Guid.NewGuid());

        await writer.AppendAsync(mine, ct);
        await writer.AppendAsync(mine, ct); // nova tentativa idempotente com o mesmo payload
        await writer.AppendAsync(other, ct);
        await writer.AppendAsync(agent, ct);

        var page = await new PostgresAuditEventReader(context).ListAsync(Query([environment]), ct);
        Assert.Equal(3, page.Events.Count);
        Assert.Equal("ana", Assert.Single(page.Events, e => e.EventId == mine.EventId).ActorName);
        Assert.Null(Assert.Single(page.Events, e => e.EventId == other.EventId).ActorName);
        Assert.Null(Assert.Single(page.Events, e => e.EventId == agent.EventId).ActorName);
    }

    [Fact]
    public async Task Reader_FiltersByHostActionOutcomeActorTextAndPeriodWithKeysetPaging()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        var writer = new PostgresAuditEventWriter(context, new AuditEventMetrics(), new Names("sub-ana", "ana"));
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        var correlation = Guid.NewGuid();
        var command = Guid.NewGuid();
        var stop = AuditEvent.Create(Guid.NewGuid(), Now.AddMinutes(-10), correlation, AuditActorKind.User, "sub-ana", "container.stop", a, AuditOutcome.Succeeded, null, command)
            .WithSubject("c0ffee00aa", new Dictionary<string, string> { ["justification"] = "Manutenção planejada" });
        var result = AuditEvent.Create(Guid.NewGuid(), Now.AddMinutes(-9), Guid.NewGuid(), AuditActorKind.Agent, "agent-1", "container.command.result", a, AuditOutcome.Succeeded, null, command);
        var denied = Event(a, "sub-bruno", "container.exec.denied", Now.AddMinutes(-8), AuditOutcome.Denied, "scope");
        var old = Event(a, "sub-ana", "environment.stage_changed", Now.AddDays(-3));
        var otherHost = Event(b, "sub-ana", "environment.stage_changed", Now.AddMinutes(-5));
        foreach (var e in new[] { stop, result, denied, old, otherHost }) await writer.AppendAsync(e, ct);
        for (var i = 0; i < 5; i++) await writer.AppendAsync(Event(a, "sub-carla", "container.inspected", Now.AddMinutes(-20 - i)), ct);
        var reader = new PostgresAuditEventReader(context);

        Assert.Equal(8, (await reader.ListAsync(Query([a]), ct)).Events.Count); // 24 h, sem o evento de 3 dias
        Assert.Equal(otherHost.EventId, Assert.Single((await reader.ListAsync(Query([b]), ct)).Events).EventId);
        Assert.Equal(denied.EventId, Assert.Single((await reader.ListAsync(Query([a]) with { Outcomes = [AuditOutcome.Denied] }, ct)).Events).EventId);
        Assert.Equal(denied.EventId, Assert.Single((await reader.ListAsync(Query([a]) with { Actions = AuditCategories.Actions["terminal"] }, ct)).Events).EventId);
        Assert.Equal(stop.EventId, Assert.Single((await reader.ListAsync(Query([a]) with { Actor = "an" }, ct)).Events).EventId);
        Assert.Equal(stop.EventId, Assert.Single((await reader.ListAsync(Query([a]) with { Text = "c0ffee" }, ct)).Events).EventId);
        Assert.Equal(stop.EventId, Assert.Single((await reader.ListAsync(Query([a]) with { Text = correlation.ToString() }, ct)).Events).EventId);
        Assert.Equal(2, (await reader.ListAsync(Query([a]) with { Text = command.ToString() }, ct)).Events.Count);
        Assert.Empty((await reader.ListAsync(Query([a]) with { Text = "c0ffee%" }, ct)).Events); // curinga escapado
        Assert.Equal(old.EventId, Assert.Single((await reader.ListAsync(Query([a]) with { SinceUtc = Now.AddDays(-4), UntilUtc = Now.AddDays(-2) }, ct)).Events).EventId);

        var first = await reader.ListAsync(Query([a]) with { Limit = 3 }, ct);
        var second = await reader.ListAsync(Query([a]) with { Limit = 3, After = first.Next }, ct);
        var third = await reader.ListAsync(Query([a]) with { Limit = 3, After = second.Next }, ct);
        Assert.Equal(8, first.Events.Concat(second.Events).Concat(third.Events).Select(e => e.EventId).Distinct().Count());
        Assert.Null(third.Next);
        var times = first.Events.Concat(second.Events).Concat(third.Events).Select(e => e.OccurredAtUtc).ToArray();
        Assert.Equal(times.OrderByDescending(t => t), times);

        var detail = (await reader.GetAsync(stop.EventId, [a], stop.OccurredAtUtc, Now.AddDays(-90), ct))!;
        Assert.Equal("Manutenção planejada", detail.Details!["justification"]);
        Assert.Equal("ana", detail.ActorName);
        Assert.Null(await reader.GetAsync(stop.EventId, [b], null, Now.AddDays(-90), ct));
        Assert.Equal([result.EventId], (await reader.RelatedAsync(detail, [a], TimeSpan.FromDays(7), 50, ct)).Select(e => e.EventId));
        Assert.Equal(5, await reader.CountAsync(Query([a]) with { Actions = AuditCategories.Actions["inspection"] }, 5, ct));
        Assert.InRange((await reader.OldestAsync([a], ct))!.Value - old.OccurredAtUtc, TimeSpan.FromMilliseconds(-1), TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task FullTextSearch_FindsJustificationActorActionAndFailureIgnoringAccents()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        var writer = new PostgresAuditEventWriter(context, new AuditEventMetrics(), new Names("sub-mariana", "Mariana Costa"));
        var host = Guid.NewGuid();
        var maintenance = AuditEvent.Create(Guid.NewGuid(), Now.AddMinutes(-5), Guid.NewGuid(), AuditActorKind.User, "sub-mariana", "container.stop", host, AuditOutcome.Succeeded, null, Guid.NewGuid())
            .WithSubject("aa11bb22cc33", new Dictionary<string, string> { ["justification"] = "Manutenção planejada do banco de pedidos" });
        var kernel = AuditEvent.Create(Guid.NewGuid(), Now.AddMinutes(-4), Guid.NewGuid(), AuditActorKind.User, "sub-joao", "container.pause", host, AuditOutcome.Succeeded, null, Guid.NewGuid())
            .WithSubject("dd44ee55ff66", new Dictionary<string, string> { ["justification"] = "Atualização do kernel" });
        var pull = AuditEvent.Create(Guid.NewGuid(), Now.AddMinutes(-3), Guid.NewGuid(), AuditActorKind.User, "sub-joao", "image.pull", host, AuditOutcome.Failed, "registry_unauthorized", Guid.NewGuid());
        foreach (var e in new[] { maintenance, kernel, pull }) await writer.AppendAsync(e, ct);
        var reader = new PostgresAuditEventReader(context);

        async Task<Guid[]> Search(string text) =>
            (await reader.ListAsync(Query([host]) with { Text = text }, ct)).Events.Select(e => e.EventId).ToArray();

        Assert.Equal([maintenance.EventId], await Search("manutencao"));            // sem acento encontra com acento
        Assert.Equal([maintenance.EventId], await Search("planejadas pedidos"));    // radical e "e" implícito
        Assert.Empty(await Search("manutenção -pedidos"));                          // exclusão
        Assert.Equal([kernel.EventId, maintenance.EventId], await Search("kernel or banco"));
        Assert.Equal([pull.EventId], await Search("registry unauthorized"));       // código de falha
        Assert.Equal([maintenance.EventId], await Search("mariana"));               // nome do ator
        Assert.Equal([kernel.EventId], await Search("pause"));                      // palavra da ação
        Assert.Equal([kernel.EventId], await Search("dd44ee"));                     // prefixo do alvo continua valendo

        // O plano usa o índice GIN da partição (sem varredura sequencial disponível).
        await context.Database.ExecuteSqlRawAsync("SET LOCAL enable_seqscan = off", ct);
        var plan = string.Join("\n", await context.Database.SqlQueryRaw<string>(
            """
            EXPLAIN SELECT event_id FROM dokpod.audit_events
            WHERE occurred_at_utc >= now() - interval '1 hour'
              AND dokpod.dokpod_audit_search_document(action, actor_id, actor_name, subject_id, failure_code, details) @@ dokpod.dokpod_audit_search_query('manutencao')
            """).ToListAsync(ct));
        Assert.Contains("search", plan, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Bitmap Index Scan", plan, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Partitions_CoverTheNextTwelveMonthsAndAllHaveTheSearchIndex()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var partitions = await context.Database.SqlQueryRaw<string>(
            """
            SELECT child.relname AS "Value" FROM pg_inherits i JOIN pg_class child ON child.oid = i.inhrelid
            WHERE i.inhparent = 'dokpod.audit_events'::regclass
            """).ToListAsync(ct);
        var month = new DateTime(Now.Year, Now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i <= 12; i++) Assert.Contains($"audit_events_{month.AddMonths(i):yyyy_MM}", partitions);
        Assert.Contains("audit_events_default", partitions);

        var withoutSearchIndex = await context.Database.SqlQueryRaw<string>(
            """
            SELECT child.relname AS "Value" FROM pg_inherits i JOIN pg_class child ON child.oid = i.inhrelid
            WHERE i.inhparent = 'dokpod.audit_events'::regclass
              AND NOT EXISTS (SELECT 1 FROM pg_indexes x WHERE x.schemaname = 'dokpod' AND x.tablename = child.relname
                              AND x.indexdef LIKE '%dokpod_audit_search_document%' AND x.indexdef LIKE '%USING gin%')
            """).ToListAsync(ct);
        Assert.Empty(withoutSearchIndex);
    }

    [Fact]
    public async Task Service_RestrictsToAuditReadHostsAndAuditsExports()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        var writer = new PostgresAuditEventWriter(context, new AuditEventMetrics(), new Names("sub-ana", "ana"));
        var (allowed, hidden) = (Guid.NewGuid(), Guid.NewGuid());
        var correlation = Guid.NewGuid();
        var visible = Event(allowed, "sub-ana", "environment.stage_changed", Now.AddMinutes(-2), correlation: correlation);
        var secret = Event(hidden, "sub-ana", "environment.stage_changed", Now.AddMinutes(-1), correlation: correlation);
        await writer.AppendAsync(visible, ct);
        await writer.AppendAsync(secret, ct);
        var decider = new Decider([allowed]);
        var service = new AuditQueryService(new PostgresAuditEventReader(context), decider, writer, TimeProvider.System);
        var caller = new AuditCaller(AuthenticatedActor.FromSubject("sub-ana") with { DisplayName = "ana" }, "token", Guid.NewGuid(),
        [
            EnvironmentRegistration.Create(allowed, "prod-1", "prod-1", true, EnvironmentResourceScopes.Supported, EnvironmentStage.Production),
            EnvironmentRegistration.Create(hidden, "prod-2", "prod-2", true, EnvironmentResourceScopes.Supported, EnvironmentStage.Production),
        ]);
        var all = new AuditFilter(null, null, null, [], null, [], [], [], null);

        Assert.Equal(1, (await service.SummarizeAsync(caller, ct)).ReadableHosts);
        Assert.Equal([visible.EventId], (await service.ListAsync(caller, all, null, 50, ct)).Page.Events.Select(e => e.EventId));
        Assert.Null(await service.GetAsync(caller, secret.EventId, null, ct)); // FR-010: indistinguível de inexistente
        Assert.Empty((await service.GetAsync(caller, visible.EventId, null, ct))!.Related); // o relacionado do outro host não aparece
        Assert.Equal(AuditQueryOutcome.PeriodTooLong, (await service.ListAsync(caller, all with { SinceUtc = Now.AddDays(-91), UntilUtc = Now }, null, 50, ct)).Outcome);
        Assert.Equal(AuditQueryOutcome.InvalidFilter, (await service.ListAsync(caller, all with { Categories = ["inexistente"] }, null, 50, ct)).Outcome);

        var export = await service.ExportAsync(caller, all, ct);
        Assert.Equal(AuditQueryOutcome.Ok, export.Outcome);
        Assert.Equal([visible.EventId], export.Events.Select(e => e.EventId));
        var exported = Assert.Single((await new PostgresAuditEventReader(context).ListAsync(Query([allowed, hidden]) with { Actions = ["audit.exported"] }, ct)).Events);
        Assert.Equal(allowed, exported.EnvironmentId);
        Assert.Equal("1", exported.Details!["count"]);
        Assert.Equal("ana", exported.ActorName);
    }

    [Fact]
    public async Task Export_RefusesMoreThanTenThousandEvents()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        var host = Guid.NewGuid();
        await BulkInsertAsync(context, [host], 10_001, TimeSpan.FromHours(20), ct);
        var service = new AuditQueryService(new PostgresAuditEventReader(context), new Decider([host]), new PostgresAuditEventWriter(context), TimeProvider.System);
        var caller = new AuditCaller(AuthenticatedActor.FromSubject("sub-ana"), "token", Guid.NewGuid(), [EnvironmentRegistration.Create(host, "h", "h", true, EnvironmentResourceScopes.Supported)]);

        var export = await service.ExportAsync(caller, new AuditFilter(null, null, null, [], null, [], [], [], null), ct);

        Assert.Equal(AuditQueryOutcome.ExportTooLarge, export.Outcome);
        Assert.Empty((await new PostgresAuditEventReader(context).ListAsync(Query([host]) with { Actions = ["audit.exported"] }, ct)).Events);
    }

    internal static async Task BulkInsertAsync(ControlPlaneDbContext context, Guid[] hosts, int count, TimeSpan spread, CancellationToken ct) =>
        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO dokpod.audit_events (event_id, occurred_at_utc, correlation_id, actor_kind, actor_id, action, environment_id, outcome,
                failure_code, subject_id, actor_name, details)
            SELECT gen_random_uuid(), now() - (random() * @spread * interval '1 second'), gen_random_uuid(), 0, 'user-' || (i % 40),
                (ARRAY['container.inspected','container.logs.read','environment.stage_changed','container.exec.started','alert.acknowledged'])[1 + i % 5],
                (@hosts::uuid[])[1 + i % array_length(@hosts::uuid[], 1)],
                CASE WHEN i % 50 = 0 THEN 2 ELSE 0 END, CASE WHEN i % 50 = 0 THEN 'scope' ELSE NULL END,
                'subject-' || (i % 5000), 'user-' || (i % 40),
                CASE WHEN i % 100 = 0 THEN jsonb_build_object('justification', 'Manutenção planejada do lote ' || (i % 977)) END
            FROM generate_series(1, @count) AS i
            """,
            [new NpgsqlParameter("spread", spread.TotalSeconds), new NpgsqlParameter("hosts", hosts), new NpgsqlParameter("count", count)],
            ct);

    private static AuditEvent Event(Guid environment, string actor, string action, DateTimeOffset at, AuditOutcome outcome = AuditOutcome.Succeeded, string? failure = null, Guid? correlation = null) =>
        AuditEvent.Create(Guid.NewGuid(), at, correlation ?? Guid.NewGuid(), AuditActorKind.User, actor, action, environment, outcome, failure);

    internal static AuditQuery Query(Guid[] environments) =>
        new(environments, Now.AddHours(-24), Now.AddMinutes(1), null, [], [], null, null, 50);

    internal static ControlPlaneDbContext CreateContextOrSkip()
    {
        var connectionString = Environment.GetEnvironmentVariable("DOKPOD_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) Assert.Skip("DOKPOD_TEST_POSTGRES_CONNECTION is not configured.");
        return new ControlPlaneDbContext(new DbContextOptionsBuilder<ControlPlaneDbContext>().UseNpgsql(connectionString).Options);
    }

    private sealed class Names(string subject, string name) : IAuditActorNameProvider
    {
        public string? NameFor(string actorId) => actorId == subject ? name : null;
    }

    private sealed class Decider(IReadOnlyCollection<Guid> allowed) : IEnvironmentAuthorizationDecider
    {
        public Task<EnvironmentAuthorizationDecision> DecideAsync(string resource, string scope, AuthenticatedActor actor, string accessToken, Guid correlationId, CancellationToken cancellationToken)
        {
            var id = Guid.Parse(resource["urn:dokpod:environment:".Length..]);
            var ok = scope == EnvironmentResourceScopes.AuditRead && allowed.Contains(id);
            return Task.FromResult(new EnvironmentAuthorizationDecision(ok ? AuthorizationDecisionOutcome.Allowed : AuthorizationDecisionOutcome.Denied, ok ? null : "access_denied"));
        }
    }
}
