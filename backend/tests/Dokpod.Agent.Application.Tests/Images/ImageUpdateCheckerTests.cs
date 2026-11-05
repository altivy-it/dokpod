using Dokpod.Agent.Application.Engines;
using Dokpod.Agent.Application.Images;
using Dokpod.Domain.Images;
using Xunit;

namespace Dokpod.Agent.Application.Tests.Images;

/// <summary>Spec 025 T010: comparação de digests e plataforma (D-002) e referências não verificáveis (D-003).</summary>
public sealed class ImageUpdateCheckerTests
{
    private static readonly string Index = "sha256:" + new string('1', 64);
    private static readonly string NewIndex = "sha256:" + new string('2', 64);
    private static readonly string Amd = "sha256:" + new string('a', 64);
    private static readonly string Arm = "sha256:" + new string('b', 64);
    private static readonly string NewArm = "sha256:" + new string('c', 64);
    private const string Image = "sha256:" + "f";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RemoteDigestAmongTheLocalOnes_IsUpToDate()
    {
        var registry = new FakeRegistry(new ManifestHead(Index, ManifestMediaTypes.OciIndex, null));

        var outcome = await Checker(registry, Local("linux", "amd64", null, Index, Amd)).CheckAsync(Input("nginx:1.28"), Ct);

        Assert.Equal((ImageUpdateStates.UpToDate, (string?)null, Index, Index), (outcome.State, outcome.Reason, outcome.RemoteDigest, outcome.MatchedLocalDigest));
        Assert.Equal("linux/amd64", outcome.Platform);
        Assert.Equal(0, registry.IndexCalls);
    }

    [Fact]
    public async Task IndexChanged_ButNotTheHostPlatform_IsUpToDate()
    {
        var registry = new FakeRegistry(new ManifestHead(NewIndex, ManifestMediaTypes.OciIndex, null),
            new ManifestIndex([("linux/amd64", Amd), ("linux/arm64/v8", NewArm)], null));

        var outcome = await Checker(registry, Local("linux", "amd64", null, Index, Amd)).CheckAsync(Input("nginx:1.28"), Ct);

        Assert.Equal((ImageUpdateStates.UpToDate, Amd), (outcome.State, outcome.MatchedLocalDigest));
        Assert.Equal(1, registry.IndexCalls);
    }

    [Fact]
    public async Task HostPlatformChanged_IsUpdateAvailable_EvenWhenTheLocalImageHasNoVariant()
    {
        var registry = new FakeRegistry(new ManifestHead(NewIndex, ManifestMediaTypes.OciIndex, null),
            new ManifestIndex([("linux/amd64", Amd), ("linux/arm64/v8", NewArm)], null));

        var outcome = await Checker(registry, Local("linux", "arm64", null, Index, Arm)).CheckAsync(Input("nginx:1.28"), Ct);

        Assert.Equal((ImageUpdateStates.UpdateAvailable, (string?)null, NewIndex), (outcome.State, outcome.Reason, outcome.RemoteDigest));
        Assert.Equal("linux/arm64", outcome.Platform);
    }

    [Fact]
    public async Task SingleManifestChanged_IsUpdateAvailable_WithoutFetchingIt()
    {
        var registry = new FakeRegistry(new ManifestHead(NewArm, ManifestMediaTypes.DockerManifest, null));

        var outcome = await Checker(registry, Local("linux", "arm64", "v8", Arm)).CheckAsync(Input("nginx:1.28"), Ct);

        Assert.Equal(ImageUpdateStates.UpdateAvailable, outcome.State);
        Assert.Equal("linux/arm64/v8", outcome.Platform);
        Assert.Equal(0, registry.IndexCalls);
    }

    [Fact]
    public async Task OnlyDigestsOfTheSameRepositoryCount()
    {
        var registry = new FakeRegistry(new ManifestHead(Amd, ManifestMediaTypes.OciManifest, null));
        var local = new EngineImagePlatform("linux", "amd64", null, [$"docker.io/library/outro@{Amd}"]);

        var outcome = await Checker(registry, local).CheckAsync(Input("nginx:1.28"), Ct);

        Assert.Equal((ImageUpdateStates.NotCheckable, ImageUpdateReasons.LocalImage), (outcome.State, outcome.Reason));
        Assert.Equal(0, registry.HeadCalls);
    }

    [Theory]
    [InlineData("docker.io/library/nginx@sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", ImageUpdateReasons.DigestReference)]
    [InlineData("registro.local:5000/app:1.0", ImageUpdateReasons.InsecureRegistry)]
    public async Task NotCheckable_NeverCallsTheRegistry(string reference, string reason)
    {
        var registry = new FakeRegistry(new ManifestHead(Amd, ManifestMediaTypes.OciManifest, null));
        var local = new EngineImagePlatform("linux", "amd64", null, [$"docker.io/library/nginx@{Amd}", $"registro.local:5000/app@{Amd}"]);

        var outcome = await Checker(registry, local, insecure: "registro.local:5000").CheckAsync(Input(reference), Ct);

        Assert.Equal((ImageUpdateStates.NotCheckable, reason), (outcome.State, outcome.Reason));
        Assert.Equal(0, registry.HeadCalls);
    }

