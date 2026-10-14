namespace Dokpod.ControlPlane.Application.Images;

public sealed record ImageSnapshotPage(string SnapshotId, ulong Revision, uint PageNumber, bool IsLastPage, IReadOnlyList<ImageInventoryItem> Images);

/// <summary>Junta as páginas de um snapshot de imagens (spec 012 D-007); fora de ordem ou excesso invalida a sessão.</summary>
public sealed class ImageSnapshotAccumulator(Guid environmentId, TimeProvider timeProvider, int maximumPages = 200, int maximumImages = 20_000)
{
    private readonly Dictionary<string, ImageInventoryItem> images = new(StringComparer.Ordinal);
    private string? snapshotId;
    private ulong revision;
    private uint nextPage;

    /// <summary>Snapshot completo na última página; nulo enquanto faltam páginas.</summary>
    public ImageSnapshot? AddPage(ImageSnapshotPage page, out string? failureCode)
    {
        failureCode = null;
        if (page.PageNumber == 0)
        {
            images.Clear();
            snapshotId = page.SnapshotId;
            revision = page.Revision;
            nextPage = 0;
        }

        if (snapshotId is null || page.PageNumber != nextPage || page.PageNumber >= maximumPages ||
            !string.Equals(snapshotId, page.SnapshotId, StringComparison.Ordinal) || revision != page.Revision ||
            !Guid.TryParseExact(page.SnapshotId, "D", out _) || page.Revision == 0)
        {
            failureCode = "image_snapshot_invalid";
            return null;
        }

        foreach (var image in page.Images)
        {
            if (string.IsNullOrWhiteSpace(image.ImageId) || image.ImageId.Length > 160 ||
                images.Count >= maximumImages || !images.TryAdd(image.ImageId, image))
            {
                failureCode = "image_snapshot_invalid";
                return null;
            }
        }

        nextPage++;
        if (!page.IsLastPage)
        {
            return null;
        }

        var snapshot = new ImageSnapshot(environmentId, revision, timeProvider.GetUtcNow(), images.Values.ToArray());
        images.Clear();
        snapshotId = null;
        return snapshot;
    }
}
