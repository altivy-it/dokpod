using Dokpod.Domain.Alerts;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Events;
using Xunit;

namespace Dokpod.Domain.Tests.Alerts;

public sealed class AlertModelsTests
{
    private static readonly Guid Host = Guid.Parse("00000000-0000-4000-8000-000000000016");

    [Theory]
    [InlineData("api-*", "api-pedidos", true)]
    [InlineData("api-*", "web-api", false)]
    [InlineData("*-db", "pedidos-db", true)]
    [InlineData("*cache*", "redis-cache-01", true)]
    [InlineData("worker", "worker", true)]
    [InlineData("worker", "worker-2", false)]
    [InlineData("API-*", "api-pedidos", true)]
    public void Pattern_MatchesWithWildcardIgnoringCase(string pattern, string name, bool expected) =>
        Assert.Equal(expected, AlertScope.MatchesPattern(pattern, name));

    [Fact]
    public void Scope_CoversStageHostAndContainer()
    {
        var scope = new AlertScope(EnvironmentStage.Production, Host, "api-*");

        Assert.True(scope.Covers(EnvironmentStage.Production, Host, "api-1"));
        Assert.False(scope.Covers(EnvironmentStage.Staging, Host, "api-1"));
        Assert.False(scope.Covers(EnvironmentStage.Production, Guid.NewGuid(), "api-1"));
        Assert.False(scope.Covers(EnvironmentStage.Production, Host, null));
        Assert.True(AlertScope.Everything.Covers(null, Host, null));
    }

    [Fact]
    public void Rule_ValidatesEventAndMetricConditions()
    {
        AlertRule Rule(AlertConditionKind kind, string? eventType, AlertMetric? metric, double? threshold, int duration = 0) =>
            new(Guid.NewGuid(), "Regra", null, kind, eventType, metric, threshold, duration, AlertSeverity.High, AlertScope.Everything, true, false, 1);

        Assert.Empty(Rule(AlertConditionKind.Event, OperationalEventTypes.ContainerOomKilled, null, null).Validate());
        Assert.Empty(Rule(AlertConditionKind.Metric, null, AlertMetric.HostMemory, 80, 300).Validate());
        Assert.Contains("eventType", Rule(AlertConditionKind.Event, "tipo.inexistente", null, null).Validate());
        Assert.Contains("thresholdPercent", Rule(AlertConditionKind.Metric, null, AlertMetric.HostCpu, 0).Validate());
        Assert.Contains("durationSeconds", Rule(AlertConditionKind.Metric, null, AlertMetric.HostCpu, 80, 7_200).Validate());
        Assert.Contains("name", (Rule(AlertConditionKind.Event, OperationalEventTypes.ContainerDied, null, null) with { Name = " " }).Validate());
    }

    [Fact]
    public void Rule_TargetsHostOrContainer()
    {
        AlertRule Event(string type) => new(Guid.NewGuid(), "r", null, AlertConditionKind.Event, type, null, null, 0, AlertSeverity.Low, AlertScope.Everything, true, false, 1);

        Assert.True(Event(OperationalEventTypes.ContainerDied).IsContainerTarget);
        Assert.False(Event(OperationalEventTypes.AgentDisconnected).IsContainerTarget);
        Assert.Contains(143, AlertRule.StopExitCodes);
    }

    [Fact]
    public void Silence_AcceptsUpToSevenDaysAndCoversItsScope()
    {
        var start = new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

        Assert.Null(AlertSilence.ValidateWindow(start, start.AddDays(7)));
        Assert.Equal("endsAt", AlertSilence.ValidateWindow(start, start.AddDays(7).AddMinutes(1)));
        Assert.Equal("endsAt", AlertSilence.ValidateWindow(start, start));

        var silence = new AlertSilence(Guid.NewGuid(), new AlertScope(null, Host, null), null, start, start.AddHours(1), null, "Manutenção do host", "u");
        Assert.True(silence.IsActive(start.AddMinutes(30)));
        Assert.False(silence.IsActive(start.AddHours(1)));
        Assert.False((silence with { EndedAtUtc = start.AddMinutes(10) }).IsActive(start.AddMinutes(30)));
        Assert.True(silence.Covers(Guid.NewGuid(), EnvironmentStage.Production, Host, "qualquer"));
        var single = silence with { AlertId = Guid.NewGuid() };
        Assert.False(single.Covers(Guid.NewGuid(), null, Host, null));
    }

    [Theory]
    [InlineData("curta", false)]
    [InlineData("Manutenção programada da janela 42", true)]
    [InlineData("          ", false)]
    public void Justification_RequiresTenToFiveHundredCharacters(string text, bool valid) =>
        Assert.Equal(valid, ActionJustifications.IsValidLength(text));

    [Fact]
    public void Values_RoundTrip()
    {
        foreach (var severity in Enum.GetValues<AlertSeverity>())
        {
            Assert.True(AlertValues.TryParseSeverity(AlertValues.Severity(severity), out var parsed));
            Assert.Equal(severity, parsed);
        }

        foreach (var action in Enum.GetValues<JustifiedAction>())
        {
            Assert.True(ActionJustifications.TryParse(ActionJustifications.Value(action), out var parsed));
            Assert.Equal(action, parsed);
        }

        Assert.False(AlertValues.TryParseState("aberto", out _));
    }
}
