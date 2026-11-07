using System.Collections.Concurrent;
using Dokpod.ControlPlane.Application.Environments;
using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.ControlPlane.Application.Inventory;
using Dokpod.ControlPlane.Application.Metrics;
using Dokpod.ControlPlane.Application.Realtime;
using Dokpod.Domain.Alerts;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Events;
using Dokpod.Domain.Hosts;
using Dokpod.Domain.Images;
using Dokpod.Domain.Inventory;

namespace Dokpod.ControlPlane.Application.Alerts;

/// <summary>
/// Desde quando cada condição de duração está ativa (limites acima do valor, agente desconectado). Em memória, como as
/// amostras da spec 009: depois de reiniciar a API a contagem recomeça (réplica única, plano 016).
/// </summary>
public sealed class AlertConditionState
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> since = new(StringComparer.Ordinal);

    public DateTimeOffset Track(string key, DateTimeOffset now) => since.GetOrAdd(key, now);

    public void Clear(string key) => since.TryRemove(key, out _);
}

public sealed record AlertSweepResult(int Opened, int Resolved, int Silenced, int Unsilenced, int JustificationsEnded);

/// <summary>Varredura periódica (spec 016 D-003): resolve, abre por duração, aplica silenciamentos e encerra marcações.</summary>
public sealed class AlertSweepService(
    IAlertStore alerts,
    IActionJustificationStore justifications,
    IEnvironmentRegistrationStore registrations,
    HostQueryService hostQuery,
    IContainerSearchStore containers,
    LatestMetricsCache metrics,
    AlertConditionState state,
    IControlPlaneNotifier notifier,
    TimeProvider timeProvider,
    IImageOutdatedSource? imageOutdated = null)
{
    /// <summary>Tempo para o inventário refletir um container antes de a ausência valer como remoção.</summary>
    public static readonly TimeSpan InventoryGrace = TimeSpan.FromMinutes(2);

    public async Task<AlertSweepResult> RunOnceAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var touched = new HashSet<Guid>();
        int opened = 0, resolved = 0, silenced = 0, unsilenced = 0;
        var hosts = (await hostQuery.DescribeAsync(await ListRegistrationsAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false))
            .Where(h => h.Registration.Enabled)
            .ToDictionary(h => h.Registration.EnvironmentId);
        var rules = (await alerts.ListRulesAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(r => r.RuleId);
        var silences = await alerts.ListSilencesAsync(true, now, cancellationToken).ConfigureAwait(false);
        var active = await alerts.ListActiveAsync(cancellationToken).ConfigureAwait(false);
        var containerCache = new Dictionary<(Guid, string), ContainerInventory?>();

        async Task<ContainerInventory?> Container(Guid environmentId, string containerId)
        {
            if (!containerCache.TryGetValue((environmentId, containerId), out var found))
            {
                found = await containers.GetAsync(environmentId, containerId, cancellationToken).ConfigureAwait(false);
                containerCache[(environmentId, containerId)] = found;
            }

            return found;
        }

        // 1. Resolução automática das condições observáveis de container e de agente.
        foreach (var alert in active)
        {
            if (!rules.TryGetValue(alert.RuleId, out var rule) || rule.ConditionKind != AlertConditionKind.Event) continue;
            string? reason = null;
            if (alert.ContainerId is { } containerId && AlertEvaluator.ContainerFailureEvents.Contains(rule.EventType!))
            {
                var container = await Container(alert.EnvironmentId, containerId).ConfigureAwait(false);
                // Ausência só conta depois da carência: um container que nasce e falha em segundos ainda pode não
                // estar no inventário; a remoção imediata chega pelo evento container.removed.
                if (container is null) reason = now - alert.LastOccurredAtUtc > InventoryGrace ? AlertResolutionReasons.ContainerRemoved : null;
                else if (IsHealthyRunning(container) && container.ObservedAtUtc > alert.LastOccurredAtUtc) reason = AlertResolutionReasons.ConditionCleared;
            }
            else if (rule.EventType == OperationalEventTypes.AgentDisconnected && hosts.TryGetValue(alert.EnvironmentId, out var host) && host.Connected)
            {
                reason = AlertResolutionReasons.ConditionCleared;
            }

            if (reason is not null && await alerts.TransitionAsync(alert.AlertId, AlertState.Resolved, null, null, reason, now, AlertTimelineKinds.Resolved, null, null, cancellationToken).ConfigureAwait(false))
            {
                resolved++;
                touched.Add(alert.EnvironmentId);
            }
        }

        // 2. Agente desconectado por mais que a duração da regra.
        foreach (var rule in rules.Values.Where(r => r.Enabled && r.ConditionKind == AlertConditionKind.Event && r.EventType == OperationalEventTypes.AgentDisconnected))
        {
            foreach (var host in hosts.Values)
            {
                var key = $"disconnected:{rule.RuleId:N}:{host.Registration.EnvironmentId:N}";
                if (host.Connected || host.Situation != HostSituation.Disconnected || !rule.Scope.Covers(host.Registration.Stage, host.Registration.EnvironmentId, null))
                {
                    state.Clear(key);
                    continue;
                }

                if (now - state.Track(key, now) < TimeSpan.FromSeconds(rule.DurationSeconds)) continue;
                if (active.Any(a => a.RuleId == rule.RuleId && a.EnvironmentId == host.Registration.EnvironmentId)) continue;
                var silence = CoveringSilence(silences, host.Registration.Stage, host.Registration.EnvironmentId, null);
                var (_, isNew) = await alerts.OpenOrOccurAsync(
                    new AlertOccurrence(rule, host.Registration.EnvironmentId, null, null, "Agente desconectado", now, silence?.SilenceId), cancellationToken).ConfigureAwait(false);
                if (isNew)
                {
                    opened++;
                    touched.Add(host.Registration.EnvironmentId);
                }
            }
        }

        // 3. Limites de CPU e memória com duração mínima; sem amostra recente não abre nem resolve.
        foreach (var rule in rules.Values.Where(r => r.Enabled && r.ConditionKind == AlertConditionKind.Metric))
        {
            foreach (var host in hosts.Values.Where(h => rule.Scope.Covers(h.Registration.Stage, h.Registration.EnvironmentId, null) || rule.IsContainerTarget))
            {
                foreach (var (containerId, containerName, value) in await MetricValuesAsync(rule, host, Container).ConfigureAwait(false))
                {
                    if (containerName is not null && !rule.Scope.Covers(host.Registration.Stage, host.Registration.EnvironmentId, containerName)) continue;
                    var environmentId = host.Registration.EnvironmentId;
                    var key = $"metric:{rule.RuleId:N}:{AlertRecord.TargetKey(environmentId, containerId)}";
                    var existing = active.FirstOrDefault(a => a.RuleId == rule.RuleId && a.EnvironmentId == environmentId && a.ContainerId == containerId);
                    if (value is null) continue;
                    if (value <= rule.ThresholdPercent)
                    {
                        state.Clear(key);
                        if (existing is not null && await alerts.TransitionAsync(existing.AlertId, AlertState.Resolved, null, null, AlertResolutionReasons.ConditionCleared, now, AlertTimelineKinds.Resolved, null, null, cancellationToken).ConfigureAwait(false))
                        {
                            resolved++;
                            touched.Add(environmentId);
                        }

                        continue;
                    }

                    if (existing is not null || now - state.Track(key, now) < TimeSpan.FromSeconds(rule.DurationSeconds)) continue;
                    var subject = containerName ?? host.Registration.Name;
                    var text = $"{MetricLabel(rule.Metric!.Value)} de {subject} em {value:0.#}% (limite {rule.ThresholdPercent:0.#}%)";
                    var silence = CoveringSilence(silences, host.Registration.Stage, environmentId, containerName);
                    var (_, isNew) = await alerts.OpenOrOccurAsync(new AlertOccurrence(rule, environmentId, containerId, containerName, text, now, silence?.SilenceId), cancellationToken).ConfigureAwait(false);
                    if (isNew)
                    {
                        opened++;
                        touched.Add(environmentId);
                    }
                }
            }
        }

        // 3b. Imagem desatualizada há N dias (spec 025 T022).
        var (imageOpened, imageResolved) = await SweepImageOutdatedAsync(rules.Values, hosts, active, silences, now, touched, cancellationToken).ConfigureAwait(false);
        opened += imageOpened;
        resolved += imageResolved;

        // 4. Silenciamentos: aplica aos abertos cobertos e devolve a "aberto" quando a janela termina (FR-013).
        foreach (var alert in await alerts.ListActiveAsync(cancellationToken).ConfigureAwait(false))
        {
            var stage = hosts.TryGetValue(alert.EnvironmentId, out var host) ? host.Registration.Stage : null;
            var covering = silences.FirstOrDefault(s => s.Covers(alert.AlertId, stage, alert.EnvironmentId, alert.ContainerName));
            if (alert.State is AlertState.Open or AlertState.Acknowledged && covering is not null)
            {
                if (await alerts.TransitionAsync(alert.AlertId, AlertState.Silenced, null, covering.SilenceId, null, now, AlertTimelineKinds.Silenced, null, $"Silenciamento até {covering.EndsAtUtc:dd/MM/yyyy HH:mm} UTC.", cancellationToken).ConfigureAwait(false))
                {
                    silenced++;
                    touched.Add(alert.EnvironmentId);
                }
            }
            else if (alert.State == AlertState.Silenced && covering is null)
            {
                if (await alerts.TransitionAsync(alert.AlertId, AlertState.Open, null, null, null, now, AlertTimelineKinds.Unsilenced, null, "Fim do silenciamento.", cancellationToken).ConfigureAwait(false))
                {
                    unsilenced++;
                    touched.Add(alert.EnvironmentId);
                }
            }
        }

        // 5. Marcação intencional termina quando o container volta a executar (FR-018).
        var ended = 0;
        foreach (var justification in await justifications.ListOpenAsync(cancellationToken).ConfigureAwait(false))
        {
            var container = await Container(justification.EnvironmentId, justification.ContainerId).ConfigureAwait(false);
            if (container is not null && container.DisplayStatus is ContainerDisplayStatus.Running or ContainerDisplayStatus.Starting or ContainerDisplayStatus.Restarting
                && container.ObservedAtUtc > justification.CreatedAtUtc)
            {
                await justifications.EndAsync(justification.JustificationId, now, cancellationToken).ConfigureAwait(false);
                ended++;
                touched.Add(justification.EnvironmentId);
            }
        }

        foreach (var environmentId in touched)
        {
            await notifier.NotifyAsync(environmentId, InvalidationKinds.Alerts, cancellationToken).ConfigureAwait(false);
            if (ended > 0) await notifier.NotifyAsync(environmentId, InvalidationKinds.Inventory, cancellationToken).ConfigureAwait(false);
        }

        return new AlertSweepResult(opened, resolved, silenced, unsilenced, ended);
    }

    /// <summary>
    /// Abre um alerta por container cuja referência está desatualizada há N dias ou mais. O alerta continua enquanto o
    /// container usa uma versão anterior (<c>update_available</c>, <c>container_behind</c>) ou a verificação falha (uma
    /// falha nunca vira "Atualizada", SC-002), e resolve quando ele é recriado em dia ou deixa de existir. Sem o módulo
    /// ou sem o aviso disponível, nada muda.
    /// </summary>
    private async Task<(int Opened, int Resolved)> SweepImageOutdatedAsync(
        IEnumerable<AlertRule> allRules,
        IReadOnlyDictionary<Guid, HostView> hosts,
        IReadOnlyList<AlertRecord> active,
        IReadOnlyList<AlertSilence> silences,
        DateTimeOffset now,
        HashSet<Guid> touched,
        CancellationToken cancellationToken)
    {
        var rules = allRules.Where(r => r.ConditionKind == AlertConditionKind.ImageOutdated).ToArray();
        if (imageOutdated is null || rules.Length == 0 || hosts.Count == 0) return (0, 0);
        var targets = await imageOutdated.ListAsync(hosts.Keys.ToArray(), cancellationToken).ConfigureAwait(false);
        if (targets is null) return (0, 0);
        var byContainer = targets.ToDictionary(t => (t.EnvironmentId, t.ContainerId));
        int opened = 0, resolved = 0;

        foreach (var alert in active.Where(a => rules.Any(r => r.RuleId == a.RuleId) && hosts.ContainsKey(a.EnvironmentId) && a.ContainerId is not null))
        {
            var reason = !byContainer.TryGetValue((alert.EnvironmentId, alert.ContainerId!), out var target)
                ? now - alert.LastOccurredAtUtc > InventoryGrace ? AlertResolutionReasons.ContainerRemoved : null
                : target.State is ImageUpdateStates.UpdateAvailable or ImageUpdateStates.ContainerBehind or ImageUpdateStates.Failed ? null
                : AlertResolutionReasons.ConditionCleared;
            if (reason is not null && await alerts.TransitionAsync(alert.AlertId, AlertState.Resolved, null, null, reason, now, AlertTimelineKinds.Resolved, null, null, cancellationToken).ConfigureAwait(false))
            {
                resolved++;
                touched.Add(alert.EnvironmentId);
            }
        }

        foreach (var rule in rules.Where(r => r.Enabled && r.OutdatedDays is not null))
        {
            var limit = TimeSpan.FromDays(rule.OutdatedDays!.Value);
            foreach (var target in targets)
            {
                if (target.State != ImageUpdateStates.UpdateAvailable || target.OutdatedSinceUtc is not { } since || now - since < limit) continue;
                if (!hosts.TryGetValue(target.EnvironmentId, out var host) || !rule.Scope.Covers(host.Registration.Stage, target.EnvironmentId, target.ContainerName)) continue;
                if (active.Any(a => a.RuleId == rule.RuleId && a.EnvironmentId == target.EnvironmentId && a.ContainerId == target.ContainerId)) continue;
                var days = (int)Math.Floor((now - since).TotalDays);
                var text = $"Imagem {target.Reference} de {target.ContainerName} desatualizada há {days} {(days == 1 ? "dia" : "dias")} (limite {rule.OutdatedDays} {(rule.OutdatedDays == 1 ? "dia" : "dias")})";
                var silence = CoveringSilence(silences, host.Registration.Stage, target.EnvironmentId, target.ContainerName);
                var (_, isNew) = await alerts.OpenOrOccurAsync(
                    new AlertOccurrence(rule, target.EnvironmentId, target.ContainerId, target.ContainerName, text, now, silence?.SilenceId), cancellationToken).ConfigureAwait(false);
                if (isNew)
                {
                    opened++;
                    touched.Add(target.EnvironmentId);
                }
            }
        }

        return (opened, resolved);
    }

    private async Task<IReadOnlyList<(string? ContainerId, string? ContainerName, double? Value)>> MetricValuesAsync(
        AlertRule rule,
        HostView host,
        Func<Guid, string, Task<ContainerInventory?>> container)
    {
        var environmentId = host.Registration.EnvironmentId;
        var hostSample = metrics.GetHost(environmentId);
        switch (rule.Metric)
        {
            case AlertMetric.HostCpu:
                return [(null, null, hostSample?.CpuPercent)];
            case AlertMetric.HostMemory:
                return [(null, null, hostSample is { MemoryUsedBytes: { } used, MemoryTotalBytes: > 0 } ? used * 100d / hostSample.MemoryTotalBytes!.Value : null)];
            default:
                var values = new List<(string?, string?, double?)>();
                foreach (var (containerId, sample) in metrics.GetContainersWithIds(environmentId))
                {
                    var inventory = await container(environmentId, containerId).ConfigureAwait(false);
                    if (inventory is null) continue;
                    var memoryReference = sample.MemoryLimitBytes ?? hostSample?.MemoryTotalBytes;
                    double? value = rule.Metric == AlertMetric.ContainerCpu
                        ? sample.CpuPercentOfLimit is { } ofLimit ? Math.Min(100, ofLimit) : sample.CpuPercentOfHost
                        : sample.MemoryUsedBytes is { } memoryUsed && memoryReference is > 0 ? memoryUsed * 100d / memoryReference.Value : null;
                    values.Add((containerId, inventory.Name, value));
                }

                return values;
        }
    }

    private static AlertSilence? CoveringSilence(IReadOnlyList<AlertSilence> silences, EnvironmentStage? stage, Guid environmentId, string? containerName) =>
        silences.FirstOrDefault(s => s.AlertId is null && s.Scope.Covers(stage, environmentId, containerName));

    private static bool IsHealthyRunning(ContainerInventory container) =>
        container.DisplayStatus is ContainerDisplayStatus.Running && container.Health != ContainerHealth.Unhealthy;

    private static string MetricLabel(AlertMetric metric) => metric switch
    {
        AlertMetric.HostCpu or AlertMetric.ContainerCpu => "CPU",
        _ => "Memória",
    };

    private async Task<IReadOnlyList<EnvironmentRegistration>> ListRegistrationsAsync(CancellationToken cancellationToken)
    {
        var all = new List<EnvironmentRegistration>();
        Guid? cursor = null;
        do
        {
            var page = await registrations.ListAsync(cursor, 100, cancellationToken).ConfigureAwait(false);
            all.AddRange(page.Registrations);
            cursor = page.NextCursor;
        }
        while (cursor is not null && all.Count < 10_000);

        return all;
    }
}
