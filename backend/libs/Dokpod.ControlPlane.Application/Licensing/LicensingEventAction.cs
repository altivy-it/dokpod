namespace Dokpod.ControlPlane.Application.Licensing;

/// <summary>
/// Ações da trilha global de licenciamento (spec 031, data-model.md) e as chaves de <c>details</c> permitidas
/// em cada uma. Nada fora da allowlist é gravado: nunca o conteúdo da licença nem dados pessoais (FR-026).
/// </summary>
public static class LicensingEventAction
{
    public const string LicenseImported = "license.imported";
    public const string LicenseRejected = "license.rejected";
    public const string LicenseStateChanged = "license.state_changed";
    public const string LicenseGraceStarted = "license.grace_started";
    public const string LicenseGraceEnded = "license.grace_ended";
    public const string LicenseExpiryWarning = "license.expiry_warning";
    public const string LicenseReportExported = "license.report_exported";
    public const string LicenseSlotChanged = "license.slot_changed";
    public const string LicenseTrustedInstantReset = "license.trusted_instant_reset";
    public const string CapacityDailySummary = "capacity.daily_summary";
    public const string RevocationImported = "revocation.imported";
    public const string HostLinked = "host.linked";
    public const string HostUnlinked = "host.unlinked";
    public const string HostRemoved = "host.removed";

    public static IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedDetails { get; } =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [LicenseImported] = Keys("licenseId", "revision", "slotId", "contentSha256"),
            [LicenseRejected] = Keys("documentType", "failureCode", "reason", "contentSha256"),
            [LicenseStateChanged] = Keys("from", "to", "reasons"),
            [LicenseGraceStarted] = Keys("cause", "startedAt", "until"),
            [LicenseGraceEnded] = Keys("cause", "endedAt"),
            [LicenseExpiryWarning] = Keys("daysBefore", "expiresAt"),
            [LicenseReportExported] = Keys("challenge"),
            [LicenseSlotChanged] = Keys("fromSlotId", "toSlotId"),
            [LicenseTrustedInstantReset] = Keys("previousInstant", "newInstant", "reason"),
            [CapacityDailySummary] = Keys("date", "vcpuBand", "peakNodeEquivalents", "unknownHosts", "hosts", "late"),
            [RevocationImported] = Keys("sequence", "revokedLicenses"),
            [HostLinked] = Keys("hostId", "environmentId"),
            [HostUnlinked] = Keys("hostId", "environmentId"),
            [HostRemoved] = Keys("hostId"),
        };

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(AllowedDetails.Keys, StringComparer.Ordinal);

    private static HashSet<string> Keys(params string[] keys) => new(keys, StringComparer.Ordinal);
}
