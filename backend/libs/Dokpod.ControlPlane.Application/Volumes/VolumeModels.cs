namespace Dokpod.ControlPlane.Application.Volumes;

/// <summary>Container que monta o volume (spec 018 D-002); sem o caminho do volume no host.</summary>
public sealed record VolumeMount(string ContainerId, string ContainerName, string Destination, bool ReadOnly);

/// <summary>Volume projetado de um host.</summary>
public sealed record VolumeInventoryItem(
    string Name,
    string Driver,
    string Scope,
    DateTimeOffset? CreatedAtUtc,
    IReadOnlyDictionary<string, string> Labels,
    IReadOnlyList<VolumeMount> Mounts,
    bool Truncated)
{
    public bool Anonymous => Dokpod.Domain.Volumes.VolumeNames.IsAnonymous(Name);
}

public sealed record VolumeSnapshot(Guid EnvironmentId, ulong Revision, DateTimeOffset ObservedAtUtc, IReadOnlyList<VolumeInventoryItem> Volumes);

public sealed record VolumeSnapshotPage(string SnapshotId, ulong Revision, uint PageNumber, bool IsLastPage, IReadOnlyList<VolumeInventoryItem> Volumes);

public enum VolumeSnapshotOutcome
{
    Accepted,
    Stale,
}

public enum VolumeSort
{
    NameAsc,
    NameDesc,
    CreatedAtDesc,
    CreatedAtAsc,
    Environment,
}

public sealed record VolumeCursor(int Offset);

public sealed record VolumeSearchQuery(
    IReadOnlyCollection<Guid> EnvironmentIds,
    bool UnusedOnly,
    bool? Anonymous,
    string? Text,
    VolumeSort Sort,
    int Offset,
    int Limit);

public sealed record VolumeSearchRow(Guid EnvironmentId, VolumeInventoryItem Volume, DateTimeOffset ObservedAtUtc);

public sealed record VolumeSummary(int Total, int Unused);

public sealed record VolumeSearchResult(IReadOnlyList<VolumeSearchRow> Rows, VolumeSummary Summary, bool HasMore);

public interface IVolumeInventoryStore
{
    Task<VolumeSnapshotOutcome> ReplaceSnapshotAsync(VolumeSnapshot snapshot, CancellationToken cancellationToken);

    Task<ulong?> GetRevisionAsync(Guid environmentId, CancellationToken cancellationToken);

    /// <summary>Hosts com projeção de volumes (os demais aparecem como "inventário indisponível").</summary>
    Task<IReadOnlySet<Guid>> ListProjectedAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken);

    Task<VolumeSearchResult> SearchAsync(VolumeSearchQuery query, CancellationToken cancellationToken);

    Task<VolumeSearchRow?> GetAsync(Guid environmentId, string name, CancellationToken cancellationToken);
}

/// <summary>Junta as páginas de um snapshot de volumes; fora de ordem ou excesso invalida a sessão (como as imagens).</summary>
public sealed class VolumeSnapshotAccumulator(Guid environmentId, TimeProvider timeProvider, int maximumPages = 100, int maximumVolumes = 50_000)
{
    private readonly Dictionary<string, VolumeInventoryItem> volumes = new(StringComparer.Ordinal);
    private string? snapshotId;
    private ulong revision;
    private uint nextPage;

    public VolumeSnapshot? AddPage(VolumeSnapshotPage page, out string? failureCode)
    {
        failureCode = null;
        if (page.PageNumber == 0)
        {
            volumes.Clear();
            snapshotId = page.SnapshotId;
            revision = page.Revision;
            nextPage = 0;
        }

        if (snapshotId is null || page.PageNumber != nextPage || page.PageNumber >= maximumPages ||
            !string.Equals(snapshotId, page.SnapshotId, StringComparison.Ordinal) || revision != page.Revision ||
            !Guid.TryParseExact(page.SnapshotId, "D", out _) || page.Revision == 0)
        {
            failureCode = "volume_snapshot_invalid";
            return null;
        }

        foreach (var volume in page.Volumes)
        {
            if (!Dokpod.Domain.Volumes.VolumeNames.IsValid(volume.Name) || volumes.Count >= maximumVolumes || !volumes.TryAdd(volume.Name, volume))
            {
                failureCode = "volume_snapshot_invalid";
                return null;
            }
        }

        nextPage++;
        if (!page.IsLastPage) return null;
        var snapshot = new VolumeSnapshot(environmentId, revision, timeProvider.GetUtcNow(), volumes.Values.ToArray());
        volumes.Clear();
        snapshotId = null;
        return snapshot;
    }
}
