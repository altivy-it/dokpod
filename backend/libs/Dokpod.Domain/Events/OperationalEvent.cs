using System.Collections.Frozen;

namespace Dokpod.Domain.Events;

public static class OperationalEventTypes
{
    public const string ContainerDied = "container.died";
    public const string ContainerOomKilled = "container.oom_killed";
    public const string ContainerUnhealthy = "container.unhealthy";
    public const string ContainerRecovered = "container.recovered";
    public const string ContainerAutoRestarted = "container.auto_restarted";
    public const string ContainerCreated = "container.created";
    public const string ContainerRemoved = "container.removed";
    public const string ImagePulled = "image.pulled";
    public const string ImageRemoved = "image.removed";
    public const string VolumeRemoved = "volume.removed";
    public const string EngineVersionOutdated = "engine.version_outdated";
    public const string AgentConnected = "agent.connected";
    public const string AgentDisconnected = "agent.disconnected";
    public const string CommandSucceeded = "command.succeeded";
    public const string CommandFailed = "command.failed";
    public const string CommandIndeterminate = "command.indeterminate";
    public const string EventsDropped = "events.dropped";
    public const string ContainerExecOrphanSuspected = "container.exec_orphan_suspected";

    /// <summary>Severidade por tipo (spec 008 D-002).</summary>
    public static FrozenDictionary<string, string> Severity { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [ContainerDied] = OperationalEventSeverities.Error,
        [ContainerOomKilled] = OperationalEventSeverities.Error,
        [ContainerUnhealthy] = OperationalEventSeverities.Warning,
        [ContainerRecovered] = OperationalEventSeverities.Success,
        [ContainerAutoRestarted] = OperationalEventSeverities.Warning,
        [ContainerCreated] = OperationalEventSeverities.Info,
        [ContainerRemoved] = OperationalEventSeverities.Neutral,
        [ImagePulled] = OperationalEventSeverities.Success,
        [ImageRemoved] = OperationalEventSeverities.Neutral,
        [VolumeRemoved] = OperationalEventSeverities.Neutral,
        [EngineVersionOutdated] = OperationalEventSeverities.Warning,
        [AgentConnected] = OperationalEventSeverities.Info,
        [AgentDisconnected] = OperationalEventSeverities.Warning,
        [CommandSucceeded] = OperationalEventSeverities.Success,
        [CommandFailed] = OperationalEventSeverities.Error,
        [CommandIndeterminate] = OperationalEventSeverities.Warning,
        [EventsDropped] = OperationalEventSeverities.Warning,
        [ContainerExecOrphanSuspected] = OperationalEventSeverities.Warning,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Atributos aceitos por tipo; labels e variáveis nunca são copiados.</summary>
    public static FrozenSet<string> AllowedParameters { get; } = new HashSet<string>(
        ["name", "image", "exitCode", "action", "engine", "version", "minimumVersion", "droppedCount", "commandId", "failureCode"],
        StringComparer.Ordinal).ToFrozenSet(StringComparer.Ordinal);
}

public static class OperationalEventSeverities
{
    public const string Success = "success";
    public const string Info = "info";
    public const string Warning = "warning";
    public const string Error = "error";
    public const string Neutral = "neutral";

    public static bool IsAttention(string severity) => severity is Warning or Error;

    public static FrozenSet<string> All { get; } = new HashSet<string>([Success, Info, Warning, Error, Neutral], StringComparer.Ordinal)
        .ToFrozenSet(StringComparer.Ordinal);
}

public sealed record OperationalEvent(
    Guid EventId,
    Guid EnvironmentId,
    string Type,
    string Severity,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset ReceivedAtUtc,
    string? SubjectKind,
    string? SubjectId,
    string? SubjectName,
    IReadOnlyDictionary<string, string> Parameters,
    int AggregatedCount,
    string? SourceKey)
{
    public const int MaxTextLength = 255;
    public const int MaxParameterValueLength = 256;
    public const int MaxParametersBytes = 2048;
    public static readonly TimeSpan ClockSkewThreshold = TimeSpan.FromMinutes(5);

    public bool ClockSkewDetected => (ReceivedAtUtc - OccurredAtUtc).Duration() > ClockSkewThreshold;

    /// <summary>Cria um evento sanitizado: tipo da allowlist, textos truncados e parâmetros filtrados.</summary>
    public static OperationalEvent Create(
        Guid environmentId,
        string type,
        DateTimeOffset occurredAtUtc,
        DateTimeOffset receivedAtUtc,
        string? subjectKind,
        string? subjectId,
        string? subjectName,
        IReadOnlyDictionary<string, string>? parameters,
        int aggregatedCount = 1,
        string? sourceKey = null)
    {
        if (environmentId == Guid.Empty) throw new ArgumentException("Environment ID is required.", nameof(environmentId));
        if (!OperationalEventTypes.Severity.TryGetValue(type, out var severity))
        {
            throw new ArgumentException("Unsupported event type.", nameof(type));
        }

        var sanitized = new Dictionary<string, string>(StringComparer.Ordinal);
        var bytes = 0;
        foreach (var (key, value) in parameters ?? new Dictionary<string, string>())
        {
            if (!OperationalEventTypes.AllowedParameters.Contains(key) || value is null) continue;
            var clean = Clean(value, MaxParameterValueLength);
            bytes += key.Length + clean.Length;
            if (bytes > MaxParametersBytes) break;
            sanitized[key] = clean;
        }

        return new OperationalEvent(
            Guid.NewGuid(),
            environmentId,
            type,
            severity,
            occurredAtUtc.ToUniversalTime(),
            receivedAtUtc.ToUniversalTime(),
            subjectKind,
            subjectId is null ? null : Clean(subjectId, MaxTextLength),
            subjectName is null ? null : Clean(subjectName, MaxTextLength),
            sanitized,
            Math.Max(1, aggregatedCount),
            sourceKey is null ? null : Clean(sourceKey, MaxTextLength));
    }

    private static string Clean(string value, int maxLength)
    {
        var chars = value.Where(c => !char.IsControl(c)).ToArray();
        var text = new string(chars).Trim();
        return text.Length <= maxLength ? text : text[..maxLength];
    }
}
