namespace Dokpod.Agent.Application.Protocol;

/// <summary>Número da falha consecutiva e espera antes da próxima tentativa de sessão.</summary>
public readonly record struct AgentReconnectDelay(int Attempt, TimeSpan Delay);

/// <summary>
/// Espera entre reconexões da sessão de controle (spec 035): 1 s dobrando até 30 s, sorteada entre a metade e o
/// limite, para agentes que caíram juntos não voltarem juntos. Só uma sessão de 60 s recomeça a sequência.
/// </summary>
public sealed class AgentReconnectBackoff(Random random)
{
    public static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan StableSession = TimeSpan.FromSeconds(60);

    private readonly Random random = random ?? throw new ArgumentNullException(nameof(random));
    private int consecutiveFailures;

    /// <summary>Chamado após toda saída da sessão, com quanto tempo ela durou (zero quando nem foi estabelecida).</summary>
    public AgentReconnectDelay NextDelay(TimeSpan sessionDuration)
    {
        if (sessionDuration >= StableSession) consecutiveFailures = 0;
        if (consecutiveFailures < int.MaxValue) consecutiveFailures++;

        // 2^5 = 32 s já passa do teto: o expoente fica limitado e não estoura.
        var limit = TimeSpan.FromTicks(Math.Min(MaxDelay.Ticks, InitialDelay.Ticks << Math.Min(consecutiveFailures - 1, 5)));
        var delay = limit / 2 + limit / 2 * random.NextDouble();
        return new AgentReconnectDelay(consecutiveFailures, delay);
    }
}
