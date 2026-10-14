using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Dokpod.Agent.Application.Engines;
using Dokpod.Agent.Contracts.V1;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace Dokpod.Agent.Application.Queries;

/// <summary>
/// Consultas sob demanda (spec 011): resumo, inspeção e trecho de logs, sem persistência,
/// sanitizados no agente e enviados em partes de até 256 KiB.
/// </summary>
public sealed class ContainerQueryHandler(IContainerEngine engine, TimeProvider timeProvider)
{
    public const int MaxMessageBytes = 256 * 1024;
    public const int MaxConcurrentQueries = 4;
    public const int DefaultTail = 500;
    public const int MaxTail = 5000;
    public const int MaxResponseBytes = 1024 * 1024;
    private const int MaxContains = 128;

    private readonly SemaphoreSlim concurrency = new(MaxConcurrentQueries, MaxConcurrentQueries);

    public async Task HandleAsync(ContainerQueryRequest request, Func<AgentMessage, Task> send, CancellationToken cancellationToken)
    {
        if (!await concurrency.WaitAsync(0, cancellationToken))
        {
            await send(Failure(request.QueryId, QueryResultState.Failed, "too_many_requests"));
            return;
        }

        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (request.Deadline is not null)
            {
                var remaining = request.Deadline.ToDateTimeOffset() - timeProvider.GetUtcNow();
                deadline.CancelAfter(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero);
            }

            if (!IsValidContainerId(request.ContainerId))
            {
                await send(Failure(request.QueryId, QueryResultState.Failed, "invalid_container"));
                return;
            }

            switch (request.Kind)
            {
                case ContainerQueryKind.Summary:
                case ContainerQueryKind.Inspect:
                    await InspectAsync(request, send, deadline.Token);
                    break;
                case ContainerQueryKind.Logs:
                    await LogsAsync(request, send, deadline.Token);
                    break;
                default:
                    await send(Failure(request.QueryId, QueryResultState.NotSupported, "query_kind_not_supported"));
                    break;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await send(Failure(request.QueryId, QueryResultState.Failed, "agent_timeout"));
        }
        catch (KeyNotFoundException)
        {
            await send(Failure(request.QueryId, QueryResultState.NotFound, "container_not_found"));
        }
        catch (NotSupportedException)
        {
            await send(Failure(request.QueryId, QueryResultState.NotSupported, "log_driver_unreadable"));
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException or InvalidDataException)
        {
            await send(Failure(request.QueryId, QueryResultState.Failed, "engine_unavailable"));
        }
        finally
        {
            concurrency.Release();
        }
    }

    private async Task InspectAsync(ContainerQueryRequest request, Func<AgentMessage, Task> send, CancellationToken cancellationToken)
    {
        var json = await engine.GetContainerJsonAsync(request.ContainerId, cancellationToken);
        if (json is null)
        {
            await send(Failure(request.QueryId, QueryResultState.NotFound, "container_not_found"));
            return;
        }

        using var document = JsonDocument.Parse(json);
        var sanitized = request.Kind == ContainerQueryKind.Summary
            ? ContainerInspectSanitizer.Summary(document.RootElement)
            : ContainerInspectSanitizer.Inspect(document.RootElement);
        var response = new ContainerQueryResponse
        {
            QueryId = request.QueryId,
            State = QueryResultState.Ok,
            SanitizedJson = ByteString.CopyFrom(sanitized.Json),
            Truncated = sanitized.Truncated,
            Final = true,
        };
        response.RedactedPaths.AddRange(sanitized.RedactedPaths.Take(500));
        await send(new AgentMessage { ContainerQueryResponse = response });
    }

