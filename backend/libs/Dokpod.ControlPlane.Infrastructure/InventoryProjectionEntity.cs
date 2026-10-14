namespace Dokpod.ControlPlane.Infrastructure;

public sealed class InventoryProjectionEntity
{
    public Guid EnvironmentId { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset ObservedAtUtc { get; set; }
    public List<InventoryContainerEntity> Containers { get; set; } = [];
}

public sealed class InventoryContainerEntity
{
    public Guid EnvironmentId { get; set; }
    public string ContainerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ImageReference { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Revision { get; set; } = string.Empty;
    public DateTimeOffset ObservedAtUtc { get; set; }
    public DateTimeOffset? CreatedAtUtc { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public string? Health { get; set; }
    public int? RestartCount { get; set; }
    public int? ExitCode { get; set; }
    public bool? OomKilled { get; set; }
    public string? Ports { get; set; }
    public string? ImageId { get; set; }
    public string DisplayStatus { get; set; } = "stopped";
    public InventoryProjectionEntity Projection { get; set; } = null!;
}