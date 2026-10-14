using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Dokpod.Agent.Application.Engines;
using Dokpod.Agent.Contracts.V1;
using Google.Protobuf;

namespace Dokpod.Agent.Application.Terminal;

/// <summary>Canal do RPC `Exec` (aberto pelo agente); abstraído para testes.</summary>
public interface IExecChannel : IAsyncDisposable
{
    Task SendAsync(ExecFrame frame, CancellationToken cancellationToken);

    IAsyncEnumerable<ExecFrame> ReadAllAsync(CancellationToken cancellationToken);

    Task CompleteAsync();
}

/// <summary>Execs ativos persistidos no estado local do agente (`&lt;data&gt;/exec-sessions.json`, D-007).</summary>
public sealed record ActiveExec(string ExecSessionId, string EngineExecId, string ContainerId, int Pid, DateTimeOffset StartedAtUtc);

public interface IActiveExecStore
{
    Task AddAsync(ActiveExec exec, CancellationToken cancellationToken);

    Task RemoveAsync(string execSessionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ActiveExec>> ListAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Sessões de terminal no agente (spec 014 T007): at-most-once por `exec_session_id`, recusa após `expires_at`,
/// limite por agente, label `dokpod.exec=disabled`, saída com taxa limitada, término confirmado por inspeção
/// do exec e `orphan_suspected` quando o shell não termina em 5 s.
/// </summary>
public sealed class ExecSessionManager(IContainerEngine engine, IActiveExecStore store, TimeProvider timeProvider, int maxSessions = 4)
{
    public const int MaxChunkBytes = 32 * 1024;
    public const int MaxInputBytes = 64 * 1024;
    public static readonly TimeSpan VerifyTimeout = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<string, byte> active = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTimeOffset> seen = new(StringComparer.Ordinal);

    public int ActiveSessions => active.Count;

    /// <summary>Execs registrados antes do reinício que continuam em execução (encerrados aqui e reportados como órfãos).</summary>
    public async Task<IReadOnlyList<ActiveExec>> RecoverOrphansAsync(CancellationToken cancellationToken)
    {
        var orphans = new List<ActiveExec>();
        foreach (var exec in await store.ListAsync(cancellationToken))
        {
            var state = await SafeInspectAsync(exec.EngineExecId, cancellationToken);
            if (state?.Running == true)
            {
                await TerminateAsync(exec, cancellationToken);
                if ((await SafeInspectAsync(exec.EngineExecId, cancellationToken))?.Running == true) orphans.Add(exec);
            }

            await store.RemoveAsync(exec.ExecSessionId, cancellationToken);
        }

        return orphans;
    }

    public async Task RunAsync(ExecOpenRequest request, Func<CancellationToken, Task<IExecChannel>> openChannel, ExecHandshake handshake, CancellationToken sessionToken)
    {
        // Deduplicação at-most-once: um id repetido é ignorado sem abrir canal.
        var now = timeProvider.GetUtcNow();
        foreach (var (id, at) in seen) if (now - at > TimeSpan.FromHours(1)) seen.TryRemove(id, out _);
        if (!Guid.TryParse(request.ExecSessionId, out _) || !seen.TryAdd(request.ExecSessionId, now)) return;

        await using var channel = await openChannel(sessionToken);
        await channel.SendAsync(new ExecFrame { Handshake = handshake }, sessionToken);
        var refusal = Refuse(request, now);
        if (refusal is null && !active.TryAdd(request.ExecSessionId, 0)) refusal = "limit";
        if (refusal is null && active.Count > maxSessions)
        {
            active.TryRemove(request.ExecSessionId, out _);
            refusal = "limit";
        }

        if (refusal is not null)
        {
            await SendStatusAsync(channel, ExecState.Error, refusal, sessionToken);
            await channel.CompleteAsync();
            return;
        }

        try
        {
            await RunSessionAsync(request, channel, sessionToken);
        }
        finally
        {
            active.TryRemove(request.ExecSessionId, out _);
        }
    }

    private string? Refuse(ExecOpenRequest request, DateTimeOffset now)
    {
        if (request.ExpiresAt is null || request.ExpiresAt.ToDateTimeOffset() <= now) return "expired";
        if (request.ContainerId.Length != 64 || !request.ContainerId.All(Uri.IsHexDigit)) return "error";
        return null;
    }

    private async Task RunSessionAsync(ExecOpenRequest request, IExecChannel channel, CancellationToken sessionToken)
    {
        var json = await engine.GetContainerJsonAsync(request.ContainerId, sessionToken);
        var (running, disabled, root) = Describe(json);
        if (!running || disabled)
        {
            await SendStatusAsync(channel, ExecState.Error, !running ? "not_running" : "label_disabled", sessionToken);
            await channel.CompleteAsync();
            return;
        }

        EngineExecSession exec;
        try
        {
            exec = await engine.StartExecAsync(request.ContainerId, ShellLauncher.Command, (int)Math.Max(request.Cols, 1), (int)Math.Max(request.Rows, 1), sessionToken);
        }
        catch (EngineExecException exception)
        {
            await SendStatusAsync(channel, ExecState.Error, exception.ExecutableNotFound ? "shell_not_available" : exception.Message == "not_running" ? "not_running" : "error", sessionToken);
            await channel.CompleteAsync();
            return;
        }

        var (pid, shell, pending) = await ReadBannerAsync(exec, sessionToken);
        if (pid == 0)
        {
            await exec.DisposeAsync();
            await SendStatusAsync(channel, ExecState.Error, "shell_not_available", sessionToken);
            await channel.CompleteAsync();
            return;
        }

        var record = new ActiveExec(request.ExecSessionId, exec.ExecId, request.ContainerId, pid, timeProvider.GetUtcNow());
        await store.AddAsync(record, CancellationToken.None);
        await channel.SendAsync(new ExecFrame { Status = new ExecStatus { State = ExecState.Opened, Shell = shell, ContainerUserIsRoot = root } }, sessionToken);

        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(sessionToken);
        var expires = request.ExpiresAt.ToDateTimeOffset() - timeProvider.GetUtcNow();
        if (expires > TimeSpan.Zero) lifetime.CancelAfter(expires);
        string reason = "error";
        var output = PumpOutputAsync(exec, channel, pending, Math.Max(4096, (int)request.MaxOutputBytesPerSecond), lifetime.Token);
        var input = PumpInputAsync(exec, channel, lifetime.Token);
        var finished = await Task.WhenAny(output, input);
        if (finished == output)
        {
            reason = await StillRunningAsync(request.ContainerId, sessionToken) ? "exit" : "container_stopped";
        }
        else
        {
            reason = input.IsCompletedSuccessfully ? input.Result
                : sessionToken.IsCancellationRequested ? "agent_disconnected"
                : input.IsCanceled || input.Exception?.InnerException is OperationCanceledException ? "max_duration"
                : "client_disconnected";
        }

        await lifetime.CancelAsync();
        await exec.DisposeAsync();
        await TerminateAsync(record, CancellationToken.None);
        var orphan = (await SafeInspectAsync(exec.ExecId, CancellationToken.None))?.Running == true;
        if (!orphan) await store.RemoveAsync(record.ExecSessionId, CancellationToken.None);
        try
        {
            await channel.SendAsync(new ExecFrame { Status = new ExecStatus { State = ExecState.Closed, Reason = reason, OrphanSuspected = orphan } }, CancellationToken.None);
            await channel.CompleteAsync();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Canal já encerrado pelo plano de controle.
        }

        await IgnoreAsync(output);
        await IgnoreAsync(input);
    }

    /// <summary>Encerra o shell: SIGHUP pelo PID e, se ainda rodar após o prazo, SIGKILL (D-007).</summary>
    private async Task TerminateAsync(ActiveExec exec, CancellationToken cancellationToken)
    {
        try
        {
            await engine.SignalContainerProcessAsync(exec.ContainerId, exec.Pid, "HUP", cancellationToken);
            var deadline = timeProvider.GetUtcNow() + VerifyTimeout;
            while (timeProvider.GetUtcNow() < deadline)
            {
                var state = await SafeInspectAsync(exec.EngineExecId, cancellationToken);
                if (state is null || !state.Running) return;
                await Task.Delay(200, cancellationToken);
            }

            await engine.SignalContainerProcessAsync(exec.ContainerId, exec.Pid, "KILL", cancellationToken);
            await Task.Delay(300, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidOperationException or ArgumentException)
        {
        }
    }

    private async Task<EngineExecState?> SafeInspectAsync(string execId, CancellationToken cancellationToken)
    {
        try
        {
            return await engine.InspectExecAsync(execId, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException)
        {
            return new EngineExecState(true, null);
        }
    }

    private async Task<bool> StillRunningAsync(string containerId, CancellationToken cancellationToken)
    {
        try
        {
            return Describe(await engine.GetContainerJsonAsync(containerId, cancellationToken)).Running;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException)
        {
            return false;
        }
    }

    private static async Task<(int Pid, string Shell, byte[] Pending)> ReadBannerAsync(EngineExecSession exec, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        var text = new StringBuilder();
        var buffer = new byte[4096];
        try
        {
            while (text.Length < 8192)
            {
                var read = await exec.Stream.ReadAsync(buffer, timeout.Token);
                if (read == 0) break;
                text.Append(Encoding.UTF8.GetString(buffer, 0, read));
                if (ShellLauncher.TryParse(text.ToString(), out var pid, out var shell, out var remainder))
                {
                    return (pid, shell, Encoding.UTF8.GetBytes(remainder));
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }

        return (0, string.Empty, []);
    }

    /// <summary>Saída do TTY em partes ≤ 32 KiB, limitada a `bytesPerSecond` (token bucket simples).</summary>
    private async Task PumpOutputAsync(EngineExecSession exec, IExecChannel channel, byte[] pending, int bytesPerSecond, CancellationToken cancellationToken)
    {
        if (pending.Length > 0) await channel.SendAsync(new ExecFrame { Output = ByteString.CopyFrom(pending) }, cancellationToken);
        var buffer = new byte[MaxChunkBytes];
        var windowStart = timeProvider.GetUtcNow();
        var sent = 0;
        try
        {
            while (true)
            {
                var read = await exec.Stream.ReadAsync(buffer.AsMemory(0, Math.Min(MaxChunkBytes, bytesPerSecond)), cancellationToken);
                if (read == 0) return;
                var elapsed = timeProvider.GetUtcNow() - windowStart;
                if (elapsed >= TimeSpan.FromSeconds(1))
                {
                    windowStart = timeProvider.GetUtcNow();
                    sent = 0;
                }
                else if (sent + read > bytesPerSecond)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1) - elapsed, cancellationToken);
                    windowStart = timeProvider.GetUtcNow();
                    sent = 0;
                }

                sent += read;
                await channel.SendAsync(new ExecFrame { Output = ByteString.CopyFrom(buffer, 0, read) }, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
        }
    }

    /// <summary>Entrada e controle vindos do plano de controle; devolve o motivo do término pedido pelo servidor.</summary>
    private async Task<string> PumpInputAsync(EngineExecSession exec, IExecChannel channel, CancellationToken cancellationToken)
    {
        await foreach (var frame in channel.ReadAllAsync(cancellationToken))
        {
            switch (frame.FrameCase)
            {
                case ExecFrame.FrameOneofCase.Stdin when frame.Stdin.Length is > 0 and <= MaxInputBytes:
                    await exec.Stream.WriteAsync(frame.Stdin.Memory, cancellationToken);
                    await exec.Stream.FlushAsync(cancellationToken);
                    break;
                case ExecFrame.FrameOneofCase.Resize:
                    await engine.ResizeExecAsync(exec.ExecId, (int)Math.Clamp(frame.Resize.Cols, 1, 1000), (int)Math.Clamp(frame.Resize.Rows, 1, 500), cancellationToken);
                    break;
                case ExecFrame.FrameOneofCase.Status when frame.Status.State == ExecState.Closed:
                    return string.IsNullOrEmpty(frame.Status.Reason) ? "user_closed" : frame.Status.Reason[..Math.Min(64, frame.Status.Reason.Length)];
            }
        }

        return "client_disconnected";
    }

    private static (bool Running, bool Disabled, bool Root) Describe(string? json)
    {
        if (json is null) return (false, false, false);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var running = root.TryGetProperty("State", out var state) && state.TryGetProperty("Running", out var value) && value.ValueKind == JsonValueKind.True;
        var config = root.TryGetProperty("Config", out var c) ? c : default;
        var disabled = config.ValueKind == JsonValueKind.Object && config.TryGetProperty("Labels", out var labels) && labels.ValueKind == JsonValueKind.Object &&
            labels.TryGetProperty("dokpod.exec", out var label) && string.Equals(label.GetString(), "disabled", StringComparison.OrdinalIgnoreCase);
        var user = config.ValueKind == JsonValueKind.Object && config.TryGetProperty("User", out var u) ? u.GetString() ?? string.Empty : string.Empty;
        var isRoot = user is "" or "root" or "0" || user.StartsWith("root:", StringComparison.Ordinal) || user.StartsWith("0:", StringComparison.Ordinal);
        return (running, disabled, isRoot);
    }

    private static Task SendStatusAsync(IExecChannel channel, ExecState state, string reason, CancellationToken cancellationToken) =>
        channel.SendAsync(new ExecFrame { Status = new ExecStatus { State = state, Reason = reason } }, cancellationToken);

    private static async Task IgnoreAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException or ObjectDisposedException or Grpc.Core.RpcException)
        {
        }
    }
}
