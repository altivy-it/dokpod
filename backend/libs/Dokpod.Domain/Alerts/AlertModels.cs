using Dokpod.Domain.Environments;
using Dokpod.Domain.Events;

namespace Dokpod.Domain.Alerts;

public enum AlertSeverity
{
    Critical,
    High,
    Medium,
    Low,
}

public enum AlertState
{
    Open,
    Acknowledged,
    Silenced,
    Resolved,
}

public enum AlertConditionKind
{
    Event,
    Metric,
}

public enum AlertMetric
{
    HostCpu,
    HostMemory,
    ContainerCpu,
    ContainerMemory,
}

/// <summary>Valores persistidos e expostos pela API (spec 016, data-model).</summary>
public static class AlertValues
{
    public static string Severity(AlertSeverity value) => value switch
    {
        AlertSeverity.Critical => "critical",
        AlertSeverity.High => "high",
        AlertSeverity.Medium => "medium",
        AlertSeverity.Low => "low",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    public static bool TryParseSeverity(string? value, out AlertSeverity severity) =>
        TryParse(value, out severity, ("critical", AlertSeverity.Critical), ("high", AlertSeverity.High), ("medium", AlertSeverity.Medium), ("low", AlertSeverity.Low));

    public static string State(AlertState value) => value switch
    {
        AlertState.Open => "open",
        AlertState.Acknowledged => "acknowledged",
        AlertState.Silenced => "silenced",
        AlertState.Resolved => "resolved",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    public static bool TryParseState(string? value, out AlertState state) =>
        TryParse(value, out state, ("open", AlertState.Open), ("acknowledged", AlertState.Acknowledged), ("silenced", AlertState.Silenced), ("resolved", AlertState.Resolved));

    public static string Metric(AlertMetric value) => value switch
    {
        AlertMetric.HostCpu => "host_cpu",
        AlertMetric.HostMemory => "host_memory",
        AlertMetric.ContainerCpu => "container_cpu",
        AlertMetric.ContainerMemory => "container_memory",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    public static bool TryParseMetric(string? value, out AlertMetric metric) =>
        TryParse(value, out metric, ("host_cpu", AlertMetric.HostCpu), ("host_memory", AlertMetric.HostMemory), ("container_cpu", AlertMetric.ContainerCpu), ("container_memory", AlertMetric.ContainerMemory));

    private static bool TryParse<T>(string? value, out T result, params (string Text, T Value)[] options)
    {
        foreach (var (text, option) in options)
        {
            if (string.Equals(value, text, StringComparison.Ordinal))
            {
                result = option;
                return true;
            }
        }

        result = default!;
        return false;
    }
}

/// <summary>Abrangência de regra ou silenciamento: estágio, host e padrão de nome de container (curinga `*`).</summary>
public sealed record AlertScope(EnvironmentStage? Stage, Guid? EnvironmentId, string? ContainerPattern)
{
    public const int MaxPatternLength = 128;

    public static AlertScope Everything { get; } = new(null, null, null);

    public bool Covers(EnvironmentStage? stage, Guid environmentId, string? containerName)
    {
        if (Stage is { } wanted && stage != wanted) return false;
        if (EnvironmentId is { } host && host != environmentId) return false;
        if (string.IsNullOrEmpty(ContainerPattern)) return true;
        return containerName is not null && MatchesPattern(ContainerPattern, containerName);
    }

    public static bool MatchesPattern(string pattern, string name)
    {
        // Curinga simples: `*` casa qualquer sequência; comparação ordinal e sem diferenciar maiúsculas.
        var parts = pattern.Split('*');
        var position = 0;
        for (var index = 0; index < parts.Length; index++)
        {
            var part = parts[index];
            if (part.Length == 0) continue;
            var found = name.IndexOf(part, position, StringComparison.OrdinalIgnoreCase);
            if (found < 0 || (index == 0 && found != 0)) return false;
            position = found + part.Length;
        }

        return parts[^1].Length == 0 || position == name.Length;
    }
}

/// <summary>Regra de alerta (spec 016 FR-009/FR-010).</summary>
public sealed record AlertRule(
    Guid RuleId,
    string Name,
    string? Description,
    AlertConditionKind ConditionKind,
    string? EventType,
    AlertMetric? Metric,
    double? ThresholdPercent,
    int DurationSeconds,
    AlertSeverity Severity,
    AlertScope Scope,
    bool Enabled,
    bool BuiltIn,
    int Version)
{
    public const int MaxNameLength = 120;
    public const int MaxDescriptionLength = 500;
    public const int MaxDurationSeconds = 3_600;
    public const int MaxOccurrences = 100;

    /// <summary>
    /// Ocorrências mínimas dentro de `DurationSeconds` para abrir (regras de evento; ex.: 3 reinícios em 10 minutos).
    /// Para regras de métrica, `DurationSeconds` é o tempo contínuo acima do limite.
    /// </summary>
    public int MinOccurrences { get; init; } = 1;

    /// <summary>Códigos de saída que representam parada, não falha (D-004): normal, SIGKILL e SIGTERM.</summary>
    public static IReadOnlySet<int> StopExitCodes { get; } = new HashSet<int> { 0, 137, 143 };

    public bool IsContainerTarget => ConditionKind == AlertConditionKind.Metric
        ? Metric is AlertMetric.ContainerCpu or AlertMetric.ContainerMemory
        : EventType is not (OperationalEventTypes.AgentDisconnected or OperationalEventTypes.EngineVersionOutdated or OperationalEventTypes.EventsDropped);

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Name) || Name.Trim().Length > MaxNameLength) errors.Add("name");
        if (Description is { Length: > MaxDescriptionLength }) errors.Add("description");
        if (DurationSeconds is < 0 or > MaxDurationSeconds) errors.Add("durationSeconds");
        if (MinOccurrences is < 1 or > MaxOccurrences || (MinOccurrences > 1 && DurationSeconds == 0)) errors.Add("minOccurrences");
        if (Scope.ContainerPattern is { Length: > AlertScope.MaxPatternLength }) errors.Add("scope.containerPattern");
        if (ConditionKind == AlertConditionKind.Event)
        {
            if (EventType is null || !OperationalEventTypes.Severity.ContainsKey(EventType)) errors.Add("eventType");
            if (Metric is not null || ThresholdPercent is not null) errors.Add("metric");
        }
        else
        {
            if (Metric is null) errors.Add("metric");
            if (ThresholdPercent is not (>= 1 and <= 100)) errors.Add("thresholdPercent");
            if (EventType is not null) errors.Add("eventType");
        }

        return errors;
    }
}

