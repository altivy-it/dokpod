using System.Diagnostics;
using System.Diagnostics.Metrics;
using Dokpod.ControlPlane.Application.Authorization;

namespace Dokpod.ControlPlane.Application.Hosts;

/// <summary>
/// Métricas operacionais do plano de controle (ADR 2026-0001 e 2026-0002; spec 005, gate de observabilidade):
/// agentes conectados por engine e versão, idade do inventário, latência e resultado dos comandos e decisões do
/// Keycloak. As tags são de conjunto fechado (engine, versão, tipo, estado, código de falha); nunca IDs.
/// </summary>
public sealed class ControlPlaneMetrics : IDisposable
{
    public const string MeterName = "Dokpod.ControlPlane";

    private readonly Meter meter = new(MeterName, "1.0.0");
    private readonly UpDownCounter<long> agentsConnected;
    private readonly Counter<long> commandsCompleted;
    private readonly Histogram<double> commandDuration;
    private readonly Counter<long> authorizationDecisions;
    private readonly Histogram<double> authorizationDuration;

    // Os buckets padrão do OpenTelemetry (0–10.000) são para milissegundos; em segundos, tudo cairia no primeiro.
    private static readonly InstrumentAdvice<double> SecondsBuckets = new()
    {
        HistogramBucketBoundaries = [0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30, 60],
    };

    /// <summary>Meter desta instância (o listener de teste filtra por ela).</summary>
    public Meter Meter => meter;

    public ControlPlaneMetrics(AgentLivenessTracker liveness)
    {
        agentsConnected = meter.CreateUpDownCounter<long>(
            "dokpod.agents.connected", "{agent}", "Sessões de agente ativas nesta instância.");
        commandsCompleted = meter.CreateCounter<long>(
            "dokpod.commands.completed", "{command}", "Comandos que chegaram a estado terminal.");
        commandDuration = meter.CreateHistogram(
            "dokpod.command.duration", "s", "Do despacho ao agente até o resultado.", tags: null, advice: SecondsBuckets);
        authorizationDecisions = meter.CreateCounter<long>(
            "dokpod.authorization.decisions", "{decision}", "Decisões UMA do Keycloak por resultado.");
        authorizationDuration = meter.CreateHistogram(
            "dokpod.authorization.duration", "s", "Duração das decisões UMA do Keycloak.", tags: null, advice: SecondsBuckets);
        meter.CreateObservableGauge(
            "dokpod.inventory.lag",
            () => liveness.MaxDivergence().TotalSeconds,
            "s",
            "Maior tempo em que a revisão relatada por um agente segue sem projeção.");
        meter.CreateObservableGauge(
            "dokpod.agents.heartbeat.age",
            () => liveness.MaxHeartbeatAge().TotalSeconds,
            "s",
            "Maior idade do último heartbeat entre os agentes conectados.");
    }

    public void AgentConnected(string engine, string agentVersion) =>
        agentsConnected.Add(1, Agent(engine, agentVersion));

    public void AgentDisconnected(string engine, string agentVersion) =>
        agentsConnected.Add(-1, Agent(engine, agentVersion));

    public void CommandCompleted(string kind, string state, string? failureCode, TimeSpan? sinceDispatch)
    {
        var tags = new TagList
        {
            { "command.kind", kind },
            { "command.state", state },
            { "command.failure_code", failureCode ?? string.Empty },
        };
        commandsCompleted.Add(1, tags);
        if (sinceDispatch is { } duration)
        {
            commandDuration.Record(duration.TotalSeconds, new TagList { { "command.kind", kind }, { "command.state", state } });
        }
    }

    public void AuthorizationDecided(AuthorizationDecisionOutcome outcome, string? failureCode, TimeSpan duration)
    {
        var tags = new TagList
        {
            { "authorization.outcome", outcome.ToString().ToLowerInvariant() },
            { "authorization.failure_code", failureCode ?? string.Empty },
        };
        authorizationDecisions.Add(1, tags);
        authorizationDuration.Record(duration.TotalSeconds, new TagList { { "authorization.outcome", outcome.ToString().ToLowerInvariant() } });
    }

    public void Dispose() => meter.Dispose();

    private static TagList Agent(string engine, string agentVersion) =>
        new() { { "agent.engine", engine }, { "agent.version", agentVersion.Length > 32 ? agentVersion[..32] : agentVersion } };
}