    private async Task LogsAsync(ContainerQueryRequest request, Func<AgentMessage, Task> send, CancellationToken cancellationToken)
    {
        var query = request.Logs ?? new LogQuery();
        var tail = query.Tail == 0 ? DefaultTail : (int)Math.Min(query.Tail, MaxTail);
        var maxBytes = query.MaxBytes == 0 ? MaxResponseBytes : (int)Math.Min(query.MaxBytes, MaxResponseBytes);
        var contains = string.IsNullOrEmpty(query.Contains) ? null : query.Contains.Length > MaxContains ? query.Contains[..MaxContains] : query.Contains;
        var engineRequest = new EngineLogRequest(
            contains is null ? tail : MaxTail,
            query.Since?.ToDateTimeOffset(),
            query.Until?.ToDateTimeOffset(),
            Follow: false);

        var lines = new List<LogLine>();
        var total = 0;
        var truncated = false;
        await foreach (var raw in engine.ReadLogsAsync(request.ContainerId, engineRequest, cancellationToken))
        {
            var line = ToProto(raw);
            if (contains is not null && line.Text.IndexOf(contains, StringComparison.OrdinalIgnoreCase) < 0) continue;
            lines.Add(line);
        }

        // Mantém as últimas N linhas (após o filtro) dentro do limite de bytes da resposta.
        if (lines.Count > tail)
        {
            lines.RemoveRange(0, lines.Count - tail);
            truncated = contains is not null;
        }

        var kept = new List<LogLine>(lines.Count);
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            total += lines[i].CalculateSize();
            if (total > maxBytes)
            {
                truncated = true;
                break;
            }

            kept.Add(lines[i]);
        }

        kept.Reverse();
        uint sequence = 0;
        var part = new ContainerQueryResponse { QueryId = request.QueryId, State = QueryResultState.Ok, Sequence = sequence };
        var partSize = 0;
        foreach (var line in kept)
        {
            var size = line.CalculateSize() + 8;
            if (partSize + size > MaxMessageBytes - 1024 && part.Lines.Count > 0)
            {
                await send(new AgentMessage { ContainerQueryResponse = part });
                part = new ContainerQueryResponse { QueryId = request.QueryId, State = QueryResultState.Ok, Sequence = ++sequence };
                partSize = 0;
            }

            part.Lines.Add(line);
            partSize += size;
        }

        part.Truncated = truncated;
        part.Final = true;
        await send(new AgentMessage { ContainerQueryResponse = part });
    }

    public static LogLine ToProto(EngineLogLine raw)
    {
        var parsed = LogLineParser.Parse(raw.Raw, hasTimestamp: true);
        var line = new LogLine
        {
            Stream = raw.Stream == 2 ? LogStream.Stderr : LogStream.Stdout,
            Text = parsed.Text,
            Level = parsed.Level,
        };
        if (parsed.Timestamp is not null) line.Timestamp = Timestamp.FromDateTimeOffset(parsed.Timestamp.Value);
        return line;
    }

    public static bool IsValidContainerId(string id) => id.Length == 64 && id.All(Uri.IsHexDigit);

    private static AgentMessage Failure(string queryId, QueryResultState state, string code) =>
        new() { ContainerQueryResponse = new ContainerQueryResponse { QueryId = queryId, State = state, FailureCode = code, Final = true } };
}

/// <summary>
/// Sessões de logs ao vivo (spec 011 FR-005): ≤ 10 por agente, taxa de linhas e bytes limitada,
/// expiração no horário informado pelo servidor e encerramento por STOP ou perda do stream.
/// </summary>
public sealed class LogStreamManager(IContainerEngine engine, TimeProvider timeProvider)
{
    public const int MaxSessions = 10;
    private const int DefaultLinesPerSecond = 200;
    private const int DefaultBytesPerSecond = 256 * 1024;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> sessions = new(StringComparer.Ordinal);

    public int ActiveSessions => sessions.Count;

