using System.Net;
using System.Text;
using System.Text.Json;
using Dokpod.Agent.Application.Containers;
using Dokpod.Agent.Infrastructure.Engines.Docker;
using Dokpod.Agent.Infrastructure.Secrets;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Containers;
using Dokpod.Domain.Images;
using Xunit;

namespace Dokpod.Agent.Podman.IntegrationTests;

/// <summary>Spec 013 em Podman real: configuração aplicada = solicitada (SC-005), Libpod para segredos/health, rollback e reconciliação.</summary>
public sealed class ContainerCreationPodmanTests : IAsyncLifetime
{
    private const string FixtureImage = "docker.io/lzocateli/nginx:1.28.0-bookworm";
    private readonly string suffix = Guid.NewGuid().ToString("N")[..8];
    private HttpClient? http;
    private DockerEngineClient? engine;
    private string apiVersion = string.Empty;
    private string secretsDirectory = string.Empty;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var socket = Environment.GetEnvironmentVariable("DOKPOD_TEST_PODMAN_SOCKET");
        if (string.IsNullOrWhiteSpace(socket)) return;
        http = DockerEngineClient.CreateUnixSocketClient(socket, TimeSpan.FromSeconds(60));
        engine = new DockerEngineClient(http);
        apiVersion = (await engine.InspectAsync(CancellationToken.None)).ApiVersion;
        secretsDirectory = Directory.CreateTempSubdirectory("dokpod-it-secrets-").FullName;
    }

    public async ValueTask DisposeAsync()
    {
        if (http is null) return;
        var filters = Uri.EscapeDataString(JsonSerializer.Serialize(new Dictionary<string, string[]> { ["label"] = [$"{ContainerCreationSpec.CreatedByLabel}=dokpod"] }));
        using (var list = await http.GetAsync($"v{apiVersion}/containers/json?all=true&filters={filters}", CancellationToken.None))
        {
            foreach (var item in JsonDocument.Parse(await list.Content.ReadAsStringAsync(CancellationToken.None)).RootElement.EnumerateArray())
            {
                if (item.GetProperty("Names")[0].GetString()!.Contains(suffix, StringComparison.Ordinal))
                {
                    using var _ = await http.DeleteAsync($"v{apiVersion}/containers/{item.GetProperty("Id").GetString()}?force=1", CancellationToken.None);
                }
            }
        }

        using (var _ = await http.DeleteAsync($"v{apiVersion}/volumes/dados-{suffix}?force=1", CancellationToken.None)) { }
        using (var _ = await http.DeleteAsync($"v4.0.0/libpod/secrets/jwt-{suffix}", CancellationToken.None)) { }
        Directory.Delete(secretsDirectory, recursive: true);
        http.Dispose();
    }

    private ContainerCreator Creator() => new(engine!, new HostSecretFileStore(secretsDirectory), new ContainerCreationPolicy([], null));

    private static AgentCommand Command() =>
        new(Guid.NewGuid(), Guid.NewGuid(), AgentCommandKind.CreateContainer, "", "", new string('A', 64), DateTimeOffset.UtcNow.AddMinutes(5), 1);

    private ContainerCreationSpec Spec(string name, int hostPort, Func<ContainerCreationSpec, ContainerCreationSpec>? edit = null)
    {
        Assert.True(ImageReference.TryParse(FixtureImage, out var image));
        var spec = new ContainerCreationSpec(
            image,
            $"{name}-{suffix}",
            [new PortSpec(null, hostPort, 80, "tcp")],
            [new EnvironmentSpec("MODO", "teste", null, false)],
            [],
            null,
            null,
            null,
            new RestartSpec(RestartPolicyKind.No),
            null,
            false);
        return edit?.Invoke(spec) ?? spec;
    }

    private async Task<JsonElement> InspectAsync(string id)
    {
        using var response = await http!.GetAsync($"v4.0.0/libpod/containers/{id}/json", Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token)).RootElement.Clone();
    }

    private void RequirePodman()
    {
        if (engine is null) Assert.Skip("DOKPOD_TEST_PODMAN_SOCKET não configurado.");
    }

    [Fact]
    public async Task Create_AppliesTheRequestedConfigurationAndReconcilesWithoutDuplicating()
    {
        RequirePodman();
        var command = Command();
        var secretFile = Path.Combine(secretsDirectory, "token");
        await File.WriteAllTextAsync(secretFile, "valor-arquivo", Token);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(secretFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        var spec = Spec("completo", 18081, s => s with
        {
            Environment = [new EnvironmentSpec("MODO", "teste", null, false), new EnvironmentSpec("TOKEN", null, new HostSecretReference(HostSecretKind.File, "token"), true)],
            Volumes = [new VolumeSpec(VolumeKind.Volume, $"dados-{suffix}", "/srv/dados", false)],
            CpuLimitCores = 0.5,
            MemoryLimitBytes = 128L * 1024 * 1024,
            Restart = new RestartSpec(RestartPolicyKind.OnFailure, 3),
            HealthCheck = new HealthCheckSpec(["CMD-SHELL", "true"], 15, 5, 3, 0, HealthCheckOnFailure.None),
        });

        var (state, failure, id) = await Creator().CreateAsync(command, spec, Token);

        Assert.True(state == CommandExecutionState.Succeeded, failure);
        var inspect = await InspectAsync(id!);
        Assert.Equal("running", inspect.GetProperty("State").GetProperty("Status").GetString());
        var config = inspect.GetProperty("Config");
        var env = config.GetProperty("Env").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Contains("MODO=teste", env);
        Assert.Contains("TOKEN=valor-arquivo", env);
        Assert.Equal(command.CommandId.ToString("D"), config.GetProperty("Labels").GetProperty(ContainerCreationSpec.CommandLabel).GetString());
        var host = inspect.GetProperty("HostConfig");
        Assert.Equal(128L * 1024 * 1024, host.GetProperty("Memory").GetInt64());
        Assert.Equal(50_000, host.GetProperty("CpuQuota").GetInt64());
        Assert.Equal("on-failure", host.GetProperty("RestartPolicy").GetProperty("Name").GetString());
        Assert.Equal(3, host.GetProperty("RestartPolicy").GetProperty("MaximumRetryCount").GetInt32());
        Assert.Equal("18081", host.GetProperty("PortBindings").GetProperty("80/tcp")[0].GetProperty("HostPort").GetString());
        Assert.Contains(inspect.GetProperty("Mounts").EnumerateArray(), m => m.GetProperty("Name").GetString() == $"dados-{suffix}" && m.GetProperty("Destination").GetString() == "/srv/dados");
        Assert.Equal(3, config.GetProperty("Healthcheck").GetProperty("Retries").GetInt32());

        // Reentrega do mesmo comando (resposta perdida / reinício do agente): reconcilia pelo label.
        var (again, _, sameId) = await Creator().CreateAsync(command, spec.WithoutValues(), Token);
        Assert.Equal(CommandExecutionState.Succeeded, again);
        Assert.Equal(id, sameId);
    }

    [Fact]
    public async Task Create_UsesLibpodForPodmanSecretsAndHealthRestart()
    {
        RequirePodman();
        using (var createdResponse = await http!.PostAsync($"v4.0.0/libpod/secrets/create?name=jwt-{suffix}", new StringContent("segredo-podman", Encoding.UTF8, "text/plain"), Token))
        {
            Assert.True(createdResponse.IsSuccessStatusCode, await createdResponse.Content.ReadAsStringAsync(Token));
        }

        var spec = Spec("libpod", 18082, s => s with
        {
            Environment = [new EnvironmentSpec("JWT", null, new HostSecretReference(HostSecretKind.PodmanSecret, $"jwt-{suffix}"), true)],
            HealthCheck = new HealthCheckSpec(["CMD-SHELL", "true"], 15, 5, 3, 0, HealthCheckOnFailure.Restart),
            Restart = new RestartSpec(RestartPolicyKind.UnlessStopped),
        });

        var (state, failure, id) = await Creator().CreateAsync(Command(), spec, Token);

        Assert.True(state == CommandExecutionState.Succeeded, failure);
        var inspect = await InspectAsync(id!);
        Assert.Equal("restart", inspect.GetProperty("Config").GetProperty("HealthcheckOnFailureAction").GetString());
        Assert.Equal("unless-stopped", inspect.GetProperty("HostConfig").GetProperty("RestartPolicy").GetProperty("Name").GetString());
        Assert.DoesNotContain(inspect.GetProperty("Config").GetProperty("Env").EnumerateArray(), e => e.GetString()!.Contains("segredo-podman", StringComparison.Ordinal));
        Assert.Equal("secret_not_found", (await Creator().CreateAsync(Command(), spec with { Name = $"libpod2-{suffix}", Environment = [new EnvironmentSpec("JWT", null, new HostSecretReference(HostSecretKind.PodmanSecret, "nao-existe"), true)] }, Token)).FailureCode);
    }

    [Fact]
    public async Task Create_PortConflictRemovesTheNewContainerAndNameConflictIsReported()
    {
        RequirePodman();
        var (first, _, _) = await Creator().CreateAsync(Command(), Spec("porta-a", 18083), Token);
        Assert.Equal(CommandExecutionState.Succeeded, first);
        var command = Command();

        var (state, failure, id) = await Creator().CreateAsync(command, Spec("porta-b", 18083), Token);

        Assert.Equal(CommandExecutionState.Failed, state);
        Assert.Equal("port_conflict", failure);
        Assert.Null(id);
        Assert.Null(await engine!.FindByLabelAsync(ContainerCreationSpec.CommandLabel, command.CommandId.ToString("D"), Token));
        Assert.Equal("name_conflict", (await Creator().CreateAsync(Command(), Spec("porta-a", 18084), Token)).FailureCode);
    }

    [Fact]
    public async Task HostObjects_ListNetworksVolumesAndSecretsByName()
    {
        RequirePodman();
        using (var volume = await http!.PostAsync($"v{apiVersion}/volumes/create", new StringContent($"{{\"Name\":\"dados-{suffix}\"}}", Encoding.UTF8, "application/json"), Token)) { }

        var volumes = await engine!.ListHostObjectsAsync(Dokpod.Agent.Application.Engines.EngineHostObjectKind.Volumes, Token);
        var networks = await engine.ListHostObjectsAsync(Dokpod.Agent.Application.Engines.EngineHostObjectKind.Networks, Token);
        await engine.ListHostObjectsAsync(Dokpod.Agent.Application.Engines.EngineHostObjectKind.PodmanSecrets, Token);

        Assert.Contains(volumes, v => v.Name == $"dados-{suffix}");
        Assert.NotEmpty(networks);
    }
}
