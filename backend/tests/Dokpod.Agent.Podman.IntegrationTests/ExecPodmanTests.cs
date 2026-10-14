using System.Net;
using System.Text;
using System.Text.Json;
using Dokpod.Agent.Application.Engines;
using Dokpod.Agent.Infrastructure.Engines.Docker;
using Xunit;

namespace Dokpod.Agent.Podman.IntegrationTests;

/// <summary>Spec 014 T006 em Podman real: exec sequestrado, eco, resize, término por `exit` e por fechamento do TTY.</summary>
public sealed class ExecPodmanTests : IAsyncLifetime
{
    private const string FixtureImage = "docker.io/lzocateli/nginx:1.28.0-bookworm-v1";
    private HttpClient? http;
    private DockerEngineClient? engine;
    private string apiVersion = string.Empty;
    private string? containerId;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var socket = Environment.GetEnvironmentVariable("DOKPOD_TEST_PODMAN_SOCKET");
        if (string.IsNullOrWhiteSpace(socket)) return;
        http = DockerEngineClient.CreateUnixSocketClient(socket, TimeSpan.FromSeconds(60));
        engine = new DockerEngineClient(http, null, new EngineRawConnector(socket));
        apiVersion = (await engine.InspectAsync(CancellationToken.None)).ApiVersion;
        using var content = new StringContent(JsonSerializer.Serialize(new { Image = FixtureImage }), Encoding.UTF8, "application/json");
        using var created = await http.PostAsync($"v{apiVersion}/containers/create?name=dokpod-exec-it-{Guid.NewGuid():N}", content, CancellationToken.None);
        containerId = JsonDocument.Parse(await created.Content.ReadAsStringAsync(CancellationToken.None)).RootElement.GetProperty("Id").GetString();
        using var _ = await http.PostAsync($"v{apiVersion}/containers/{containerId}/start", null, CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        if (http is null) return;
        using (var _ = await http.DeleteAsync($"v{apiVersion}/containers/{containerId}?force=1", CancellationToken.None)) { }
        http.Dispose();
    }

    private static async Task<string> ReadUntilAsync(Stream stream, string marker, TimeSpan timeout)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Token);
        cts.CancelAfter(timeout);
        var text = new StringBuilder();
        var buffer = new byte[4096];
        while (!text.ToString().Contains(marker, StringComparison.Ordinal))
        {
            var read = await stream.ReadAsync(buffer, cts.Token);
            if (read == 0) break;
            text.Append(Encoding.UTF8.GetString(buffer, 0, read));
        }

        return text.ToString();
    }

    private static async Task<EngineExecState?> WaitStoppedAsync(DockerEngineClient engine, string execId)
    {
        EngineExecState? state = null;
        for (var i = 0; i < 50; i++)
        {
            state = await engine.InspectExecAsync(execId, Token);
            if (state is null || !state.Running) return state;
            await Task.Delay(100, Token);
        }

        return state;
    }

    [Fact]
    public async Task Exec_EchoesResizesAndEndsOnExit()
    {
        if (engine is null) Assert.Skip("DOKPOD_TEST_PODMAN_SOCKET não configurado.");
        await using var session = await engine.StartExecAsync(containerId!, ["/bin/bash"], 100, 30, Token);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        await session.Stream.WriteAsync(Encoding.UTF8.GetBytes("echo dokpod-$((20+22))\n"), Token);
        var output = await ReadUntilAsync(session.Stream, "dokpod-42", TimeSpan.FromSeconds(5));
        Assert.Contains("dokpod-42", output);
        TestContext.Current.SendDiagnosticMessage($"eco em {stopwatch.ElapsedMilliseconds} ms");
        Assert.True((await engine.InspectExecAsync(session.ExecId, Token))!.Running);

        await engine.ResizeExecAsync(session.ExecId, 132, 40, Token);
        await session.Stream.WriteAsync(Encoding.UTF8.GetBytes("stty size\n"), Token);
        Assert.Contains("40 132", await ReadUntilAsync(session.Stream, "40 132", TimeSpan.FromSeconds(5)));

        await session.Stream.WriteAsync(Encoding.UTF8.GetBytes("exit\n"), Token);
        await ReadUntilAsync(session.Stream, "\u0000never", TimeSpan.FromSeconds(5));
        var state = await WaitStoppedAsync(engine, session.ExecId);
        Assert.False(state?.Running ?? false);
    }

    [Fact]
    public async Task Exec_ClosingTheTtyEndsTheShellWithin5Seconds()
    {
        if (engine is null) Assert.Skip("DOKPOD_TEST_PODMAN_SOCKET não configurado.");
        var session = await engine.StartExecAsync(containerId!, Dokpod.Agent.Application.Terminal.ShellLauncher.Command, 80, 24, Token);
        var banner = await ReadUntilAsync(session.Stream, "\u0007", TimeSpan.FromSeconds(5));
        Assert.True(Dokpod.Agent.Application.Terminal.ShellLauncher.TryParse(banner, out var pid, out var shell, out _), banner);
        Assert.Equal("/bin/bash", shell);
        await session.Stream.WriteAsync(Encoding.UTF8.GetBytes("sleep 300\n"), Token);
        await Task.Delay(300, Token);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await session.DisposeAsync();
        await engine.SignalContainerProcessAsync(containerId!, pid, "HUP", Token);
        var state = await WaitStoppedAsync(engine, session.ExecId);

        Assert.False(state?.Running ?? false, "shell ainda em execução após fechar o TTY");
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Exec_MissingBinaryIsDetectable()
    {
        if (engine is null) Assert.Skip("DOKPOD_TEST_PODMAN_SOCKET não configurado.");
        try
        {
            await using var session = await engine.StartExecAsync(containerId!, ["/bin/nao-existe"], 80, 24, Token);
            var output = await ReadUntilAsync(session.Stream, "\u0000never", TimeSpan.FromSeconds(3));
            var state = await WaitStoppedAsync(engine, session.ExecId);
            TestContext.Current.SendDiagnosticMessage($"saida={output.Trim()} exit={state?.ExitCode}");
            Assert.True(state?.ExitCode is 126 or 127 || DockerEngineClient.IsExecutableNotFound(output), $"exit={state?.ExitCode} out={output}");
        }
        catch (EngineExecException exception)
        {
            TestContext.Current.SendDiagnosticMessage($"exceção: {exception.Message} notFound={exception.ExecutableNotFound}");
            Assert.True(exception.ExecutableNotFound);
        }
    }
}
