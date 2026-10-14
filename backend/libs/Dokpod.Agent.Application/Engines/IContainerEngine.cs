using Dokpod.Domain.Commands;

namespace Dokpod.Agent.Application.Engines;

public sealed record EngineDescriptor(string EngineVersion, string ApiVersion);

public sealed record EngineContainer(
    string ContainerId,
    string Name,
    string ImageReference,
    string State,
    string Revision)
{
    public DateTimeOffset? CreatedAtUtc { get; init; }
    public DateTimeOffset? StartedAtUtc { get; init; }
    /// <summary>`none`, `starting`, `healthy` ou `unhealthy`; nulo quando desconhecido.</summary>
    public string? Health { get; init; }
    public int? RestartCount { get; init; }
    public int? ExitCode { get; init; }
    public bool? OomKilled { get; init; }
    public IReadOnlyList<EnginePortBinding> Ports { get; init; } = [];
    public double? CpuLimitCores { get; init; }
}

public sealed record EnginePortBinding(string? HostIp, int HostPort, int ContainerPort, string Protocol);

/// <summary>Descritor do host (spec 008): engine detectado, nome, CPUs, memória e modo rootless.</summary>
public sealed record EngineHostInfo(string Engine, string? HostName, int? CpuCount, long? MemoryBytes, bool? Rootless);

/// <summary>Evento bruto do stream `/events` do engine, já reduzido à allowlist de atributos.</summary>
public sealed record EngineEventRecord(
    string Type,
    string Action,
    string SubjectId,
    string? Name,
    string? Image,
    int? ExitCode,
    DateTimeOffset OccurredAtUtc,
    long TimeNano);

public sealed record ContainerMutationResult(bool Succeeded, string? FailureCode);

public interface IContainerEngine
{
    Task<EngineDescriptor> InspectAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<EngineContainer>> ListContainersAsync(CancellationToken cancellationToken);

    Task<EngineContainer?> InspectContainerAsync(string containerId, CancellationToken cancellationToken);

    Task<ContainerMutationResult> ExecuteAsync(
        AgentCommandKind commandKind,
        string containerId,
        CancellationToken cancellationToken);

    Task<EngineHostInfo?> DescribeHostAsync(CancellationToken cancellationToken) =>
        Task.FromResult<EngineHostInfo?>(null);

    IAsyncEnumerable<EngineEventRecord> WatchEventsAsync(CancellationToken cancellationToken) =>
        AsyncEnumerable.Empty<EngineEventRecord>();

    /// <summary>JSON bruto de `/containers/{id}/json` (nulo quando o container não existe). Nunca sai do agente sem sanitização.</summary>
    Task<string?> GetContainerJsonAsync(string containerId, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);

    /// <summary>Linhas de log (com timestamp do engine) de stdout e stderr; `Follow` mantém o stream aberto.</summary>
    IAsyncEnumerable<EngineLogLine> ReadLogsAsync(string containerId, EngineLogRequest request, CancellationToken cancellationToken) =>
        AsyncEnumerable.Empty<EngineLogLine>();

    /// <summary>Contadores brutos de `/containers/{id}/stats?stream=false` (spec 009); nulo se indisponível.</summary>
    Task<EngineContainerStats?> GetContainerStatsAsync(string containerId, CancellationToken cancellationToken) =>
        Task.FromResult<EngineContainerStats?>(null);
}

public sealed record EngineContainerStats(
    ulong CpuTotalNanos,
    ulong SystemCpuNanos,
    int OnlineCpus,
    ulong MemoryUsedBytes,
    ulong? MemoryLimitBytes,
    ulong NetRxBytes,
    ulong NetTxBytes,
    ulong BlockReadBytes,
    ulong BlockWriteBytes);

public sealed record EngineLogRequest(int? Tail, DateTimeOffset? Since, DateTimeOffset? Until, bool Follow);

/// <summary>Stream 1 = stdout, 2 = stderr; `Raw` começa com o timestamp RFC 3339 do engine.</summary>
public sealed record EngineLogLine(int Stream, string Raw);