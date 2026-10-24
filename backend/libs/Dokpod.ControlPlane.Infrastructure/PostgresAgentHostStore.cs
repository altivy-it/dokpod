using System.Text.Json;
using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Dokpod.ControlPlane.Infrastructure;

public sealed class PostgresAgentHostStore(ControlPlaneDbContext dbContext)
    : IAgentHostStore, IInventoryStatisticsStore, IEnvironmentStageStore
{
    public async Task UpsertDescriptorAsync(AgentDescriptor descriptor, CancellationToken cancellationToken)
    {
        var entity = await dbContext.AgentDescriptors
            .SingleOrDefaultAsync(item => item.EnvironmentId == descriptor.EnvironmentId, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            entity = new AgentDescriptorEntity { EnvironmentId = descriptor.EnvironmentId };
            dbContext.AgentDescriptors.Add(entity);
        }

        entity.EngineKind = descriptor.EngineKind;
        entity.EngineVersion = Truncate(descriptor.EngineVersion, 64);
        entity.EngineApiVersion = Truncate(descriptor.EngineApiVersion, 32);
        entity.EngineRootless = descriptor.EngineRootless;
        entity.OperatingSystem = descriptor.OperatingSystem;
        entity.OperatingSystemVersion = Truncate(descriptor.OperatingSystemVersion, 128);
        entity.Architecture = descriptor.Architecture;
        entity.AgentVersion = Truncate(descriptor.AgentVersion, 64);
        entity.Capabilities = Truncate(JsonSerializer.Serialize(descriptor.Capabilities), 1024);
        entity.HostName = Truncate(descriptor.HostName, 255);
        entity.CpuCount = descriptor.CpuCount is > 0 ? descriptor.CpuCount : null;
        entity.MemoryBytes = descriptor.MemoryBytes is >= 0 ? descriptor.MemoryBytes : null;
        entity.UpdatedAtUtc = descriptor.UpdatedAtUtc;
        await SaveOrDiscardAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool?> SetConnectionAsync(Guid environmentId, bool connected, DateTimeOffset atUtc, CancellationToken cancellationToken)
    {
        var entity = await dbContext.AgentConnections
            .SingleOrDefaultAsync(item => item.EnvironmentId == environmentId, cancellationToken)
            .ConfigureAwait(false);
        bool? previous = entity?.Connected;
        if (entity is null)
        {
            entity = new AgentConnectionEntity { EnvironmentId = environmentId };
            dbContext.AgentConnections.Add(entity);
        }

        entity.Connected = connected;
        if (connected)
        {
            entity.SessionStartedAtUtc = atUtc;
            entity.LastSeenAtUtc = atUtc;
        }
        else
        {
            entity.DisconnectedAtUtc = atUtc;
        }

        await SaveOrDiscardAsync(cancellationToken).ConfigureAwait(false);
        return previous;
    }

    // Spec 005 T016: uma gravação que falhou não deixa entidades pendentes no contexto da sessão do agente,
    // para que a nova tentativa do registro da sessão parta do estado do banco.
    private async Task SaveOrDiscardAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task TouchLastSeenAsync(Guid environmentId, DateTimeOffset atUtc, CancellationToken cancellationToken)
    {
        await dbContext.AgentConnections
            .Where(item => item.EnvironmentId == environmentId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.LastSeenAtUtc, atUtc), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<Guid, AgentDescriptor>> GetDescriptorsAsync(
        IReadOnlyCollection<Guid> environmentIds,
        CancellationToken cancellationToken)
    {
        var ids = environmentIds.ToArray();
        var entities = await dbContext.AgentDescriptors.AsNoTracking()
            .Where(item => ids.Contains(item.EnvironmentId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return entities.ToDictionary(item => item.EnvironmentId, item => new AgentDescriptor(
            item.EnvironmentId,
            item.EngineKind,
            item.EngineVersion,
            item.EngineApiVersion,
            item.EngineRootless,
            item.OperatingSystem,
            item.OperatingSystemVersion,
            item.Architecture,
            item.AgentVersion,
            JsonSerializer.Deserialize<string[]>(item.Capabilities) ?? [],
            item.HostName,
            item.CpuCount,
            item.MemoryBytes,
            item.UpdatedAtUtc));
    }

    public async Task<IReadOnlyDictionary<Guid, AgentConnectionState>> GetConnectionsAsync(
        IReadOnlyCollection<Guid> environmentIds,
        CancellationToken cancellationToken)
    {
        var ids = environmentIds.ToArray();
        var entities = await dbContext.AgentConnections.AsNoTracking()
            .Where(item => ids.Contains(item.EnvironmentId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return entities.ToDictionary(item => item.EnvironmentId, item => new AgentConnectionState(
            item.EnvironmentId, item.Connected, item.SessionStartedAtUtc, item.LastSeenAtUtc, item.DisconnectedAtUtc));
    }

    public async Task<IReadOnlyList<Guid>> ListConnectedAsync(CancellationToken cancellationToken) =>
        await dbContext.AgentConnections.AsNoTracking()
            .Where(item => item.Connected)
            .Select(item => item.EnvironmentId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<Guid, InventoryStatistics>> GetStatisticsAsync(
        IReadOnlyCollection<Guid> environmentIds,
        CancellationToken cancellationToken)
    {
        var ids = environmentIds.ToArray();
        var counts = await dbContext.InventoryContainers.AsNoTracking()
            .Where(item => ids.Contains(item.EnvironmentId))
            .GroupBy(item => new { item.EnvironmentId, item.DisplayStatus })
            .Select(group => new { group.Key.EnvironmentId, group.Key.DisplayStatus, Count = group.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var projections = await dbContext.InventoryProjections.AsNoTracking()
            .Where(item => ids.Contains(item.EnvironmentId))
            .Select(item => new { item.EnvironmentId, item.ObservedAtUtc, item.Revision })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var result = new Dictionary<Guid, InventoryStatistics>();
        foreach (var projection in projections)
        {
            var byStatus = Enum.GetValues<ContainerDisplayStatus>()
                .ToDictionary(ContainerDisplayStatuses.ToValue, _ => 0);
            foreach (var count in counts.Where(c => c.EnvironmentId == projection.EnvironmentId))
            {
                if (byStatus.ContainsKey(count.DisplayStatus)) byStatus[count.DisplayStatus] = count.Count;
            }

            result[projection.EnvironmentId] = new InventoryStatistics(
                projection.EnvironmentId, byStatus, projection.ObservedAtUtc, checked((ulong)projection.Revision));
        }

        return result;
    }

    public async Task<(bool Found, EnvironmentStage? Previous)> SetStageAsync(
        Guid environmentId,
        EnvironmentStage? stage,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.EnvironmentRegistrations
            .SingleOrDefaultAsync(item => item.EnvironmentId == environmentId, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null) return (false, null);
        var previous = EnvironmentStages.ParseNullable(entity.Stage);
        entity.Stage = stage is null ? null : EnvironmentStages.ToValue(stage.Value);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (true, previous);
    }

    private static string Truncate(string? value, int length) =>
        string.IsNullOrEmpty(value) ? string.Empty : value.Length <= length ? value : value[..length];
}
