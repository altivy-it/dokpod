using Dokpod.ControlPlane.Application.Alerts;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.Domain.Alerts;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests;

/// <summary>Spec 016 T002: stores de alertas e justificativas em PostgreSQL real.</summary>
[Collection(AlertGovernancePostgresCollection.Name)]
public sealed class AlertGovernanceIntegrationTests
{
    private static readonly Guid DiedRule = Guid.Parse("01600000-0000-4000-8000-000000000001");

    [Fact]
    public async Task Migration_SeedsDefaultRulesAndProductionPolicy()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;

        var rules = await new PostgresAlertStore(context).ListRulesAsync(ct);
        var policies = await new PostgresActionJustificationStore(context).GetPoliciesAsync(ct);

        Assert.True(rules.Count(r => r.BuiltIn) >= 9);
        var restarts = Assert.Single(rules, r => r.RuleId == Guid.Parse("01600000-0000-4000-8000-000000000004"));
        Assert.Equal(3, restarts.MinOccurrences);
        Assert.Equal(600, restarts.DurationSeconds);
        var memory = Assert.Single(rules, r => r.RuleId == Guid.Parse("01600000-0000-4000-8000-000000000007"));
        Assert.Equal(AlertMetric.HostMemory, memory.Metric);
        Assert.Equal(80, memory.ThresholdPercent);
        Assert.True(policies[EnvironmentStage.Production]);
        Assert.False(policies[EnvironmentStage.Staging]);
    }

    [Fact]
    public async Task OpenOrOccur_KeepsOneActiveAlertPerRuleAndTargetAndReopensAfterResolution()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var store = new PostgresAlertStore(context);
        var environment = Guid.NewGuid();
        var rule = (await store.GetRuleAsync(DiedRule, ct))!;
        var at = DateTimeOffset.UtcNow;
        try
        {
            AlertOccurrence Occurrence(int minutes) => new(rule, environment, new string('a', 64), "api", "Container encerrado com erro (código 1)", at.AddMinutes(minutes), null);
            var (first, opened) = await store.OpenOrOccurAsync(Occurrence(0), ct);
            var (again, openedAgain) = await store.OpenOrOccurAsync(Occurrence(1), ct);
            Assert.True(opened);
            Assert.False(openedAgain);
            Assert.Equal(first.AlertId, again.AlertId);
            Assert.Equal(2, again.Occurrences);

            // SC-002: 100 repetições continuam num único alerta.
            for (var i = 2; i < 100; i++) await store.OpenOrOccurAsync(Occurrence(i), ct);
            var active = (await store.GetAsync(first.AlertId, ct))!;
            Assert.Equal(100, active.Occurrences);

            Assert.True(await store.TransitionAsync(first.AlertId, AlertState.Acknowledged, "operador", null, null, at.AddMinutes(100), AlertTimelineKinds.Acknowledged, "operador", "Investigando o incidente", ct));
            Assert.True(await store.TransitionAsync(first.AlertId, AlertState.Resolved, null, null, AlertResolutionReasons.ConditionCleared, at.AddMinutes(200), AlertTimelineKinds.Resolved, null, null, ct));
            Assert.False(await store.TransitionAsync(first.AlertId, AlertState.Open, null, null, null, at, AlertTimelineKinds.Opened, null, null, ct));

            var (reopened, isNew) = await store.OpenOrOccurAsync(Occurrence(300), ct);
            Assert.True(isNew);
            Assert.NotEqual(first.AlertId, reopened.AlertId);

            var timeline = await store.GetTimelineAsync(first.AlertId, ct);
            Assert.Equal([AlertTimelineKinds.Opened, AlertTimelineKinds.Occurred], timeline.Take(2).Select(t => t.Kind));
            Assert.Contains(timeline, t => t.Kind == AlertTimelineKinds.Acknowledged && t.Actor == "operador");
            Assert.Equal(AlertTimelineKinds.Resolved, timeline[^1].Kind);

            var open = await store.ListAsync(new AlertQuery([environment], false, [], [], null, null, null, null, null, 10), ct);
            Assert.Equal(reopened.AlertId, Assert.Single(open.Alerts).AlertId);
            var history = await store.ListAsync(new AlertQuery([environment], true, [AlertSeverity.High], [], "api", null, null, null, null, 10), ct);
            Assert.Equal(first.AlertId, Assert.Single(history.Alerts).AlertId);
            Assert.Equal("Container encerrado com erro", history.Alerts[0].RuleName);
        }
        finally
        {
            await Cleanup(context, environment, ct);
        }
    }

    [Fact]
    public async Task Silences_AreListedWhileActiveAndCanBeEndedEarly()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var store = new PostgresAlertStore(context);
        var environment = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var silence = new AlertSilence(Guid.NewGuid(), new AlertScope(EnvironmentStage.Production, environment, "api-*"), null, now.AddMinutes(-1), now.AddHours(1), null, "Manutenção do host", "admin");
        try
        {
            await store.CreateSilenceAsync(silence, ct);
            Assert.Contains(await store.ListSilencesAsync(true, now, ct), s => s.SilenceId == silence.SilenceId && s.Scope.ContainerPattern == "api-*");
            Assert.True(await store.EndSilenceAsync(silence.SilenceId, now, ct));
            Assert.False(await store.EndSilenceAsync(silence.SilenceId, now, ct));
            Assert.DoesNotContain(await store.ListSilencesAsync(true, now.AddSeconds(1), ct), s => s.SilenceId == silence.SilenceId);
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync("DELETE FROM dokpod.alert_silences WHERE silence_id = @id", [new NpgsqlParameter("id", silence.SilenceId)], ct);
        }
    }

    [Fact]
    public async Task Justifications_AreRecordedOncePerCommandAndExposeTheCurrentOne()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var store = new PostgresActionJustificationStore(context);
        var environment = Guid.NewGuid();
        var container = new string('b', 64);
        var command = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        try
        {
            var stop = new ContainerActionJustification(Guid.NewGuid(), command, environment, container, "api", JustifiedAction.Stop, "operador", "Janela de manutenção 42", now, null);
            await store.RecordAsync(stop, ct);
            await store.RecordAsync(stop with { JustificationId = Guid.NewGuid(), Reason = "Outra tentativa" }, ct);

            var history = await store.ListAsync(environment, container, null, null, null, 10, ct);
            Assert.Equal("Janela de manutenção 42", Assert.Single(history).Reason);
            var current = await store.GetCurrentAsync([(environment, container), (environment, new string('c', 64))], ct);
            Assert.Equal(JustifiedAction.Stop, Assert.Single(current).Value.Action);
            Assert.True(await store.HasRecentAsync(environment, container, now.AddMinutes(-5), ct));

            await store.EndAsync(stop.JustificationId, now.AddMinutes(1), ct);
            Assert.Empty(await store.GetCurrentAsync([(environment, container)], ct));
            Assert.True(await store.HasRecentAsync(environment, container, now.AddMinutes(-5), ct));
            Assert.False(await store.HasRecentAsync(environment, container, now.AddMinutes(5), ct));

            var delete = stop with { JustificationId = Guid.NewGuid(), CommandId = Guid.NewGuid(), Action = JustifiedAction.Delete, CreatedAtUtc = now.AddMinutes(2) };
            await store.RecordAsync(delete, ct);
            Assert.Empty(await store.GetCurrentAsync([(environment, container)], ct));
            Assert.Equal(2, (await store.ListAsync(environment, null, now.AddMinutes(-1), null, null, 10, ct)).Count);
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync("DELETE FROM dokpod.container_action_justifications WHERE environment_id = @id", [new NpgsqlParameter("id", environment)], ct);
        }
    }

    [Fact]
    public async Task CountEvents_CountsByTypeSubjectAndWindow()
    {
        await using var context = CreateContextOrSkip();
        var ct = TestContext.Current.CancellationToken;
        var environment = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var registrations = new PostgresEnvironmentRegistrationStore(context);
        try
        {
            await registrations.CreateAsync(EnvironmentRegistration.Create(environment, $"e-{environment:N}"[..18], "srv", true, [EnvironmentResourceScopes.Read]), ct);
            var events = new PostgresOperationalEventStore(context);
            OperationalEvent Restart(int minutesAgo) => OperationalEvent.Create(environment, OperationalEventTypes.ContainerAutoRestarted, now.AddMinutes(-minutesAgo), now, "container", "c1", "api", null);
            await events.AppendAsync([Restart(1), Restart(5), Restart(30)], ct);

            Assert.Equal(2, await new PostgresAlertStore(context).CountEventsAsync(environment, OperationalEventTypes.ContainerAutoRestarted, "c1", now.AddMinutes(-10), ct));
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync("DELETE FROM dokpod.environment_events WHERE environment_id = @id", [new NpgsqlParameter("id", environment)], ct);
            await context.EnvironmentRegistrations.Where(r => r.EnvironmentId == environment).ExecuteDeleteAsync(ct);
        }
    }

    private static async Task Cleanup(ControlPlaneDbContext context, Guid environment, CancellationToken ct) =>
        await context.Database.ExecuteSqlRawAsync("DELETE FROM dokpod.alerts WHERE environment_id = @id", [new NpgsqlParameter("id", environment)], ct);

    private static ControlPlaneDbContext CreateContextOrSkip()
    {
        var connectionString = Environment.GetEnvironmentVariable("DOKPOD_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) Assert.Skip("DOKPOD_TEST_POSTGRES_CONNECTION is not configured.");
        return new ControlPlaneDbContext(new DbContextOptionsBuilder<ControlPlaneDbContext>().UseNpgsql(connectionString).Options);
    }
}

[CollectionDefinition(Name)]
public sealed class AlertGovernancePostgresCollection
{
    public const string Name = "Alert governance PostgreSQL";
}
