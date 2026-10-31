using System.Text.Json.Nodes;
using Dokpod.Licensing;

namespace Dokpod.ControlPlane.Application.Licensing;

/// <summary>
/// Importação do pacote de revogação (spec 031 T052, D-011): verificado offline, da mesma organização da licença ativa
/// e com sequência maior que a já importada. Aceito, restringe só os recursos Business da licença coberta; CE e
/// workloads seguem intactos (FR-003, FR-021). Recusa grava hash, código e autoria, como na licença.
/// </summary>
public sealed class RevocationImportService(
    ILicenseStore store,
    ILicenseStateReader stateReader,
    LicenseStatusService statusService,
    TrustStore trustStore,
    TimeProvider timeProvider)
{
    public const string OrganizationMismatch = "organization_mismatch";
    public const string SequenceNotIncreasing = "sequence_not_increasing";

    public async Task<LicenseImportResult> ImportAsync(byte[] content, string actorId, Guid correlationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        var now = timeProvider.GetUtcNow();
        var verification = RevocationVerifier.Verify(content, trustStore);
        if (!verification.IsValid)
        {
            return await RejectAsync(verification, null, LicenseImportCodes.Invalid, verification.Failure!.Value.ToCode(), actorId, correlationId, now, cancellationToken).ConfigureAwait(false);
        }

        var revocation = verification.Document!;
        var state = await stateReader.ReadAsync(cancellationToken).ConfigureAwait(false);
        var active = state?.ActiveRevisionId is null ? null : await store.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        var license = active?.Content is { } licenseContent ? LicenseVerifier.Verify(licenseContent, trustStore, now).Document : null;
        if (license is null || !string.Equals(license.OrganizationId, revocation.OrganizationId, StringComparison.Ordinal))
        {
            return await RejectAsync(verification, revocation, LicenseImportCodes.Invalid, OrganizationMismatch, actorId, correlationId, now, cancellationToken).ConfigureAwait(false);
        }

        if (revocation.Sequence <= state!.RevocationSequence)
        {
            return await RejectAsync(verification, revocation, LicenseImportCodes.Obsolete, SequenceNotIncreasing, actorId, correlationId, now, cancellationToken).ConfigureAwait(false);
        }

        var accepted = new LicenseRevisionRecord(Guid.NewGuid(), "revocation", now, actorId, verification.ContentSha256, content, null, revocation.Sequence, "accepted", null);
        var imported = new LicensingEvent(Guid.NewGuid(), now, actorId, correlationId, LicensingEventAction.RevocationImported, new JsonObject
        {
            ["sequence"] = revocation.Sequence,
            ["revokedLicenses"] = revocation.RevokedLicenses.Count,
        });
        if (await store.AcceptRevocationAsync(accepted, revocation.Sequence, [imported], cancellationToken).ConfigureAwait(false) != LicenseActivationOutcome.Activated)
        {
            return await RejectAsync(verification, revocation, LicenseImportCodes.Obsolete, SequenceNotIncreasing, actorId, correlationId, now, cancellationToken).ConfigureAwait(false);
        }

        return new LicenseImportResult(LicenseImportOutcome.Accepted, null, await statusService.GetAsync(cancellationToken).ConfigureAwait(false));
    }

    private async Task<LicenseImportResult> RejectAsync(
        RevocationVerificationResult verification,
        RevocationDocument? revocation,
        string problemCode,
        string reason,
        string actorId,
        Guid correlationId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var rejected = new LicenseRevisionRecord(Guid.NewGuid(), "revocation", now, actorId, verification.ContentSha256, null, null, revocation?.Sequence, "rejected", problemCode);
        await store.RecordRejectedAsync(
            rejected,
            new LicensingEvent(Guid.NewGuid(), now, actorId, correlationId, LicensingEventAction.LicenseRejected, new JsonObject
            {
                ["documentType"] = "revocation",
                ["failureCode"] = problemCode,
                ["reason"] = reason,
                ["contentSha256"] = Convert.ToHexStringLower(verification.ContentSha256),
            }),
            cancellationToken).ConfigureAwait(false);
        return new LicenseImportResult(LicenseImportOutcome.Rejected, problemCode, null);
    }
}
