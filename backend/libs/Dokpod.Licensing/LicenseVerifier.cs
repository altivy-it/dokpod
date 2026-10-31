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
        if (!TryAuthenticate(document, trustStore, JwsCompactReader.LicenseType, out var jws, out var failure, out var contentSha256))
        {
            return Failed(failure, jws?.KeyId, contentSha256);
        }

        if (!LicensePayloadParser.TryParse(jws!.Payload, out var license, out failure))
        {
            return Failed(failure, jws.KeyId, contentSha256);
        }

        var validity = instant < license!.NotBefore
            ? LicenseValidity.NotStarted
            : instant < license.ExpiresAt ? LicenseValidity.Active : LicenseValidity.Expired;
        return new LicenseVerificationResult(null, license, jws.KeyId, contentSha256, validity);
    }

    /// <summary>
    /// Envelope, tipo, <c>kid</c> no trust store e assinatura ES256 sobre os bytes recebidos, nessa ordem, antes de
    /// interpretar o payload. Um tipo nunca é aceito como outro: licença não vale como revogação e vice-versa (D-011).
    /// </summary>
    internal static bool TryAuthenticate(
        ReadOnlySpan<byte> document,
        TrustStore trustStore,
        string expectedType,
        out JwsCompact? jws,
        out LicenseFailure failure,
        out byte[] contentSha256)
    {
        ArgumentNullException.ThrowIfNull(trustStore);
        if (!JwsCompactReader.TryRead(document, out jws, out failure))
        {
            contentSha256 = SHA256.HashData(document);
            return false;
        }

        contentSha256 = SHA256.HashData(jws!.SigningInput);
        if (jws.Type != expectedType)
        {
            failure = LicenseFailure.UnsupportedProfile;
            return false;
        }

        var candidates = trustStore.Find(jws.KeyId);
        if (candidates.Count == 0)
        {
            failure = LicenseFailure.UnknownKey;
            return false;
        }

        var signed = jws;
        if (!candidates.Any(key => key.PublicKey.VerifyData(
                signed.SigningInput,
                signed.Signature,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation)))
        {
            failure = LicenseFailure.BadSignature;
            return false;
        }

        return true;
    }

    private static LicenseVerificationResult Failed(LicenseFailure failure, string? keyId, byte[] contentSha256) =>
        new(failure, null, keyId, contentSha256, null);
}
