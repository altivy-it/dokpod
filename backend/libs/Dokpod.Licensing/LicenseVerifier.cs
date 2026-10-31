// SPDX-License-Identifier: MIT
using System.Security.Cryptography;

namespace Dokpod.Licensing;

/// <summary>Vigência da licença num instante; a tolerância após a expiração é decidida pelo produto, não pelo verificador.</summary>
public enum LicenseValidity
{
    NotStarted,
    Active,
    Expired,
}

/// <summary>
/// Resultado imutável da verificação. <see cref="ContentSha256"/> é o SHA-256 do conteúdo assinado
/// (<c>header.payload</c> recebido), base da idempotência; quando o envelope nem pôde ser lido, é o hash dos bytes.
/// </summary>
public sealed record LicenseVerificationResult(
    LicenseFailure? Failure,
    LicenseDocument? Document,
    string? KeyId,
    byte[] ContentSha256,
    LicenseValidity? Validity)
{
    public bool IsValid => Failure is null;
}

/// <summary>
/// Verificador offline da licença (spec 031 T024, ADR 2026-0008): lê o envelope, procura o <c>kid</c> só no trust store,
/// confere a assinatura ES256 sobre os bytes recebidos e só então interpreta o payload. Sem I/O, sem rede, sem relógio.
/// </summary>
public static class LicenseVerifier
{
    public static LicenseVerificationResult Verify(ReadOnlySpan<byte> document, TrustStore trustStore, DateTimeOffset instant)
    {
        ArgumentNullException.ThrowIfNull(trustStore);
        if (!JwsCompactReader.TryRead(document, out var jws, out var failure))
        {
            return Failed(failure, null, SHA256.HashData(document));
        }

        var contentSha256 = SHA256.HashData(jws!.SigningInput);
        if (jws.Type != JwsCompactReader.LicenseType)
        {
            return Failed(LicenseFailure.UnsupportedProfile, jws.KeyId, contentSha256);
        }

        var candidates = trustStore.Find(jws.KeyId);
        if (candidates.Count == 0)
        {
            return Failed(LicenseFailure.UnknownKey, jws.KeyId, contentSha256);
        }

        if (!candidates.Any(key => key.PublicKey.VerifyData(
                jws.SigningInput,
                jws.Signature,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation)))
        {
            return Failed(LicenseFailure.BadSignature, jws.KeyId, contentSha256);
        }

        if (!LicensePayloadParser.TryParse(jws.Payload, out var license, out failure))
        {
            return Failed(failure, jws.KeyId, contentSha256);
        }

        var validity = instant < license!.NotBefore
            ? LicenseValidity.NotStarted
            : instant < license.ExpiresAt ? LicenseValidity.Active : LicenseValidity.Expired;
        return new LicenseVerificationResult(null, license, jws.KeyId, contentSha256, validity);
    }

    private static LicenseVerificationResult Failed(LicenseFailure failure, string? keyId, byte[] contentSha256) =>
        new(failure, null, keyId, contentSha256, null);
}
