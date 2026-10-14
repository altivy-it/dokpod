using Dokpod.Domain.Images;

namespace Dokpod.ControlPlane.Application.Images;

/// <summary>Imagem projetada de um host (spec 012); tags e digests já normalizados na ingestão.</summary>
public sealed record ImageInventoryItem(
    string ImageId,
    IReadOnlyList<string> RepoTags,
    IReadOnlyList<string> RepoDigests,
    long SizeBytes,
    DateTimeOffset? CreatedAtUtc,
    string? Architecture,
    string? Os,
    bool Truncated);

public sealed record ImageSnapshot(Guid EnvironmentId, ulong Revision, DateTimeOffset ObservedAtUtc, IReadOnlyList<ImageInventoryItem> Images);

public enum ImageSort
{
    NameAsc,
    NameDesc,
    SizeDesc,
    SizeAsc,
    CreatedAtDesc,
    CreatedAtAsc,
}

public sealed record ImageSearchQuery(
    IReadOnlyCollection<Guid> EnvironmentIds,
    bool UnusedOnly,
    string? Text,
    ImageSort Sort,
    int Offset,
    int Limit);

public sealed record ImageSearchRow(Guid EnvironmentId, ImageInventoryItem Image, int Containers, DateTimeOffset ObservedAtUtc);

/// <summary>`Partial` = há host sem inventário de imagens ou com containers sem `image_id` (agente N-1, D-006).</summary>
public sealed record ImageSummary(int Total, int Unused, long TotalSizeBytes, bool Partial);

public sealed record ImageSearchResult(IReadOnlyList<ImageSearchRow> Rows, ImageSummary Summary, bool HasMore);

public sealed record ImageLookupResult(bool Present, string? ImageId, long? SizeBytes, DateTimeOffset? CreatedAtUtc, string? Digest);

public enum ImageSnapshotOutcome
{
    Accepted,
    Stale,
}

public interface IImageInventoryStore
{
    Task<ImageSnapshotOutcome> ReplaceSnapshotAsync(ImageSnapshot snapshot, CancellationToken cancellationToken);

    Task<ulong?> GetRevisionAsync(Guid environmentId, CancellationToken cancellationToken);

    Task<ImageSearchResult> SearchAsync(ImageSearchQuery query, CancellationToken cancellationToken);

    Task<ImageSummary> SummarizeAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken);

    Task<ImageLookupResult> LookupAsync(Guid environmentId, ImageReference reference, CancellationToken cancellationToken);
}

public static class ImageNames
{
    /// <summary>Forma canônica de uma tag ou digest informado pelo engine; mantém o texto original se fora da gramática.</summary>
    public static string Normalize(string value) =>
        ImageReference.TryParse(value, out var reference) ? reference.Canonical : value;
}
