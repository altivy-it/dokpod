using Dokpod.Agent.Application.Containers;
using Dokpod.Agent.Application.Engines;
using Dokpod.Agent.Infrastructure.Engines.Docker;
using Dokpod.Agent.Infrastructure.Secrets;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Containers;
using Dokpod.Domain.Images;
using Xunit;

namespace Dokpod.Agent.Application.Tests.Containers;

/// <summary>Spec 013 T008/T010: orquestração no agente, defesa em profundidade e reconciliação.</summary>
public sealed class ContainerCreatorTests
{
    private static readonly string Id = new('a', 64);

    private static ContainerCreationSpec Spec(Func<ContainerCreationSpec, ContainerCreationSpec>? edit = null)
    {
        Assert.True(ImageReference.TryParse("nginx:1.27", out var image));
        var spec = new ContainerCreationSpec(image, "web-01", [new PortSpec(null, 8081, 80, "tcp")], [new EnvironmentSpec("A", "canario-013", null, true)], [], null, null, null, new RestartSpec(RestartPolicyKind.No), null, false);
        return edit?.Invoke(spec) ?? spec;
    }

    private static AgentCommand Command() =>
        new(Guid.NewGuid(), Guid.NewGuid(), AgentCommandKind.CreateContainer, "", "", new string('A', 64), DateTimeOffset.UtcNow.AddMinutes(5), 1);

    [Fact]
    public async Task Create_PullsWhenMissingCreatesAndStartsWithLabel()
    {
        var engine = new FakeEngine { ImagePresent = false };
        var command = Command();

        var (state, failure, id) = await new ContainerCreator(engine, new Secrets(), new ContainerCreationPolicy([], "/run/docker.sock")).CreateAsync(command, Spec(), CancellationToken.None);

        Assert.Equal(CommandExecutionState.Succeeded, state);
        Assert.Null(failure);
        Assert.Equal(Id, id);
        Assert.Equal(1, engine.Pulls);
        Assert.Equal(command.CommandId, engine.Created!.CommandId);
        Assert.Equal([new KeyValuePair<string, string>("A", "canario-013")], engine.Created.Environment);
    }

    [Fact]
    public async Task Create_ReconcilesByLabelWithoutCreatingAgain()
    {
        var engine = new FakeEngine { Existing = new EngineContainer(Id, "web-01", "nginx", "running", "r") };

        var (state, _, id) = await new ContainerCreator(engine, new Secrets(), new ContainerCreationPolicy([], null)).CreateAsync(Command(), Spec().WithoutValues(), CancellationToken.None);

        Assert.Equal(CommandExecutionState.Succeeded, state);
        Assert.Equal(Id, id);
        Assert.Null(engine.Created);
    }

    [Fact]
    public async Task Create_WithoutValuesAndNoContainerFailsWithParametersUnavailable()
    {
        var engine = new FakeEngine();

        var (_, failure, _) = await new ContainerCreator(engine, new Secrets(), new ContainerCreationPolicy([], null)).CreateAsync(Command(), Spec().WithoutValues(), CancellationToken.None);

        Assert.Equal("parameters_unavailable", failure);
        Assert.Null(engine.Created);
    }

    [Theory]
    [InlineData("/var/run/docker.sock")]
    [InlineData("/run/podman/podman.sock")]
    [InlineData("/etc")]
    [InlineData("/srv/dados/../../etc")]
    [InlineData("/home/usuario")]
    [InlineData("/srv/dados/app")]
    [InlineData("/srv/dados/link-para-raiz")]
    public async Task Create_RefusesBindOutsideTheAllowlist(string source)
    {
        var engine = new FakeEngine();
        var spec = Spec(s => s with { Volumes = [new VolumeSpec(VolumeKind.Bind, source, "/data", false)] });

        var (_, failure, _) = await new ContainerCreator(engine, new Secrets(), new ContainerCreationPolicy(["/srv/dados"], "/var/run/docker.sock")).CreateAsync(Command(), spec, CancellationToken.None);

        Assert.True(failure is "mount_not_allowed" or "invalid_spec", failure);
        Assert.Null(engine.Created);
    }

