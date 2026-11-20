using Dokpod.Agent.Application.Protocol;
using Xunit;

namespace Dokpod.Agent.Application.Tests.Protocol;

// Spec 035: espera crescente e aleatória entre reconexões da sessão de controle.
public sealed class AgentReconnectBackoffTests
{
    [Fact]
    public void NextDelay_DoublesFromOneSecondUpToThirtyAndStaysWithinHalfAndLimit()
    {
        var backoff = new AgentReconnectBackoff(new Random(35));
        double[] limits = [1, 2, 4, 8, 16, 30, 30];

        for (var i = 0; i < limits.Length; i++)
        {
            var next = backoff.NextDelay(TimeSpan.Zero);

            Assert.Equal(i + 1, next.Attempt);
            Assert.InRange(next.Delay.TotalSeconds, limits[i] / 2, limits[i]);
        }
    }

    [Fact]
    public void NextDelay_RestartsOnlyAfterAStableSession()
    {
        var backoff = new AgentReconnectBackoff(new Random(35));
        backoff.NextDelay(TimeSpan.Zero);
        backoff.NextDelay(TimeSpan.Zero);

        Assert.Equal(3, backoff.NextDelay(TimeSpan.FromSeconds(59)).Attempt);
        Assert.Equal(1, backoff.NextDelay(AgentReconnectBackoff.StableSession).Attempt);
    }

    [Fact]
    public void NextDelay_StaysAtTheCeilingAfterManyFailures()
    {
        var backoff = new AgentReconnectBackoff(new Random(35));
        AgentReconnectDelay next = default;
        for (var i = 0; i < 1_000; i++) next = backoff.NextDelay(TimeSpan.Zero);

        Assert.Equal(1_000, next.Attempt);
        Assert.InRange(next.Delay, AgentReconnectBackoff.MaxDelay / 2, AgentReconnectBackoff.MaxDelay);
    }

    [Fact]
    public void WorstCase_MakesAtMostFortyFiveAttemptsInTenMinutes()
    {
        // Sorteio sempre no mínimo: a espera mais curta possível (SC-002).
        var backoff = new AgentReconnectBackoff(new MinimumRandom());
        var elapsed = TimeSpan.Zero;
        var attempts = 1;
        while (true)
        {
            elapsed += backoff.NextDelay(TimeSpan.Zero).Delay;
            if (elapsed > TimeSpan.FromMinutes(10)) break;
            attempts++;
        }

        Assert.True(attempts <= 45, $"{attempts} tentativas");
    }

    [Theory]
    [InlineData(35)]
    [InlineData(600)]
    public void HundredAgentsDroppedTogether_ReconnectSpreadAndWithinSixtySeconds(int apiBackAfterSeconds)
    {
        // Simulação de SC-001 e SC-003 (research R6): 100 agentes caem em t = 0, cada tentativa leva 0,2 s.
        var random = new Random(2026);
        var reconnected = new List<double>();
        for (var agent = 0; agent < 100; agent++)
        {
            var backoff = new AgentReconnectBackoff(random);
            var time = 0d;
            while (time < apiBackAfterSeconds) time += 0.2 + backoff.NextDelay(TimeSpan.Zero).Delay.TotalSeconds;
            reconnected.Add(time);
        }

        reconnected.Sort();
        Assert.True(reconnected[^1] - apiBackAfterSeconds <= 60, $"última reconexão {reconnected[^1] - apiBackAfterSeconds:F1} s após a volta");
        var densest = 0;
        for (int first = 0, last = 0; last < reconnected.Count; last++)
        {
            while (reconnected[last] - reconnected[first] > 1) first++;
            densest = Math.Max(densest, last - first + 1);
        }

        Assert.True(densest <= 20, $"{densest} reconexões na mesma janela de 1 s");
    }

    private sealed class MinimumRandom : Random
    {
        public override double NextDouble() => 0;
    }
}
