using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace Dokpod.Agent.Infrastructure.Tests.Registries;

/// <summary>Spec 025 T002: o registro de teste segue o fluxo do Distribution (desafio, token, HEAD com digest).</summary>
public sealed class OciTestRegistryTests : IAsyncLifetime
{
    private readonly RegistryTestPki pki = new();
    private X509Certificate2? certificate;
    private OciTestRegistry? registry;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        certificate = pki.IssueServer();
        registry = await OciTestRegistry.StartAsync(certificate, Ct);
    }

    [Fact]
    public async Task BearerChallenge_ThenHead_ReturnsTheDigest()
    {
        registry!.Auth = OciTestAuth.Bearer;
        var digest = registry.PutManifest("app", "1.0", "linux/amd64");
        using var client = Client();

        using var challenge = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, $"https://{registry.Registry}/v2/app/manifests/1.0"), Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, challenge.StatusCode);
        Assert.Contains($"realm=\"https://localhost:{registry.Port}/token\"", challenge.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);

        using var token = await client.GetAsync($"https://{registry.Registry}/token?service=dokpod-test&scope=repository:app:pull", Ct);
        Assert.Contains("token-anonimo", await token.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);

        using var head = new HttpRequestMessage(HttpMethod.Head, $"https://{registry.Registry}/v2/app/manifests/1.0");
        head.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "token-anonimo");
        using var response = await client.SendAsync(head, Ct);
        Assert.Equal((HttpStatusCode.OK, digest), (response.StatusCode, response.Headers.GetValues("Docker-Content-Digest").Single()));
    }

    [Fact]
    public async Task Index_IsServedByTagAndByDigest()
    {
        var amd = registry!.PutManifest("app", "amd", "linux/amd64");
        var index = registry.PutIndex("app", "1.0", new Dictionary<string, string> { ["linux/amd64"] = amd });
        using var client = Client();

        using var byTag = await client.GetAsync($"https://{registry.Registry}/v2/app/manifests/1.0", Ct);
        using var byDigest = await client.GetAsync($"https://{registry.Registry}/v2/app/manifests/{index}", Ct);

        Assert.Equal(OciTestRegistry.IndexType, byTag.Content.Headers.ContentType!.MediaType);
        Assert.Contains(amd, await byDigest.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    private HttpClient Client()
    {
        var handler = new SocketsHttpHandler
        {
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                CertificateChainPolicy = new X509ChainPolicy { TrustMode = X509ChainTrustMode.CustomRootTrust, RevocationMode = X509RevocationMode.NoCheck },
            },
        };
        handler.SslOptions.CertificateChainPolicy.CustomTrustStore.Add(pki.Ca);
        return new HttpClient(handler);
    }

    public async ValueTask DisposeAsync()
    {
        if (registry is not null) await registry.DisposeAsync();
        certificate?.Dispose();
        pki.Dispose();
    }
}