    [Fact]
    public async Task Create_AllowsBindOfTheExactAllowlistedDirectory()
    {
        var engine = new FakeEngine();
        var spec = Spec(s => s with { Volumes = [new VolumeSpec(VolumeKind.Bind, "/srv/dados/", "/data", true)] });

        var (state, _, _) = await new ContainerCreator(engine, new Secrets(), new ContainerCreationPolicy(["/srv/dados", "/srv/outros"], null)).CreateAsync(Command(), spec, CancellationToken.None);

        Assert.Equal(CommandExecutionState.Succeeded, state);
    }

    [Fact]
    public void Policy_DetectsNestedAllowlistRoots()
    {
        Assert.Equal(("/srv/dados", "/srv/dados/app"), ContainerCreationPolicy.FindNestedRoots(["/srv/dados", "/srv/dados/app"]));
        Assert.Null(ContainerCreationPolicy.FindNestedRoots(["/srv/dados", "/srv/dados-2", "/srv/outros"]));
    }

    [Fact]
    public async Task Create_ResolvesFileSecretsAndRejectsPodmanSecretsOnDocker()
    {
        var engine = new FakeEngine();
        var withFile = Spec(s => s with { Environment = [new EnvironmentSpec("TOKEN", null, new HostSecretReference(HostSecretKind.File, "token"), true)] });
        var missing = Spec(s => s with { Environment = [new EnvironmentSpec("TOKEN", null, new HostSecretReference(HostSecretKind.File, "nao-existe"), true)] });
        var podman = Spec(s => s with { Environment = [new EnvironmentSpec("TOKEN", null, new HostSecretReference(HostSecretKind.PodmanSecret, "jwt"), true)] });
        var creator = new ContainerCreator(engine, new Secrets { ["token"] = "valor-do-arquivo" }, new ContainerCreationPolicy([], null));

        Assert.Equal(CommandExecutionState.Succeeded, (await creator.CreateAsync(Command(), withFile, CancellationToken.None)).State);
        Assert.Equal("valor-do-arquivo", engine.Created!.Environment.Single().Value);
        Assert.Equal("secret_not_found", (await creator.CreateAsync(Command(), missing, CancellationToken.None)).FailureCode);
        Assert.Equal("unsupported_option", (await creator.CreateAsync(Command(), podman, CancellationToken.None)).FailureCode);
    }

    [Fact]
    public async Task Create_RefusesAutoRemoveWhenImageDeclaresUnnamedVolume()
    {
        var engine = new FakeEngine { ImageVolumes = ["/data"] };
        var spec = Spec(s => s with { AutoRemove = true });
        var covered = spec with { Volumes = [new VolumeSpec(VolumeKind.Volume, "dados", "/data/", false)] };
        var creator = new ContainerCreator(engine, new Secrets(), new ContainerCreationPolicy([], null));

        Assert.Equal("anonymous_volume_with_auto_remove", (await creator.CreateAsync(Command(), spec, CancellationToken.None)).FailureCode);
        Assert.Equal(CommandExecutionState.Succeeded, (await creator.CreateAsync(Command(), covered, CancellationToken.None)).State);
    }

    [Theory]
    [InlineData("port_conflict", true)]
    [InlineData("privileged_port", true)]
    [InlineData("start_failed", false)]
    public async Task Create_RollsBackPortFailuresAndKeepsOtherStartFailures(string startFailure, bool removed)
    {
        var engine = new FakeEngine { StartFailure = startFailure };

        var (state, failure, id) = await new ContainerCreator(engine, new Secrets(), new ContainerCreationPolicy([], null)).CreateAsync(Command(), Spec(), CancellationToken.None);

        Assert.Equal(CommandExecutionState.Failed, state);
        Assert.Equal(startFailure, failure);
        Assert.Equal(removed, engine.Removed);
        Assert.Equal(removed ? null : Id, id);
    }

    [Fact]
    public async Task Create_HealthRestartOnDockerIsUnsupported()
    {
        var spec = Spec(s => s with { HealthCheck = new HealthCheckSpec(["CMD", "true"], 15, 5, 3, 0, HealthCheckOnFailure.Restart) });

        var (_, failure, _) = await new ContainerCreator(new FakeEngine(), new Secrets(), new ContainerCreationPolicy([], null)).CreateAsync(Command(), spec, CancellationToken.None);

        Assert.Equal("unsupported_option", failure);
    }

