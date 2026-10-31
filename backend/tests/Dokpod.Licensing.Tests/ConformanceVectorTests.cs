using System.Text.Json;
using System.Text.Json.Nodes;
using Dokpod.Licensing.Tests.ReportVectors;
using Dokpod.Licensing.Tests.Vectors;
using Xunit;

namespace Dokpod.Licensing.Tests;

/// <summary>
/// Vetores de conformidade estáticos publicados no pacote <c>Dokpod.Licensing</c> (spec 031 T054, T071): licenças,
/// revogação e relatórios com o resultado esperado em <c>manifest.json</c>, para a CLI e outros consumidores conferirem
/// a mesma interpretação. Só a chave pública sintética fica em disco; a privada é gerada e descartada na regeneração
/// (<c>DOKPOD_REGENERATE_CONFORMANCE=1</c>), e o <c>kid</c> <c>dokpod-test-1</c> nunca entra no trust store de produção.
/// </summary>
public sealed class ConformanceVectorTests
{
    private const string Challenge = "rnw-2027-org_51d2e0-a91f";
    private const string ContentSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly Guid Installation = Guid.Parse("3f0c5a1e-2b4d-4c6e-8f90-a1b2c3d4e5f6");
    private static readonly DateTimeOffset Start = new(2026, 11, 14, 13, 0, 0, TimeSpan.Zero);
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../ConformanceVectors"));

