using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Realtime;
using Xunit;

namespace Dokpod.ControlPlane.Application.Tests.Realtime;

public sealed class StreamingAuthorizationMonitorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task ChecksOnlyEveryInterval()
    {
        var clock = new Clock(T0);
        var monitor = new StreamingAuthorizationMonitor(clock, T0.AddHours(1));
        var calls = 0;
        Task<AuthorizationDecisionOutcome> Authorize(CancellationToken _) { calls++; return Task.FromResult(AuthorizationDecisionOutcome.Allowed); }

        Assert.Equal(StreamingAuthorizationState.Allowed, await monitor.CheckAsync(Authorize, TestContext.Current.CancellationToken));
        clock.Now = T0.AddSeconds(59);
        await monitor.CheckAsync(Authorize, TestContext.Current.CancellationToken);
        Assert.Equal(0, calls);
        clock.Now = T0.AddSeconds(60);
        Assert.True(monitor.IsDue);
        await monitor.CheckAsync(Authorize, TestContext.Current.CancellationToken);
        Assert.Equal(1, calls);
        Assert.False(monitor.IsDue);
    }

    [Theory]
    [InlineData(AuthorizationDecisionOutcome.Denied, StreamingAuthorizationState.Revoked, "authorization_revoked")]
    [InlineData(AuthorizationDecisionOutcome.Indeterminate, StreamingAuthorizationState.Unavailable, "authorization_unavailable")]
    public async Task MapsDecisionToEndReason(AuthorizationDecisionOutcome decision, StreamingAuthorizationState expected, string reason)
    {
        var clock = new Clock(T0);
        var monitor = new StreamingAuthorizationMonitor(clock, T0.AddHours(1));
        clock.Now = T0.AddMinutes(1);

        var state = await monitor.CheckAsync(_ => Task.FromResult(decision), TestContext.Current.CancellationToken);

        Assert.Equal(expected, state);
        Assert.Equal(reason, StreamingAuthorizationMonitor.EndReason(state));
    }

    [Fact]
    public async Task ExpiringTokenRequiresRefreshWithoutAskingKeycloak()
    {
        var clock = new Clock(T0);
        var monitor = new StreamingAuthorizationMonitor(clock, T0.AddSeconds(80));
        clock.Now = T0.AddSeconds(60); // 20 s antes do exp, dentro da margem de 30 s

        var state = await monitor.CheckAsync(_ => throw new InvalidOperationException("não deve consultar"), TestContext.Current.CancellationToken);

        Assert.Equal(StreamingAuthorizationState.RefreshRequired, state);
        Assert.Equal("session_refresh_required", StreamingAuthorizationMonitor.EndReason(state));
    }

    [Fact]
    public async Task DeciderFailureFailsClosed()
    {
        var clock = new Clock(T0);
        var monitor = new StreamingAuthorizationMonitor(clock, null);
        clock.Now = T0.AddMinutes(2);

        Assert.Equal(
            StreamingAuthorizationState.Unavailable,
            await monitor.CheckAsync(_ => throw new HttpRequestException("keycloak"), TestContext.Current.CancellationToken));
    }
}
