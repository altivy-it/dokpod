using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Dokpod.Licensing.Tests.Vectors;
using Xunit;

namespace Dokpod.Licensing.Tests;

/// <summary>Spec 031 T018: um caso por falha tipada de contracts/license-format.md, mais o documento válido.</summary>
public sealed class LicenseVerifierVectorTests : IDisposable
{
    private readonly LicenseVectors vectors = new();

    public void Dispose() => vectors.Dispose();

    [Fact]
    public void ValidLicense_IsAcceptedWithDocumentAndValidity()
    {
        var result = LicenseVerifier.Verify(vectors.Valid(), vectors.TrustStore, LicenseVectors.Instant);

        Assert.True(result.IsValid);
        Assert.Equal(LicenseVectors.TestKeyId, result.KeyId);
        Assert.Equal(LicenseValidity.Active, result.Validity);
        var document = result.Document!;
        Assert.Equal(("lic_7f3c9a", "org_51d2e0", 2), (document.LicenseId, document.OrganizationId, document.Revision));
        Assert.Equal(10, document.Capacity.NodeEquivalents);
        Assert.Equal(["prd", "hml"], document.Slots.Select(slot => slot.SlotId));
        Assert.Equal(new DateTimeOffset(2027, 11, 1, 0, 0, 0, TimeSpan.Zero), document.ExpiresAt);
    }

    [Fact]
    public void Validity_FollowsNotBeforeAndExpiresAt()
    {
        var document = vectors.Valid();

        Assert.Equal(LicenseValidity.NotStarted, LicenseVerifier.Verify(document, vectors.TrustStore, new DateTimeOffset(2026, 10, 31, 23, 59, 59, TimeSpan.Zero)).Validity);
        Assert.Equal(LicenseValidity.Expired, LicenseVerifier.Verify(document, vectors.TrustStore, new DateTimeOffset(2027, 11, 1, 0, 0, 0, TimeSpan.Zero)).Validity);
    }

    [Fact]
    public void ContentHash_CoversSigningInputSoMalleatedSignaturesShareIt()
    {
        var first = vectors.Valid();
        var second = vectors.Valid();

        Assert.NotEqual(first, second);
        Assert.Equal(
            LicenseVerifier.Verify(first, vectors.TrustStore, LicenseVectors.Instant).ContentSha256,
            LicenseVerifier.Verify(second, vectors.TrustStore, LicenseVectors.Instant).ContentSha256);
    }

    public static TheoryData<string> MalformedCases() =>
    [
        "id-outside-pattern", "base64-padding", "invalid-utf8", "duplicate-property", "unknown-property", "depth-over-8",
        "file-over-64kib", "integer-outside-int32", "string-over-128", "plan-over-64", "three-segments-missing",
        "short-signature", "kid-uppercase",
    ];

    [Theory]
    [MemberData(nameof(MalformedCases))]
    public void Malformed(string scenario) => AssertFailure(LicenseFailure.Malformed, MalformedDocument(scenario));

    public static TheoryData<string> UnsupportedProfileCases() =>
    [
        "alg-none", "alg-hs256", "header-jwk", "header-jku", "header-x5u", "header-x5c", "header-crit", "typ-other",
        "typ-revocation", "schema-version-2", "metering-version-2", "window-not-30",
    ];

    [Theory]
    [MemberData(nameof(UnsupportedProfileCases))]
    public void UnsupportedProfile(string scenario) => AssertFailure(LicenseFailure.UnsupportedProfile, UnsupportedDocument(scenario));

    [Fact]
    public void UnknownKey() => AssertFailure(
        LicenseFailure.UnknownKey,
        vectors.Sign(LicenseVectors.Header(kid: "dokpod-other-1"), LicenseVectors.Payload().ToJsonString()));

    [Fact]
    public void BadSignature_OneBitFlipped()
    {
        var parts = Encoding.ASCII.GetString(vectors.Valid()).Split('.');
        var signature = Base64Url.DecodeFromChars(parts[2]);
        signature[10] ^= 0x01;

        AssertFailure(LicenseFailure.BadSignature, Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}.{Base64Url.EncodeToString(signature)}"));
    }

    [Fact]
    public void BadSignature_PayloadChangedAfterSigning()
    {
        var parts = Encoding.ASCII.GetString(vectors.Valid()).Split('.');
        var tampered = LicenseVectors.Payload();
        tampered["capacity"]!["nodeEquivalents"] = 1000;

        AssertFailure(LicenseFailure.BadSignature, Encoding.ASCII.GetBytes($"{parts[0]}.{Base64Url.EncodeToString(Encoding.UTF8.GetBytes(tampered.ToJsonString()))}.{parts[2]}"));
    }

