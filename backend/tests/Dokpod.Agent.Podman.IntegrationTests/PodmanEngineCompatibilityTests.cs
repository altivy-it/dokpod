using System.Net;
using System.Text;
using System.Text.Json;
using Dokpod.Agent.Application.Engines;
using Dokpod.Agent.Infrastructure.Engines.Docker;
using Dokpod.Domain.Commands;
using Xunit;

namespace Dokpod.Agent.Podman.IntegrationTests;

/// <summary>
/// Qualificação do adapter de engine contra Podman real pela API compatível (spec 008 T011).
/// Requer `DOKPOD_TEST_PODMAN_SOCKET` (socket rootless ou rootful) e a imagem de fixture local.
/// </summary>
public sealed class PodmanEngineCompatibilityTests : IAsyncLifetime
{
    private const string FixtureImage = "docker.io/lzocateli/nginx:1.28.0-bookworm";
    private readonly List<string> created = [];
    private HttpClient? http;
    private DockerEngineClient? engine;
    private string apiVersion = string.Empty;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var socket = Environment.GetEnvironmentVariable("DOKPOD_TEST_PODMAN_SOCKET");
        if (string.IsNullOrWhiteSpace(socket))
        {
            return;
        }

        http = DockerEngineClient.CreateUnixSocketClient(socket, TimeSpan.FromSeconds(60));
        engine = new DockerEngineClient(http);
        apiVersion = (await engine.InspectAsync(CancellationToken.None)).ApiVersion;
    }

    public async ValueTask DisposeAsync()
    {
        if (http is null)
        {
            return;
        }

        foreach (var id in created)
        {
            using var _ = await http.DeleteAsync($"v{apiVersion}/containers/{id}?v=1&force=1", CancellationToken.None);
        }

        http.Dispose();
    }

    private DockerEngineClient Engine => engine ?? throw new InvalidOperationException();

    private void RequirePodman()
    {
        if (engine is null)
        {
            Assert.Skip("DOKPOD_TEST_PODMAN_SOCKET não configurado.");
        }
    }

    [Fact]
    public async Task Detection_IdentifiesPodmanAndDescribesHost()
    {
        RequirePodman();
        var descriptor = await Engine.InspectAsync(Token);
        var host = await Engine.DescribeHostAsync(Token);

        Assert.True(EngineVersionIsSemver(descriptor.EngineVersion), descriptor.EngineVersion);
        Assert.NotNull(host);
        Assert.Equal("podman", host.Engine);
        Assert.True(host.CpuCount > 0);
        Assert.True(host.MemoryBytes > 0);
        var expectRootless = Environment.GetEnvironmentVariable("DOKPOD_TEST_PODMAN_ROOTLESS");
        if (expectRootless is not null)
        {
            Assert.Equal(expectRootless == "1", host.Rootless);
        }
    }

    [Fact]
    public async Task Inventory_ReportsPortsHealthAndStartTime()
    {
        RequirePodman();
        var id = await CreateAsync(new
        {
            Image = FixtureImage,
            ExposedPorts = new Dictionary<string, object> { ["80/tcp"] = new { } },
            HostConfig = new { PortBindings = new Dictionary<string, object> { ["80/tcp"] = new[] { new { HostIp = "127.0.0.1", HostPort = "18081" } } } },
            Healthcheck = new { Test = new[] { "CMD-SHELL", "exit 0" }, Interval = 1_000_000_000L, Retries = 1 },
        });
        Assert.True((await Engine.ExecuteAsync(AgentCommandKind.StartContainer, id, Token)).Succeeded);

        EngineContainer? container = null;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            container = (await Engine.ListContainersAsync(Token)).Single(c => c.ContainerId == id);
            if (container.Health == "healthy") break;
            await Task.Delay(500, Token);
        }

        Assert.NotNull(container);
        Assert.Equal("running", container.State);
        Assert.Equal("healthy", container.Health);
        Assert.NotNull(container.StartedAtUtc);
        Assert.NotNull(container.CreatedAtUtc);
        Assert.Equal(0, container.RestartCount);
        Assert.Contains(container.Ports, p => p.HostPort == 18081 && p.ContainerPort == 80 && p.Protocol == "tcp");
    }

    [Fact]
    public async Task Lifecycle_SupportsEveryAnnouncedMutation()
    {
        RequirePodman();
        var id = await CreateAsync(new { Image = FixtureImage });

        foreach (var (kind, state) in new[]
                 {
                     (AgentCommandKind.StartContainer, "running"),
                     (AgentCommandKind.PauseContainer, "paused"),
                     (AgentCommandKind.UnpauseContainer, "running"),
                     (AgentCommandKind.RestartContainer, "running"),
                     (AgentCommandKind.PauseContainer, "paused"),
                     // Podman recusa stop em pausado; o adapter retoma antes de parar (paridade com Docker).
                     (AgentCommandKind.StopContainer, "exited"),
                 })
        {
            var result = await Engine.ExecuteAsync(kind, id, Token);
            Assert.True(result.Succeeded, $"{kind}: {result.FailureCode}");
            var inspected = await Engine.InspectContainerAsync(id, Token);
            Assert.Equal(state, inspected!.State);
        }

        Assert.Equal("not_paused", (await Engine.ExecuteAsync(AgentCommandKind.UnpauseContainer, id, Token)).FailureCode);
        Assert.Equal("not_running", (await Engine.ExecuteAsync(AgentCommandKind.PauseContainer, id, Token)).FailureCode);
        Assert.True((await Engine.ExecuteAsync(AgentCommandKind.DeleteContainer, id, Token)).Succeeded);
        Assert.Null(await Engine.InspectContainerAsync(id, Token));
        Assert.Equal("target_not_found", (await Engine.ExecuteAsync(AgentCommandKind.StartContainer, id, Token)).FailureCode);
    }

    [Fact]
    public async Task Inventory_ReportsExitCodeOfFailedContainer()
    {
        RequirePodman();
        var id = await CreateAsync(new { Image = FixtureImage, Entrypoint = new[] { "/bin/sh", "-c", "exit 3" } });
        Assert.True((await Engine.ExecuteAsync(AgentCommandKind.StartContainer, id, Token)).Succeeded);

        EngineContainer? container = null;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            container = await Engine.InspectContainerAsync(id, Token);
            if (container?.State == "exited") break;
            await Task.Delay(250, Token);
        }

        Assert.Equal("exited", container!.State);
        var listed = (await Engine.ListContainersAsync(Token)).Single(c => c.ContainerId == id);
        Assert.Equal(3, listed.ExitCode);
        Assert.False(listed.OomKilled ?? false);
    }

    [Fact]
    public async Task Events_StreamStartAndDieWithAllowlistedAttributes()
    {
        RequirePodman();
        var id = await CreateAsync(new { Image = FixtureImage, Entrypoint = new[] { "/bin/sh", "-c", "sleep 1; exit 2" } });
        using var watch = CancellationTokenSource.CreateLinkedTokenSource(Token);
        watch.CancelAfter(TimeSpan.FromSeconds(30));
        var seen = new List<EngineEventRecord>();
        var reader = Task.Run(async () =>
        {
            try
            {
                await foreach (var record in Engine.WatchEventsAsync(watch.Token))
                {
                    if (record.SubjectId != id) continue;
                    seen.Add(record);
                    if (record.Action == "die") break;
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, Token);

        await Task.Delay(500, Token);
        Assert.True((await Engine.ExecuteAsync(AgentCommandKind.StartContainer, id, Token)).Succeeded);
        await reader;

        Assert.Contains(seen, e => e.Type == "container" && e.Action == "start");
        var die = Assert.Single(seen, e => e.Action == "die");
        Assert.Equal(2, die.ExitCode);
        Assert.False(string.IsNullOrEmpty(die.Name));
        Assert.True(die.TimeNano > 0);
    }

    [Fact]
    public async Task Metrics_StatsOfRunningContainerAndHostProcMatchEngine()
    {
        RequirePodman();
        var id = await CreateAsync(new { Image = FixtureImage });
        Assert.True((await Engine.ExecuteAsync(AgentCommandKind.StartContainer, id, Token)).Succeeded);

        var stats = await Engine.GetContainerStatsAsync(id, Token);
        Assert.NotNull(stats);
        Assert.True(stats.MemoryUsedBytes > 0);
        Assert.True(stats.OnlineCpus > 0);
        Assert.True(stats.SystemCpuNanos > 0);

        var sampler = new Dokpod.Agent.Application.Metrics.ResourceSampler(
            Engine, new Dokpod.Agent.Infrastructure.Metrics.ProcHostProbe(), TimeProvider.System)
        {
            EngineMemoryTotalBytes = (await Engine.DescribeHostAsync(Token))!.MemoryBytes,
        };
        await sampler.SampleAsync([new Dokpod.Agent.Application.Metrics.SampleTarget(id, null)], Token);
        await Task.Delay(1500, Token);
        var batch = (await sampler.SampleAsync([new Dokpod.Agent.Application.Metrics.SampleTarget(id, null)], Token)).Single();

        // `/proc` lido de dentro de um container rootless reflete o host (D-003): memória coerente com `/info`.
        Assert.True(batch.Host.HasMemoryTotalBytes, "host sem memória: /proc virtualizado ou ilegível");
        Assert.True(batch.Host.HasCpuPercent);
        Assert.InRange(batch.Host.CpuPercent, 0, 100);
        var container = Assert.Single(batch.Containers);
        Assert.True(container.HasCpuPercentOfHost);
        Assert.InRange(container.CpuPercentOfHost, 0, 100);
        Assert.True(container.MemoryUsedBytes > 0);
    }

    private async Task<string> CreateAsync(object spec)
    {
        using var content = new StringContent(JsonSerializer.Serialize(spec), Encoding.UTF8, "application/json");
        using var response = await http!.PostAsync($"v{apiVersion}/containers/create?name=dokpod-podman-it-{Guid.NewGuid():N}", content, Token);
        var body = await response.Content.ReadAsStringAsync(Token);
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        var id = JsonDocument.Parse(body).RootElement.GetProperty("Id").GetString()!;
        created.Add(id);
        return id;
    }

    private static bool EngineVersionIsSemver(string version) => Version.TryParse(version.Split('-')[0], out _);
}
