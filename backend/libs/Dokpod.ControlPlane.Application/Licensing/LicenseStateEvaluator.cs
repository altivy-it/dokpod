using Dokpod.Licensing;

namespace Dokpod.ControlPlane.Application.Licensing;

/// <summary>Recurso único da instalação no Keycloak e seus scopes (D-008).</summary>
public static class LicensingAuthorization
{
    public const string Resource = "urn:dokpod:installation";
    public const string ReadScope = "licensing:read";
    public const string ManageScope = "licensing:manage";

    public static IReadOnlySet<string> Scopes { get; } = new HashSet<string>([ReadScope, ManageScope], StringComparer.Ordinal);
}

public sealed record LicenseStatusLicense(
    string LicenseId,
    string OrganizationId,
    int Revision,
    string Plan,
    DateTimeOffset NotBefore,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<string> Capabilities,
    bool UnlimitedCapacity,
    int? NodeEquivalents);

public sealed record LicenseStatusInstallation(string? SlotId, string? Kind, int? Quota);

public sealed record LicenseStatusGrace(string Cause, DateTimeOffset StartedAt, DateTimeOffset Until);

/// <summary>Estado efetivo com os eixos separados (FR-018); <see cref="Version"/> é o <c>If-Match</c> da gestão.</summary>
public sealed record LicenseStatus(
    string Entitlement,
    string Authenticity,
    string? Validity,
    string Metering,
    IReadOnlyList<string> Reasons,
    LicenseStatusLicense? License,
    LicenseStatusInstallation Installation,
    int? VcpuBand,
    LicenseStatusGrace? Grace,
    long Version);

/// <summary>
/// Avalia autenticidade e vigência (spec 031 T026; a apuração entra na US3 e os prazos persistidos na US4).
/// O conteúdo persistido é reverificado a cada avaliação: um resultado gravado nunca vale como prova (FR-010).
/// </summary>
public sealed class LicenseStateEvaluator(TrustStore trustStore)
{
    public LicenseStatus Evaluate(LicensingStateRecord? state, LicenseRevisionRecord? active, DateTimeOffset now)
    {
        var version = state?.Version ?? 0;
        if (state?.ActiveRevisionId is null || active?.Content is null)
        {
            return new LicenseStatus("ce_only", "absent", null, "regular", [], null, new LicenseStatusInstallation(state?.InstallationSlotId, null, null), null, null, version);
        }

        var verification = LicenseVerifier.Verify(active.Content, trustStore, now);
        if (!verification.IsValid)
        {
            return new LicenseStatus(
                "restricted", "invalid", null, "regular", ["license_invalid"], null,
                new LicenseStatusInstallation(state.InstallationSlotId, null, null), null, null, version);
        }

        var license = verification.Document!;
        var graceUntil = license.ExpiresAt.AddDays(license.GraceDays);
        var (validity, entitlement, reasons, grace) = verification.Validity switch
        {
            LicenseValidity.NotStarted => ("not_started", "restricted", new[] { "license_not_started" }, (LicenseStatusGrace?)null),
            LicenseValidity.Active => ("active", "granted", [], null),
            _ when now < graceUntil => ("expired_in_grace", "granted_in_grace", ["license_expired"], new LicenseStatusGrace("expiry", license.ExpiresAt, graceUntil)),
            _ => ("expired", "restricted", ["license_expired"], null),
        };

        var slot = state.InstallationSlotId is { } slotId ? license.FindSlot(slotId) : null;
        return new LicenseStatus(
            entitlement,
            "valid",
            validity,
            "regular",
            reasons,
            new LicenseStatusLicense(
                license.LicenseId, license.OrganizationId, license.Revision, license.Plan, license.NotBefore, license.ExpiresAt,
                license.Capabilities, license.Capacity.IsUnlimited, license.Capacity.NodeEquivalents),
            new LicenseStatusInstallation(state.InstallationSlotId, slot?.Kind, slot?.IsDisabled == true ? 0 : slot?.NodeEquivalents),
            license.VcpuBand,
            grace,
            version);
    }
}

public sealed class LicenseStatusService(
    ILicenseStateReader stateReader,
    ILicenseStore store,
    LicenseStateEvaluator evaluator,
    TimeProvider timeProvider)
{
    public async Task<LicenseStatus> GetAsync(CancellationToken cancellationToken)
    {
        var state = await stateReader.ReadAsync(cancellationToken).ConfigureAwait(false);
        var active = state?.ActiveRevisionId is null ? null : await store.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        return evaluator.Evaluate(state, active, timeProvider.GetUtcNow());
    }
}
