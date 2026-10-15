using System.Collections.Concurrent;
using System.Threading.Channels;
using Dokpod.Agent.Contracts.V1;
using Google.Protobuf.WellKnownTypes;

namespace Dokpod.ControlPlane.Application.Queries;

public enum QueryOutcomeKind
{
    Ok,
    NotFound,
    NotSupported,
    AgentDisconnected,
    Timeout,
    TooManyRequests,
    Failed,
}

public sealed record QueryOutcome(
    QueryOutcomeKind Kind,
    string? FailureCode,
    byte[] Json,
    IReadOnlyList<LogLine> Lines,
    bool Truncated,
    IReadOnlyList<string> RedactedPaths);

public sealed record LogStreamHandle(string SessionId, ChannelReader<LogChunk> Chunks);

public enum LogStreamStartOutcome
{
    Started,
    AgentDisconnected,
    NotSupported,
    TooManySessions,
}

/// <summary>
/// Consultas sob demanda e sessões de logs ao vivo pelo stream do agente (spec 011 D-001/D-002).
/// Mantém a correlação em memória da instância que detém a sessão; nada é persistido.
/// </summary>
public sealed class AgentQueryDispatcher(TimeProvider timeProvider)
{
    public const int MaxConcurrentPerAgent = 4;
    public const int MaxLiveSessionsPerAgent = 10;
    public const int MaxLiveSessionsPerUser = 3;
    public const int MaxResponseBytes = 1024 * 1024;
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<Guid, AgentChannel> agents = new();
    private readonly ConcurrentDictionary<string, PendingQuery> pending = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, LiveSession> live = new(StringComparer.Ordinal);

    private sealed class AgentChannel(Guid sessionId, IReadOnlySet<Capability> capabilities, ChannelWriter<ControlPlaneMessage> writer)
    {
        public Guid SessionId { get; } = sessionId;
        public IReadOnlySet<Capability> Capabilities { get; } = capabilities;
        public ChannelWriter<ControlPlaneMessage> Writer { get; } = writer;
        public SemaphoreSlim Concurrency { get; } = new(MaxConcurrentPerAgent, MaxConcurrentPerAgent);
    }

    private sealed class PendingQuery(Guid environmentId)
    {
        public Guid EnvironmentId { get; } = environmentId;
        public TaskCompletionSource<QueryOutcome> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<LogLine> Lines { get; } = [];
        public List<byte> Json { get; } = [];
        public List<string> Redacted { get; } = [];
        public int Bytes;
        public bool Truncated;
        public uint NextSequence;
    }

    private sealed record LiveSession(Guid EnvironmentId, string User, Channel<LogChunk> Chunks);

    public IDisposable Register(Guid environmentId, Guid sessionId, IEnumerable<Capability> capabilities, ChannelWriter<ControlPlaneMessage> writer)
    {
        var channel = new AgentChannel(sessionId, capabilities.ToHashSet(), writer);
        agents[environmentId] = channel;
        return new Registration(() =>
        {
            if (agents.TryGetValue(environmentId, out var current) && current.SessionId == sessionId)
            {
                agents.TryRemove(environmentId, out _);
                FailEnvironment(environmentId);
            }
        });
    }

    public bool Supports(Guid environmentId, Capability capability) =>
        agents.TryGetValue(environmentId, out var channel) && channel.Capabilities.Contains(capability);

    public bool IsConnected(Guid environmentId) => agents.ContainsKey(environmentId);

    /// <summary>Envia uma mensagem do servidor pelo stream `Connect` do agente (spec 014: `ExecOpenRequest`).</summary>
    public async Task<bool> TrySendAsync(Guid environmentId, ControlPlaneMessage message, Capability required, CancellationToken cancellationToken)
    {
        if (!agents.TryGetValue(environmentId, out var channel) || !channel.Capabilities.Contains(required)) return false;
        try
        {
            await channel.Writer.WriteAsync(message, cancellationToken);
            return true;
        }
        catch (ChannelClosedException)
        {
            return false;
        }
    }

