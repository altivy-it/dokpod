using System.Net.Http.Headers;
using System.Text;
using Xunit;

namespace Dokpod.Agent.Infrastructure.Tests.Registries;

/// <summary>Spec 025 T002: a fixture publica no registro real e o manifesto volta pela tag com o mesmo digest.</summary>
[Trait("Category", "Integration")]
public sealed class OciRegistryFixtureTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PushedImage_IsServedByTagWithItsDigest()
    {
        using var fixture = OciRegistryFixture.CreateOrSkip();
        var repository = $"dokpod-t002/{Guid.NewGuid():N}";
        var digest = await fixture.PushImageAsync(repository, "1.0", "primeira", Ct);

        using var handler = new SocketsHttpHandler();
        handler.SslOptions.CertificateChainPolicy = new System.Security.Cryptography.X509Certificates.X509ChainPolicy
        {
            TrustMode = System.Security.Cryptography.X509Certificates.X509ChainTrustMode.CustomRootTrust,
            RevocationMode = System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck,
        };
        handler.SslOptions.CertificateChainPolicy.CustomTrustStore.AddRange(fixture.Trust);
        using var client = new HttpClient(handler) { BaseAddress = fixture.Endpoint };
        using var request = new HttpRequestMessage(HttpMethod.Head, $"/v2/{repository}/manifests/1.0");
        request.Headers.Accept.ParseAdd("application/vnd.oci.image.manifest.v1+json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{fixture.Username}:{fixture.Credential}")));
        using var response = await client.SendAsync(request, Ct);

        Assert.Equal(digest, response.Headers.GetValues("Docker-Content-Digest").Single());
    }
}
