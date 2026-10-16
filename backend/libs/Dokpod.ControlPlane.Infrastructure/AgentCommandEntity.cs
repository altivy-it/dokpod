using Dokpod.ControlPlane.Application.Commands;
using Dokpod.Domain.Commands;

namespace Dokpod.ControlPlane.Infrastructure;

public sealed class AgentCommandEntity
{
    public Guid EnvironmentId { get; set; }
    public Guid CommandId { get; set; }
    public AgentCommandKind Kind { get; set; }
    /// <summary>Nulo para comandos com alvo de host (spec 012).</summary>
    public string? ContainerId { get; set; }
    public string? ExpectedContainerRevision { get; set; }
    public string PayloadHash { get; set; } = string.Empty;
    public DateTimeOffset DeadlineUtc { get; set; }
    public long FencingToken { get; set; }
    public long? LastDispatchFencingToken { get; set; }
    public ControlPlaneCommandState State { get; set; }
    public string? FailureCode { get; set; }
    public string? ObservedContainerRevision { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string? ResolvedImageDigest { get; set; }
    public long? ImageSizeBytes { get; set; }
    public string? CreatedContainerId { get; set; }

    /// <summary>Membros do pod que falharam na ação (spec 021), separados por vírgula.</summary>
    public string? FailedContainers { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
/// <summary>Parâmetros de comandos com alvo de host (spec 012 D-001), particionados com o comando.</summary>
public sealed class AgentCommandParametersEntity
{
    public Guid EnvironmentId { get; set; }
    public Guid CommandId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Parameters { get; set; } = "{}";
    public byte[]? ProtectedValues { get; set; }
    public DateTimeOffset? PurgedAt { get; set; }
}
