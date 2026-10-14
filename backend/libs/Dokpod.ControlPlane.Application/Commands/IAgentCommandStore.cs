using Dokpod.Domain.Commands;
using Dokpod.Domain.Auditing;

namespace Dokpod.ControlPlane.Application.Commands;

public enum ControlPlaneCommandState
{
    Pending = 0,
    Dispatched = 1,
    Accepted = 2,
    Succeeded = 3,
    Failed = 4,
    Indeterminate = 5,
}

public sealed record PersistedAgentCommand(
    AgentCommand Command,
    ControlPlaneCommandState State,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record AgentCommandStatusSnapshot(
    Guid EnvironmentId,
    Guid CommandId,
    AgentCommandKind Kind,
    string ContainerId,
    string ExpectedContainerRevision,
    ControlPlaneCommandState State,
    string? FailureCode,
    string? ObservedContainerRevision,
    DateTimeOffset DeadlineUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? CompletedAtUtc)
{
    /// <summary>Digest e tamanho do pull (spec 012); nulo para comandos de container.</summary>
    public AgentCommandOutput? Output { get; init; }

    /// <summary>Referência canônica da imagem do pull (012) ou da criação (013).</summary>
    public string? ImageReference { get; init; }

    /// <summary>Nome do container pedido na criação (spec 013).</summary>
    public string? ContainerName { get; init; }
}

public enum AgentCommandEnqueueResult
{
    Created,
    Duplicate,
    ConflictingPayload,
}

public sealed record AgentCommandStatusUpdate(
    Guid EnvironmentId,
    Guid CommandId,
    ControlPlaneCommandState State,
    string? FailureCode,
    string? ObservedContainerRevision,
    DateTimeOffset UpdatedAtUtc)
{
    public AgentCommandOutput? Output { get; init; }
}

public enum AgentCommandStatusUpdateResult
{
    Applied,
    Duplicate,
    NotFound,
    InvalidTransition,
}

public interface IAgentCommandStore
{
    Task<AgentCommandStatusSnapshot?> GetAsync(
        Guid environmentId,
        Guid commandId,
        CancellationToken cancellationToken);

    Task<AgentCommandEnqueueResult> EnqueueAsync(
        PersistedAgentCommand command,
        CancellationToken cancellationToken);

    Task<AgentCommandEnqueueResult> EnqueueAuditedAsync(
        PersistedAgentCommand command,
        AuditEvent auditEvent,
        CancellationToken cancellationToken);

    Task<AgentCommandStatusUpdateResult> ApplyStatusAsync(
        AgentCommandStatusUpdate update,
        CancellationToken cancellationToken);

    Task<AgentCommandStatusUpdateResult> ApplyStatusAuditedAsync(
        AgentCommandStatusUpdate update,
        AuditEvent auditEvent,
        CancellationToken cancellationToken);

    Task<int> ExpireNonTerminalAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);

    /// <summary>Remove comandos concluídos ou com falha (e seus parâmetros) criados antes do corte; nunca pendentes nem indeterminados (012 D-008).</summary>
    Task<int> PurgeTerminalAsync(DateTimeOffset cutoffUtc, int batchSize, CancellationToken cancellationToken) =>
        Task.FromResult(0);

    /// <summary>Zera valores protegidos de comandos terminais (spec 013 FR-004).</summary>
    Task<int> PurgeProtectedValuesAsync(CancellationToken cancellationToken) => Task.FromResult(0);

    Task<IReadOnlyList<PersistedAgentCommand>> ClaimDispatchableAsync(
        Guid environmentId,
        long activeFencingToken,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
}