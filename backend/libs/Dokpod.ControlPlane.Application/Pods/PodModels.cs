namespace Dokpod.ControlPlane.Application.Pods;

public sealed record PodMemberItem(string ContainerId, string Name, string Status, int Restarts, bool Infra);

/// <summary>Pod projetado de um host Podman (spec 021).</summary>
public sealed record PodInventoryItem(
    string PodId,
    string Name,
    string Status,
    DateTimeOffset? CreatedAtUtc,
    IReadOnlyDictionary<string, string> Labels,
    IReadOnlyList<string> Networks,
    string? InfraId,
    IReadOnlyList<PodMemberItem> Containers,
    bool AgentPod,
    bool Truncated)
{
    /// <summary>Situação normalizada: running, degraded, exited, paused, created ou error.</summary>
    public string NormalizedStatus => PodStatuses.Normalize(Status);

    public int RunningContainers => Containers.Count(c => !c.Infra && c.Status.StartsWith("running", StringComparison.OrdinalIgnoreCase));
}

public static class PodStatuses
{
    public static IReadOnlyList<string> All { get; } = ["running", "degraded", "exited", "paused", "created", "error"];

    public static string Normalize(string status) => status.ToLowerInvariant() switch
    {
        "running" => "running",
        "degraded" => "degraded",
        "exited" or "stopped" => "exited",
        "paused" => "paused",
        "created" => "created",
        _ => "error",
    };
}

public sealed record PodSnapshot(Guid EnvironmentId, ulong Revision, bool PodsSupported, DateTimeOffset ObservedAtUtc, IReadOnlyList<PodInventoryItem> Pods);

public sealed record PodSnapshotPage(string SnapshotId, ulong Revision, uint PageNumber, bool IsLastPage, bool PodsSupported, IReadOnlyList<PodInventoryItem> Pods);

public enum PodSort
{
    NameAsc,
    NameDesc,
    CreatedAtDesc,
    CreatedAtAsc,
    Environment,
}

public sealed record PodSearchQuery(
    IReadOnlyCollection<Guid> EnvironmentIds,
    IReadOnlyCollection<string> Statuses,
    string? ContainerId,
    string? Text,
    PodSort Sort,
    int Offset,
    int Limit);

public sealed record PodSearchRow(Guid EnvironmentId, PodInventoryItem Pod, DateTimeOffset ObservedAtUtc);

public sealed record PodSummary(int Total, IReadOnlyDictionary<string, int> ByStatus);

public sealed record PodSearchResult(IReadOnlyList<PodSearchRow> Rows, PodSummary Summary, bool HasMore);

/// <summary>Situação da projeção de pods por host: sem projeção (inventário indisponível), sem pods (Docker) ou com pods.</summary>
public sealed record PodProjectionState(Guid EnvironmentId, bool PodsSupported);

public interface IPodInventoryStore
{
    Task<bool> ReplaceSnapshotAsync(PodSnapshot snapshot, CancellationToken cancellationToken);

    Task<IReadOnlyList<PodProjectionState>> ListProjectionsAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken);

    Task<PodSearchResult> SearchAsync(PodSearchQuery query, CancellationToken cancellationToken);

    /// <summary>Pod pelo identificador (64 hex) ou pelo nome.</summary>
    Task<PodSearchRow?> GetAsync(Guid environmentId, string podIdOrName, CancellationToken cancellationToken);
}

/// <summary>Junta as páginas de um snapshot de pods; fora de ordem ou excesso invalida a sessão.</summary>
public sealed class PodSnapshotAccumulator(Guid environmentId, TimeProvider timeProvider, int maximumPages = 50, int maximumPods = 10_000)
{
    private readonly Dictionary<string, PodInventoryItem> pods = new(StringComparer.Ordinal);
    private string? snapshotId;
    private ulong revision;
    private uint nextPage;
    private bool supported;

    public PodSnapshot? AddPage(PodSnapshotPage page, out string? failureCode)
    {
        failureCode = null;
        if (page.PageNumber == 0)
        {
            pods.Clear();
            snapshotId = page.SnapshotId;
            revision = page.Revision;
            supported = page.PodsSupported;
            nextPage = 0;
        }

        if (snapshotId is null || page.PageNumber != nextPage || page.PageNumber >= maximumPages ||
            !string.Equals(snapshotId, page.SnapshotId, StringComparison.Ordinal) || revision != page.Revision ||
            !Guid.TryParseExact(page.SnapshotId, "D", out _) || page.Revision == 0 || supported != page.PodsSupported)
        {
            failureCode = "pod_snapshot_invalid";
            return null;
        }

        foreach (var pod in page.Pods)
        {
            if (!Dokpod.Domain.Pods.PodNames.IsValidId(pod.PodId) || !Dokpod.Domain.Pods.PodNames.IsValidName(pod.Name) ||
                pods.Count >= maximumPods || !pods.TryAdd(pod.PodId, pod))
            {
                failureCode = "pod_snapshot_invalid";
                return null;
            }
        }

        nextPage++;
        if (!page.IsLastPage) return null;
        var snapshot = new PodSnapshot(environmentId, revision, supported, timeProvider.GetUtcNow(), pods.Values.ToArray());
        pods.Clear();
        snapshotId = null;
        return snapshot;
    }
}
