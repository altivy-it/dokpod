using Xunit;
using Dokpod.Domain.Events;
using Dokpod.Domain.Hosts;
using Dokpod.Domain.Inventory;

namespace Dokpod.Domain.Tests.Hosts;

public sealed class ConsoleDomainTests
{
    [Theory]
    [InlineData(false, true, false, "27.3.1", "27.0.0", HostSituation.Disabled)]
    [InlineData(true, false, true, "27.3.1", "27.0.0", HostSituation.Disconnected)]
    [InlineData(true, true, true, "26.1.4", "27.0.0", HostSituation.Stale)]
    [InlineData(true, true, false, "26.1.4", "27.0.0", HostSituation.UpdateRequired)]
    [InlineData(true, true, false, null, "27.0.0", HostSituation.Unknown)]
    [InlineData(true, true, false, "garbage", "27.0.0", HostSituation.Unknown)]
    [InlineData(true, true, false, "27.3.1", "27.0.0", HostSituation.UpToDate)]
    [InlineData(true, true, false, "5.2.3-dev", "5.0.0", HostSituation.UpToDate)]
    public void HostSituation_FollowsEvaluationOrder(bool enabled, bool connected, bool diverging, string? version, string minimum, HostSituation expected)
    {
        Assert.Equal(expected, HostSituations.Evaluate(enabled, connected, diverging, version, minimum));
    }

    [Theory]
    [InlineData("27.3.1", "27.3.0", 1)]
    [InlineData("26.1", "26.1.0", 0)]
    [InlineData("v5.2.3", "5.10.0", -1)]
    public void EngineVersion_ComparesTolerantly(string left, string right, int sign)
    {
        Assert.True(EngineVersion.TryParse(left, out var a));
        Assert.True(EngineVersion.TryParse(right, out var b));
        Assert.Equal(sign, Math.Sign(a.CompareTo(b)));
    }

    [Theory]
    [InlineData("Running", null, null, null, ContainerDisplayStatus.Running)]
    [InlineData("running", ContainerHealth.Healthy, null, null, ContainerDisplayStatus.Running)]
    [InlineData("Running", ContainerHealth.Starting, null, null, ContainerDisplayStatus.Starting)]
    [InlineData("Running", ContainerHealth.Unhealthy, null, null, ContainerDisplayStatus.Failed)]
    [InlineData("Dead", null, null, null, ContainerDisplayStatus.Failed)]
    [InlineData("Exited", null, 137, null, ContainerDisplayStatus.Failed)]
    [InlineData("Exited", null, 0, true, ContainerDisplayStatus.Failed)]
    [InlineData("Exited", null, 0, false, ContainerDisplayStatus.Stopped)]
    [InlineData("Exited", null, null, null, ContainerDisplayStatus.Stopped)]
    [InlineData("Created", null, null, null, ContainerDisplayStatus.Stopped)]
    [InlineData("Restarting", null, 1, null, ContainerDisplayStatus.Restarting)]
    [InlineData("Paused", null, null, null, ContainerDisplayStatus.Paused)]
    public void DisplayStatus_MapsEveryCombination(string state, ContainerHealth? health, int? exitCode, bool? oom, ContainerDisplayStatus expected)
    {
        Assert.Equal(expected, ContainerDisplayStatuses.Evaluate(state, health, exitCode, oom));
    }

    [Fact]
    public void OperationalEvent_SanitizesTypeAndParameters()
    {
        var now = DateTimeOffset.UtcNow;
        var item = OperationalEvent.Create(
            Guid.NewGuid(),
            OperationalEventTypes.ContainerOomKilled,
            now.AddMinutes(-10),
            now,
            "container",
            "abc",
            "worker\u0000-relatorios",
            new Dictionary<string, string> { ["name"] = "w", ["DATABASE_PASSWORD"] = "secret", ["image"] = new string('x', 400) });

        Assert.Equal(OperationalEventSeverities.Error, item.Severity);
        Assert.Equal("worker-relatorios", item.SubjectName);
        Assert.False(item.Parameters.ContainsKey("DATABASE_PASSWORD"));
        Assert.Equal(OperationalEvent.MaxParameterValueLength, item.Parameters["image"].Length);
        Assert.True(item.ClockSkewDetected);
        Assert.Throws<ArgumentException>(() => OperationalEvent.Create(Guid.NewGuid(), "pod.created", now, now, null, null, null, null));
    }
}
