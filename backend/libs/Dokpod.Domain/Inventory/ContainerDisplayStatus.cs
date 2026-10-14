namespace Dokpod.Domain.Inventory;

public enum ContainerDisplayStatus
{
    Running,
    Starting,
    Failed,
    Restarting,
    Paused,
    Stopped,
}

public static class ContainerDisplayStatuses
{
    public static string ToValue(ContainerDisplayStatus status) => status switch
    {
        ContainerDisplayStatus.Running => "running",
        ContainerDisplayStatus.Starting => "starting",
        ContainerDisplayStatus.Failed => "failed",
        ContainerDisplayStatus.Restarting => "restarting",
        ContainerDisplayStatus.Paused => "paused",
        ContainerDisplayStatus.Stopped => "stopped",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    public static bool TryParse(string? value, out ContainerDisplayStatus status)
    {
        foreach (var candidate in Enum.GetValues<ContainerDisplayStatus>())
        {
            if (ToValue(candidate) == value)
            {
                status = candidate;
                return true;
            }
        }

        status = default;
        return false;
    }

    /// <summary>
    /// Mapeamento de FR-006 (spec 008). O estado bruto aceita o nome do enum do protocolo
    /// ("Running") ou o valor do engine ("running").
    /// </summary>
    public static ContainerDisplayStatus Evaluate(string? state, ContainerHealth? health, int? exitCode, bool? oomKilled)
    {
        switch (NormalizeState(state))
        {
            case "running":
                return health switch
                {
                    ContainerHealth.Starting => ContainerDisplayStatus.Starting,
                    ContainerHealth.Unhealthy => ContainerDisplayStatus.Failed,
                    _ => ContainerDisplayStatus.Running,
                };
            case "restarting":
                return ContainerDisplayStatus.Restarting;
            case "paused":
                return ContainerDisplayStatus.Paused;
            case "dead":
                return ContainerDisplayStatus.Failed;
            case "exited":
                return oomKilled == true || (exitCode is not null and not 0)
                    ? ContainerDisplayStatus.Failed
                    : ContainerDisplayStatus.Stopped;
            default:
                return ContainerDisplayStatus.Stopped;
        }
    }

    public static string NormalizeState(string? state) =>
        (state ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "running" => "running",
            "restarting" => "restarting",
            "paused" => "paused",
            "dead" => "dead",
            "exited" => "exited",
            "created" => "created",
            _ => "unknown",
        };
}
