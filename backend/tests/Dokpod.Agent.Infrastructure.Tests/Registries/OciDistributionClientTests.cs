using System.Net;
using System.Security.Cryptography.X509Certificates;
using Dokpod.Agent.Application.Images;
using Dokpod.Agent.Infrastructure.Engines;
using Dokpod.Agent.Infrastructure.Registries;
using Dokpod.Domain.Images;
using Xunit;

namespace Dokpod.Agent.Infrastructure.Tests.Registries;

/// <summary>Spec 025 T009: consulta, autenticação, limites e threat model (L1–L4) do cliente OCI contra o registro de teste.</summary>
public sealed class OciDistributionClientTests : IAsyncLifetime
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
    public async Task Head_ReturnsTheDigestAndMediaType()
    {
        var digest = registry!.PutManifest("app", "1.0", "linux/amd64");

        var head = await Client().HeadAsync(Reference("app:1.0"), Ct);

        Assert.Equal((digest, OciTestRegistry.ManifestType, (string?)null), (head.Digest, head.MediaType, head.FailureReason));
        Assert.False(head.IsIndex);
        Assert.Contains(registry.Requests, r => r is { Method: "HEAD" });
    }

    [Fact]
    public async Task Bearer_WithoutCredential_UsesAnAnonymousToken()
    {
        registry!.Auth = OciTestAuth.Bearer;
        var digest = registry.PutManifest("app", "1.0", "linux/amd64");

        var head = await Client().HeadAsync(Reference("app:1.0"), Ct);

        Assert.Equal(digest, head.Digest);
        Assert.Contains(registry.Requests, r => r.Path == "/token" && r.Authorization is null);
    }

    [Fact]
    public async Task Bearer_SameHostRealm_ReceivesTheCredential_AndTheTokenIsReusedPerRepository()
    {
        registry!.Auth = OciTestAuth.Bearer;
        registry.PutManifest("app", "1.0", "linux/amd64");
        registry.PutManifest("app", "2.0", "linux/amd64");
        var client = Client(credential: true);

        await client.HeadAsync(Reference("app:1.0"), Ct);
        var second = await client.HeadAsync(Reference("app:2.0"), Ct);

        Assert.NotNull(second.Digest);
        Assert.Single(registry.Requests, r => r.Path == "/token");
        Assert.Contains(registry.Requests, r => r.Path == "/token" && r.Authorization!.StartsWith("Basic ", StringComparison.Ordinal));
        Assert.Contains(registry.Requests, r => r.Method == "HEAD" && r.Authorization == "Bearer token-autenticado");
    }

    [Fact]
    public async Task Bearer_RealmOnAnotherHost_NeverReceivesTheCredential()
    {
        registry!.Auth = OciTestAuth.Bearer;
        registry.Realm = $"https://127.0.0.1:{registry.Port}/token";
        registry.PutManifest("app", "1.0", "linux/amd64");

        var head = await Client(credential: true).HeadAsync(Reference("app:1.0"), Ct);

        Assert.NotNull(head.Digest);
        Assert.All(registry.Requests.Where(r => r.Path == "/token"), r => Assert.Null(r.Authorization));
    }

    [Fact]
    public async Task Bearer_HttpRealm_IsRefusedWithoutAnyTokenRequest()
    {
        registry!.Auth = OciTestAuth.Bearer;
        registry.Realm = $"http://localhost:{registry.Port}/token";
        registry.PutManifest("app", "1.0", "linux/amd64");

        var head = await Client(credential: true).HeadAsync(Reference("app:1.0"), Ct);

        Assert.Equal(ImageUpdateReasons.Unauthorized, head.FailureReason);
        Assert.DoesNotContain(registry.Requests, r => r.Path == "/token");
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, ImageUpdateReasons.Unauthorized)]
    public async Task Basic_NeedsTheHostCredential(bool credential, string? reason)
    {
        registry!.Auth = OciTestAuth.Basic;
        registry.PutManifest("app", "1.0", "linux/amd64");

        var head = await Client(credential).HeadAsync(Reference("app:1.0"), Ct);

        Assert.Equal(reason, head.FailureReason);
    }

    [Fact]
    public async Task WrongCredential_IsUnauthorized()
    {
        registry!.Auth = OciTestAuth.Bearer;
        registry.PutManifest("app", "1.0", "linux/amd64");
        registry.Credential = "outra-senha";

        var head = await Client(credential: true).HeadAsync(Reference("app:1.0"), Ct);

        Assert.Equal(ImageUpdateReasons.Unauthorized, head.FailureReason);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, ImageUpdateReasons.ReferenceNotFound)]
    [InlineData(HttpStatusCode.TooManyRequests, ImageUpdateReasons.RateLimited)]
    [InlineData(HttpStatusCode.ServiceUnavailable, ImageUpdateReasons.RegistryUnavailable)]
    [InlineData(HttpStatusCode.Forbidden, ImageUpdateReasons.Unauthorized)]
    [InlineData((HttpStatusCode)418, ImageUpdateReasons.InvalidResponse)]
    public async Task Status_MapsToTheReason(HttpStatusCode status, string reason)
    {
        registry!.ManifestStatus = status;

        var head = await Client().HeadAsync(Reference("app:1.0"), Ct);

        Assert.Equal(reason, head.FailureReason);
    }

    [Fact]
    public async Task MissingTag_IsReferenceNotFound_AndMissingDigestHeaderIsInvalid()
    {
        Assert.Equal(ImageUpdateReasons.ReferenceNotFound, (await Client().HeadAsync(Reference("app:sumiu"), Ct)).FailureReason);

        registry!.PutManifest("app", "1.0", "linux/amd64");
        registry.OmitDigest = true;
        Assert.Equal(ImageUpdateReasons.InvalidResponse, (await Client().HeadAsync(Reference("app:1.0"), Ct)).FailureReason);
    }

    [Fact]
    public async Task SlowRegistry_IsTimeout()
    {
        registry!.PutManifest("app", "1.0", "linux/amd64");
        registry.Delay = TimeSpan.FromSeconds(3);

        var head = await Client(timeout: TimeSpan.FromMilliseconds(500)).HeadAsync(Reference("app:1.0"), Ct);

        Assert.Equal(ImageUpdateReasons.Timeout, head.FailureReason);
    }

    [Fact]
    public async Task UnreachableRegistry_IsRegistryUnavailable()
    {
        Assert.True(ImageReference.TryParse("localhost:1/app:1.0", out var reference));

        var head = await Client().HeadAsync(reference, Ct);

        Assert.Equal(ImageUpdateReasons.RegistryUnavailable, head.FailureReason);
    }

    [Fact]
    public async Task Redirect_ToAnotherHost_IsRefused_ToTheSameHostIsFollowed()
    {
        var digest = registry!.PutManifest("app", "1.0", "linux/amd64");
        registry.ManifestRedirect = $"https://127.0.0.1:{registry.Port}/v2/app/manifests/1.0";
        Assert.Equal(ImageUpdateReasons.InvalidResponse, (await Client().HeadAsync(Reference("app:1.0"), Ct)).FailureReason);
        Assert.Single(registry.Requests);

        registry.ManifestRedirect = "/v2/app/manifests/1.0?segundo=1";
        var head = await Client().HeadAsync(Reference("app:1.0"), Ct);
        // O registro de teste redireciona sempre; depois de 3 saltos no mesmo host o cliente desiste, sem sair dele.
        Assert.Equal(ImageUpdateReasons.InvalidResponse, head.FailureReason);
        Assert.All(registry.Requests, r => Assert.StartsWith("/v2/app/manifests/", r.Path, StringComparison.Ordinal));
        Assert.NotNull(digest);
    }

    [Fact]
    public async Task LargeTokenResponse_IsInvalid()
    {
        registry!.Auth = OciTestAuth.Bearer;
        registry.PutManifest("app", "1.0", "linux/amd64");
        registry.PaddingBytes = OciDistributionClient.MaxTokenBytes + 1024;

        var head = await Client().HeadAsync(Reference("app:1.0"), Ct);

        Assert.Equal(ImageUpdateReasons.InvalidResponse, head.FailureReason);
    }

    [Fact]
    public async Task Index_ListsThePlatforms_AndAnOversizedBodyIsInvalid()
    {
        var amd = registry!.PutManifest("app", "amd", "linux/amd64");
        var arm = registry.PutManifest("app", "arm", "linux/arm64/v8");
        var index = registry.PutIndex("app", "1.0", new Dictionary<string, string> { ["linux/amd64"] = amd, ["linux/arm64/v8"] = arm });

        var head = await Client().HeadAsync(Reference("app:1.0"), Ct);
        var manifests = await Client().GetIndexAsync(Reference("app:1.0"), index, Ct);

        Assert.True(head.IsIndex);
        Assert.Equal([("linux/amd64", amd), ("linux/arm64/v8", arm)], manifests.Manifests);

        registry.PaddingBytes = OciDistributionClient.MaxManifestBytes + 1;
        Assert.Equal(ImageUpdateReasons.InvalidResponse, (await Client().GetIndexAsync(Reference("app:1.0"), index, Ct)).FailureReason);
    }

    private ImageReference Reference(string repositoryAndTag)
    {
        Assert.True(ImageReference.TryParse($"{registry!.Registry}/{repositoryAndTag}", out var reference));
        return reference;
    }

    private OciDistributionClient Client(bool credential = false, TimeSpan? timeout = null)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                CertificateChainPolicy = new X509ChainPolicy { TrustMode = X509ChainTrustMode.CustomRootTrust, RevocationMode = X509RevocationMode.NoCheck },
            },
        };
        handler.SslOptions.CertificateChainPolicy!.CustomTrustStore.Add(pki.Ca);
        var value = credential ? new RegistryCredential(registry!.Username, "senha-de-teste") : null;
        return new OciDistributionClient(new HttpMessageInvoker(handler), _ => value, timeout);
    }

    public async ValueTask DisposeAsync()
    {
        if (registry is not null) await registry.DisposeAsync();
        certificate?.Dispose();
        pki.Dispose();
    }
}
