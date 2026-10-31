using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Dokpod.ControlPlane.Application.Licensing;
using Dokpod.Licensing;
using Xunit;

namespace Dokpod.ControlPlane.Application.Tests.Licensing;

/// <summary>Spec 031 T020: importação atômica, idempotente e com revisão monotônica (FR-009, FR-010).</summary>
public sealed class LicenseImportServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly ECDsa signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly TrustStore trustStore;
    private readonly FakeLicenseStore store = new();
    private readonly LicenseImportService service;

    public LicenseImportServiceTests()
    {
        trustStore = new TrustStore([TrustedKey.FromPublicKey("dokpod-test-1", signingKey)]);
        service = new LicenseImportService(store, store, new LicenseStateEvaluator(trustStore), trustStore, new FixedClock(Now));
    }

    public void Dispose()
    {
        trustStore.Dispose();
        signingKey.Dispose();
    }

    [Fact]
    public async Task Accepted_UpdatesStateAndRecordsLicenseImported()
    {
        var result = await Import(License(revision: 2), "prd", 0);

        Assert.Equal(LicenseImportOutcome.Accepted, result.Outcome);
        Assert.Equal("granted", result.Status!.Entitlement);
        Assert.Equal(("prd", "production", 8), (result.Status.Installation.SlotId, result.Status.Installation.Kind, result.Status.Installation.Quota));
        Assert.Equal(1, store.Version);
        Assert.Equal([LicensingEventAction.LicenseImported], store.Events.Select(e => e.Action));
        Assert.Equal("lic_7f3c9a", store.Events[0].Details["licenseId"]!.GetValue<string>());
        Assert.False(store.Events[0].Details.ContainsKey("content"));
    }

    [Fact]
    public async Task Rejected_RecordsLicenseRejectedAndKeepsActiveRevision()
    {
        await Import(License(revision: 2), "prd", 0);
        var activeBefore = store.ActiveRevisionId;
        var tampered = License(revision: 3);
        tampered[^5] = tampered[^5] == (byte)'A' ? (byte)'B' : (byte)'A';

        var result = await Import(tampered, "prd", 1);

        Assert.Equal((LicenseImportOutcome.Rejected, "license_invalid"), (result.Outcome, result.ProblemCode));
        Assert.Equal(activeBefore, store.ActiveRevisionId);
        Assert.Equal(1, store.Version);
        var rejected = store.Rejected.Single();
        Assert.Null(rejected.Content);
        Assert.Equal("bad_signature", store.Events[^1].Details["reason"]!.GetValue<string>());
        Assert.Equal(LicensingEventAction.LicenseRejected, store.Events[^1].Action);
    }

    [Fact]
    public async Task LowerRevision_IsObsolete()
    {
        await Import(License(revision: 3), "prd", 0);

        var result = await Import(License(revision: 2), "prd", 1);

        Assert.Equal("license_obsolete", result.ProblemCode);
    }

    [Fact]
    public async Task SameRevisionSameContent_IsIdempotentEvenWithAnotherSignature()
    {
        var payload = Payload(revision: 2).ToJsonString();
        await Import(Sign(payload), "prd", 0);
        var eventsBefore = store.Events.Count;
        var revisionsBefore = store.Revisions.Count;

        // ECDSA é probabilístico: a segunda assinatura é outro arquivo com o mesmo conteúdo assinado.
        var result = await Import(Sign(payload), "prd", 1);

        Assert.Equal(LicenseImportOutcome.Unchanged, result.Outcome);
        Assert.Equal((eventsBefore, revisionsBefore, 1L), (store.Events.Count, store.Revisions.Count, store.Version));
    }

    [Fact]
    public async Task SameRevisionOtherContent_IsInvalidWithRevisionReused()
    {
        await Import(License(revision: 2), "prd", 0);

        var result = await Import(License(revision: 2, plan: "business-plus"), "prd", 1);

        Assert.Equal("license_invalid", result.ProblemCode);
        Assert.Equal("revision_reused", store.Events[^1].Details["reason"]!.GetValue<string>());
    }

    [Fact]
    public async Task UnknownAndDisabledSlots_AreRefused()
    {
        Assert.Equal("license_slot_unknown", (await Import(License(revision: 2), "dev", 0)).ProblemCode);

        var disabled = Payload(revision: 2);
        disabled["slots"]![1]!["nodeEquivalents"] = 0;
        Assert.Equal("license_slot_disabled", (await Import(Sign(disabled.ToJsonString()), "hml", 0)).ProblemCode);
        Assert.Equal(0, store.Version);
    }

    [Fact]
    public async Task SlotChange_RecordsLicenseSlotChangedWithoutNewRevision()
    {
        var payload = Payload(revision: 2).ToJsonString();
        await Import(Sign(payload), "prd", 0);

        var result = await Import(Sign(payload), "hml", 1);

        Assert.Equal(LicenseImportOutcome.Accepted, result.Outcome);
        Assert.Equal("hml", result.Status!.Installation.SlotId);
        Assert.Single(store.Revisions);
        Assert.Equal(LicensingEventAction.LicenseSlotChanged, store.Events[^1].Action);
        Assert.Equal(("prd", "hml"), (store.Events[^1].Details["fromSlotId"]!.GetValue<string>(), store.Events[^1].Details["toSlotId"]!.GetValue<string>()));
    }

    [Fact]
    public async Task DivergentIfMatch_IsRevisionConflictWithoutWriting()
    {
        var result = await Import(License(revision: 2), "prd", 7);

        Assert.Equal((LicenseImportOutcome.Conflict, "license_revision_conflict"), (result.Outcome, result.ProblemCode));
        Assert.Empty(store.Events);
        Assert.Empty(store.Revisions);
    }

    [Fact]
    public async Task PersistedLicense_IsReverifiedOnEveryRead()
    {
        await Import(License(revision: 2), "prd", 0);
        var status = new LicenseStatusService(store, store, new LicenseStateEvaluator(trustStore), new FixedClock(Now));
        Assert.Equal("valid", (await status.GetAsync(CancellationToken.None)).Authenticity);

        store.TamperActiveContent();

        var after = await status.GetAsync(CancellationToken.None);
        Assert.Equal(("invalid", "restricted"), (after.Authenticity, after.Entitlement));
        using var otherStore = new TrustStore([]);
        var withoutKey = new LicenseStatusService(store, store, new LicenseStateEvaluator(otherStore), new FixedClock(Now));
        Assert.Equal("invalid", (await withoutKey.GetAsync(CancellationToken.None)).Authenticity);
    }

    [Fact]
    public void Evaluator_DerivesValidityAndGraceFromSignedDates()
    {
        var evaluator = new LicenseStateEvaluator(trustStore);
        var content = License(revision: 2);
        // Instante confiável anterior a todos os instantes avaliados: o relógio nunca está "atrasado" aqui.
        var state = new LicensingStateRecord(Guid.NewGuid(), "prd", Guid.NewGuid(), 0, null, null, null, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), 1, 1);
        var active = new LicenseRevisionRecord(state.ActiveRevisionId!.Value, "license", Now, "user", new byte[32], content, "lic_7f3c9a", 2, "accepted", null);

        var inGrace = evaluator.Evaluate(state, active, new DateTimeOffset(2027, 11, 10, 0, 0, 0, TimeSpan.Zero));
        var expired = evaluator.Evaluate(state, active, new DateTimeOffset(2027, 12, 1, 0, 0, 0, TimeSpan.Zero));
        var notStarted = evaluator.Evaluate(state, active, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));
        var absent = evaluator.Evaluate(null, null, Now);

        Assert.Equal(("expired_in_grace", "granted_in_grace"), (inGrace.Validity, inGrace.Entitlement));
        Assert.Equal(new DateTimeOffset(2027, 12, 1, 0, 0, 0, TimeSpan.Zero), inGrace.Grace!.Until);
        Assert.Equal(("expired", "restricted"), (expired.Validity, expired.Entitlement));
        Assert.Equal(("not_started", "restricted"), (notStarted.Validity, notStarted.Entitlement));
        Assert.Equal(("absent", "ce_only", 0L), (absent.Authenticity, absent.Entitlement, absent.Version));
    }

    private Task<LicenseImportResult> Import(byte[] content, string slotId, long expectedVersion) =>
        service.ImportAsync(content, slotId, expectedVersion, "user-sub-1", Guid.NewGuid(), CancellationToken.None);

    private byte[] License(int revision, string plan = "business") => Sign(Payload(revision, plan).ToJsonString());

    private static JsonObject Payload(int revision, string plan = "business") => new()
    {
        ["schemaVersion"] = 1,
        ["meteringVersion"] = 1,
        ["licenseId"] = "lic_7f3c9a",
        ["organizationId"] = "org_51d2e0",
        ["revision"] = revision,
        ["product"] = "dokpod-business",
        ["plan"] = plan,
        ["issuedAt"] = "2026-11-01T00:00:00Z",
        ["notBefore"] = "2026-11-01T00:00:00Z",
        ["expiresAt"] = "2027-11-01T00:00:00Z",
        ["capacity"] = new JsonObject { ["mode"] = "limited", ["nodeEquivalents"] = 10 },
        ["vcpuBand"] = 32,
        ["windowDays"] = 30,
        ["graceDays"] = 30,
        ["slots"] = new JsonArray(
            new JsonObject { ["slotId"] = "prd", ["kind"] = "production", ["enabled"] = true, ["nodeEquivalents"] = 8 },
            new JsonObject { ["slotId"] = "hml", ["kind"] = "staging", ["enabled"] = true, ["nodeEquivalents"] = 2 }),
        ["capabilities"] = new JsonArray("change-windows"),
    };

    private byte[] Sign(string payload)
    {
        var input = $"{Base64Url.EncodeToString("""{"alg":"ES256","kid":"dokpod-test-1","typ":"dokpod-license+jws"}"""u8)}.{Base64Url.EncodeToString(Encoding.UTF8.GetBytes(payload))}";
        var signature = signingKey.SignData(Encoding.ASCII.GetBytes(input), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return Encoding.ASCII.GetBytes($"{input}.{Base64Url.EncodeToString(signature)}");
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeLicenseStore : ILicenseStore, ILicenseStateReader
    {
        private readonly Guid installationId = Guid.NewGuid();
        private string? slotId;

        public long Version { get; private set; }

        public Guid? ActiveRevisionId { get; private set; }

        public List<LicenseRevisionRecord> Revisions { get; } = [];

        public List<LicenseRevisionRecord> Rejected { get; } = [];

        public List<LicensingEvent> Events { get; } = [];

        public Task<LicensingStateRecord?> ReadAsync(CancellationToken cancellationToken) => Task.FromResult<LicensingStateRecord?>(
            Version == 0 && ActiveRevisionId is null
                ? null
                : new LicensingStateRecord(installationId, slotId, ActiveRevisionId, 0, null, null, null, Now, Events.Count, Version));

        public Task<LicenseRevisionRecord?> GetActiveAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Revisions.FirstOrDefault(revision => revision.RevisionId == ActiveRevisionId));

        public Task<LicenseActivationOutcome> ActivateAsync(
            LicenseRevisionRecord? revision,
            string slot,
            long expectedVersion,
            IReadOnlyList<LicensingEvent> events,
            CancellationToken cancellationToken)
        {
            if (expectedVersion != Version) return Task.FromResult(LicenseActivationOutcome.Conflict);
            if (revision is not null)
            {
                if (Revisions.Any(r => r.LicenseId == revision.LicenseId && r.Revision == revision.Revision)) return Task.FromResult(LicenseActivationOutcome.Duplicate);
                Revisions.Add(revision);
                ActiveRevisionId = revision.RevisionId;
            }

            slotId = slot;
            Version++;
            Events.AddRange(events);
            return Task.FromResult(LicenseActivationOutcome.Activated);
        }

        public Task RecordRejectedAsync(LicenseRevisionRecord revision, LicensingEvent rejected, CancellationToken cancellationToken)
        {
            Rejected.Add(revision);
            Events.Add(rejected);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<byte[]>> GetAcceptedRevocationsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<byte[]>>([]);

        public Task<LicenseActivationOutcome> AcceptRevocationAsync(LicenseRevisionRecord revision, int sequence, IReadOnlyList<LicensingEvent> events, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public void TamperActiveContent()
        {
            var index = Revisions.FindIndex(r => r.RevisionId == ActiveRevisionId);
            var content = (byte[])Revisions[index].Content!.Clone();
            var dot = Array.IndexOf(content, (byte)'.') + 5;
            content[dot] = content[dot] == (byte)'A' ? (byte)'B' : (byte)'A';
            Revisions[index] = Revisions[index] with { Content = content };
        }
    }
}