    [Fact]
    public void LicenseVectors_MatchTheManifest()
    {
        RegenerateIfRequested();
        using var trustStore = new TrustStore([TrustedKey.FromPublicKeyPem(LicenseVectors.TestKeyId, File.ReadAllText(Path.Combine(Root, "license", $"{LicenseVectors.TestKeyId}.pem")))]);
        var cases = Manifest("license");
        Assert.True(cases.Count >= 6);
        foreach (var item in cases)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Root, "license", item["file"]!.GetValue<string>()));
            var instant = DateTimeOffset.Parse(item["instant"]!.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture);
            var actual = item["kind"]!.GetValue<string>() == "revocation"
                ? Outcome(RevocationVerifier.Verify(bytes, trustStore).Failure)
                : Outcome(LicenseVerifier.Verify(bytes, trustStore, instant).Failure);
            Assert.True(item["expected"]!.GetValue<string>() == actual, $"{item["file"]}: esperado {item["expected"]}, obtido {actual}");
        }
    }

    [Fact]
    public void ReportVectors_MatchTheManifest()
    {
        RegenerateIfRequested();
        var cases = Manifest("report");
        Assert.True(cases.Count >= 8);
        foreach (var item in cases)
        {
            var context = item["context"]!.AsObject();
            var result = LicensingReportVerifier.Verify(
                File.ReadAllBytes(Path.Combine(Root, "report", item["file"]!.GetValue<string>())),
                new ReportVerificationContext(
                    context["challenge"]!.GetValue<string>(),
                    Guid.Parse(context["installationId"]!.GetValue<string>()),
                    context["previousHeadSeq"]?.GetValue<long>(),
                    context["previousHeadHash"]?.GetValue<string>(),
                    [.. context["issuedLicenses"]!.AsArray().Select(l => new IssuedLicense(l!["licenseId"]!.GetValue<string>(), l["revision"]!.GetValue<int>(), l["contentSha256"]!.GetValue<string>()))],
                    [.. context["issuedRevocationSequences"]!.AsArray().Select(s => s!.GetValue<int>())]));
            var actual = JsonNamingPolicy.CamelCase.ConvertName(result.Code.ToString());
            Assert.True(item["expected"]!.GetValue<string>() == actual, $"{item["file"]}: esperado {item["expected"]}, obtido {actual}");
        }
    }

    private static string Outcome(LicenseFailure? failure) => failure?.ToCode() ?? "valid";

    private static List<JsonObject> Manifest(string kind) =>
        [.. JsonNode.Parse(File.ReadAllText(Path.Combine(Root, kind, "manifest.json")))!["cases"]!.AsArray().Select(c => c!.AsObject())];

    private static readonly Lock RegenerationLock = new();
    private static bool regenerated;

    private static void RegenerateIfRequested()
    {
        if (Environment.GetEnvironmentVariable("DOKPOD_REGENERATE_CONFORMANCE") != "1") return;
        lock (RegenerationLock)
        {
            if (regenerated) return;
            GenerateLicenseVectors();
            GenerateReportVectors();
            regenerated = true;
        }
    }

    private static void GenerateLicenseVectors()
    {
        var directory = Directory.CreateDirectory(Path.Combine(Root, "license")).FullName;
        using var vectors = new LicenseVectors();
        File.WriteAllText(Path.Combine(directory, $"{LicenseVectors.TestKeyId}.pem"), vectors.SigningKey.ExportSubjectPublicKeyInfoPem() + "\n");
        var valid = vectors.Valid();
        var flipped = (byte[])valid.Clone();
        flipped[^3] = (byte)(flipped[^3] == (byte)'A' ? 'B' : 'A');
        var cases = new (string File, string Kind, byte[] Content, string Expected)[]
        {
            ("valid.jws", "license", valid, "valid"),
            ("bad-signature.jws", "license", flipped, "bad_signature"),
            ("unknown-key.jws", "license", vectors.Sign(LicenseVectors.Header(kid: "dokpod-test-unknown"), LicenseVectors.Payload().ToJsonString()), "unknown_key"),
            ("alg-none.jws", "license", vectors.Sign(LicenseVectors.Header(alg: "none"), LicenseVectors.Payload().ToJsonString()), "unsupported_profile"),
            ("wrong-product.jws", "license", vectors.WithPayload(p => p["product"] = "dokpod-other"), "wrong_product"),
            ("unknown-capability.jws", "license", vectors.WithPayload(p => p["capabilities"] = new JsonArray("not-a-capability")), "unknown_capability"),
            ("revocation-valid.jws", "revocation", vectors.Revocation(), "valid"),
            ("revocation-as-license.jws", "license", vectors.Revocation(), "unsupported_profile"),
        };
        var manifest = new JsonArray();
        foreach (var (file, kind, content, expected) in cases)
        {
            File.WriteAllBytes(Path.Combine(directory, file), content);
            manifest.Add(new JsonObject { ["file"] = file, ["kind"] = kind, ["instant"] = "2026-12-01T00:00:00Z", ["expected"] = expected });
        }

        WriteManifest(directory, "Licenças e revogação assinadas por uma chave sintética; verifique com o PEM público desta pasta como trust store, no instante indicado.", manifest);
    }

    private static void GenerateReportVectors()
    {
        var directory = Directory.CreateDirectory(Path.Combine(Root, "report")).FullName;
        var seed = 0;
        Guid Next() => new($"00000000-0000-4000-8000-{++seed:D12}");

        ReportBuilder Accepted() => new ReportBuilder(Installation)
            .Add("license.imported", Start, $$"""{"contentSha256":"{{ContentSha}}","licenseId":"lic_7f3c9a","revision":2,"slotId":"prd"}""", "actor-ref-1", Next(), Next())
            .Add("host.linked", Start.AddMinutes(1), """{"environmentId":"e1000000-0000-4000-8000-000000000001","hostId":"a1000000-0000-4000-8000-000000000001"}""", null, Next(), Next())
            .Add("capacity.daily_summary", Start.AddDays(1), """{"date":"2026-11-14","hosts":[],"late":false,"peakNodeEquivalents":3,"unknownHosts":0,"vcpuBand":16}""", null, Next(), Next())
            .Add("revocation.imported", Start.AddDays(1).AddHours(2), """{"revokedLicenses":1,"sequence":3}""", "actor-ref-1", Next(), Next())
            .Add("license.report_exported", Start.AddDays(1).AddHours(3), $$"""{"challenge":"{{Challenge}}"}""", "actor-ref-1", Next(), Next());

        var intact = Accepted();
        var continued = Accepted();
        var acceptedSeq = continued.HeadSeq;
        var acceptedHash = continued.HeadHash;
        continued.Add("license.report_exported", Start.AddDays(40), """{"challenge":"rnw-2028-next-0001"}""", "actor-ref-1", Next(), Next());

        JsonObject Mutate(Action<JsonArray> change)
        {
            var report = Accepted().Build();
            change(report["events"]!.AsArray());
            return report;
        }

        var edited = Mutate(events => events[2]!["details"]!["peakNodeEquivalents"] = 1);
        var removed = Mutate(events => events.RemoveAt(1));
        var reordered = Mutate(events =>
        {
            var moved = events[1]!.DeepClone();
            events.RemoveAt(1);
            events.Insert(2, moved);
        });

        var cases = new (string File, JsonObject Report, JsonObject Context, string Expected)[]
        {
            ("intact.json", intact.Build(), Context(Challenge), "ok"),
            ("continued.json", continued.Build(), Context("rnw-2028-next-0001", acceptedSeq, acceptedHash), "ok"),
            ("tampered-detail.json", edited, Context(Challenge), "tampered"),
            ("tampered-removed.json", removed, Context(Challenge), "tampered"),
            ("tampered-reordered.json", reordered, Context(Challenge), "tampered"),
            ("discontinuous-head.json", intact.Build(), Context(Challenge, acceptedSeq - 1, "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"), "discontinuous"),
            ("discontinuous-installation.json", intact.Build(), Context(Challenge, installation: "9f0c5a1e-2b4d-4c6e-8f90-a1b2c3d4e5f6"), "discontinuous"),
            ("mismatch-license.json", intact.Build(), Context(Challenge, issued: false), "mismatch"),
            ("mismatch-revocation.json", intact.Build(), Context(Challenge, revocations: []), "mismatch"),
            ("stale.json", intact.Build(), Context("rnw-2026-old-0001"), "stale"),
        };
        var manifest = new JsonArray();
        foreach (var (file, report, context, expected) in cases)
        {
            File.WriteAllText(Path.Combine(directory, file), report.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
            manifest.Add(new JsonObject { ["file"] = file, ["context"] = context, ["expected"] = expected });
        }

        WriteManifest(directory, "Relatórios de apuração (report-format.md) com o contexto do fornecedor e o resultado esperado de LicensingReportVerifier.", manifest);
    }

    private static JsonObject Context(string challenge, long? previousSeq = null, string? previousHash = null, string? installation = null, bool issued = true, int[]? revocations = null) => new()
    {
        ["challenge"] = challenge,
        ["installationId"] = installation ?? Installation.ToString("D"),
        ["previousHeadSeq"] = previousSeq,
        ["previousHeadHash"] = previousHash,
        ["issuedLicenses"] = issued ? new JsonArray(new JsonObject { ["licenseId"] = "lic_7f3c9a", ["revision"] = 2, ["contentSha256"] = ContentSha }) : new JsonArray(),
        ["issuedRevocationSequences"] = new JsonArray([.. (revocations ?? [3]).Select(s => (JsonNode?)s)]),
    };

    private static void WriteManifest(string directory, string description, JsonArray cases) =>
        File.WriteAllText(
            Path.Combine(directory, "manifest.json"),
            new JsonObject { ["formatVersion"] = 1, ["description"] = description, ["cases"] = cases }.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
}
