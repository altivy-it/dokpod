using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Dokpod.ControlPlane.Application.Licensing;
using Dokpod.Licensing;

namespace Dokpod.ControlPlane.Application.Tests.Licensing;

/// <summary>Licenças assinadas com chave sintética gerada na execução e fakes das portas de licenciamento (spec 031).</summary>
internal sealed class LicensingTestKit : IDisposable
{
    public static readonly DateTimeOffset ExpiresAt = new(2027, 11, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public LicensingTestKit() => TrustStore = new TrustStore([TrustedKey.FromPublicKey("dokpod-test-1", key)]);

    public TrustStore TrustStore { get; }

    public LicenseStateEvaluator Evaluator => new(TrustStore);

    public static JsonObject Payload(string? notBefore = null, bool unlimited = false, int productionQuota = 4, int stagingQuota = 6) => new()
    {
        ["schemaVersion"] = 1,
        ["meteringVersion"] = 1,
        ["licenseId"] = "lic_us4",
        ["organizationId"] = "org_us4",
        ["revision"] = 1,
        ["product"] = "dokpod-business",
        ["plan"] = "business",
        ["issuedAt"] = "2026-11-01T00:00:00Z",
        ["notBefore"] = notBefore ?? "2026-11-01T00:00:00Z",
        ["expiresAt"] = "2027-11-01T00:00:00Z",
        ["capacity"] = unlimited ? new JsonObject { ["mode"] = "unlimited" } : new JsonObject { ["mode"] = "limited", ["nodeEquivalents"] = 10 },
        ["vcpuBand"] = 16,
        ["windowDays"] = 30,
        ["graceDays"] = 30,
        ["slots"] = unlimited
            ? new JsonArray(new JsonObject { ["slotId"] = "prd", ["kind"] = "production", ["enabled"] = true }, new JsonObject { ["slotId"] = "hml", ["kind"] = "staging", ["enabled"] = true })
            : new JsonArray(
                new JsonObject { ["slotId"] = "prd", ["kind"] = "production", ["enabled"] = true, ["nodeEquivalents"] = productionQuota },
                new JsonObject { ["slotId"] = "hml", ["kind"] = "staging", ["enabled"] = true, ["nodeEquivalents"] = stagingQuota }),
        ["capabilities"] = new JsonArray("change-windows"),
    };

    public byte[] Sign(JsonObject payload)
    {
        var input = $"{Base64Url.EncodeToString("""{"alg":"ES256","kid":"dokpod-test-1","typ":"dokpod-license+jws"}"""u8)}.{Base64Url.EncodeToString(Encoding.UTF8.GetBytes(payload.ToJsonString()))}";
        var signature = key.SignData(Encoding.ASCII.GetBytes(input), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return Encoding.ASCII.GetBytes($"{input}.{Base64Url.EncodeToString(signature)}");
    }

    public static LicensingStateRecord State(
        string slot = "prd",
        DateTimeOffset? lastTrusted = null,
        DateTimeOffset? expiryGraceUntil = null,
        DateTimeOffset? excessStarted = null,
        DateTimeOffset? excessUntil = null) =>
        new(Guid.Empty, slot, Guid.Parse("00000000-0000-0000-0000-000000000001"), 0, expiryGraceUntil, excessStarted, excessUntil,
            lastTrusted ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), 1, 1);

    public LicenseRevisionRecord Active(JsonObject? payload = null) =>
        new(Guid.Parse("00000000-0000-0000-0000-000000000001"), "license", DateTimeOffset.UnixEpoch, "user", new byte[32], Sign(payload ?? Payload()), "lic_us4", 1, "accepted", null);

    public static CapacitySnapshot Peak(int peak, int unknownHosts = 0) => new(peak, peak, null, unknownHosts, []);

    public void Dispose()
    {
        TrustStore.Dispose();
        key.Dispose();
    }
}

internal sealed class ManualClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>Estado, licença, apuração, trilha e gravação da avaliação em memória, com a mesma semântica do PostgreSQL.</summary>
internal sealed class InMemoryLicensing(LicensingStateRecord state, LicenseRevisionRecord active, MeteringInput? metering = null)
    : ILicenseStateReader, ILicenseStore, ICapacityLedger, ILicensingEvaluationStore, ILicensingAuditWriter
{
    public LicensingStateRecord State { get; private set; } = state;

    public List<LicensingEvent> Events { get; } = [];

    public Task<LicensingStateRecord?> ReadAsync(CancellationToken cancellationToken) => Task.FromResult<LicensingStateRecord?>(State);

    public Task<LicenseRevisionRecord?> GetActiveAsync(CancellationToken cancellationToken) => Task.FromResult<LicenseRevisionRecord?>(active);

    public Task<LicenseActivationOutcome> ActivateAsync(LicenseRevisionRecord? revision, string slotId, long expectedVersion, IReadOnlyList<LicensingEvent> events, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task RecordRejectedAsync(LicenseRevisionRecord revision, LicensingEvent rejected, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<byte[]>> GetAcceptedRevocationsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<byte[]>>([]);

    public Task<LicenseActivationOutcome> AcceptRevocationAsync(LicenseRevisionRecord revision, int sequence, IReadOnlyList<LicensingEvent> events, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task RecordAsync(CapacityObservation observation, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<MeteringInput> ReadMeteringInputAsync(DateTimeOffset windowStartUtc, CancellationToken cancellationToken) =>
        Task.FromResult(metering ?? new MeteringInput([], [], []));

    public Task<long> ApplyRetentionAsync(TimeSpan retention, CancellationToken cancellationToken) => Task.FromResult(0L);

    public Task<string?> LastRecordedEntitlementAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Events.LastOrDefault(e => e.Action == LicensingEventAction.LicenseStateChanged)?.Details["to"]?.GetValue<string>());

    public Task ApplyAsync(LicensingStateUpdate update, IReadOnlyList<LicensingEvent> events, CancellationToken cancellationToken)
    {
        State = State with
        {
            LastTrustedInstantUtc = update.LastTrustedInstantUtc is { } trusted && trusted > State.LastTrustedInstantUtc ? trusted : State.LastTrustedInstantUtc,
            ExpiryGraceUntilUtc = State.ExpiryGraceUntilUtc ?? update.ExpiryGraceUntilUtc,
            ExcessStartedAtUtc = update.ClearExcess ? null : State.ExcessStartedAtUtc ?? update.ExcessStartedAtUtc,
            ExcessGraceUntilUtc = update.ClearExcess ? null : State.ExcessGraceUntilUtc ?? update.ExcessGraceUntilUtc,
        };
        Events.AddRange(events);
        return Task.CompletedTask;
    }

    public Task<IReadOnlySet<int>> RecordedExpiryWarningsAsync(DateTimeOffset expiresAt, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<int>>(Events
            .Where(e => e.Action == LicensingEventAction.LicenseExpiryWarning && e.Details["expiresAt"]!.GetValue<string>() == LicenseStatusService.Iso(expiresAt))
            .Select(e => e.Details["daysBefore"]!.GetValue<int>())
            .ToHashSet());

    public Task<DateOnly?> LastDailySummaryDateAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Events.Where(e => e.Action == LicensingEventAction.CapacityDailySummary)
            .Select(e => (DateOnly?)DateOnly.Parse(e.Details["date"]!.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture))
            .Max());

    public Task<bool> ResetTrustedInstantAsync(DateTimeOffset now, long expectedVersion, LicensingEvent reset, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<LicensingChainLink> AppendAsync(LicensingEvent licensingEvent, CancellationToken cancellationToken)
    {
        Events.Add(licensingEvent);
        return Task.FromResult(new LicensingChainLink(Events.Count, new byte[32]));
    }
}
