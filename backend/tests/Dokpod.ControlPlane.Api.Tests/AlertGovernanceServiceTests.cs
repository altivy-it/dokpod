using Dokpod.ControlPlane.Application.Alerts;
using Dokpod.ControlPlane.Application.Auditing;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Realtime;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.Domain.Alerts;
using Dokpod.Domain.Auditing;
using Dokpod.Domain.Environments;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests;

/// <summary>Spec 016 T011/T012/T015/T005: autorização e auditoria da governança de alertas, com stores em PostgreSQL real.</summary>
[Collection(AlertGovernancePostgresCollection.Name)]
public sealed class AlertGovernanceServiceTests
{
    private static readonly Guid DiedRule = Guid.Parse("01600000-0000-4000-8000-000000000001");
    private static readonly AuthenticatedActor Operator = AuthenticatedActor.FromSubject("sub-operador") with { DisplayName = "ana" };

    [Fact]
    public async Task Alerts_AreFilteredByAlertReadAndActionsNeedAlertManage()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var (readable, hidden) = (Guid.NewGuid(), Guid.NewGuid());
        var decider = new Decider { [(readable, EnvironmentResourceScopes.ReadAlerts)] = true };
        var audit = new Audit();
        var (service, store) = Create(context, decider, audit);
        var caller = Caller(readable, hidden);
        try
        {
            var (mine, _) = await store.OpenOrOccurAsync(Occurrence(store, readable, "api", ct), ct);
            var (other, _) = await store.OpenOrOccurAsync(Occurrence(store, hidden, "db", ct), ct);

            var (page, manageable) = await service.ListAsync(caller, null, new AlertQuery([], false, [], [], null, null, null, null, null, 50), ct);
            Assert.Equal([mine.AlertId], page.Alerts.Select(a => a.AlertId));
            Assert.Empty(manageable);
            Assert.Null(await service.GetAsync(caller, other.AlertId, ct)); // FR-023: indistinguível de inexistente
            Assert.Equal(GovernanceOutcome.NotFound, (await service.AcknowledgeAsync(caller, other.AlertId, "Investigando o banco", ct)).Outcome);
            Assert.Equal(GovernanceOutcome.Forbidden, (await service.AcknowledgeAsync(caller, mine.AlertId, "Investigando a API", ct)).Outcome);

            decider[(readable, EnvironmentResourceScopes.ManageAlerts)] = true;
            Assert.Equal(GovernanceOutcome.Invalid, (await service.AcknowledgeAsync(caller, mine.AlertId, "curto", ct)).Outcome);
            Assert.Equal(GovernanceOutcome.Ok, (await service.AcknowledgeAsync(caller, mine.AlertId, "Investigando " + "pass" + "word=segredo123", ct)).Outcome);

            var detail = (await service.GetAsync(caller, mine.AlertId, ct))!;
            Assert.True(detail.CanManage);
            Assert.Equal(AlertState.Acknowledged, detail.Alert.State);
            Assert.Equal("ana", detail.Alert.Assignee);
            var entry = Assert.Single(detail.Timeline, t => t.Kind == AlertTimelineKinds.Acknowledged);
            Assert.Equal("ana", entry.Actor);
            Assert.DoesNotContain("segredo123", entry.Comment);
            var audited = Assert.Single(audit.Events);
            Assert.Equal("alert.acknowledged", audited.Action);
            Assert.Equal("sub-operador", audited.ActorId);
            Assert.Equal(readable, audited.EnvironmentId);

            Assert.Equal(GovernanceOutcome.Ok, (await service.ResolveAsync(caller, mine.AlertId, "Corrigido no deploy 42", ct)).Outcome);
            Assert.Equal(GovernanceOutcome.Conflict, (await service.ResolveAsync(caller, mine.AlertId, "Corrigido no deploy 42", ct)).Outcome);
        }
        finally
        {
            await Cleanup(context, [readable, hidden], ct);
        }
    }

    [Fact]
    public async Task Rules_NeedAlertManageOnEveryCoveredHostAndBuiltInsKeepTheirCondition()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        var decider = new Decider
        {
            [(a, EnvironmentResourceScopes.ReadAlerts)] = true,
            [(b, EnvironmentResourceScopes.ReadAlerts)] = true,
            [(a, EnvironmentResourceScopes.ManageAlerts)] = true,
        };
        var audit = new Audit();
        var (service, store) = Create(context, decider, audit);
        var caller = Caller(a, b);
        var ruleId = Guid.NewGuid();
        var rule = new AlertRule(ruleId, "CPU dos containers de API", null, AlertConditionKind.Metric, null, AlertMetric.ContainerCpu, 90, 300, AlertSeverity.Medium, new AlertScope(null, null, "api-*"), true, false, 0);
        var builtIn = (await store.GetRuleAsync(DiedRule, ct))!;
        try
        {
            // Regra global abrange os dois hosts: falta alert:manage em b.
            Assert.Equal(GovernanceOutcome.Forbidden, (await service.SaveRuleAsync(caller, rule, null, ct)).Outcome);
            Assert.Equal(GovernanceOutcome.Ok, (await service.SaveRuleAsync(caller, rule with { Scope = new AlertScope(null, a, "api-*") }, null, ct)).Outcome);
            Assert.Equal("alert.rule_created", Assert.Single(audit.Events).Action);
            Assert.Equal(GovernanceOutcome.Conflict, (await service.SaveRuleAsync(caller, rule with { Scope = new AlertScope(null, a, null) }, 7, ct)).Outcome);
            Assert.Equal(GovernanceOutcome.Invalid, (await service.SaveRuleAsync(caller, rule with { ThresholdPercent = 120 }, null, ct)).Outcome);

            decider[(b, EnvironmentResourceScopes.ManageAlerts)] = true;
            var tampered = builtIn with { EventType = "command.failed", Severity = AlertSeverity.Critical, BuiltIn = false };
            Assert.Equal(GovernanceOutcome.Ok, (await service.SaveRuleAsync(caller, tampered, builtIn.Version, ct)).Outcome);
            var saved = (await store.GetRuleAsync(DiedRule, ct))!;
            Assert.Equal("container.died", saved.EventType);
            Assert.True(saved.BuiltIn);
            Assert.Equal(AlertSeverity.Critical, saved.Severity);
            Assert.Equal(GovernanceOutcome.Conflict, (await service.DeleteRuleAsync(caller, DiedRule, ct)).Outcome);
            Assert.Equal(GovernanceOutcome.Ok, (await service.DeleteRuleAsync(caller, ruleId, ct)).Outcome);
            Assert.Null(await store.GetRuleAsync(ruleId, ct));
        }
        finally
        {
            var current = await store.GetRuleAsync(DiedRule, ct);
            if (current is not null && current.Severity != builtIn.Severity)
            {
                await store.SaveRuleAsync(builtIn, "teste", current.Version, DateTimeOffset.UtcNow, ct);
            }

            await context.Database.ExecuteSqlRawAsync("DELETE FROM dokpod.alert_rules WHERE rule_id = @id", [new NpgsqlParameter("id", ruleId)], ct);
        }
    }

    [Fact]
    public async Task Silences_AreLimitedToSevenDaysAndPolicyNeedsEnvironmentManageOnTheStage()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var (prod1, prod2) = (Guid.NewGuid(), Guid.NewGuid());
        var decider = new Decider
        {
            [(prod1, EnvironmentResourceScopes.ReadAlerts)] = true,
            [(prod1, EnvironmentResourceScopes.ManageAlerts)] = true,
            [(prod1, EnvironmentResourceScopes.Manage)] = true,
        };
        var audit = new Audit();
        var (service, store) = Create(context, decider, audit);
        var caller = new AlertCaller(Operator, "token", Guid.NewGuid(),
        [
            EnvironmentRegistration.Create(prod1, "prod-1", "prod-1", true, EnvironmentResourceScopes.Supported, EnvironmentStage.Production),
            EnvironmentRegistration.Create(prod2, "prod-2", "prod-2", true, EnvironmentResourceScopes.Supported, EnvironmentStage.Production),
        ]);
        var now = DateTimeOffset.UtcNow;
        Guid? silenceId = null;
        try
        {
            var scope = new AlertScope(null, prod1, null);
            Assert.Equal(GovernanceOutcome.Invalid, (await service.CreateSilenceAsync(caller, scope, now, now.AddDays(8), "Janela longa demais", ct)).Outcome);
            Assert.Equal(GovernanceOutcome.Invalid, (await service.CreateSilenceAsync(caller, scope, now, now.AddHours(1), "curta", ct)).Outcome);
            Assert.Equal(GovernanceOutcome.Forbidden, (await service.CreateSilenceAsync(caller, AlertScope.Everything, now, now.AddHours(1), "Manutenção geral", ct)).Outcome);
            var created = await service.CreateSilenceAsync(caller, scope, now, now.AddHours(1), "Manutenção do host prod-1", ct);
            Assert.Equal(GovernanceOutcome.Ok, created.Outcome);
            silenceId = created.Id;
            var (silences, canManage) = await service.ListSilencesAsync(caller, true, ct);
            Assert.Contains(silences, s => s.SilenceId == silenceId);
            Assert.True(canManage);
            Assert.Equal(GovernanceOutcome.Ok, (await service.EndSilenceAsync(caller, silenceId!.Value, ct)).Outcome);
            Assert.Equal(GovernanceOutcome.Conflict, (await service.EndSilenceAsync(caller, silenceId.Value, ct)).Outcome);

            // D-007: environment:manage em todos os hosts habilitados do estágio.
            Assert.Equal(GovernanceOutcome.Forbidden, (await service.SetPolicyAsync(caller, EnvironmentStage.Production, false, ct)).Outcome);
            Assert.True((await new PostgresActionJustificationStore(context).GetPoliciesAsync(ct))[EnvironmentStage.Production]);
            Assert.DoesNotContain(audit.Events, e => e.Action == "container.justification_policy_changed");

            // Spec 022 D-005: canEdit segue a mesma regra; com só o host administrado, edita e o autor fica registrado.
            Assert.All(await service.ListPoliciesAsync(caller, ct), p => Assert.False(p.CanEdit));
            var onlyManaged = caller with { Readable = caller.Readable.Where(r => r.EnvironmentId == prod1).ToArray() };
            var listed = await service.ListPoliciesAsync(onlyManaged, ct);
            Assert.True(listed.Single(p => p.Policy.Stage == EnvironmentStage.Production).CanEdit);
            Assert.Equal(GovernanceOutcome.Ok, (await service.SetPolicyAsync(onlyManaged, EnvironmentStage.Production, true, ct)).Outcome);
            var production = (await service.ListPoliciesAsync(onlyManaged, ct)).Single(p => p.Policy.Stage == EnvironmentStage.Production).Policy;
            Assert.True(production.Required);
            Assert.Equal(Operator.Label, production.UpdatedBy);
            Assert.NotNull(production.UpdatedAt);
        }
        finally
        {
            if (silenceId is { } id) await context.Database.ExecuteSqlRawAsync("DELETE FROM dokpod.alert_silences WHERE silence_id = @id", [new NpgsqlParameter("id", id)], ct);
        }
    }

    private static AlertCaller Caller(params Guid[] environments) =>
        new(Operator, "token", Guid.NewGuid(), environments.Select((id, i) => EnvironmentRegistration.Create(id, $"host-{i}", $"host-{i}", true, EnvironmentResourceScopes.Supported, EnvironmentStage.Development)).ToArray());

    private static AlertOccurrence Occurrence(PostgresAlertStore store, Guid environment, string container, CancellationToken ct) =>
        new(store.GetRuleAsync(DiedRule, ct).GetAwaiter().GetResult()!, environment, container, container, $"Container {container} encerrado com erro (código 1)", DateTimeOffset.UtcNow, null);

    private static (AlertGovernanceService Service, PostgresAlertStore Store) Create(ControlPlaneDbContext context, Decider decider, Audit audit)
    {
        var store = new PostgresAlertStore(context);
        return (new AlertGovernanceService(store, new PostgresActionJustificationStore(context), decider, decider, audit, new NullControlPlaneNotifier(), TimeProvider.System), store);
    }

    private static async Task Cleanup(ControlPlaneDbContext context, Guid[] environments, CancellationToken ct) =>
        await context.Database.ExecuteSqlRawAsync("DELETE FROM dokpod.alerts WHERE environment_id = ANY(@ids)", [new NpgsqlParameter("ids", environments)], ct);

    private static ControlPlaneDbContext CreateContextOrSkip()
    {
        var connectionString = Environment.GetEnvironmentVariable("DOKPOD_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) Assert.Skip("DOKPOD_TEST_POSTGRES_CONNECTION is not configured.");
        return new ControlPlaneDbContext(new DbContextOptionsBuilder<ControlPlaneDbContext>().UseNpgsql(connectionString).Options);
    }

    private sealed class Decider : Dictionary<(Guid, string), bool>, IEnvironmentAuthorizationDecider, IEnvironmentGrantReader
    {
        public Task<IReadOnlyDictionary<Guid, IReadOnlySet<string>>?> ReadAsync(AuthenticatedActor actor, string accessToken, Guid correlationId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlySet<string>>?>(this.Where(p => p.Value).GroupBy(p => p.Key.Item1)
                .ToDictionary(g => g.Key, g => (IReadOnlySet<string>)g.Select(p => p.Key.Item2).ToHashSet(StringComparer.Ordinal)));

        public Task<EnvironmentAuthorizationDecision> DecideAsync(string resource, string scope, AuthenticatedActor actor, string accessToken, Guid correlationId, CancellationToken cancellationToken)
        {
            var id = Guid.Parse(resource["urn:dokpod:environment:".Length..]);
            var allowed = TryGetValue((id, scope), out var value) && value;
            return Task.FromResult(new EnvironmentAuthorizationDecision(allowed ? AuthorizationDecisionOutcome.Allowed : AuthorizationDecisionOutcome.Denied, allowed ? null : "access_denied"));
        }
    }

    private sealed class Audit : IAuditEventWriter
    {
        public List<AuditEvent> Events { get; } = [];

        public Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }
}
