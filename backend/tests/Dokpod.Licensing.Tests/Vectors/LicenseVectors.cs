using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Dokpod.Licensing.Tests.Vectors;

/// <summary>
/// Vetores de conformidade (spec 031 T018). A chave P-256 é sintética e gerada a cada execução: nenhuma chave privada
/// fica no repositório, e o <c>kid</c> de teste nunca entra no trust store de produção.
/// </summary>
public sealed class LicenseVectors : IDisposable
{
    public const string TestKeyId = "dokpod-test-1";

    public LicenseVectors()
    {
        SigningKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        TrustStore = new TrustStore([TrustedKey.FromPublicKey(TestKeyId, SigningKey)]);
    }

    public ECDsa SigningKey { get; }

    public TrustStore TrustStore { get; }

    public static DateTimeOffset Instant { get; } = new(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);

    public static string Header(string alg = "ES256", string kid = TestKeyId, string typ = "dokpod-license+jws") =>
        $$"""{"alg":"{{alg}}","kid":"{{kid}}","typ":"{{typ}}"}""";

    public static JsonObject Payload() => new()
    {
        ["schemaVersion"] = 1,
        ["meteringVersion"] = 1,
        ["licenseId"] = "lic_7f3c9a",
        ["organizationId"] = "org_51d2e0",
        ["revision"] = 2,
        ["product"] = "dokpod-business",
        ["plan"] = "business",
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
        ["capabilities"] = new JsonArray("image-update-notice", "change-windows"),
    };

    public byte[] Sign(string header, string payload) => Sign(Encoding.UTF8.GetBytes(header), Encoding.UTF8.GetBytes(payload));

    public byte[] Sign(byte[] header, byte[] payload)
    {
        var signingInput = $"{Base64Url.EncodeToString(header)}.{Base64Url.EncodeToString(payload)}";
        var signature = SigningKey.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return Encoding.ASCII.GetBytes($"{signingInput}.{Base64Url.EncodeToString(signature)}");
    }

    public byte[] Valid() => Sign(Header(), Payload().ToJsonString());

    public byte[] WithPayload(Action<JsonObject> change)
    {
        var payload = Payload();
        change(payload);
        return Sign(Header(), payload.ToJsonString());
    }

    public void Dispose()
    {
        TrustStore.Dispose();
        SigningKey.Dispose();
    }
}