    [Fact]
    public void WrongProduct() => AssertFailure(LicenseFailure.WrongProduct, vectors.WithPayload(p => p["product"] = "dokpod-community"));

    [Theory]
    [InlineData("2026-11-02T00:00:00Z", "2026-11-01T00:00:00Z", "2027-11-01T00:00:00Z")]
    [InlineData("2026-11-01T00:00:00Z", "2027-11-01T00:00:00Z", "2027-11-01T00:00:00Z")]
    [InlineData("2026-11-01T00:00:00Z", "2027-12-01T00:00:00Z", "2027-11-01T00:00:00Z")]
    public void InconsistentDates(string issuedAt, string notBefore, string expiresAt) => AssertFailure(
        LicenseFailure.InconsistentDates,
        vectors.WithPayload(p =>
        {
            p["issuedAt"] = issuedAt;
            p["notBefore"] = notBefore;
            p["expiresAt"] = expiresAt;
        }));

    public static TheoryData<string> InvalidCapacityCases() =>
    [
        "nodes-zero", "nodes-negative", "no-slots", "three-slots", "quota-sum-above-capacity", "vcpu-band-zero",
        "unlimited-with-nodes", "limited-without-nodes", "slot-kind-other", "duplicate-slot", "negative-quota",
    ];

    [Theory]
    [MemberData(nameof(InvalidCapacityCases))]
    public void InvalidCapacity(string scenario) => AssertFailure(LicenseFailure.InvalidCapacity, vectors.WithPayload(p => ChangeCapacity(p, scenario)));

    [Fact]
    public void Capacity_DisabledSlotDoesNotUseQuotaAndUnlimitedIsExplicit()
    {
        var disabledOverflow = vectors.WithPayload(p =>
        {
            p["slots"]![1]!["enabled"] = false;
            p["slots"]![1]!["nodeEquivalents"] = 50;
        });
        var unlimited = vectors.WithPayload(p =>
        {
            p["capacity"] = new JsonObject { ["mode"] = "unlimited" };
            p["slots"] = new JsonArray(new JsonObject { ["slotId"] = "prd", ["kind"] = "production", ["enabled"] = true });
        });

        Assert.True(LicenseVerifier.Verify(disabledOverflow, vectors.TrustStore, LicenseVectors.Instant).IsValid);
        var result = LicenseVerifier.Verify(unlimited, vectors.TrustStore, LicenseVectors.Instant);
        Assert.True(result.IsValid);
        Assert.True(result.Document!.Capacity.IsUnlimited);
        Assert.Null(result.Document.Slots[0].NodeEquivalents);
    }

    [Fact]
    public void UnknownCapability_OutsideAllowlist() => AssertFailure(
        LicenseFailure.UnknownCapability,
        vectors.WithPayload(p => p["capabilities"] = new JsonArray("change-windows", "telemetry")));

    [Fact]
    public void UnknownCapability_MoreThan64Items() => AssertFailure(
        LicenseFailure.UnknownCapability,
        vectors.WithPayload(p => p["capabilities"] = new JsonArray([.. Enumerable.Range(0, 65).Select(_ => (JsonNode?)JsonValue.Create("change-windows"))])));

    [Fact]
    public void TrustStore_RefusesPrivateKeysAndOtherCurves()
    {
        using var p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);

