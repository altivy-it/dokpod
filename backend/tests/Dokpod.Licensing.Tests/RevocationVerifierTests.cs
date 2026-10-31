using System.Text.Json.Nodes;
using Dokpod.Licensing.Tests.Vectors;
using Xunit;

namespace Dokpod.Licensing.Tests;

/// <summary>Spec 031 T049 (D-011): pacote de revogação só como revogação, escopo mínimo e rotação com duas chaves.</summary>
public sealed class RevocationVerifierTests : IDisposable
{
    private readonly LicenseVectors vectors = new();

    public void Dispose() => vectors.Dispose();

    [Fact]
    public void ValidPackage_IsAcceptedAndRevokesUpToTheRevision()
    {
        var result = RevocationVerifier.Verify(vectors.Revocation(), vectors.TrustStore);

        Assert.True(result.IsValid);
        var document = result.Document!;
        Assert.Equal((3, "org_51d2e0"), (document.Sequence, document.OrganizationId));
        Assert.True(document.Revokes("org_51d2e0", "lic_7f3c9a", 2));
        Assert.False(document.Revokes("org_51d2e0", "lic_7f3c9a", 3));
        Assert.False(document.Revokes("org_other", "lic_7f3c9a", 1));
    }

    [Fact]
    public void Types_AreNeverAcceptedAsEachOther()
    {
        Assert.Equal(LicenseFailure.UnsupportedProfile, RevocationVerifier.Verify(vectors.Valid(), vectors.TrustStore).Failure);
        Assert.Equal(LicenseFailure.UnsupportedProfile, LicenseVerifier.Verify(vectors.Revocation(), vectors.TrustStore, LicenseVectors.Instant).Failure);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("over-64")]
    [InlineData("duplicate-license")]
    [InlineData("sequence-zero")]
    [InlineData("revision-zero")]
    [InlineData("unknown-property")]
    [InlineData("id-outside-pattern")]
    public void MalformedPackages_AreRefused(string scenario)
    {
        var document = vectors.Revocation(payload =>
        {
            JsonObject Entry(string id) => new() { ["licenseId"] = id, ["revisionsUpTo"] = 1 };
            switch (scenario)
            {
                case "empty": payload["revokedLicenses"] = new JsonArray(); break;
                case "over-64": payload["revokedLicenses"] = new JsonArray([.. Enumerable.Range(0, 65).Select(i => (JsonNode?)Entry($"lic_{i}"))]); break;
                case "duplicate-license": payload["revokedLicenses"] = new JsonArray(Entry("lic_a"), Entry("lic_a")); break;
                case "sequence-zero": payload["sequence"] = 0; break;
                case "revision-zero": payload["revokedLicenses"]![0]!["revisionsUpTo"] = 0; break;
                case "unknown-property": payload["customers"] = new JsonArray("org_other"); break;
                case "id-outside-pattern": payload["organizationId"] = "org 1"; break;
            }
        });

        Assert.Equal(LicenseFailure.Malformed, RevocationVerifier.Verify(document, vectors.TrustStore).Failure);
    }

    [Fact]
    public void SixtyFourEntries_AreAccepted()
    {
        var document = vectors.Revocation(payload => payload["revokedLicenses"] = new JsonArray(
            [.. Enumerable.Range(0, 64).Select(i => (JsonNode?)new JsonObject { ["licenseId"] = $"lic_{i}", ["revisionsUpTo"] = 1 })]));

        Assert.Equal(64, RevocationVerifier.Verify(document, vectors.TrustStore).Document!.RevokedLicenses.Count);
    }

    [Fact]
    public void SchemaVersion2_IsUnsupported() =>
        Assert.Equal(LicenseFailure.UnsupportedProfile, RevocationVerifier.Verify(vectors.Revocation(p => p["schemaVersion"] = 2), vectors.TrustStore).Failure);

    [Fact]
    public void Rotation_TrustStoreWithTwoKeysAcceptsLicensesFromBoth()
    {
        using var other = new LicenseVectors();
        // Sobreposição na rotação: duas chaves no mesmo kid, e a nova também publicada sob um kid próprio.
        using var store = new TrustStore([
            TrustedKey.FromPublicKey(LicenseVectors.TestKeyId, vectors.SigningKey),
            TrustedKey.FromPublicKey(LicenseVectors.TestKeyId, other.SigningKey),
            TrustedKey.FromPublicKey("dokpod-2027-1", other.SigningKey),
        ]);

        Assert.True(LicenseVerifier.Verify(vectors.Valid(), store, LicenseVectors.Instant).IsValid);
        Assert.True(LicenseVerifier.Verify(other.Valid(), store, LicenseVectors.Instant).IsValid);
        Assert.True(LicenseVerifier.Verify(other.Sign(LicenseVectors.Header(kid: "dokpod-2027-1"), LicenseVectors.Payload().ToJsonString()), store, LicenseVectors.Instant).IsValid);
        Assert.True(RevocationVerifier.Verify(other.Revocation(), store).IsValid);
        Assert.Equal(2, store.KeyIds.Count);

        // Retirada da chave antiga: o que ela assinou deixa de valer.
        using var retired = new TrustStore([TrustedKey.FromPublicKey(LicenseVectors.TestKeyId, other.SigningKey)]);
        Assert.Equal(LicenseFailure.BadSignature, LicenseVerifier.Verify(vectors.Valid(), retired, LicenseVectors.Instant).Failure);
    }
}
