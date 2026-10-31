// SPDX-License-Identifier: MIT
using System.Text.Json;

namespace Dokpod.Licensing;

/// <summary>Licença revogada até a revisão <see cref="RevisionsUpTo"/>, inclusive; revisões posteriores seguem válidas.</summary>
public sealed record RevokedLicense(string LicenseId, int RevisionsUpTo);

/// <summary>Pacote de revogação verificado (contracts/license-format.md, D-011): escopo mínimo, uma organização.</summary>
public sealed record RevocationDocument(int SchemaVersion, string OrganizationId, int Sequence, DateTimeOffset IssuedAt, IReadOnlyList<RevokedLicense> RevokedLicenses)
{
    public bool Revokes(string organizationId, string licenseId, int revision) =>
        string.Equals(OrganizationId, organizationId, StringComparison.Ordinal)
        && RevokedLicenses.Any(entry => string.Equals(entry.LicenseId, licenseId, StringComparison.Ordinal) && revision <= entry.RevisionsUpTo);
}

public sealed record RevocationVerificationResult(LicenseFailure? Failure, RevocationDocument? Document, string? KeyId, byte[] ContentSha256)
{
    public bool IsValid => Failure is null;
}

/// <summary>
/// Verificador offline do pacote de revogação (spec 031 T051): mesmo perfil fechado da licença, com
/// <c>typ: dokpod-revocation+jws</c>, payload estrito, sequência ≥ 1 e de 1 a 64 licenças, só IDs opacos.
/// A comparação da sequência com a já importada e da organização com a licença ativa é do produto.
/// </summary>
public static class RevocationVerifier
{
    public const int MaxRevokedLicenses = 64;

    private static readonly HashSet<string> RootProperties = new(["schemaVersion", "organizationId", "sequence", "issuedAt", "revokedLicenses"], StringComparer.Ordinal);

    public static RevocationVerificationResult Verify(ReadOnlySpan<byte> document, TrustStore trustStore)
    {
        if (!LicenseVerifier.TryAuthenticate(document, trustStore, JwsCompactReader.RevocationType, out var jws, out var failure, out var contentSha256))
        {
            return new RevocationVerificationResult(failure, null, jws?.KeyId, contentSha256);
        }

        return TryParse(jws!.Payload, out var revocation, out failure)
            ? new RevocationVerificationResult(null, revocation, jws.KeyId, contentSha256)
            : new RevocationVerificationResult(failure, null, jws.KeyId, contentSha256);
    }

    private static bool TryParse(byte[] payload, out RevocationDocument? revocation, out LicenseFailure failure)
    {
        revocation = null;
        failure = LicenseFailure.Malformed;
        if (!LicensePayloadParser.HasNoDuplicateProperties(payload)) return false;

        JsonDocument json;
        try
        {
            json = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = LicensePayloadParser.MaxDepth });
        }
        catch (JsonException)
        {
            return false;
        }

        using (json)
        {
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !LicensePayloadParser.TryInt(root, "schemaVersion", out var schemaVersion)) return false;
            if (schemaVersion != 1)
            {
                failure = LicenseFailure.UnsupportedProfile;
                return false;
            }

            if (root.EnumerateObject().Any(property => !RootProperties.Contains(property.Name))
                || !LicensePayloadParser.TryId(root, "organizationId", out var organizationId)
                || !LicensePayloadParser.TryInt(root, "sequence", out var sequence)
                || sequence < 1
                || !LicensePayloadParser.TryInstant(root, "issuedAt", out var issuedAt)
                || !root.TryGetProperty("revokedLicenses", out var entries)
                || entries.ValueKind != JsonValueKind.Array
                || entries.GetArrayLength() is 0 or > MaxRevokedLicenses)
            {
                return false;
            }

            var revoked = new List<RevokedLicense>();
            foreach (var entry in entries.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object
                    || entry.EnumerateObject().Any(property => property.Name is not ("licenseId" or "revisionsUpTo"))
                    || !LicensePayloadParser.TryId(entry, "licenseId", out var licenseId)
                    || !LicensePayloadParser.TryInt(entry, "revisionsUpTo", out var revisionsUpTo)
                    || revisionsUpTo < 1)
                {
                    return false;
                }

                revoked.Add(new RevokedLicense(licenseId!, revisionsUpTo));
            }

            if (revoked.Select(entry => entry.LicenseId).Distinct(StringComparer.Ordinal).Count() != revoked.Count) return false;

            revocation = new RevocationDocument(schemaVersion, organizationId!, sequence, issuedAt, revoked);
            return true;
        }
    }
}