        Assert.Throws<ArgumentException>(() => TrustedKey.FromPublicKeyPem("dokpod-x", vectors.SigningKey.ExportPkcs8PrivateKeyPem()));
        Assert.Throws<ArgumentException>(() => TrustedKey.FromPublicKey("dokpod-x", p384));
    }

    private void AssertFailure(LicenseFailure expected, byte[] document)
    {
        var result = LicenseVerifier.Verify(document, vectors.TrustStore, LicenseVectors.Instant);

        Assert.False(result.IsValid);
        Assert.Equal(expected, result.Failure);
        Assert.Null(result.Document);
        Assert.Equal(32, result.ContentSha256.Length);
    }

    private byte[] MalformedDocument(string scenario)
    {
        var header = LicenseVectors.Header();
        var payload = LicenseVectors.Payload().ToJsonString();
        switch (scenario)
        {
            case "id-outside-pattern":
                return vectors.WithPayload(p => p["licenseId"] = "lic 7f3c9a");
            case "base64-padding":
                {
                    var parts = Encoding.ASCII.GetString(vectors.Valid()).Split('.');
                    return Encoding.ASCII.GetBytes($"{parts[0]}=.{parts[1]}.{parts[2]}");
                }
            case "invalid-utf8":
                {
                    var bytes = Encoding.UTF8.GetBytes(payload.Replace("\"business\"", "\"busXness\"", StringComparison.Ordinal));
                    var index = Encoding.UTF8.GetString(bytes).IndexOf("busXness", StringComparison.Ordinal) + 3;
                    bytes[index] = 0xC3;
                    return vectors.Sign(Encoding.UTF8.GetBytes(header), bytes);
                }
            case "duplicate-property":
                return vectors.Sign(header, payload.Replace("\"plan\":\"business\"", "\"plan\":\"business\",\"plan\":\"enterprise\"", StringComparison.Ordinal));
            case "unknown-property":
                return vectors.WithPayload(p => p["price"] = 10);
            case "depth-over-8":
                return vectors.Sign(header, payload.TrimEnd('}') + ",\"x\":[[[[[[[[[1]]]]]]]]]}");
            case "file-over-64kib":
                return [.. vectors.Valid(), .. Enumerable.Repeat((byte)'A', JwsCompactReader.MaxDocumentBytes)];
            case "integer-outside-int32":
                return vectors.Sign(header, payload.Replace("\"revision\":2", "\"revision\":2147483648", StringComparison.Ordinal));
            case "string-over-128":
                return vectors.WithPayload(p => p["organizationId"] = new string('o', 129));
            case "plan-over-64":
                return vectors.WithPayload(p => p["plan"] = new string('p', 65));
            case "three-segments-missing":
                {
                    var parts = Encoding.ASCII.GetString(vectors.Valid()).Split('.');
                    return Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
                }
            case "short-signature":
                {
                    var parts = Encoding.ASCII.GetString(vectors.Valid()).Split('.');
                    return Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}.{Base64Url.EncodeToString(new byte[32])}");
                }
            case "kid-uppercase":
                return vectors.Sign(LicenseVectors.Header(kid: "Dokpod-Test-1"), payload);
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }
    }

    private byte[] UnsupportedDocument(string scenario)
    {
        var payload = LicenseVectors.Payload().ToJsonString();
        return scenario switch
        {
            "alg-none" => vectors.Sign(LicenseVectors.Header(alg: "none"), payload),
            "alg-hs256" => vectors.Sign(LicenseVectors.Header(alg: "HS256"), payload),
            "header-jwk" => vectors.Sign(LicenseVectors.Header().TrimEnd('}') + ",\"jwk\":{\"kty\":\"EC\"}}", payload),
            "header-jku" => vectors.Sign(LicenseVectors.Header().TrimEnd('}') + ",\"jku\":\"https://example.invalid/keys\"}", payload),
            "header-x5u" => vectors.Sign(LicenseVectors.Header().TrimEnd('}') + ",\"x5u\":\"https://example.invalid/cert\"}", payload),
            "header-x5c" => vectors.Sign(LicenseVectors.Header().TrimEnd('}') + ",\"x5c\":[\"MIIB\"]}", payload),
            "header-crit" => vectors.Sign(LicenseVectors.Header().TrimEnd('}') + ",\"crit\":[\"exp\"]}", payload),
            "typ-other" => vectors.Sign(LicenseVectors.Header(typ: "JWT"), payload),
            "typ-revocation" => vectors.Sign(LicenseVectors.Header(typ: "dokpod-revocation+jws"), payload),
            "schema-version-2" => vectors.WithPayload(p => p["schemaVersion"] = 2),
            "metering-version-2" => vectors.WithPayload(p => p["meteringVersion"] = 2),
            "window-not-30" => vectors.WithPayload(p => p["windowDays"] = 7),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };
    }

    private static void ChangeCapacity(JsonObject payload, string scenario)
    {
        JsonObject Slot(string id, string kind, int quota) => new() { ["slotId"] = id, ["kind"] = kind, ["enabled"] = true, ["nodeEquivalents"] = quota };
        switch (scenario)
        {
            case "nodes-zero": payload["capacity"]!["nodeEquivalents"] = 0; break;
            case "nodes-negative": payload["capacity"]!["nodeEquivalents"] = -1; break;
            case "no-slots": payload["slots"] = new JsonArray(); break;
            case "three-slots": payload["slots"] = new JsonArray(Slot("a", "production", 1), Slot("b", "staging", 1), Slot("c", "staging", 1)); break;
            case "quota-sum-above-capacity": payload["slots"]![0]!["nodeEquivalents"] = 9; break;
            case "vcpu-band-zero": payload["vcpuBand"] = 0; break;
            case "unlimited-with-nodes": payload["capacity"] = new JsonObject { ["mode"] = "unlimited", ["nodeEquivalents"] = 10 }; break;
            case "limited-without-nodes": payload["capacity"] = new JsonObject { ["mode"] = "limited" }; break;
            case "slot-kind-other": payload["slots"]![1]!["kind"] = "development"; break;
            case "duplicate-slot": payload["slots"]![1]!["slotId"] = "prd"; break;
            case "negative-quota": payload["slots"]![1]!["nodeEquivalents"] = -1; break;
            default: throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }
    }
}
