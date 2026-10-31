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

/// <summary>Consumo apurado com a faixa da licença vigente (US3); nulo sem licença válida.</summary>
public sealed record LicenseStatusConsumption(int Current, int WindowPeak, DateTimeOffset? WindowPeakAt, int UnknownHosts)
{
    public static LicenseStatusConsumption From(CapacitySnapshot snapshot) =>
        new(snapshot.Current, snapshot.WindowPeak, snapshot.WindowPeakAtUtc, snapshot.UnknownHosts);
}

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
    long Version)
{
    public LicenseStatusConsumption? Consumption { get; init; }
}

/// <summary>
/// O que a avaliação precisa gravar em <c>licensing_state</c> (D-006): avanço monotônico do instante confiável e os prazos
/// na primeira detecção. Prazos já gravados nunca são recalculados nem estendidos.
/// </summary>
public sealed record LicensingStateUpdate(
    DateTimeOffset? LastTrustedInstantUtc,
    DateTimeOffset? ExpiryGraceUntilUtc,
    DateTimeOffset? ExcessStartedAtUtc,
    DateTimeOffset? ExcessGraceUntilUtc,
    bool ClearExcess)
{
    public bool IsEmpty => LastTrustedInstantUtc is null && ExpiryGraceUntilUtc is null && ExcessStartedAtUtc is null && !ClearExcess;
}

public sealed record LicensingAssessment(LicenseStatus Status, LicensingStateUpdate Update);

/// <summary>Motivos legíveis de cada restrição (FR-018).</summary>
public static class LicenseReasons
{
    public const string Invalid = "license_invalid";
    public const string NotStarted = "license_not_started";
    public const string Expired = "license_expired";
    public const string CapacityExceeded = "license_capacity_exceeded";
    public const string Indeterminate = "license_state_indeterminate";
    public const string MeteringUnknown = "metering_unknown";
}

/// <summary>
/// Avalia autenticidade, vigência, apuração e direitos (spec 031 T026 e T045, D-006). O conteúdo persistido é
/// reverificado a cada avaliação (FR-010). Prazos vêm de <c>licensing_state</c> quando já gravados; senão, a
/// avaliação os calcula e devolve em <see cref="LicensingStateUpdate"/>. Irregularidades simultâneas não somam
/// prazos: vale o mais cedo (FR-020). Relógio mais de 5 minutos abaixo do último instante confiável deixa o estado
/// <c>indeterminate</c>, sem tolerância nova; recuo menor (correção de NTP) não muda nada.
/// </summary>
public sealed class LicenseStateEvaluator(TrustStore trustStore)
{
    public static readonly TimeSpan ClockTolerance = TimeSpan.FromMinutes(5);

    public LicenseStatus Evaluate(LicensingStateRecord? state, LicenseRevisionRecord? active, DateTimeOffset now) =>
        Assess(state, active, now, null).Status;

