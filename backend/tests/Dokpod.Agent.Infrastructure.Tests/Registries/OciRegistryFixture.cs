using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Dokpod.Agent.Infrastructure.Tests.Registries;

/// <summary>
/// Registro OCI real (spec 025 T002), em container <c>docker.io/library/registry:3.1.2</c> (CNCF Distribution, Apache-2.0),
/// com TLS de uma CA de teste e <c>htpasswd</c> gerados na execução. O teste não sobe o container: usa a URL de
/// <c>DOKPOD_TEST_OCI_REGISTRY</c> (<c>https://host:porta</c>), a CA de <c>DOKPOD_TEST_OCI_REGISTRY_CA</c> (arquivo PEM) e a
/// credencial de <c>DOKPOD_TEST_OCI_REGISTRY_USER</c>/<c>DOKPOD_TEST_OCI_REGISTRY_PASSWORD</c>; sem a URL, o teste é ignorado.
/// Publica imagens pela API de upload (blob monolítico e manifesto), sem engine.
/// </summary>
internal sealed class OciRegistryFixture : IDisposable
{
    public const string Image = "docker.io/library/registry:3.1.2";
    private const string ManifestType = "application/vnd.oci.image.manifest.v1+json";
    private const string IndexType = "application/vnd.oci.image.index.v1+json";

    private readonly HttpClient client;

    private OciRegistryFixture(Uri endpoint, X509Certificate2Collection trust, string username, string credential)
    {
        Endpoint = endpoint;
        Trust = trust;
        Username = username;
        Credential = credential;
        var handler = new SocketsHttpHandler
        {
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                CertificateChainPolicy = new X509ChainPolicy { TrustMode = X509ChainTrustMode.CustomRootTrust, RevocationMode = X509RevocationMode.NoCheck },
            },
        };
        handler.SslOptions.CertificateChainPolicy.CustomTrustStore.AddRange(trust);
        client = new HttpClient(handler) { BaseAddress = endpoint };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{credential}")));
    }

    public Uri Endpoint { get; }

    public X509Certificate2Collection Trust { get; }

    public string Username { get; }

    public string Credential { get; }

    /// <summary>Registro como aparece numa referência de imagem (<c>host:porta</c>).</summary>
    public string Registry => Endpoint.IsDefaultPort ? Endpoint.Host : $"{Endpoint.Host}:{Endpoint.Port}";

    public static OciRegistryFixture CreateOrSkip()
    {
        var url = Environment.GetEnvironmentVariable("DOKPOD_TEST_OCI_REGISTRY");
        if (string.IsNullOrWhiteSpace(url)) Assert.Skip("DOKPOD_TEST_OCI_REGISTRY não configurado (registro OCI real em container).");
        var trust = new X509Certificate2Collection();
        if (Environment.GetEnvironmentVariable("DOKPOD_TEST_OCI_REGISTRY_CA") is { Length: > 0 } caPath) trust.ImportFromPemFile(caPath);
        return new OciRegistryFixture(
            new Uri(url),
            trust,
            Environment.GetEnvironmentVariable("DOKPOD_TEST_OCI_REGISTRY_USER") ?? string.Empty,
            Environment.GetEnvironmentVariable("DOKPOD_TEST_OCI_REGISTRY_PASSWORD") ?? string.Empty);
    }

    /// <summary>Publica um manifesto de uma camada vazia; <paramref name="label"/> muda o conteúdo e, com ele, o digest.</summary>
    public async Task<string> PushImageAsync(string repository, string tag, string label, CancellationToken cancellationToken)
    {
        var manifest = await ManifestAsync(repository, label, cancellationToken);
        return await PutManifestAsync(repository, tag, ManifestType, manifest, cancellationToken);
    }

    /// <summary>Publica um índice com um manifesto por plataforma (<c>os/arquitetura</c> → rótulo do conteúdo).</summary>
    public async Task<(string Index, IReadOnlyDictionary<string, string> Platforms)> PushIndexAsync(
        string repository, string tag, IReadOnlyDictionary<string, string> labels, CancellationToken cancellationToken)
    {
        var platforms = new Dictionary<string, string>(StringComparer.Ordinal);
        var entries = new List<object>();
        foreach (var (platform, label) in labels)
        {
            var manifest = await ManifestAsync(repository, label, cancellationToken);
            var digest = await PutManifestAsync(repository, Digest(manifest), ManifestType, manifest, cancellationToken);
            platforms[platform] = digest;
            var parts = platform.Split('/');
            entries.Add(new { mediaType = ManifestType, digest, size = manifest.Length, platform = new { os = parts[0], architecture = parts[1] } });
        }

        var index = JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 2, mediaType = IndexType, manifests = entries });
        return (await PutManifestAsync(repository, tag, IndexType, index, cancellationToken), platforms);
    }

    public async Task DeleteTagAsync(string repository, string digest, CancellationToken cancellationToken)
    {
        using var response = await client.DeleteAsync($"/v2/{repository}/manifests/{digest}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<byte[]> ManifestAsync(string repository, string label, CancellationToken cancellationToken)
    {
        var config = JsonSerializer.SerializeToUtf8Bytes(new { architecture = "amd64", os = "linux", label });
        var configDigest = await PushBlobAsync(repository, config, cancellationToken);
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 2,
            mediaType = ManifestType,
            config = new { mediaType = "application/vnd.oci.image.config.v1+json", digest = configDigest, size = config.Length },
            layers = Array.Empty<object>(),
        });
    }

    private async Task<string> PushBlobAsync(string repository, byte[] content, CancellationToken cancellationToken)
    {
        var digest = Digest(content);
        using var start = await client.PostAsync($"/v2/{repository}/blobs/uploads/", null, cancellationToken);
        start.EnsureSuccessStatusCode();
        var location = start.Headers.Location!;
        var upload = location.IsAbsoluteUri ? location : new Uri(Endpoint, location);
        var separator = upload.Query.Length > 0 ? "&" : "?";
        using var body = new ByteArrayContent(content);
        body.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var finish = await client.PutAsync($"{upload}{separator}digest={Uri.EscapeDataString(digest)}", body, cancellationToken);
        finish.EnsureSuccessStatusCode();
        return digest;
    }

    private async Task<string> PutManifestAsync(string repository, string reference, string mediaType, byte[] manifest, CancellationToken cancellationToken)
    {
        using var body = new ByteArrayContent(manifest);
        body.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        using var response = await client.PutAsync($"/v2/{repository}/manifests/{reference}", body, cancellationToken);
        response.EnsureSuccessStatusCode();
        return Digest(manifest);
    }

    private static string Digest(byte[] content) => "sha256:" + Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    public void Dispose()
    {
        client.Dispose();
        foreach (var certificate in Trust) certificate.Dispose();
    }
}
