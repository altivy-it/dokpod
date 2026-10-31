// SPDX-License-Identifier: MIT
using System.Buffers.Text;
using System.Text.Json;

namespace Dokpod.Licensing;

/// <summary>Documento JWS Compact já separado; <see cref="SigningInput"/> são os bytes recebidos de <c>header.payload</c>.</summary>
public sealed class JwsCompact
{
    internal JwsCompact(string keyId, string type, byte[] signingInput, byte[] payload, byte[] signature)
    {
        KeyId = keyId;
        Type = type;
        SigningInput = signingInput;
        Payload = payload;
        Signature = signature;
    }

    public string KeyId { get; }

    public string Type { get; }

    public byte[] SigningInput { get; }

    public byte[] Payload { get; }

    public byte[] Signature { get; }
}

/// <summary>
/// Leitor JWS Compact de perfil fechado (spec 031 D-001, D-003, T022): arquivo ASCII de até 64 KiB, Base64URL sem
/// padding, header com exatamente <c>alg</c>, <c>kid</c> e <c>typ</c>, <c>ES256</c> e assinatura IEEE P1363 de 64
/// bytes. Não verifica a assinatura nem interpreta o payload: isso é do <see cref="LicenseVerifier"/>.
/// </summary>
public static class JwsCompactReader
{
    public const int MaxDocumentBytes = 65536;
    public const string LicenseType = "dokpod-license+jws";
    public const string RevocationType = "dokpod-revocation+jws";
    private const int SignatureBytes = 64;
    private const int MaxKeyIdLength = 64;

    public static bool TryRead(ReadOnlySpan<byte> document, out JwsCompact? jws, out LicenseFailure failure)
    {
        jws = null;
        failure = LicenseFailure.Malformed;
        if (document.IsEmpty || document.Length > MaxDocumentBytes)
        {
            return false;
        }

        var firstDot = -1;
        var secondDot = -1;
        for (var index = 0; index < document.Length; index++)
        {
            var value = document[index];
            if (value == (byte)'.')
            {
                if (firstDot < 0) firstDot = index;
                else if (secondDot < 0) secondDot = index;
                else return false;
            }
            else if (!IsBase64UrlCharacter(value))
            {
                return false;
            }
        }

        if (firstDot <= 0 || secondDot <= firstDot + 1 || secondDot == document.Length - 1)
        {
            return false;
        }

        if (!TryDecode(document[..firstDot], out var header)
            || !TryDecode(document[(firstDot + 1)..secondDot], out var payload)
            || !TryDecode(document[(secondDot + 1)..], out var signature))
        {
            return false;
        }

        if (!TryReadHeader(header, out var keyId, out var type, out failure))
        {
            return false;
        }

        if (signature.Length != SignatureBytes)
        {
            failure = LicenseFailure.Malformed;
            return false;
        }

        jws = new JwsCompact(keyId!, type!, document[..secondDot].ToArray(), payload, signature);
        return true;
    }

    private static bool TryReadHeader(byte[] header, out string? keyId, out string? type, out LicenseFailure failure)
    {
        keyId = null;
        type = null;
        string? algorithm = null;
        failure = LicenseFailure.Malformed;
        try
        {
            var reader = new Utf8JsonReader(header, new JsonReaderOptions { MaxDepth = 2 });
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) return false;
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                var name = reader.GetString();
                if (!reader.Read()) return false;
                if (name is not ("alg" or "kid" or "typ"))
                {
                    // jwk, jku, x5u, x5c, crit ou qualquer outro membro: o arquivo não escolhe chave nem algoritmo.
                    failure = LicenseFailure.UnsupportedProfile;
                    return false;
                }

                if (reader.TokenType != JsonTokenType.String) return false;
                var value = reader.GetString();
                switch (name)
                {
                    case "alg" when algorithm is null: algorithm = value; break;
                    case "kid" when keyId is null: keyId = value; break;
                    case "typ" when type is null: type = value; break;
                    default: return false;
                }
            }

            if (reader.TokenType != JsonTokenType.EndObject || reader.Read()) return false;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        if (algorithm != "ES256" || type is not (LicenseType or RevocationType) || keyId is null)
        {
            failure = LicenseFailure.UnsupportedProfile;
            return false;
        }

        if (keyId.Length is 0 or > MaxKeyIdLength || !keyId.All(character => character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'))
        {
            failure = LicenseFailure.Malformed;
            return false;
        }

        return true;
    }

    private static bool TryDecode(ReadOnlySpan<byte> segment, out byte[] decoded)
    {
        decoded = [];
        if (segment.Length % 4 == 1) return false;
        var buffer = new byte[Base64Url.GetMaxDecodedLength(segment.Length)];
        if (Base64Url.DecodeFromUtf8(segment, buffer, out var consumed, out var written) != System.Buffers.OperationStatus.Done
            || consumed != segment.Length)
        {
            return false;
        }

        decoded = buffer[..written];
        return true;
    }

    private static bool IsBase64UrlCharacter(byte value) =>
        value is (>= (byte)'A' and <= (byte)'Z') or (>= (byte)'a' and <= (byte)'z') or (>= (byte)'0' and <= (byte)'9') or (byte)'-' or (byte)'_';

}