    public async Task HandleAsync(LogStreamControl control, Func<AgentMessage, Task> send, CancellationToken sessionToken)
    {
        if (control.Action == LogStreamAction.Stop)
        {
            if (sessions.TryRemove(control.SessionId, out var running)) await running.CancelAsync();
            return;
        }

        if (control.Action != LogStreamAction.Start || string.IsNullOrWhiteSpace(control.SessionId) || control.SessionId.Length > 64)
        {
            return;
        }

        if (!ContainerQueryHandler.IsValidContainerId(control.ContainerId))
        {
            await send(Ended(control.SessionId, "invalid_container"));
            return;
        }

        if (sessions.Count >= MaxSessions)
        {
            await send(Ended(control.SessionId, "too_many_sessions"));
            return;
        }

        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(sessionToken);
        if (!sessions.TryAdd(control.SessionId, cancellation))
        {
            cancellation.Dispose();
            return;
        }

        if (control.ExpiresAt is not null)
        {
            var remaining = control.ExpiresAt.ToDateTimeOffset() - timeProvider.GetUtcNow();
            cancellation.CancelAfter(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero);
        }

        _ = Task.Run(() => RunAsync(control, send, cancellation, sessionToken), CancellationToken.None);
    }

    private async Task RunAsync(LogStreamControl control, Func<AgentMessage, Task> send, CancellationTokenSource cancellation, CancellationToken sessionToken)
    {
        var maxLines = control.MaxLinesPerSecond is > 0 and <= 1000 ? (int)control.MaxLinesPerSecond : DefaultLinesPerSecond;
        var maxBytes = control.MaxBytesPerSecond is > 0 and <= 1024 * 1024 ? (int)control.MaxBytesPerSecond : DefaultBytesPerSecond;
        var contains = string.IsNullOrEmpty(control.Contains) ? null : control.Contains.Length > 128 ? control.Contains[..128] : control.Contains;
        var reason = "stopped";
        var chunk = new LogChunk { SessionId = control.SessionId };
        var chunkBytes = 0;
        var windowStart = timeProvider.GetUtcNow();
        var windowLines = 0;
        var windowBytes = 0;
        var lastFlush = timeProvider.GetUtcNow();
        try
        {
            var request = new EngineLogRequest(0, timeProvider.GetUtcNow().AddSeconds(-1), null, Follow: true);
            await foreach (var raw in engine.ReadLogsAsync(control.ContainerId, request, cancellation.Token))
            {
                var line = ContainerQueryHandler.ToProto(raw);
                if (contains is not null && line.Text.IndexOf(contains, StringComparison.OrdinalIgnoreCase) < 0) continue;
                var now = timeProvider.GetUtcNow();
                if (now - windowStart >= TimeSpan.FromSeconds(1))
                {
                    windowStart = now;
                    windowLines = 0;
                    windowBytes = 0;
                }

                var size = line.CalculateSize();
                if (windowLines >= maxLines || windowBytes + size > maxBytes)
                {
                    chunk.OmittedLines++;
                    continue;
                }

                windowLines++;
                windowBytes += size;
                chunk.Lines.Add(line);
                chunkBytes += size + 8;
                if (chunkBytes >= ContainerQueryHandler.MaxMessageBytes - 4096 || now - lastFlush >= TimeSpan.FromMilliseconds(500))
                {
                    await send(new AgentMessage { LogChunk = chunk });
                    chunk = new LogChunk { SessionId = control.SessionId };
                    chunkBytes = 0;
                    lastFlush = now;
                }
            }

            reason = "container_stopped";
        }
        catch (OperationCanceledException)
        {
            reason = sessionToken.IsCancellationRequested ? "agent_disconnected" : sessions.ContainsKey(control.SessionId) ? "expired" : "stopped";
        }
        catch (KeyNotFoundException)
        {
            reason = "container_not_found";
        }
        catch (NotSupportedException)
        {
            reason = "log_driver_unreadable";
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            reason = "engine_unavailable";
        }
        finally
        {
            sessions.TryRemove(control.SessionId, out _);
            cancellation.Dispose();
        }

        if (!sessionToken.IsCancellationRequested)
        {
            chunk.Ended = true;
            chunk.EndReason = reason;
            try
            {
                await send(new AgentMessage { LogChunk = chunk });
            }
            catch (Exception exception) when (exception is OperationCanceledException or InvalidOperationException)
            {
            }
        }
    }

    private static AgentMessage Ended(string sessionId, string reason) =>
        new() { LogChunk = new LogChunk { SessionId = sessionId, Ended = true, EndReason = reason } };
}
