using System.Security.Cryptography.X509Certificates;
using Dokpod.Agent.Application.Engines;
using Dokpod.Agent.Application.Images;
using Dokpod.Agent.Infrastructure.Engines;
using Dokpod.Agent.Infrastructure.Registries;
using Dokpod.Domain.Images;
using Xunit;

namespace Dokpod.Agent.Infrastructure.Tests.Registries;

/// <summary>
/// Spec 025 T012: cliente e comparação contra o CNCF Distribution real (fixture de T002), com TLS e <c>htpasswd</c>.
/// Ignorado sem <c>DOKPOD_TEST_OCI_REGISTRY</c>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class OciRegistryIntegrationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RepublishedTag_GoesFromUpToDateToUpdateAvailable()
    {
        using var fixture = OciRegistryFixture.CreateOrSkip();
        var repository = $"dokpod-t012/{Guid.NewGuid():N}";
        var first = await fixture.PushImageAsync(repository, "1.0", "primeira", Ct);
        var local = new EngineImagePlatform("linux", "amd64", null, [$"{fixture.Registry}/{repository}@{first}"]);

        var before = await Checker(fixture, local).CheckAsync(Input(fixture, repository), Ct);
        var second = await fixture.PushImageAsync(repository, "1.0", "segunda", Ct);
        var after = await Checker(fixture, local).CheckAsync(Input(fixture, repository), Ct);

        Assert.Equal((ImageUpdateStates.UpToDate, first), (before.State, before.MatchedLocalDigest));
        Assert.Equal((ImageUpdateStates.UpdateAvailable, second), (after.State, after.RemoteDigest));
    }

    [Fact]
    public async Task IndexWhereOnlyAnotherArchitectureChanges_StaysUpToDate()
    {
        using var fixture = OciRegistryFixture.CreateOrSkip();
        var repository = $"dokpod-t012/{Guid.NewGuid():N}";
        var (index, platforms) = await fixture.PushIndexAsync(repository, "1.0",
            new Dictionary<string, string> { ["linux/amd64"] = "amd-1", ["linux/arm64"] = "arm-1" }, Ct);
        var local = new EngineImagePlatform("linux", "amd64", null,
            [$"{fixture.Registry}/{repository}@{index}", $"{fixture.Registry}/{repository}@{platforms["linux/amd64"]}"]);

        var (newIndex, _) = await fixture.PushIndexAsync(repository, "1.0",
            new Dictionary<string, string> { ["linux/amd64"] = "amd-1", ["linux/arm64"] = "arm-2" }, Ct);
        var outcome = await Checker(fixture, local).CheckAsync(Input(fixture, repository), Ct);

        Assert.NotEqual(index, newIndex);
        Assert.Equal((ImageUpdateStates.UpToDate, newIndex, platforms["linux/amd64"]), (outcome.State, outcome.RemoteDigest, outcome.MatchedLocalDigest));
    }

    [Fact]
    public async Task WrongCredential_IsUnauthorized()
    {
        using var fixture = OciRegistryFixture.CreateOrSkip();
        var repository = $"dokpod-t012/{Guid.NewGuid():N}";
        var digest = await fixture.PushImageAsync(repository, "1.0", "primeira", Ct);
        var local = new EngineImagePlatform("linux", "amd64", null, [$"{fixture.Registry}/{repository}@{digest}"]);

        var outcome = await Checker(fixture, local, new RegistryCredential(fixture.Username, "senha-errada")).CheckAsync(Input(fixture, repository), Ct);

        Assert.Equal((ImageUpdateStates.Failed, ImageUpdateReasons.Unauthorized), (outcome.State, outcome.Reason));
    }

    [Fact]
    public async Task RemovedTag_IsReferenceNotFound()
    {
        using var fixture = OciRegistryFixture.CreateOrSkip();
        var repository = $"dokpod-t012/{Guid.NewGuid():N}";
        var digest = await fixture.PushImageAsync(repository, "1.0", "primeira", Ct);
        var local = new EngineImagePlatform("linux", "amd64", null, [$"{fixture.Registry}/{repository}@{digest}"]);

        await fixture.DeleteTagAsync(repository, digest, Ct);
        var outcome = await Checker(fixture, local).CheckAsync(Input(fixture, repository), Ct);

        Assert.Equal((ImageUpdateStates.Failed, ImageUpdateReasons.ReferenceNotFound), (outcome.State, outcome.Reason));
    }

    private static ImageUpdateCheckInput Input(OciRegistryFixture fixture, string repository) =>
        new($"{fixture.Registry}/{repository}:1.0", "sha256:" + new string('f', 64));

    private static ImageUpdateChecker Checker(OciRegistryFixture fixture, EngineImagePlatform local, RegistryCredential? credential = null)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                CertificateChainPolicy = new X509ChainPolicy { TrustMode = X509ChainTrustMode.CustomRootTrust, RevocationMode = X509RevocationMode.NoCheck },
            },
        };
        handler.SslOptions.CertificateChainPolicy!.CustomTrustStore.AddRange(fixture.Trust);
        var value = credential ?? new RegistryCredential(fixture.Username, fixture.Credential);
        var client = new OciDistributionClient(new HttpMessageInvoker(handler), _ => value);
        return new ImageUpdateChecker(new Engine(local), client, new HashSet<string>());
    }

    private sealed class Engine(EngineImagePlatform local) : IContainerEngine
    {
        public Task<EngineDescriptor> InspectAsync(CancellationToken cancellationToken) => Task.FromResult(new EngineDescriptor("1", "1.47"));
        public Task<IReadOnlyList<EngineContainer>> ListContainersAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<EngineContainer>>([]);
        public Task<EngineContainer?> InspectContainerAsync(string containerId, CancellationToken cancellationToken) => Task.FromResult<EngineContainer?>(null);
        public Task<ContainerMutationResult> ExecuteAsync(Dokpod.Domain.Commands.AgentCommandKind commandKind, string containerId, CancellationToken cancellationToken) =>
            Task.FromResult(new ContainerMutationResult(false, "x"));
        public Task<EngineImagePlatform?> InspectImagePlatformAsync(string imageId, CancellationToken cancellationToken) => Task.FromResult<EngineImagePlatform?>(local);
    }
}
