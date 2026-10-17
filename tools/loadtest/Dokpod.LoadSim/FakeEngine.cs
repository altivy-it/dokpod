using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Dokpod.Agent.Application.Engines;
using Dokpod.Domain.Commands;

namespace Dokpod.LoadSim;

/// <summary>
/// Engine em memória de um host simulado: containers em estados variados, eventos de reinício aleatórios ("churn"),
/// estatísticas crescentes e ações que mudam o estado e geram o evento correspondente, como o Docker.
/// </summary>
public sealed class FakeEngine : IContainerEngine
{
    private static readonly string[] Images = ["registry.empresa.com/carga/api:1", "registry.empresa.com/carga/web:2", "docker.io/library/nginx:1.27", "docker.io/library/redis:7", "docker.io/library/postgres:17"];
    private readonly ConcurrentDictionary<string, EngineContainer> containers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ulong> cpu = new(StringComparer.Ordinal);
    private readonly Channel<EngineEventRecord> events = Channel.CreateUnbounded<EngineEventRecord>();
    private readonly Random random;
    private readonly string host;
    private long revision;

    public FakeEngine(int index, int count, Random random)
    {
        this.random = random;
        host = $"carga-{index:D3}";
        for (var i = 0; i < count; i++)
        {
            var id = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{host}-{i}"))).ToLowerInvariant();
            var state = (i % 20) switch { 17 => "exited", 18 => "paused", 19 => "created", _ => "running" };
            containers[id] = new EngineContainer(id, $"{host}-app-{i:D2}", Images[i % Images.Length], state, Next())
            {
                CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-2),
                StartedAtUtc = state == "running" ? DateTimeOffset.UtcNow.AddHours(-3) : null,
                ExitCode = state == "exited" ? 1 : null,
                RestartCount = 0,
            };
        }
    }

    public string HostName => host;

    /// <summary>Destino opcional do registro de execuções (variável LOADSIM_EXEC_LOG).</summary>
    public static StreamWriter? ExecutionLog { get; set; }

    /// <summary>Comandos executados (para o relatório do simulador).</summary>
    public int Commands;

    /// <summary>Eventos de reinício aleatório gerados.</summary>
    public int Churned;

    private string Next() => Interlocked.Increment(ref revision).ToString(System.Globalization.CultureInfo.InvariantCulture);

    public Task<EngineDescriptor> InspectAsync(CancellationToken cancellationToken) => Task.FromResult(new EngineDescriptor("28.5.2", "1.47"));

    public Task<EngineHostInfo?> DescribeHostAsync(CancellationToken cancellationToken) =>
        Task.FromResult<EngineHostInfo?>(new EngineHostInfo("docker", host, 4, 16L * 1024 * 1024 * 1024, false));

    public Task<IReadOnlyList<EngineContainer>> ListContainersAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EngineContainer>>(containers.Values.OrderBy(c => c.Name, StringComparer.Ordinal).ToArray());

    public Task<EngineContainer?> InspectContainerAsync(string containerId, CancellationToken cancellationToken) =>
        Task.FromResult(containers.TryGetValue(containerId, out var c) ? c : null);

    public async Task<ContainerMutationResult> ExecuteAsync(AgentCommandKind commandKind, string containerId, CancellationToken cancellationToken)
    {
        await Task.Delay(random.Next(50, 200), cancellationToken).ConfigureAwait(false);
        if (!containers.TryGetValue(containerId, out var container)) return new ContainerMutationResult(false, "container_not_found");
        Interlocked.Increment(ref Commands);
        var (state, action) = commandKind switch
        {
            AgentCommandKind.StartContainer => ("running", "start"),
            AgentCommandKind.StopContainer => ("exited", "stop"),
            AgentCommandKind.RestartContainer => ("running", "restart"),
            AgentCommandKind.PauseContainer => ("paused", "pause"),
            AgentCommandKind.UnpauseContainer => ("running", "unpause"),
            AgentCommandKind.DeleteContainer => ("removed", "destroy"),
            _ => (container.State, ""),
        };
        if (action.Length == 0) return new ContainerMutationResult(false, "not_supported");
        // Registro de cada execução real (prova de ausência de efeito duplicado na recuperação, T016).
        if (ExecutionLog is { } log) lock (log) log.WriteLine($"{DateTimeOffset.UtcNow:O},{host},{containerId},{action}");
        if (state == "removed") containers.TryRemove(containerId, out _);
        else Update(container, state, state == "exited" ? 0 : null);
        Publish("container", action, container, state == "exited" ? 0 : null);
        return new ContainerMutationResult(true, null);
    }

    /// <summary>Reinício espontâneo de um container em execução (die → start), como uma aplicação que cai.</summary>
    public void Churn()
    {
        var running = containers.Values.Where(c => c.State == "running").ToArray();
        if (running.Length == 0) return;
        var target = running[random.Next(running.Length)];
        Interlocked.Increment(ref Churned);
        Update(target, "exited", 137);
        Publish("container", "die", target, 137);
        Update(containers[target.ContainerId], "running", null, restarted: true);
        Publish("container", "start", target, null);
    }

    private void Update(EngineContainer container, string state, int? exitCode, bool restarted = false)
    {
        containers[container.ContainerId] = container with
        {
            State = state,
            Revision = Next(),
            ExitCode = exitCode,
            StartedAtUtc = state == "running" ? DateTimeOffset.UtcNow : container.StartedAtUtc,
            RestartCount = (container.RestartCount ?? 0) + (restarted ? 1 : 0),
        };
    }

    private void Publish(string type, string action, EngineContainer container, int? exitCode)
    {
        var now = DateTimeOffset.UtcNow;
        events.Writer.TryWrite(new EngineEventRecord(type, action, container.ContainerId, container.Name, container.ImageReference, exitCode, now, now.ToUnixTimeMilliseconds() * 1_000_000));
    }

    /// <summary>Inspeção mínima no formato do Docker (resumo e inspeção do console).</summary>
    public Task<string?> GetContainerJsonAsync(string containerId, CancellationToken cancellationToken)
    {
        if (!containers.TryGetValue(containerId, out var c)) return Task.FromResult<string?>(null);
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            Id = c.ContainerId,
            Name = "/" + c.Name,
            Created = c.CreatedAtUtc,
            Image = "sha256:" + new string('a', 64),
            RestartCount = c.RestartCount ?? 0,
            State = new { Status = c.State, Running = c.State == "running", Paused = c.State == "paused", ExitCode = c.ExitCode ?? 0, StartedAt = c.StartedAtUtc },
            Config = new { Image = c.ImageReference, Hostname = host, Env = new[] { "APP_MODE=carga" }, Labels = new Dictionary<string, string> { ["carga"] = "spec-005" }, Cmd = new[] { "app" } },
            HostConfig = new { RestartPolicy = new { Name = "unless-stopped", MaximumRetryCount = 0 }, NanoCpus = 0, Memory = 0 },
            NetworkSettings = new { Networks = new Dictionary<string, object> { ["bridge"] = new { IPAddress = "172.17.0.2" } } },
            Mounts = Array.Empty<object>(),
        });
        return Task.FromResult<string?>(json);
    }

    public async IAsyncEnumerable<EngineEventRecord> WatchEventsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await events.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (events.Reader.TryRead(out var item)) yield return item;
        }
    }

    public Task<EngineContainerStats?> GetContainerStatsAsync(string containerId, CancellationToken cancellationToken)
    {
        var used = cpu.AddOrUpdate(containerId, (ulong)random.Next(1, 50) * 10_000_000UL, (_, v) => v + (ulong)random.Next(1, 50) * 10_000_000UL);
        var system = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 4_000_000UL;
        return Task.FromResult<EngineContainerStats?>(new EngineContainerStats(used, system, 4, (ulong)random.Next(50, 400) * 1024 * 1024, 16UL * 1024 * 1024 * 1024, 1000, 1000, 0, 0));
    }

    public Task<IReadOnlyList<EngineImage>> ListImagesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EngineImage>>(Images.Select((tag, i) => new EngineImage(
            "sha256:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(tag))).ToLowerInvariant(),
            [tag], [], 50_000_000L * (i + 1), DateTimeOffset.UtcNow.AddDays(-10))).ToArray());

    public Task<IReadOnlyList<EngineVolume>> ListVolumesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EngineVolume>>([new EngineVolume($"{host}-dados", "local", "local", DateTimeOffset.UtcNow.AddDays(-5), new Dictionary<string, string>())]);
}