    [Fact]
    public async Task ImageWithoutRepoDigests_IsLocalImage()
    {
        var registry = new FakeRegistry(new ManifestHead(Amd, ManifestMediaTypes.OciManifest, null));

        var outcome = await Checker(registry, new EngineImagePlatform("linux", "amd64", null, [])).CheckAsync(Input("app:dev"), Ct);

        Assert.Equal((ImageUpdateStates.NotCheckable, ImageUpdateReasons.LocalImage), (outcome.State, outcome.Reason));
    }

    [Theory]
    [InlineData(ImageUpdateReasons.Unauthorized)]
    [InlineData(ImageUpdateReasons.RateLimited)]
    [InlineData(ImageUpdateReasons.Timeout)]
    public async Task RegistryFailure_IsFailed_NeverUpToDate(string reason)
    {
        var registry = new FakeRegistry(ManifestHead.Failed(reason));

        var outcome = await Checker(registry, Local("linux", "amd64", null, Amd)).CheckAsync(Input("nginx:1.28"), Ct);

        Assert.Equal((ImageUpdateStates.Failed, reason), (outcome.State, outcome.Reason));
        Assert.True(ImageUpdateReasons.IsValidFor(outcome.State, outcome.Reason));
    }

    [Fact]
    public async Task IndexFailure_IsFailed()
    {
        var registry = new FakeRegistry(new ManifestHead(NewIndex, ManifestMediaTypes.OciIndex, null), ManifestIndex.Failed(ImageUpdateReasons.InvalidResponse));

        var outcome = await Checker(registry, Local("linux", "amd64", null, Index)).CheckAsync(Input("nginx:1.28"), Ct);

        Assert.Equal((ImageUpdateStates.Failed, ImageUpdateReasons.InvalidResponse), (outcome.State, outcome.Reason));
    }

    [Fact]
    public async Task MissingLocalImage_UsesTheHostPlatform()
    {
        var registry = new FakeRegistry(new ManifestHead(Amd, ManifestMediaTypes.OciManifest, null));

        var outcome = await Checker(registry, null).CheckAsync(new ImageUpdateCheckInput("nginx:1.28", string.Empty), Ct);

        Assert.Equal(ImageUpdateReasons.LocalImage, outcome.Reason);
        Assert.True(ImagePlatform.IsValid(outcome.Platform));
    }

    private static ImageUpdateCheckInput Input(string reference) => new(reference, Image);

    private static EngineImagePlatform Local(string os, string architecture, string? variant, params string[] digests) =>
        new(os, architecture, variant, digests.Select(d => $"docker.io/library/nginx@{d}").ToArray());

    private static ImageUpdateChecker Checker(FakeRegistry registry, EngineImagePlatform? local, string? insecure = null) =>
        new(new FakeEngine(local), registry, insecure is null ? new HashSet<string>() : new HashSet<string> { insecure });

    private sealed class FakeRegistry(ManifestHead head, ManifestIndex? index = null) : IRegistryManifestClient
    {
        public int HeadCalls { get; private set; }

        public int IndexCalls { get; private set; }

        public Task<ManifestHead> HeadAsync(ImageReference reference, CancellationToken cancellationToken)
        {
            HeadCalls++;
            return Task.FromResult(head);
        }

        public Task<ManifestIndex> GetIndexAsync(ImageReference reference, string digest, CancellationToken cancellationToken)
        {
            IndexCalls++;
            return Task.FromResult(index ?? ManifestIndex.Failed(ImageUpdateReasons.InvalidResponse));
        }
    }

    private sealed class FakeEngine(EngineImagePlatform? local) : IContainerEngine
    {
        public Task<EngineDescriptor> InspectAsync(CancellationToken cancellationToken) => Task.FromResult(new EngineDescriptor("1", "1.47"));
        public Task<IReadOnlyList<EngineContainer>> ListContainersAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<EngineContainer>>([]);
        public Task<EngineContainer?> InspectContainerAsync(string containerId, CancellationToken cancellationToken) => Task.FromResult<EngineContainer?>(null);
        public Task<ContainerMutationResult> ExecuteAsync(Dokpod.Domain.Commands.AgentCommandKind commandKind, string containerId, CancellationToken cancellationToken) =>
            Task.FromResult(new ContainerMutationResult(false, "x"));
        public Task<EngineImagePlatform?> InspectImagePlatformAsync(string imageId, CancellationToken cancellationToken) => Task.FromResult(local);
    }
}
