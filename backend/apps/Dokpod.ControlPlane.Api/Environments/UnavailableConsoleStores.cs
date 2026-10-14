using Dokpod.ControlPlane.Application.Events;
using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.ControlPlane.Application.Inventory;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Events;

namespace Dokpod.ControlPlane.Api.Endpoints;

/// <summary>Sem PostgreSQL configurado: leitura vazia e escrita indisponível.</summary>
public sealed class UnavailableAgentHostStore : IAgentHostStore, IInventoryStatisticsStore, IEnvironmentStageStore
{
    public Task UpsertDescriptorAsync(AgentDescriptor descriptor, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<bool?> SetConnectionAsync(Guid environmentId, bool connected, DateTimeOffset atUtc, CancellationToken cancellationToken) =>
        Task.FromResult<bool?>(null);

    public Task TouchLastSeenAsync(Guid environmentId, DateTimeOffset atUtc, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyDictionary<Guid, AgentDescriptor>> GetDescriptorsAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, AgentDescriptor>>(new Dictionary<Guid, AgentDescriptor>());

    public Task<IReadOnlyDictionary<Guid, AgentConnectionState>> GetConnectionsAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, AgentConnectionState>>(new Dictionary<Guid, AgentConnectionState>());

    public Task<IReadOnlyList<Guid>> ListConnectedAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Guid>>([]);

    public Task<IReadOnlyDictionary<Guid, InventoryStatistics>> GetStatisticsAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, InventoryStatistics>>(new Dictionary<Guid, InventoryStatistics>());

    public Task<(bool Found, EnvironmentStage? Previous)> SetStageAsync(Guid environmentId, EnvironmentStage? stage, CancellationToken cancellationToken) =>
        Task.FromException<(bool, EnvironmentStage?)>(new InvalidOperationException("Control Plane persistence is unavailable."));
}

public sealed class UnavailableOperationalEventStore : IOperationalEventStore
{
    public Task<int> AppendAsync(IReadOnlyCollection<OperationalEvent> events, CancellationToken cancellationToken) => Task.FromResult(0);

    public Task<OperationalEventPage> ListAsync(OperationalEventQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(new OperationalEventPage([], null));

    public Task<int> CountAttentionAsync(IReadOnlyCollection<Guid> environmentIds, DateTimeOffset sinceUtc, CancellationToken cancellationToken) =>
        Task.FromResult(0);

    public Task<long> ApplyRetentionAsync(TimeSpan retention, CancellationToken cancellationToken) => Task.FromResult(0L);
}

public sealed class UnavailableContainerSearchStore : IContainerSearchStore
{
    public Task<ContainerSearchResult> SearchAsync(ContainerSearchQuery query, CancellationToken cancellationToken) =>
        Task.FromException<ContainerSearchResult>(new InvalidOperationException("Control Plane persistence is unavailable."));

    public Task<Dokpod.Domain.Inventory.ContainerInventory?> GetAsync(Guid environmentId, string containerId, CancellationToken cancellationToken) =>
        Task.FromException<Dokpod.Domain.Inventory.ContainerInventory?>(new InvalidOperationException("Control Plane persistence is unavailable."));
}
