using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Dokpod.Agent.Application.Terminal;
using Dokpod.Agent.Contracts.V1;
using Dokpod.Agent.Infrastructure.Engines.Docker;
using Dokpod.Agent.Infrastructure.Terminal;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Xunit;

namespace Dokpod.Agent.Podman.IntegrationTests;

/// <summary>Spec 014 T007/SC-002 em Podman real: sessão completa no agente e término confirmado por inspeção do exec.</summary>
public sealed class ExecSessionPodmanTests : IAsyncLifetime
{
    private const string FixtureImage = "docker.io/lzocateli/nginx:1.28.0-bookworm-v1";
    private HttpClient? http;
    private DockerEngineClient? engine;
    private string apiVersion = string.Empty;
    private readonly List<string> containers = [];
    private string dataDirectory = string.Empty;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var socket = Environment.GetEnvironmentVariable("DOKPOD_TEST_PODMAN_SOCKET");
        if (string.IsNullOrWhiteSpace(socket)) return;
        http = DockerEngineClient.CreateUnixSocketClient(socket, TimeSpan.FromSeconds(60));
        engine = new DockerEngineClient(http, null, new EngineRawConnector(socket));
        apiVersion = (await engine.InspectAsync(CancellationToken.None)).ApiVersion;
        dataDirectory = Directory.CreateTempSubdirectory("dokpod-exec-it-").FullName;
    }

    public async ValueTask DisposeAsync()
    {
        if (http is null) return;
        foreach (var id in containers)
        {
            using var _ = await http.DeleteAsync($"v{apiVersion}/containers/{id}?force=1", CancellationToken.None);
        }

        http.Dispose();
        Directory.Delete(dataDirectory, recursive: true);
    }

    private async Task<string> StartContainerAsync(object? labels = null)
    {
        using var content = new StringContent(JsonSerializer.Serialize(new { Image = FixtureImage, Labels = labels ?? new { } }), Encoding.UTF8, "application/json");
        using var created = await http!.PostAsync($"v{apiVersion}/containers/create?name=dokpod-exec-session-{Guid.NewGuid():N}", content, Token);
        var id = JsonDocument.Parse(await created.Content.ReadAsStringAsync(Token)).RootElement.GetProperty("Id").GetString()!;
        containers.Add(id);
        using var _ = await http.PostAsync($"v{apiVersion}/containers/{id}/start", null, Token);
        return id;
    }

    /// <summary>Lado "API" do RPC `Exec` em memória.</summary>
    private sealed class MemoryChannel : IExecChannel
    {
        public Channel<ExecFrame> FromAgent { get; } = Channel.CreateUnbounded<ExecFrame>();
        public Channel<ExecFrame> ToAgent { get; } = Channel.CreateUnbounded<ExecFrame>();

        public Task SendAsync(ExecFrame frame, CancellationToken cancellationToken) => FromAgent.Writer.WriteAsync(frame, cancellationToken).AsTask();
        public IAsyncEnumerable<ExecFrame> ReadAllAsync(CancellationToken cancellationToken) => ToAgent.Reader.ReadAllAsync(cancellationToken);

        public Task CompleteAsync()
        {
            FromAgent.Writer.TryComplete();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public async Task<ExecStatus> NextStatusAsync(TimeSpan timeout, StringBuilder? output = null)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(Token);
            cts.CancelAfter(timeout);
            while (true)
            {
                var frame = await FromAgent.Reader.ReadAsync(cts.Token);
                if (frame.FrameCase == ExecFrame.FrameOneofCase.Status) return frame.Status;
                if (frame.FrameCase == ExecFrame.FrameOneofCase.Output) output?.Append(frame.Output.ToStringUtf8());
            }
        }

        public async Task<string> ReadUntilAsync(string marker, TimeSpan timeout)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(Token);
            cts.CancelAfter(timeout);
            var text = new StringBuilder();
            while (!text.ToString().Contains(marker, StringComparison.Ordinal))
            {
                var frame = await FromAgent.Reader.ReadAsync(cts.Token);
                if (frame.FrameCase == ExecFrame.FrameOneofCase.Output) text.Append(frame.Output.ToStringUtf8());
            }

            return text.ToString();
        }
    }

    private static ExecOpenRequest Request(string containerId, TimeSpan? expiresIn = null) => new()
    {
        ExecSessionId = Guid.NewGuid().ToString("D"),
        ContainerId = containerId,
        Cols = 100,
        Rows = 30,
        ExpiresAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow + (expiresIn ?? TimeSpan.FromMinutes(5))),
        MaxOutputBytesPerSecond = 256 * 1024,
    };

    private void RequirePodman()
    {
        if (engine is null) Assert.Skip("DOKPOD_TEST_PODMAN_SOCKET não configurado.");
    }

    [Theory]
    [InlineData("user_closed")]
    [InlineData("exit")]
    [InlineData("client_disconnected")]
    public async Task Session_EndsShellInEngineForEachReason(string reason)
    {
        RequirePodman();
        var containerId = await StartContainerAsync();
        var store = new FileActiveExecStore(dataDirectory);
        var manager = new ExecSessionManager(engine!, store, TimeProvider.System);
        var channel = new MemoryChannel();
        var request = Request(containerId);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var run = manager.RunAsync(request, _ => Task.FromResult<IExecChannel>(channel), new ExecHandshake { ExecSessionId = request.ExecSessionId }, Token);
        Assert.Equal(ExecFrame.FrameOneofCase.Handshake, (await channel.FromAgent.Reader.ReadAsync(Token)).FrameCase);
        var opened = await channel.NextStatusAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(ExecState.Opened, opened.State);
        Assert.Equal("/bin/bash", opened.Shell);
        Assert.True(opened.ContainerUserIsRoot);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3), $"SC-001: sessão aberta em {stopwatch.ElapsedMilliseconds} ms (meta ≤ 3 s)");
        var active = Assert.Single(await store.ListAsync(Token));

        var echo = System.Diagnostics.Stopwatch.StartNew();
        await channel.ToAgent.Writer.WriteAsync(new ExecFrame { Stdin = ByteString.CopyFromUtf8("echo canario-014-$((7*6))\n") }, Token);
        Assert.Contains("canario-014-42", await channel.ReadUntilAsync("canario-014-42", TimeSpan.FromSeconds(5)));
        Assert.True(echo.Elapsed < TimeSpan.FromMilliseconds(150), $"SC-001: eco no agente em {echo.ElapsedMilliseconds} ms (meta ≤ 150 ms)");
        await channel.ToAgent.Writer.WriteAsync(new ExecFrame { Stdin = ByteString.CopyFromUtf8("sleep 600\n") }, Token);
        await Task.Delay(300, Token);

        switch (reason)
        {
            case "user_closed":
                await channel.ToAgent.Writer.WriteAsync(new ExecFrame { Status = new ExecStatus { State = ExecState.Closed, Reason = "user_closed" } }, Token);
                break;
            case "exit":
                await channel.ToAgent.Writer.WriteAsync(new ExecFrame { Stdin = ByteString.CopyFromUtf8("\u0003exit\n") }, Token);
                break;
            default:
                channel.ToAgent.Writer.TryComplete();
                break;
        }

        var closedAt = System.Diagnostics.Stopwatch.StartNew();
        var closed = await channel.NextStatusAsync(TimeSpan.FromSeconds(10));
        await run;
        Assert.Equal(ExecState.Closed, closed.State);
        Assert.Equal(reason, closed.Reason);
        Assert.False(closed.OrphanSuspected);
        Assert.True(closedAt.Elapsed < TimeSpan.FromSeconds(6), $"término em {closedAt.Elapsed}");
        var state = await engine!.InspectExecAsync(active.EngineExecId, Token);
        Assert.False(state?.Running ?? false);
        Assert.Empty(await store.ListAsync(Token));
    }

    [Fact]
    public async Task Session_ContainerStoppedEndsSession()
    {
        RequirePodman();
        var containerId = await StartContainerAsync();
        var manager = new ExecSessionManager(engine!, new FileActiveExecStore(dataDirectory), TimeProvider.System);
        var channel = new MemoryChannel();
        var request = Request(containerId);
        var run = manager.RunAsync(request, _ => Task.FromResult<IExecChannel>(channel), new ExecHandshake(), Token);
        await channel.FromAgent.Reader.ReadAsync(Token);
        Assert.Equal(ExecState.Opened, (await channel.NextStatusAsync(TimeSpan.FromSeconds(5))).State);

        using (var _ = await http!.PostAsync($"v{apiVersion}/containers/{containerId}/stop?t=1", null, Token)) { }

        var closed = await channel.NextStatusAsync(TimeSpan.FromSeconds(15));
        await run;
        Assert.Equal("container_stopped", closed.Reason);
    }

    [Fact]
    public async Task Session_RefusesLabelDisabledExpiredDuplicateAndOverLimit()
    {
        RequirePodman();
        var disabled = await StartContainerAsync(new Dictionary<string, string> { ["dokpod.exec"] = "disabled" });
        var normal = await StartContainerAsync();
        var manager = new ExecSessionManager(engine!, new FileActiveExecStore(dataDirectory), TimeProvider.System, maxSessions: 1);

        async Task<string?> OpenAsync(ExecOpenRequest request, MemoryChannel channel)
        {
            var run = manager.RunAsync(request, _ => Task.FromResult<IExecChannel>(channel), new ExecHandshake(), Token);
            await channel.FromAgent.Reader.ReadAsync(Token);
            var status = await channel.NextStatusAsync(TimeSpan.FromSeconds(5));
            if (status.State != ExecState.Opened) await run; // recusas terminam e liberam a vaga
            return status.State == ExecState.Opened ? "opened" : status.Reason;
        }

        Assert.Equal("label_disabled", await OpenAsync(Request(disabled), new MemoryChannel()));
        Assert.Equal("expired", await OpenAsync(Request(normal, TimeSpan.FromSeconds(-1)), new MemoryChannel()));
        var first = new MemoryChannel();
        var firstRequest = Request(normal);
        Assert.Equal("opened", await OpenAsync(firstRequest, first));
        Assert.Equal("limit", await OpenAsync(Request(normal), new MemoryChannel()));

        // Mesmo exec_session_id de novo (at-most-once): ignorado sem abrir canal.
        var opened = 0;
        await manager.RunAsync(firstRequest, _ => { opened++; return Task.FromResult<IExecChannel>(new MemoryChannel()); }, new ExecHandshake(), Token);
        Assert.Equal(0, opened);
        await first.ToAgent.Writer.WriteAsync(new ExecFrame { Status = new ExecStatus { State = ExecState.Closed, Reason = "user_closed" } }, Token);
        Assert.Equal(ExecState.Closed, (await first.NextStatusAsync(TimeSpan.FromSeconds(10))).State);
    }

    [Fact]
    public async Task Recovery_TerminatesExecsLeftByAPreviousProcess()
    {
        RequirePodman();
        var containerId = await StartContainerAsync();
        var session = await engine!.StartExecAsync(containerId, ShellLauncher.Command, 80, 24, Token);
        var banner = new byte[256];
        var read = await session.Stream.ReadAsync(banner, Token);
        Assert.True(ShellLauncher.TryParse(Encoding.UTF8.GetString(banner, 0, read), out var pid, out _, out _));
        var store = new FileActiveExecStore(dataDirectory);
        await store.AddAsync(new ActiveExec(Guid.NewGuid().ToString("D"), session.ExecId, containerId, pid, DateTimeOffset.UtcNow), Token);
        await session.DisposeAsync(); // processo anterior "morreu" sem encerrar o shell

        var orphans = await new ExecSessionManager(engine, store, TimeProvider.System).RecoverOrphansAsync(Token);

        Assert.Empty(orphans);
        Assert.False((await engine.InspectExecAsync(session.ExecId, Token))?.Running ?? false);
        Assert.Empty(await store.ListAsync(Token));
    }
}
