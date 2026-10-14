using Dokpod.ControlPlane.Application.Images;
using Dokpod.Domain.Images;
using Microsoft.EntityFrameworkCore;

namespace Dokpod.ControlPlane.Infrastructure;

public sealed class InventoryImageProjectionEntity
{
    public Guid EnvironmentId { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset ObservedAtUtc { get; set; }
}

public sealed class InventoryImageEntity
{
    public Guid EnvironmentId { get; set; }
    public string ImageId { get; set; } = string.Empty;
    public string[] RepoTags { get; set; } = [];
    public string[] RepoDigests { get; set; } = [];
    public long SizeBytes { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public string? Architecture { get; set; }
    public string? Os { get; set; }
    public bool Truncated { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
    public long Revision { get; set; }
}

/// <summary>Projeção do inventário de imagens por host (spec 012 T005/T008); "em uso" calculado na consulta por `image_id`.</summary>
public sealed class PostgresImageInventoryStore(ControlPlaneDbContext dbContext) : IImageInventoryStore
{
    public async Task<ImageSnapshotOutcome> ReplaceSnapshotAsync(ImageSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var revision = checked((long)snapshot.Revision);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var projection = await dbContext.Set<InventoryImageProjectionEntity>()
            .FromSqlInterpolated($"SELECT * FROM dokpod.inventory_image_projections WHERE environment_id = {snapshot.EnvironmentId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (projection is not null && projection.Revision > revision)
        {
            return ImageSnapshotOutcome.Stale;
        }

        if (projection is null)
        {
            dbContext.Add(new InventoryImageProjectionEntity
            {
                EnvironmentId = snapshot.EnvironmentId,
                Revision = revision,
                ObservedAtUtc = snapshot.ObservedAtUtc,
            });
        }
        else
        {
            projection.Revision = revision;
            projection.ObservedAtUtc = snapshot.ObservedAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM dokpod.inventory_images WHERE environment_id = {snapshot.EnvironmentId}",
            cancellationToken).ConfigureAwait(false);
        foreach (var image in snapshot.Images)
        {
            dbContext.Add(new InventoryImageEntity
            {
                EnvironmentId = snapshot.EnvironmentId,
                ImageId = image.ImageId,
                RepoTags = image.RepoTags.Take(64).Select(ImageNames.Normalize).Distinct(StringComparer.Ordinal).ToArray(),
                RepoDigests = image.RepoDigests.Take(64).Select(ImageNames.Normalize).Distinct(StringComparer.Ordinal).ToArray(),
                SizeBytes = Math.Max(0, image.SizeBytes),
                CreatedAt = image.CreatedAtUtc,
                Architecture = Limit(image.Architecture),
                Os = Limit(image.Os),
                Truncated = image.Truncated,
                ObservedAt = snapshot.ObservedAtUtc,
                Revision = revision,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        dbContext.ChangeTracker.Clear();
        return ImageSnapshotOutcome.Accepted;
    }

    public async Task<ulong?> GetRevisionAsync(Guid environmentId, CancellationToken cancellationToken)
    {
        var revision = await dbContext.Set<InventoryImageProjectionEntity>()
            .AsNoTracking()
            .Where(projection => projection.EnvironmentId == environmentId)
            .Select(projection => (long?)projection.Revision)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return revision is null ? null : (ulong)revision.Value;
    }

    public async Task<ImageSearchResult> SearchAsync(ImageSearchQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var ids = query.EnvironmentIds.ToArray();
        var summary = await SummarizeAsync(ids, cancellationToken).ConfigureAwait(false);
        if (ids.Length == 0)
        {
            return new ImageSearchResult([], summary, false);
        }

        var text = string.IsNullOrWhiteSpace(query.Text) ? null : "%" + EscapeLike(query.Text.Trim().ToLowerInvariant()) + "%";
        var order = query.Sort switch
        {
            ImageSort.NameDesc => "sort_name DESC, \"EnvironmentId\", \"ImageId\"",
            ImageSort.SizeDesc => "\"SizeBytes\" DESC, \"EnvironmentId\", \"ImageId\"",
            ImageSort.SizeAsc => "\"SizeBytes\" ASC, \"EnvironmentId\", \"ImageId\"",
            ImageSort.CreatedAtDesc => "\"CreatedAt\" DESC NULLS LAST, \"EnvironmentId\", \"ImageId\"",
            ImageSort.CreatedAtAsc => "\"CreatedAt\" ASC NULLS LAST, \"EnvironmentId\", \"ImageId\"",
            _ => "sort_name ASC, \"EnvironmentId\", \"ImageId\"",
        };
        // `order` vem de um conjunto fechado; valores do usuário seguem como parâmetros.
#pragma warning disable EF1002
        var rows = await dbContext.Database.SqlQueryRaw<ImageRow>(
            $"""
            SELECT * FROM (
                SELECT i.environment_id AS "EnvironmentId", i.image_id AS "ImageId", i.repo_tags AS "RepoTags",
                       i.repo_digests AS "RepoDigests", i.size_bytes AS "SizeBytes", i.created_at AS "CreatedAt",
                       i.architecture AS "Architecture", i.os AS "Os", i.truncated AS "Truncated", i.observed_at AS "ObservedAt",
                       (SELECT count(*)::int FROM dokpod.inventory_containers c
                         WHERE c.environment_id = i.environment_id AND c.image_id = i.image_id) AS "Containers",
                       coalesce(i.repo_tags[1], '~' || i.image_id) AS sort_name
                FROM dokpod.inventory_images i
                WHERE i.environment_id = ANY({"{0}"})
                  AND ({"{1}"}::text IS NULL OR lower(array_to_string(i.repo_tags, ' ') || ' ' || i.image_id) LIKE {"{1}"} ESCAPE '\')
            ) q
            WHERE NOT {"{2}"} OR "Containers" = 0
            ORDER BY {order}
            OFFSET {"{3}"} LIMIT {"{4}"}
            """,
            ids, (object?)text ?? DBNull.Value, query.UnusedOnly, query.Offset, query.Limit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
#pragma warning restore EF1002
        return new ImageSearchResult(
            rows.Take(query.Limit).Select(row => new ImageSearchRow(
                row.EnvironmentId,
                new ImageInventoryItem(row.ImageId, row.RepoTags, row.RepoDigests, row.SizeBytes, row.CreatedAt, row.Architecture, row.Os, row.Truncated),
                row.Containers,
                row.ObservedAt)).ToArray(),
            summary,
            rows.Count > query.Limit);
    }

    public async Task<ImageSummary> SummarizeAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken)
    {
        var ids = environmentIds.ToArray();
        if (ids.Length == 0)
        {
            return new ImageSummary(0, 0, 0, false);
        }

        var row = await dbContext.Database.SqlQuery<SummaryRow>($"""
            SELECT
                count(i.image_id)::int AS "Total",
                count(i.image_id) FILTER (WHERE NOT EXISTS (
                    SELECT 1 FROM dokpod.inventory_containers c
                    WHERE c.environment_id = i.environment_id AND c.image_id = i.image_id))::int AS "Unused",
                coalesce(sum(i.size_bytes), 0)::bigint AS "TotalSizeBytes",
                (SELECT count(*)::int FROM unnest({ids}) AS e(id)
                  WHERE NOT EXISTS (SELECT 1 FROM dokpod.inventory_image_projections p WHERE p.environment_id = e.id)
                     OR EXISTS (SELECT 1 FROM dokpod.inventory_containers c WHERE c.environment_id = e.id AND c.image_id IS NULL)) AS "PartialHosts"
            FROM dokpod.inventory_images i
            WHERE i.environment_id = ANY({ids})
            """).SingleAsync(cancellationToken).ConfigureAwait(false);
        return new ImageSummary(row.Total, row.Unused, row.TotalSizeBytes, row.PartialHosts > 0);
    }

    public async Task<ImageLookupResult> LookupAsync(Guid environmentId, ImageReference reference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var tag = reference.Digest is null ? reference.Canonical : null;
        var digest = reference.Digest is null ? null : $"{reference.Name}@{reference.Digest}";
        var images = await dbContext.Set<InventoryImageEntity>()
            .AsNoTracking()
            .Where(image => image.EnvironmentId == environmentId &&
                ((tag != null && image.RepoTags.Contains(tag)) || (digest != null && image.RepoDigests.Contains(digest))))
            .OrderByDescending(image => image.CreatedAt)
            .Take(1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (images.Count == 0)
        {
            return new ImageLookupResult(false, null, null, null, null);
        }

        var found = images[0];
        var resolved = found.RepoDigests
            .Select(d => ImageReference.TryParse(d, out var parsed) ? parsed : null)
            .FirstOrDefault(parsed => parsed is not null && parsed.Name == reference.Name)?.Digest;
        return new ImageLookupResult(true, found.ImageId, found.SizeBytes, found.CreatedAt, resolved ?? reference.Digest);
    }

    private static string? Limit(string? value) => string.IsNullOrWhiteSpace(value) ? null : value[..Math.Min(value.Length, 32)];

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);

    private sealed class ImageRow
    {
        public Guid EnvironmentId { get; set; }
        public string ImageId { get; set; } = string.Empty;
        public string[] RepoTags { get; set; } = [];
        public string[] RepoDigests { get; set; } = [];
        public long SizeBytes { get; set; }
        public DateTimeOffset? CreatedAt { get; set; }
        public string? Architecture { get; set; }
        public string? Os { get; set; }
        public bool Truncated { get; set; }
        public DateTimeOffset ObservedAt { get; set; }
        public int Containers { get; set; }
    }

    private sealed class SummaryRow
    {
        public int Total { get; set; }
        public int Unused { get; set; }
        public long TotalSizeBytes { get; set; }
        public int PartialHosts { get; set; }
    }
}
