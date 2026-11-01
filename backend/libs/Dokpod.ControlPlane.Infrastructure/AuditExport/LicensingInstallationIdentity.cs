using Dokpod.ControlPlane.Application.AuditExport;
using Dokpod.ControlPlane.Infrastructure.Licensing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using static Dokpod.ControlPlane.Infrastructure.AuditExport.AuditExportSql;

namespace Dokpod.ControlPlane.Infrastructure.AuditExport;

/// <summary>
/// <c>installationId</c> do registro exportado (D-010): o mesmo da linha única de <c>licensing_state</c>, criada aqui
/// se a composição Business ainda não gravou nada. O instante confiável nasce na época Unix, como na primeira
/// importação sem documento, para não inventar relógio confiável.
/// </summary>
public sealed class LicensingInstallationIdentity(ControlPlaneDbContext dbContext) : IAuditExportInstallation
{
    private Guid? cached;

    public async Task<Guid> GetIdAsync(CancellationToken cancellationToken)
    {
        if (cached is { } known) return known;
        var existing = await QueryAsync(dbContext, "SELECT installation_id FROM dokpod.licensing_state", [], reader => reader.GetGuid(0), cancellationToken).ConfigureAwait(false);
        if (existing.Count == 0)
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await LicensingStateRow.EnsureAsync(dbContext.Database.GetDbConnection(), transaction.GetDbTransaction(), DateTimeOffset.UnixEpoch, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            existing = await QueryAsync(dbContext, "SELECT installation_id FROM dokpod.licensing_state", [], reader => reader.GetGuid(0), cancellationToken).ConfigureAwait(false);
        }

        cached = existing[0];
        return existing[0];
    }
}