    public LicensingAssessment Assess(LicensingStateRecord? state, LicenseRevisionRecord? active, DateTimeOffset now, CapacitySnapshot? snapshot)
    {
        var version = state?.Version ?? 0;
        var none = new LicensingStateUpdate(null, null, null, null, false);
        if (state?.ActiveRevisionId is null || active?.Content is null)
        {
            return new LicensingAssessment(
                new LicenseStatus("ce_only", "absent", null, "regular", [], null, new LicenseStatusInstallation(state?.InstallationSlotId, null, null), null, null, version),
                none);
        }

        var verification = LicenseVerifier.Verify(active.Content, trustStore, now);
        if (!verification.IsValid)
        {
            return new LicensingAssessment(
                new LicenseStatus("restricted", "invalid", null, "regular", [LicenseReasons.Invalid], null, new LicenseStatusInstallation(state.InstallationSlotId, null, null), null, null, version),
                none);
        }

        var license = verification.Document!;
        var slot = state.InstallationSlotId is { } slotId ? license.FindSlot(slotId) : null;
        var quota = slot is null || license.Capacity.IsUnlimited ? null : slot.IsDisabled ? 0 : slot.NodeEquivalents;
        var summary = new LicenseStatusLicense(
            license.LicenseId, license.OrganizationId, license.Revision, license.Plan, license.NotBefore, license.ExpiresAt,
            license.Capabilities, license.Capacity.IsUnlimited, license.Capacity.NodeEquivalents);
        var installation = new LicenseStatusInstallation(state.InstallationSlotId, slot?.Kind, quota);
        var consumption = snapshot is null ? null : LicenseStatusConsumption.From(snapshot);

        if (now < state.LastTrustedInstantUtc - ClockTolerance)
        {
            return new LicensingAssessment(
                new LicenseStatus("indeterminate", "valid", Validity(verification.Validity, now, license), "regular", [LicenseReasons.Indeterminate], summary, installation, license.VcpuBand, null, version)
                {
                    Consumption = consumption,
                },
                none);
        }

        var lastTrusted = now > state.LastTrustedInstantUtc ? now : (DateTimeOffset?)null;
        var validity = Validity(verification.Validity, now, license);
        var expiryUntil = state.ExpiryGraceUntilUtc ?? license.ExpiresAt.AddDays(license.GraceDays);
        DateTimeOffset? newExpiryUntil = verification.Validity == LicenseValidity.Expired && state.ExpiryGraceUntilUtc is null ? expiryUntil : null;

        var excess = quota is { } limit && snapshot is { } measured && measured.WindowPeak > limit;
        var excessStarted = state.ExcessStartedAtUtc ?? (excess ? now : (DateTimeOffset?)null);
        var excessUntil = state.ExcessGraceUntilUtc ?? (excess ? now.AddDays(license.GraceDays) : (DateTimeOffset?)null);
        var update = new LicensingStateUpdate(
            lastTrusted,
            newExpiryUntil,
            excess && state.ExcessStartedAtUtc is null ? excessStarted : null,
            excess && state.ExcessStartedAtUtc is null ? excessUntil : null,
            !excess && state.ExcessStartedAtUtc is not null);

        var metering = excess
            ? (now < excessUntil ? "excess_in_grace" : "excess")
            : snapshot?.UnknownHosts > 0 ? "unknown" : "regular";

        var graces = new List<LicenseStatusGrace>();
        if (verification.Validity == LicenseValidity.Expired) graces.Add(new LicenseStatusGrace("expiry", license.ExpiresAt, expiryUntil));
        if (excess) graces.Add(new LicenseStatusGrace("excess", excessStarted!.Value, excessUntil!.Value));

        var reasons = new List<string>();
        if (verification.Validity == LicenseValidity.Expired) reasons.Add(LicenseReasons.Expired);
        if (excess) reasons.Add(LicenseReasons.CapacityExceeded);
        if (metering == "unknown") reasons.Add(LicenseReasons.MeteringUnknown);

        string entitlement;
        LicenseStatusGrace? grace = null;
        if (verification.Validity == LicenseValidity.NotStarted)
        {
            entitlement = "restricted";
            reasons.Insert(0, LicenseReasons.NotStarted);
        }
        else if (graces.Count == 0)
        {
            entitlement = "granted";
        }
        else
        {
            // FR-020: os prazos não se somam; vale o mais cedo entre as irregularidades ativas.
            var earliest = graces.MinBy(item => item.Until)!;
            (entitlement, grace) = now < earliest.Until ? ("granted_in_grace", earliest) : ("restricted", null);
        }

        return new LicensingAssessment(
            new LicenseStatus(entitlement, "valid", validity, metering, reasons, summary, installation, license.VcpuBand, grace, version)
            {
                Consumption = consumption,
            },
            update);
    }

    private static string Validity(LicenseValidity? validity, DateTimeOffset now, LicenseDocument license) => validity switch
    {
        LicenseValidity.NotStarted => "not_started",
        LicenseValidity.Active => "active",
        _ => now < license.ExpiresAt.AddDays(license.GraceDays) ? "expired_in_grace" : "expired",
    };
}

/// <summary>
/// Gravação da avaliação (D-006, FR-019): prazos na primeira detecção, instante confiável monotônico e os eventos de
/// mudança de estado e de tolerância, numa transação. Avisos e resumos diários usam as consultas de idempotência.
/// </summary>
public interface ILicensingEvaluationStore
{
    Task<string?> LastRecordedEntitlementAsync(CancellationToken cancellationToken);

    Task ApplyAsync(LicensingStateUpdate update, IReadOnlyList<LicensingEvent> events, CancellationToken cancellationToken);

    /// <summary>Marcos de aviso (60, 30, 7) já gravados para este <paramref name="expiresAt"/>.</summary>
    Task<IReadOnlySet<int>> RecordedExpiryWarningsAsync(DateTimeOffset expiresAt, CancellationToken cancellationToken);

