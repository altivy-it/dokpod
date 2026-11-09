using Dokpod.ControlPlane.Application.Agents;
using Microsoft.EntityFrameworkCore;

namespace Dokpod.ControlPlane.Infrastructure;

public sealed class PostgresAgentIdentityRegistry(ControlPlaneDbContext dbContext) : IAgentIdentityRegistry, IAgentIdentityEnrollment
{
    public async ValueTask<AgentIdentity?> FindByFingerprintAsync(
        string certificateFingerprint,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(certificateFingerprint))
        {
            return null;
        }

        var normalizedFingerprint = certificateFingerprint.Trim().ToUpperInvariant();
        var identity = await dbContext.AgentIdentities
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.CertificateFingerprint == normalizedFingerprint &&
                    candidate.RevokedAtUtc == null,
                cancellationToken)
            .ConfigureAwait(false);

        return identity is null
            ? null
            : new AgentIdentity(identity.EnvironmentId, identity.CertificateFingerprint);
    }

    public async Task<bool> RevokeEnvironmentAsync(
        Guid environmentId,
        DateTimeOffset revokedAtUtc,
        CancellationToken cancellationToken)
    {
        var identities = await dbContext.AgentIdentities
            .Where(identity => identity.EnvironmentId == environmentId && identity.RevokedAtUtc == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (identities.Count == 0)
        {
            return false;
        }

        foreach (var identity in identities)
        {
            identity.RevokedAtUtc = revokedAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>Spec 033 (D-004): o mesmo par não muda nada; fingerprint de outro ambiente ou revogado é recusado.</summary>
    public async Task<AgentIdentityEnrollmentOutcome> RegisterAsync(
        Guid environmentId,
        string certificateFingerprint,
        CancellationToken cancellationToken)
    {
        var fingerprint = certificateFingerprint.Trim().ToUpperInvariant();
        var existing = await dbContext.AgentIdentities
            .AsNoTracking()
            .SingleOrDefaultAsync(identity => identity.CertificateFingerprint == fingerprint, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.RevokedAtUtc is not null ? AgentIdentityEnrollmentOutcome.Revoked
                : existing.EnvironmentId == environmentId ? AgentIdentityEnrollmentOutcome.Unchanged
                : AgentIdentityEnrollmentOutcome.BoundToAnotherEnvironment;
        }

        dbContext.AgentIdentities.Add(new AgentIdentityEntity { EnvironmentId = environmentId, CertificateFingerprint = fingerprint });
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return AgentIdentityEnrollmentOutcome.Created;
    }
}
