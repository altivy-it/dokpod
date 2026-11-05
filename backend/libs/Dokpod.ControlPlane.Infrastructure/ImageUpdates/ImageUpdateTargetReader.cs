using Dokpod.ControlPlane.Application.ImageUpdates;
using Dokpod.Domain.Images;
using static Dokpod.ControlPlane.Infrastructure.AuditExport.AuditExportSql;

namespace Dokpod.ControlPlane.Infrastructure.ImageUpdates;

/// <summary>
/// Referências em uso por host (spec 025 T013): containers da projeção do inventário, com a imagem local
/// (<c>image_id</c>) e a plataforma da imagem no inventário de imagens ou, sem ela, a do agente.
/// </summary>
public sealed class ImageUpdateTargetReader(ControlPlaneDbContext dbContext) : IImageUpdateTargetReader
{
    public async Task<IReadOnlyList<ImageUpdateTarget>> ListAsync(Guid environmentId, CancellationToken cancellationToken) =>
        await QueryAsync(
            dbContext,
            """
            SELECT DISTINCT ON (c.image_reference)
                   c.image_reference,
                   c.image_id,
                   COALESCE(NULLIF(i.os, ''), d.operating_system, 'linux'),
                   COALESCE(NULLIF(i.architecture, ''), d.architecture, 'amd64')
            FROM dokpod.inventory_containers c
            LEFT JOIN dokpod.inventory_images i ON i.environment_id = c.environment_id AND i.image_id = c.image_id
            LEFT JOIN dokpod.agent_descriptors d ON d.environment_id = c.environment_id
            WHERE c.environment_id = @id
            ORDER BY c.image_reference, c.image_id NULLS LAST
            LIMIT @limit
            """,
            [P("id", environmentId), P("limit", ImageUpdateLimits.MaxItems)],
            reader =>
            {
                var platform = ImagePlatform.Of(reader.GetString(2).ToLowerInvariant(), reader.GetString(3).ToLowerInvariant());
                return new ImageUpdateTarget(
                    reader.GetString(0),
                    reader.IsDBNull(1) ? null : reader.GetString(1),
                    ImagePlatform.IsValid(platform) ? platform : "linux/amd64");
            },
            cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<string, string>> LocalImageIdsAsync(Guid environmentId, CancellationToken cancellationToken)
    {
        var images = await QueryAsync(
            dbContext,
            "SELECT image_id, repo_tags FROM dokpod.inventory_images WHERE environment_id = @id",
            [P("id", environmentId)],
            reader => (ImageId: reader.GetString(0), Tags: reader.GetFieldValue<string[]>(1)),
            cancellationToken).ConfigureAwait(false);
        var byReference = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (imageId, tags) in images)
        {
            foreach (var tag in tags)
            {
                if (ImageReference.TryParse(tag, out var reference)) byReference[reference.Canonical] = imageId;
            }
        }

        return byReference;
    }
}
