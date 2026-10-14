using System.Security.Cryptography;
using System.Text;
using Dokpod.Agent.Application.Engines;
using Dokpod.Agent.Contracts.V1;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace Dokpod.Agent.Application.Images;

/// <summary>
/// Monta o snapshot do inventário de imagens (spec 012 T006, D-007): páginas de até 200 imagens e
/// 256 KiB, tags e digests limitados a 64 (excedente marcado como truncado) e revisão monotônica que só
/// avança quando o conjunto de imagens muda.
/// </summary>
public sealed class ImageInventoryBuilder(TimeProvider timeProvider)
{
    public const int MaxImagesPerPage = 200;
    public const int MaxPageBytes = 256 * 1024;
    public const int MaxNamesPerImage = 64;

    private string? fingerprint;
    private ulong revision;

    public ulong Revision => Interlocked.Read(ref revision);

    /// <summary>Páginas do snapshot, ou vazio quando nada mudou desde o último envio (e `force` é falso).</summary>
    public IReadOnlyList<ImageSnapshotPage> Build(IReadOnlyList<EngineImage> images, bool force)
    {
        var states = images.OrderBy(i => i.ImageId, StringComparer.Ordinal).Select(ToState).ToArray();
        var current = Fingerprint(states);
        if (!force && current == fingerprint) return [];
        if (current != fingerprint)
        {
            var clock = (ulong)timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
            Interlocked.Exchange(ref revision, Math.Max(Revision + 1, clock));
            fingerprint = current;
        }

        var snapshotId = Guid.NewGuid().ToString("D");
        var pages = new List<ImageSnapshotPage>();
        var page = NewPage(snapshotId, 0);
        foreach (var state in states)
        {
            if (page.Images.Count >= MaxImagesPerPage || page.CalculateSize() + state.CalculateSize() + 8 > MaxPageBytes)
            {
                pages.Add(page);
                page = NewPage(snapshotId, (uint)pages.Count);
            }

            page.Images.Add(state);
        }

        pages.Add(page);
        pages[^1].IsLastPage = true;
        return pages;
    }

    private ImageSnapshotPage NewPage(string snapshotId, uint number) =>
        new() { SnapshotId = snapshotId, ImageRevision = Revision, PageNumber = number };

    public static ImageState ToState(EngineImage image)
    {
        var state = new ImageState
        {
            ImageId = image.ImageId,
            SizeBytes = (ulong)Math.Max(0, image.SizeBytes),
            Truncated = image.RepoTags.Count > MaxNamesPerImage || image.RepoDigests.Count > MaxNamesPerImage,
        };
        state.RepoTags.AddRange(image.RepoTags.Take(MaxNamesPerImage));
        state.RepoDigests.AddRange(image.RepoDigests.Take(MaxNamesPerImage));
        if (image.CreatedAtUtc is { } created) state.CreatedAt = Timestamp.FromDateTimeOffset(created);
        return state;
    }

    private static string Fingerprint(IEnumerable<ImageState> states)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var state in states) hash.AppendData(state.ToByteArray());
        return Convert.ToHexString(hash.GetHashAndReset());
    }
}