    public async Task<QueryOutcome> QueryAsync(
        Guid environmentId,
        ContainerQueryKind kind,
        string containerId,
        LogQuery? logs,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null)
    {
        if (!agents.TryGetValue(environmentId, out var channel)) return Outcome(QueryOutcomeKind.AgentDisconnected, "agent_disconnected");
        var capability = kind switch
        {
            ContainerQueryKind.Logs => Capability.ContainerLogs,
            ContainerQueryKind.HostSecrets or ContainerQueryKind.HostNetworks or ContainerQueryKind.HostVolumes => Capability.ContainerCreate,
            ContainerQueryKind.VolumeUsage => Capability.VolumeInventory,
            _ => Capability.ContainerInspect,
        };
        if (!channel.Capabilities.Contains(capability)) return Outcome(QueryOutcomeKind.NotSupported, "not_supported");
        if (!await channel.Concurrency.WaitAsync(0, cancellationToken)) return Outcome(QueryOutcomeKind.TooManyRequests, "too_many_requests");
        var queryId = Guid.NewGuid().ToString("N");
        var entry = new PendingQuery(environmentId);
        pending[queryId] = entry;
        try
        {
            var limit = timeout ?? DefaultTimeout;
            var request = new ContainerQueryRequest
            {
                QueryId = queryId,
                Kind = kind,
                // Spec 018: em VOLUME_USAGE o alvo é o nome do volume, enviado em `volume_name`.
                ContainerId = kind == ContainerQueryKind.VolumeUsage ? string.Empty : containerId,
                VolumeName = kind == ContainerQueryKind.VolumeUsage ? containerId : string.Empty,
                Deadline = Timestamp.FromDateTimeOffset(timeProvider.GetUtcNow() + limit),
            };
            if (logs is not null) request.Logs = logs;
            await channel.Writer.WriteAsync(new ControlPlaneMessage { ContainerQuery = request }, cancellationToken);
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(limit + TimeSpan.FromSeconds(1));
            try
            {
                return await entry.Completion.Task.WaitAsync(timeoutSource.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Outcome(QueryOutcomeKind.Timeout, "agent_timeout");
            }
        }
        catch (ChannelClosedException)
        {
            return Outcome(QueryOutcomeKind.AgentDisconnected, "agent_disconnected");
        }
        finally
        {
            pending.TryRemove(queryId, out _);
            channel.Concurrency.Release();
        }
    }

    /// <summary>Recebe uma parte da resposta; partes fora de ordem ou de outro ambiente são descartadas.</summary>
    public void OnResponse(Guid environmentId, ContainerQueryResponse response)
    {
        if (!pending.TryGetValue(response.QueryId, out var entry) || entry.EnvironmentId != environmentId) return;
        lock (entry)
        {
            if (response.Sequence != entry.NextSequence)
            {
                entry.Completion.TrySetResult(Outcome(QueryOutcomeKind.Failed, "response_out_of_order"));
                return;
            }

            entry.NextSequence++;
            if (response.State != QueryResultState.Ok)
            {
                entry.Completion.TrySetResult(Outcome(
                    response.State switch
                    {
                        QueryResultState.NotFound => QueryOutcomeKind.NotFound,
                        QueryResultState.NotSupported => QueryOutcomeKind.NotSupported,
                        _ when response.FailureCode == "too_many_requests" => QueryOutcomeKind.TooManyRequests,
                        _ when response.FailureCode == "agent_timeout" => QueryOutcomeKind.Timeout,
                        _ => QueryOutcomeKind.Failed,
                    },
                    string.IsNullOrWhiteSpace(response.FailureCode) ? "query_failed" : response.FailureCode));
                return;
            }

            foreach (var line in response.Lines)
            {
                var size = line.CalculateSize();
                if (entry.Bytes + size > MaxResponseBytes)
                {
                    entry.Truncated = true;
                    break;
                }

                entry.Bytes += size;
                entry.Lines.Add(line);
            }

            if (entry.Json.Count + response.SanitizedJson.Length <= MaxResponseBytes)
            {
                entry.Json.AddRange(response.SanitizedJson);
            }
            else
            {
                entry.Truncated = true;
            }

            entry.Redacted.AddRange(response.RedactedPaths.Take(500));
            entry.Truncated |= response.Truncated;
            if (response.Final)
            {
                entry.Completion.TrySetResult(new QueryOutcome(QueryOutcomeKind.Ok, null, [.. entry.Json], entry.Lines, entry.Truncated, entry.Redacted));
            }
        }
    }

    public async Task<(LogStreamStartOutcome Outcome, LogStreamHandle? Handle)> StartLogStreamAsync(
        Guid environmentId,
        string containerId,
        string? contains,
        string user,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken)
    {
        if (!agents.TryGetValue(environmentId, out var channel)) return (LogStreamStartOutcome.AgentDisconnected, null);
        if (!channel.Capabilities.Contains(Capability.ContainerLogs)) return (LogStreamStartOutcome.NotSupported, null);
        if (live.Values.Count(s => s.EnvironmentId == environmentId) >= MaxLiveSessionsPerAgent ||
            live.Values.Count(s => s.User == user) >= MaxLiveSessionsPerUser)
        {
            return (LogStreamStartOutcome.TooManySessions, null);
        }

        var sessionId = Guid.NewGuid().ToString("N");
        var chunks = Channel.CreateBounded<LogChunk>(new BoundedChannelOptions(32) { FullMode = BoundedChannelFullMode.DropOldest });
        live[sessionId] = new LiveSession(environmentId, user, chunks);
        try
        {
            await channel.Writer.WriteAsync(new ControlPlaneMessage
            {
                LogStreamControl = new LogStreamControl
                {
                    SessionId = sessionId,
                    Action = LogStreamAction.Start,
                    ContainerId = containerId,
                    Contains = contains ?? string.Empty,
                    MaxLinesPerSecond = 200,
                    MaxBytesPerSecond = 256 * 1024,
                    ExpiresAt = Timestamp.FromDateTimeOffset(expiresAtUtc),
                },
            }, cancellationToken);
        }
        catch (ChannelClosedException)
        {
            live.TryRemove(sessionId, out _);
            return (LogStreamStartOutcome.AgentDisconnected, null);
        }

        return (LogStreamStartOutcome.Started, new LogStreamHandle(sessionId, chunks.Reader));
    }

    public void OnLogChunk(Guid environmentId, LogChunk chunk)
    {
        if (!live.TryGetValue(chunk.SessionId, out var session) || session.EnvironmentId != environmentId) return;
        session.Chunks.Writer.TryWrite(chunk);
        if (chunk.Ended && live.TryRemove(chunk.SessionId, out _)) session.Chunks.Writer.TryComplete();
    }

    public async Task StopLogStreamAsync(string sessionId)
    {
        if (!live.TryRemove(sessionId, out var session)) return;
        session.Chunks.Writer.TryComplete();
        if (agents.TryGetValue(session.EnvironmentId, out var channel))
        {
            await channel.Writer.WriteAsync(new ControlPlaneMessage
            {
                LogStreamControl = new LogStreamControl { SessionId = sessionId, Action = LogStreamAction.Stop },
            }).AsTask().ContinueWith(_ => { }, TaskScheduler.Default);
        }
    }

    private void FailEnvironment(Guid environmentId)
    {
        foreach (var entry in pending.Values.Where(p => p.EnvironmentId == environmentId))
        {
            entry.Completion.TrySetResult(Outcome(QueryOutcomeKind.AgentDisconnected, "agent_disconnected"));
        }

        foreach (var (id, session) in live.Where(s => s.Value.EnvironmentId == environmentId).ToArray())
        {
            if (!live.TryRemove(id, out _)) continue;
            session.Chunks.Writer.TryWrite(new LogChunk { SessionId = id, Ended = true, EndReason = "agent_disconnected" });
            session.Chunks.Writer.TryComplete();
        }
    }

    private static QueryOutcome Outcome(QueryOutcomeKind kind, string code) => new(kind, code, [], [], false, []);

    private sealed class Registration(Action dispose) : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0) dispose();
        }
    }
}