/// <summary>Silenciamento (spec 016 FR-012): janela de até 7 dias com justificativa.</summary>
public sealed record AlertSilence(
    Guid SilenceId,
    AlertScope Scope,
    Guid? AlertId,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    DateTimeOffset? EndedAtUtc,
    string Justification,
    string CreatedBy)
{
    public static readonly TimeSpan MaxDuration = TimeSpan.FromDays(7);

    public bool IsActive(DateTimeOffset now) => EndedAtUtc is null && StartsAtUtc <= now && now < EndsAtUtc;

    public bool Covers(Guid alertId, EnvironmentStage? stage, Guid environmentId, string? containerName) =>
        AlertId is { } only ? only == alertId : Scope.Covers(stage, environmentId, containerName);

    public static string? ValidateWindow(DateTimeOffset startsAt, DateTimeOffset endsAt) =>
        endsAt <= startsAt ? "endsAt" : endsAt - startsAt > MaxDuration ? "endsAt" : null;
}

public enum JustifiedAction
{
    Stop,
    Pause,
    Delete,
}

/// <summary>Justificativa de ação (spec 016 FR-015/FR-016).</summary>
public static class ActionJustifications
{
    public const int MinLength = 10;
    public const int MaxLength = 500;

    public static string Value(JustifiedAction action) => action switch
    {
        JustifiedAction.Stop => "stop",
        JustifiedAction.Pause => "pause",
        JustifiedAction.Delete => "delete",
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };

    public static bool TryParse(string? value, out JustifiedAction action)
    {
        switch (value)
        {
            case "stop": action = JustifiedAction.Stop; return true;
            case "pause": action = JustifiedAction.Pause; return true;
            case "delete": action = JustifiedAction.Delete; return true;
            default: action = default; return false;
        }
    }

    public static bool IsValidLength(string? text) => text?.Trim() is { Length: >= MinLength and <= MaxLength };
}
