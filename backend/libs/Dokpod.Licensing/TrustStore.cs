// SPDX-License-Identifier: MIT
using System.Security.Cryptography;

namespace Dokpod.Licensing;

/// <summary>Chave pública confiável. Só P-256 e só a parte pública: o trust store nunca carrega chave privada.</summary>
public sealed class TrustedKey : IDisposable
{
    private const string NistP256Oid = "1.2.840.10045.3.1.7";

    private TrustedKey(string keyId, ECDsa publicKey)
    {
        KeyId = keyId;
        PublicKey = publicKey;
    }

    public string KeyId { get; }

    internal ECDsa PublicKey { get; }

    /// <summary>Importa um PEM <c>PUBLIC KEY</c> (SubjectPublicKeyInfo) P-256; qualquer outro rótulo ou curva é recusado.</summary>
    public static TrustedKey FromPublicKeyPem(string keyId, string pem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(pem);
        var fields = PemEncoding.Find(pem);
        if (!pem.AsSpan()[fields.Label].SequenceEqual("PUBLIC KEY"))
        {
            throw new ArgumentException("Trust store accepts only PUBLIC KEY PEM entries.", nameof(pem));
        }

        var key = ECDsa.Create();
        try
        {
            key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(pem[fields.Base64Data]), out _);
            if (key.ExportParameters(false).Curve.Oid.Value != NistP256Oid)
            {
                throw new ArgumentException("Trust store accepts only P-256 keys.", nameof(pem));
            }

            return new TrustedKey(keyId, key);
        }
        catch
        {
            key.Dispose();
            throw;
        }
    }

    public static TrustedKey FromPublicKey(string keyId, ECDsa key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return FromPublicKeyPem(keyId, key.ExportSubjectPublicKeyInfoPem());
    }

    public void Dispose() => PublicKey.Dispose();
}

/// <summary>Chaves públicas embarcadas no produto, várias por <c>kid</c> para permitir sobreposição na rotação (D-011).</summary>
public sealed class TrustStore : IDisposable
{
    private readonly Dictionary<string, List<TrustedKey>> keys = new(StringComparer.Ordinal);

    public TrustStore(IEnumerable<TrustedKey> trustedKeys)
    {
        ArgumentNullException.ThrowIfNull(trustedKeys);
        foreach (var key in trustedKeys)
        {
            if (!keys.TryGetValue(key.KeyId, out var list))
            {
                keys[key.KeyId] = list = [];
            }

            list.Add(key);
        }
    }

    public static TrustStore Empty { get; } = new([]);

    public IReadOnlyCollection<string> KeyIds => keys.Keys;

    internal IReadOnlyList<TrustedKey> Find(string keyId) => keys.TryGetValue(keyId, out var list) ? list : [];

    public void Dispose()
    {
        foreach (var key in keys.Values.SelectMany(list => list)) key.Dispose();
    }
}