    Task<DateOnly?> LastDailySummaryDateAsync(CancellationToken cancellationToken);

    /// <summary>Regrava o instante confiável com o relógio atual se <paramref name="expectedVersion"/> conferir (T069).</summary>
    Task<bool> ResetTrustedInstantAsync(DateTimeOffset now, long expectedVersion, LicensingEvent reset, CancellationToken cancellationToken);
}

/// <summary>
/// Estado efetivo: reavaliado a cada leitura, sem cache que estenda prazo (D-006). Com <see cref="ILicensingEvaluationStore"/>,
/// grava os prazos na primeira detecção e os eventos de mudança.
/// </summary>
public sealed class LicenseStatusService(
    ILicenseStateReader stateReader,
    ILicenseStore store,
    LicenseStateEvaluator evaluator,
    TimeProvider timeProvider,
    ICapacityLedger? ledger = null,
    ILicensingEvaluationStore? evaluationStore = null)
{
    public async Task<LicenseStatus> GetAsync(CancellationToken cancellationToken) => (await AssessAsync(cancellationToken).ConfigureAwait(false)).Status;

    public async Task<LicensingAssessment> AssessAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var state = await stateReader.ReadAsync(cancellationToken).ConfigureAwait(false);
        var active = state?.ActiveRevisionId is null ? null : await store.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        var band = evaluator.Evaluate(state, active, now).VcpuBand;
        CapacitySnapshot? snapshot = null;
        if (ledger is not null && band is { } vcpuBand)
        {
            // Faixa da licença vigente sobre os dados brutos: trocar a licença reapura a janela inteira (FR-015).
            var input = await ledger.ReadMeteringInputAsync(now.AddDays(-CapacityMeter.WindowDays), cancellationToken).ConfigureAwait(false);
            snapshot = CapacityMeter.Compute(input, vcpuBand, now);
        }

        var assessment = evaluator.Assess(state, active, now, snapshot);
        if (evaluationStore is not null && state is not null)
        {
            await PersistAsync(assessment, now, cancellationToken).ConfigureAwait(false);
        }

        return assessment;
    }

    private async Task PersistAsync(LicensingAssessment assessment, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var status = assessment.Status;
        var update = assessment.Update;
        var events = new List<LicensingEvent>();
        LicensingEvent Event(string action, System.Text.Json.Nodes.JsonObject details) => new(Guid.NewGuid(), now, null, Guid.NewGuid(), action, details);

        if (update.ExpiryGraceUntilUtc is { } expiryUntil && status.License is { } expired)
        {
            events.Add(Event(LicensingEventAction.LicenseGraceStarted, new() { ["cause"] = "expiry", ["startedAt"] = Iso(expired.ExpiresAt), ["until"] = Iso(expiryUntil) }));
        }

        if (update.ExcessStartedAtUtc is { } started && update.ExcessGraceUntilUtc is { } excessUntil)
        {
            events.Add(Event(LicensingEventAction.LicenseGraceStarted, new() { ["cause"] = "excess", ["startedAt"] = Iso(started), ["until"] = Iso(excessUntil) }));
        }

        if (update.ClearExcess)
        {
            events.Add(Event(LicensingEventAction.LicenseGraceEnded, new() { ["cause"] = "excess", ["endedAt"] = Iso(now) }));
        }

        var previous = await evaluationStore!.LastRecordedEntitlementAsync(cancellationToken).ConfigureAwait(false) ?? "ce_only";
        if (!string.Equals(previous, status.Entitlement, StringComparison.Ordinal))
        {
            if (previous == "granted_in_grace" && status.Entitlement == "restricted")
            {
                events.Add(Event(LicensingEventAction.LicenseGraceEnded, new() { ["cause"] = status.Reasons.Contains(LicenseReasons.CapacityExceeded) ? "excess" : "expiry", ["endedAt"] = Iso(now) }));
            }

            var reasons = new System.Text.Json.Nodes.JsonArray();
            foreach (var reason in status.Reasons) reasons.Add(reason);
            events.Add(Event(LicensingEventAction.LicenseStateChanged, new() { ["from"] = previous, ["to"] = status.Entitlement, ["reasons"] = reasons }));
        }

        if (!update.IsEmpty || events.Count > 0)
        {
            await evaluationStore.ApplyAsync(update, events, cancellationToken).ConfigureAwait(false);
        }
    }

    public static string Iso(DateTimeOffset instant) => instant.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);
}
