using System.Text.Json.Nodes;
using Dokpod.Licensing;

namespace Dokpod.ControlPlane.Application.Licensing;

public enum LicenseImportOutcome
{
    Accepted,

    /// <summary>Mesma licença, mesma revisão, mesmo conteúdo assinado e mesmo slot: nada muda (FR-009).</summary>
    Unchanged,

    Rejected,

    /// <summary><c>If-Match</c> não corresponde ao estado atual.</summary>
    Conflict,
}

/// <summary>Códigos de recusa da importação (contrato <c>LicensingProblemCode</c>).</summary>
public static class LicenseImportCodes
{
    public const string Invalid = "license_invalid";
    public const string Obsolete = "license_obsolete";
    public const string SlotUnknown = "license_slot_unknown";
    public const string SlotDisabled = "license_slot_disabled";
    public const string RevisionConflict = "license_revision_conflict";
    public const string RevisionReused = "revision_reused";
}

public sealed record LicenseImportResult(LicenseImportOutcome Outcome, string? ProblemCode, LicenseStatus? Status);

/// <summary>
/// Importação atômica e idempotente da licença (spec 031 T026, FR-009): verifica offline, confere o slot e a revisão
/// contra a licença ativa e só então persiste, com <c>If-Match</c>. Recusa grava hash, código e autoria e nunca
/// substitui a licença íntegra.
/// </summary>
public sealed class LicenseImportService(
    ILicenseStore store,
    ILicenseStateReader stateReader,
    LicenseStateEvaluator evaluator,
    TrustStore trustStore,
    TimeProvider timeProvider)
{
    public async Task<LicenseImportResult> ImportAsync(
        byte[] content,
        string slotId,
        long expectedVersion,
        string actorId,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);

        var now = timeProvider.GetUtcNow();
        var state = await stateReader.ReadAsync(cancellationToken).ConfigureAwait(false);
        if ((state?.Version ?? 0) != expectedVersion)
        {
            return new LicenseImportResult(LicenseImportOutcome.Conflict, LicenseImportCodes.RevisionConflict, null);
        }

        var verification = LicenseVerifier.Verify(content, trustStore, now);
        var sha = Convert.ToHexStringLower(verification.ContentSha256);
        if (!verification.IsValid)
        {
            return await RejectAsync(verification, LicenseImportCodes.Invalid, verification.Failure!.Value.ToCode(), actorId, correlationId, now, cancellationToken)
                .ConfigureAwait(false);
        }

        var license = verification.Document!;
        var slot = license.FindSlot(slotId);
        if (slot is null || slot.IsDisabled)
        {
            var code = slot is null ? LicenseImportCodes.SlotUnknown : LicenseImportCodes.SlotDisabled;
            return await RejectAsync(verification, code, code, actorId, correlationId, now, cancellationToken).ConfigureAwait(false);
        }

        var active = state?.ActiveRevisionId is null ? null : await store.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        var keepActiveRevision = false;
        if (active is not null && string.Equals(active.LicenseId, license.LicenseId, StringComparison.Ordinal))
        {
            if (license.Revision < active.Revision)
            {
                return await RejectAsync(verification, LicenseImportCodes.Obsolete, LicenseImportCodes.Obsolete, actorId, correlationId, now, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (license.Revision == active.Revision)
            {
                if (!active.ContentSha256.AsSpan().SequenceEqual(verification.ContentSha256))
                {
                    return await RejectAsync(verification, LicenseImportCodes.Invalid, LicenseImportCodes.RevisionReused, actorId, correlationId, now, cancellationToken)
                        .ConfigureAwait(false);
                }

                if (string.Equals(state!.InstallationSlotId, slotId, StringComparison.Ordinal))
                {
                    return new LicenseImportResult(LicenseImportOutcome.Unchanged, null, evaluator.Evaluate(state, active, now));
                }

                keepActiveRevision = true;
            }
        }

        var events = new List<LicensingEvent>();
        LicenseRevisionRecord? revision = null;
        if (!keepActiveRevision)
        {
            revision = new LicenseRevisionRecord(
                Guid.NewGuid(), "license", now, actorId, verification.ContentSha256, content, license.LicenseId, license.Revision, "accepted", null);
            events.Add(new LicensingEvent(Guid.NewGuid(), now, actorId, correlationId, LicensingEventAction.LicenseImported, new JsonObject
            {
                ["licenseId"] = license.LicenseId,
                ["revision"] = license.Revision,
                ["slotId"] = slotId,
                ["contentSha256"] = sha,
            }));
        }

        if (state?.InstallationSlotId is { } previousSlot && !string.Equals(previousSlot, slotId, StringComparison.Ordinal))
        {
            events.Add(new LicensingEvent(Guid.NewGuid(), now, actorId, correlationId, LicensingEventAction.LicenseSlotChanged, new JsonObject
            {
                ["fromSlotId"] = previousSlot,
                ["toSlotId"] = slotId,
            }));
        }

        var outcome = await store.ActivateAsync(revision, slotId, expectedVersion, events, cancellationToken).ConfigureAwait(false);
        switch (outcome)
        {
            case LicenseActivationOutcome.Conflict:
                return new LicenseImportResult(LicenseImportOutcome.Conflict, LicenseImportCodes.RevisionConflict, null);
            case LicenseActivationOutcome.Duplicate:
                return await RejectAsync(verification, LicenseImportCodes.Obsolete, LicenseImportCodes.Obsolete, actorId, correlationId, now, cancellationToken)
                    .ConfigureAwait(false);
        }

        var updated = await stateReader.ReadAsync(cancellationToken).ConfigureAwait(false);
        var current = await store.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        return new LicenseImportResult(LicenseImportOutcome.Accepted, null, evaluator.Evaluate(updated, current, now));
    }

    private async Task<LicenseImportResult> RejectAsync(
        LicenseVerificationResult verification,
        string problemCode,
        string reason,
        string actorId,
        Guid correlationId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var document = verification.Document;
        var revision = new LicenseRevisionRecord(
            Guid.NewGuid(), "license", now, actorId, verification.ContentSha256, null, document?.LicenseId, document?.Revision, "rejected", problemCode);
        var rejected = new LicensingEvent(Guid.NewGuid(), now, actorId, correlationId, LicensingEventAction.LicenseRejected, new JsonObject
        {
            ["documentType"] = "license",
            ["failureCode"] = problemCode,
            ["reason"] = reason,
            ["contentSha256"] = Convert.ToHexStringLower(verification.ContentSha256),
        });
        await store.RecordRejectedAsync(revision, rejected, cancellationToken).ConfigureAwait(false);
        return new LicenseImportResult(LicenseImportOutcome.Rejected, problemCode, null);
    }
}
