using Dokpod.ControlPlane.Application.Environments;
using Dokpod.ControlPlane.Application.Events;
using Dokpod.ControlPlane.Application.Realtime;
using Dokpod.Domain.Alerts;
using Dokpod.Domain.Events;

namespace Dokpod.ControlPlane.Application.Alerts;

/// <summary>
/// Avaliação de regras por evento (spec 016 D-002): chamada pelo <see cref="OperationalEventService"/> com os eventos
/// efetivamente gravados. Abre ou acumula ocorrências, respeita silenciamentos e ações justificadas, e resolve na hora
/// o que um evento já encerra (recuperação, reconexão e remoção do container).
/// </summary>
public sealed class AlertEvaluator(
    IAlertStore alerts,
    IActionJustificationStore justifications,
    IEnvironmentRegistrationStore registrations,
    IControlPlaneNotifier notifier,
    TimeProvider timeProvider) : IOperationalEventObserver
{
    /// <summary>Janela em que uma ação justificada cobre o encerramento do container (comando aceito antes do evento).</summary>
    public static readonly TimeSpan JustifiedStopWindow = TimeSpan.FromMinutes(10);

    /// <summary>Eventos de falha de container resolvidos quando o container se recupera.</summary>
    public static readonly IReadOnlySet<string> ContainerFailureEvents = new HashSet<string>
    {
        OperationalEventTypes.ContainerDied,
        OperationalEventTypes.ContainerOomKilled,
        OperationalEventTypes.ContainerUnhealthy,
        OperationalEventTypes.ContainerAutoRestarted,
    };

    public async Task OnEventsAsync(Guid environmentId, IReadOnlyCollection<OperationalEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0) return;
        var changed = false;
        var registration = await registrations.GetAsync(environmentId, cancellationToken).ConfigureAwait(false);
        if (registration is null) return;
        var allRules = await alerts.ListRulesAsync(cancellationToken).ConfigureAwait(false);
        var ruleEvents = allRules.ToDictionary(rule => rule.RuleId, rule => rule.EventType);
        var rules = allRules.Where(rule => rule.Enabled && rule.ConditionKind == AlertConditionKind.Event).ToArray();
        var now = timeProvider.GetUtcNow();
        var silences = await alerts.ListSilencesAsync(true, now, cancellationToken).ConfigureAwait(false);

        foreach (var item in events.OrderBy(e => e.OccurredAtUtc))
        {
            changed |= await ResolveByEventAsync(environmentId, item, ruleEvents, cancellationToken).ConfigureAwait(false);
            foreach (var rule in rules.Where(r => r.EventType == item.Type))
            {
                var containerId = rule.IsContainerTarget ? item.SubjectId : null;
                var containerName = rule.IsContainerTarget ? item.SubjectName : null;
                if (rule.IsContainerTarget && string.IsNullOrEmpty(containerId)) continue;
                if (!rule.Scope.Covers(registration.Stage, environmentId, containerName)) continue;
                // Agente desconectado com duração: a varredura abre se a desconexão persistir (D-003).
                if (item.Type == OperationalEventTypes.AgentDisconnected && rule.DurationSeconds > 0) continue;
                if (item.Type == OperationalEventTypes.ContainerDied && !await IsFailureAsync(environmentId, item, cancellationToken).ConfigureAwait(false)) continue;
                if (rule.MinOccurrences > 1)
                {
                    var since = now - TimeSpan.FromSeconds(rule.DurationSeconds);
                    var count = await alerts.CountEventsAsync(environmentId, item.Type, containerId, since, cancellationToken).ConfigureAwait(false);
                    if (count < rule.MinOccurrences) continue;
                }

                var silence = silences.FirstOrDefault(s => s.AlertId is null && s.Covers(Guid.Empty, registration.Stage, environmentId, containerName));
                await alerts.OpenOrOccurAsync(
                    new AlertOccurrence(rule, environmentId, containerId, containerName, ConditionText(rule, item), item.OccurredAtUtc, silence?.SilenceId),
                    cancellationToken).ConfigureAwait(false);
                changed = true;
            }
        }

        if (changed) await notifier.NotifyAsync(environmentId, InvalidationKinds.Alerts, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Encerramento é falha só fora dos códigos de parada e sem ação justificada recente (D-004, FR-019).</summary>
    private async Task<bool> IsFailureAsync(Guid environmentId, OperationalEvent item, CancellationToken cancellationToken)
    {
        if (item.Parameters.TryGetValue("exitCode", out var text) && int.TryParse(text, out var exitCode) && AlertRule.StopExitCodes.Contains(exitCode))
        {
            return false;
        }

        return item.SubjectId is null ||
            !await justifications.HasRecentAsync(environmentId, item.SubjectId, item.OccurredAtUtc - JustifiedStopWindow, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> ResolveByEventAsync(
        Guid environmentId,
        OperationalEvent item,
        IReadOnlyDictionary<Guid, string?> ruleEvents,
        CancellationToken cancellationToken)
    {
        // Casa pelo tipo de evento da regra, para valer também em regras criadas pelo administrador.
        string? EventOf(AlertRecord alert) => ruleEvents.GetValueOrDefault(alert.RuleId);
        Func<AlertRecord, bool>? matches = item.Type switch
        {
            OperationalEventTypes.ContainerRecovered => alert => alert.ContainerId == item.SubjectId && EventOf(alert) is { } type && ContainerFailureEvents.Contains(type),
            OperationalEventTypes.ContainerRemoved => alert => alert.ContainerId == item.SubjectId,
            OperationalEventTypes.AgentConnected => alert => alert.ContainerId is null && EventOf(alert) == OperationalEventTypes.AgentDisconnected,
            _ => null,
        };
        if (matches is null || (item.Type != OperationalEventTypes.AgentConnected && item.SubjectId is null)) return false;
        var reason = item.Type == OperationalEventTypes.ContainerRemoved ? AlertResolutionReasons.ContainerRemoved : AlertResolutionReasons.ConditionCleared;
        var changed = false;
        foreach (var alert in (await alerts.ListActiveAsync(cancellationToken).ConfigureAwait(false)).Where(a => a.EnvironmentId == environmentId).Where(matches))
        {
            changed |= await alerts.TransitionAsync(alert.AlertId, AlertState.Resolved, null, null, reason, item.OccurredAtUtc, AlertTimelineKinds.Resolved, null, null, cancellationToken).ConfigureAwait(false);
        }

        return changed;
    }

    public static string ConditionText(AlertRule rule, OperationalEvent item)
    {
        var name = item.SubjectName ?? (item.SubjectId is { Length: > 12 } id ? id[..12] : item.SubjectId) ?? "host";
        item.Parameters.TryGetValue("exitCode", out var exitCode);
        item.Parameters.TryGetValue("action", out var action);
        item.Parameters.TryGetValue("failureCode", out var failure);
        return item.Type switch
        {
            OperationalEventTypes.ContainerDied => $"Container {name} encerrado com erro (código {exitCode ?? "?"})",
            OperationalEventTypes.ContainerOomKilled => $"Container {name} encerrado por falta de memória",
            OperationalEventTypes.ContainerUnhealthy => $"Container {name} sem saúde",
            OperationalEventTypes.ContainerAutoRestarted => $"Container {name} reiniciou {rule.MinOccurrences} ou mais vezes em {rule.DurationSeconds / 60} minutos",
            OperationalEventTypes.AgentDisconnected => "Agente desconectado",
            OperationalEventTypes.CommandFailed => $"Comando {action ?? "?"} falhou em {name}{(failure is null ? string.Empty : $" ({failure})")}",
            OperationalEventTypes.CommandIndeterminate => $"Comando {action ?? "?"} com resultado indeterminado em {name}",
            _ => $"{rule.Name} · {name}",
        };
    }
}

/// <summary>Ids das regras padrão semeadas na migration 202610140001.</summary>
public static class BuiltInRules
{
    public static readonly Guid ContainerDied = Guid.Parse("01600000-0000-4000-8000-000000000001");
    public static readonly Guid ContainerOomKilled = Guid.Parse("01600000-0000-4000-8000-000000000002");
    public static readonly Guid ContainerUnhealthy = Guid.Parse("01600000-0000-4000-8000-000000000003");
    public static readonly Guid ContainerRestarts = Guid.Parse("01600000-0000-4000-8000-000000000004");
    public static readonly Guid AgentDisconnected = Guid.Parse("01600000-0000-4000-8000-000000000005");

    public static IReadOnlySet<Guid> ContainerFailures { get; } = new HashSet<Guid> { ContainerDied, ContainerOomKilled, ContainerUnhealthy, ContainerRestarts };
}
