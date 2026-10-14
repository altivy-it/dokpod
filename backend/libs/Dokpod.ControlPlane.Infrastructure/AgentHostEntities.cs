namespace Dokpod.ControlPlane.Infrastructure;

public sealed class AgentDescriptorEntity
{
    public Guid EnvironmentId { get; set; }
    public string EngineKind { get; set; } = string.Empty;
    public string EngineVersion { get; set; } = string.Empty;
    public string EngineApiVersion { get; set; } = string.Empty;
    public bool? EngineRootless { get; set; }
    public string OperatingSystem { get; set; } = string.Empty;
    public string OperatingSystemVersion { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string AgentVersion { get; set; } = string.Empty;
    public string Capabilities { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public int? CpuCount { get; set; }
    public long? MemoryBytes { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class AgentConnectionEntity
{
    public Guid EnvironmentId { get; set; }
    public bool Connected { get; set; }
    public DateTimeOffset? SessionStartedAtUtc { get; set; }
    public DateTimeOffset? LastSeenAtUtc { get; set; }
    public DateTimeOffset? DisconnectedAtUtc { get; set; }
}

public sealed class EnvironmentEventEntity
{
    public Guid EventId { get; set; }
    public Guid EnvironmentId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public string? SubjectKind { get; set; }
    public string? SubjectId { get; set; }
    public string? SubjectName { get; set; }
    public string Parameters { get; set; } = "{}";
    public int AggregatedCount { get; set; } = 1;
    public string? SourceKey { get; set; }
}