    [Theory]
    [InlineData(500, "port is already allocated", "port_conflict")]
    [InlineData(500, "rootlessport listen tcp 0.0.0.0:8081: bind: address already in use", "port_conflict")]
    [InlineData(500, "rootlessport cannot expose privileged port 80, you can add 'net.ipv4.ip_unprivileged_port_start=80'", "privileged_port")]
    [InlineData(500, "exec: \"nao-existe\": executable file not found", "start_failed")]
    public void MapStartFailure_UsesContractCodes(int _, string message, string code)
    {
        Assert.Equal(code, DockerEngineClient.MapStartFailure(message));
    }

    [Theory]
    [InlineData(409, "Conflict. The container name \"/web\" is already in use", "name_conflict")]
    [InlineData(500, "creating container storage: the container name \"web\" is already in use by abc", "name_conflict")]
    [InlineData(404, "network rede-x not found", "network_not_found")]
    [InlineData(400, "invalid mount config", "invalid_spec")]
    public void MapCreateFailure_UsesContractCodes(int status, string message, string code)
    {
        Assert.Equal(code, DockerEngineClient.MapCreateFailure(status, message));
    }

    [Fact]
    public async Task SecretFiles_RefuseTraversalLinksAndWorldReadableFiles()
    {
        var directory = Directory.CreateTempSubdirectory("dokpod-secrets-").FullName;
        try
        {
            var store = new HostSecretFileStore(directory);
            var ok = Path.Combine(directory, "token");
            await File.WriteAllTextAsync(ok, "valor\n", TestContext.Current.CancellationToken);
            var open = Path.Combine(directory, "aberto");
            await File.WriteAllTextAsync(open, "x", TestContext.Current.CancellationToken);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(ok, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                File.SetUnixFileMode(open, UnixFileMode.UserRead | UnixFileMode.OtherRead);
                File.CreateSymbolicLink(Path.Combine(directory, "link"), ok);
            }

            Assert.Equal("valor", await store.ReadAsync("token", TestContext.Current.CancellationToken));
            Assert.Null(await store.ReadAsync("../token", TestContext.Current.CancellationToken));
            if (!OperatingSystem.IsWindows())
            {
                Assert.Null(await store.ReadAsync("aberto", TestContext.Current.CancellationToken));
                Assert.Null(await store.ReadAsync("link", TestContext.Current.CancellationToken));
            }

            Assert.Contains("token", store.List());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class Secrets : Dictionary<string, string>, IHostSecretFiles
    {
        public Task<string?> ReadAsync(string name, CancellationToken cancellationToken) => Task.FromResult(TryGetValue(name, out var value) ? value : null);
        public IReadOnlyList<string> List() => Keys.ToArray();
    }

    private sealed class FakeEngine : IContainerEngine
    {
        public bool ImagePresent { get; init; } = true;
        public string[] ImageVolumes { get; init; } = [];
        public EngineContainer? Existing { get; init; }
        public string? StartFailure { get; init; }
        public string Kind { get; init; } = "docker";
        public int Pulls { get; private set; }
        public EngineCreateRequest? Created { get; private set; }
        public bool Removed { get; private set; }

        public Task<string> GetEngineKindAsync(CancellationToken cancellationToken) => Task.FromResult(Kind);
        public Task<EngineContainer?> FindByLabelAsync(string label, string value, CancellationToken cancellationToken) => Task.FromResult(Existing);
        public Task<EngineImageConfig> InspectImageConfigAsync(ImageReference reference, CancellationToken cancellationToken) =>
            Task.FromResult(new EngineImageConfig(ImagePresent || Pulls > 0, ImageVolumes));
        public Task<ImagePullResult> PullImageAsync(ImageReference reference, CancellationToken cancellationToken)
        {
            Pulls++;
            return Task.FromResult(new ImagePullResult(true, null));
        }

        public Task<EngineCreateResult> CreateContainerAsync(EngineCreateRequest request, CancellationToken cancellationToken)
        {
            Created = request;
            return Task.FromResult(new EngineCreateResult(Id, null));
        }

        public Task<string?> StartCreatedContainerAsync(string containerId, CancellationToken cancellationToken) => Task.FromResult(StartFailure);

        public Task RemoveCreatedContainerAsync(string containerId, CancellationToken cancellationToken)
        {
            Removed = true;
            return Task.CompletedTask;
        }

        public Task<EngineDescriptor> InspectAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<EngineContainer>> ListContainersAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EngineContainer?> InspectContainerAsync(string containerId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ContainerMutationResult> ExecuteAsync(AgentCommandKind commandKind, string containerId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
