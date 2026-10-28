using System.Data;
using System.Text.Json;
using Dokpod.ControlPlane.Application.Inventory;
using Dokpod.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Dokpod.ControlPlane.Infrastructure;

public sealed class PostgresInventoryProjectionStore(ControlPlaneDbContext dbContext)
    : IInventoryProjectionStore
{
    // Semente do hash do lock consultivo, para não colidir com outros usos de pg_advisory_xact_lock.
    private const long InventoryLockSeed = 0x646F6B696E76; // "dokinv"

    public async Task<InventoryProjectionPage?> GetPageAsync(
        Guid environmentId,
        string? afterContainerId,
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var projection = await dbContext.InventoryProjections
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.EnvironmentId == environmentId, cancellationToken)
            .ConfigureAwait(false);
        if (projection is null)
        {
            return null;
        }

        var query = dbContext.InventoryContainers
            .AsNoTracking()
            .Where(container => container.EnvironmentId == environmentId);
        if (!string.IsNullOrWhiteSpace(afterContainerId))
        {
            query = query.Where(container => string.Compare(container.ContainerId, afterContainerId) > 0);
        }

        var entities = await query
            .OrderBy(container => container.ContainerId)
            .Take(limit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var hasMore = entities.Count > limit;
        var page = entities.Take(limit).Select(ToDomain).ToArray();
        return new InventoryProjectionPage(
            checked((ulong)projection.Revision),
            projection.ObservedAtUtc,
            page,
            hasMore ? page[^1].ContainerId : null);
    }

    public Task<InventoryReconciliationResult> ApplyDeltaAsync(
        Guid environmentId,
        InventoryDelta delta,
        CancellationToken cancellationToken) =>
        RetrySerializableAsync(() => ApplyDeltaOnceAsync(environmentId, delta, cancellationToken), cancellationToken);

    public Task<InventoryReconciliationResult> ReplaceSnapshotAsync(
        InventorySnapshot snapshot,
        CancellationToken cancellationToken) =>
        RetrySerializableAsync(() => ReplaceSnapshotOnceAsync(snapshot, cancellationToken), cancellationToken);

    /// <summary>
    /// Rede de segurança para 40001/40P01: com o lock por ambiente (<see cref="LockEnvironmentAsync"/>) agentes
    /// diferentes não colidem mais, mas a transação inteira continua repetível com recuo curto (spec 008 T016).
    /// </summary>
    private async Task<T> RetrySerializableAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        const int maxAttempts = 10;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation().ConfigureAwait(false);
            }
            catch (Exception exception) when (attempt < maxAttempts && IsSerializationFailure(exception))
            {
                dbContext.ChangeTracker.Clear();
                // Recuo exponencial com jitter (até ~1 s): muitos agentes publicando ao mesmo tempo disputam os mesmos índices.
                var ceiling = Math.Min(1_000, 20 << Math.Min(attempt, 6));
                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(ceiling / 2, ceiling)), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool IsSerializationFailure(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is Npgsql.PostgresException { SqlState: "40001" or "40P01" }) return true;
        }

        return false;
    }

    /// <summary>
    /// Serializa só os escritores do mesmo ambiente, até o fim da transação. Com SERIALIZABLE, a reconexão de
    /// muitos agentes gerava 40001 por conflito de predicado entre ambientes diferentes (spec 005, achado A2).
    /// </summary>
    private Task LockEnvironmentAsync(Guid environmentId, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({environmentId.ToString("D")}, {InventoryLockSeed}))",
            cancellationToken);

    private async Task<InventoryReconciliationResult> ApplyDeltaOnceAsync(
        Guid environmentId,
        InventoryDelta delta,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        await LockEnvironmentAsync(environmentId, cancellationToken).ConfigureAwait(false);

        var projection = await dbContext.InventoryProjections
            .Include(item => item.Containers)
            .SingleOrDefaultAsync(item => item.EnvironmentId == environmentId, cancellationToken)
            .ConfigureAwait(false);

        var current = projection is null
            ? InventorySnapshot.Empty(environmentId, 0)
            : new InventorySnapshot(
                environmentId,
                checked((ulong)projection.Revision),
                projection.Containers.ToDictionary(
                    container => container.ContainerId,
                    ToDomain,
                    StringComparer.Ordinal));

        var result = InventoryReconciler.Apply(current, delta);
        if (result.Outcome != InventoryReconciliationOutcome.Accepted)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }

        var isNewProjection = projection is null;
        projection ??= new InventoryProjectionEntity
        {
            EnvironmentId = environmentId,
        };
        projection.Revision = checked((long)result.Snapshot.Revision);
        projection.ObservedAtUtc = result.Snapshot.Containers.Count == 0
            ? DateTimeOffset.UtcNow
            : result.Snapshot.Containers.Values.Max(container => container.ObservedAtUtc);

        if (isNewProjection)
        {
            dbContext.InventoryProjections.Add(projection);
        }

        var targetContainers = result.Snapshot.Containers;
        foreach (var existing in projection.Containers.ToArray())
        {
            if (!targetContainers.ContainsKey(existing.ContainerId))
            {
                dbContext.InventoryContainers.Remove(existing);
            }
        }

        foreach (var container in targetContainers.Values)
        {
            var entity = projection.Containers.SingleOrDefault(item => item.ContainerId == container.ContainerId);
            if (entity is null)
            {
                projection.Containers.Add(ToEntity(environmentId, container));
                continue;
            }

            ApplyToEntity(entity, container);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    private async Task<InventoryReconciliationResult> ReplaceSnapshotOnceAsync(
        InventorySnapshot snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        await LockEnvironmentAsync(snapshot.EnvironmentId, cancellationToken).ConfigureAwait(false);
        var projection = await dbContext.InventoryProjections
            .Include(item => item.Containers)
            .SingleOrDefaultAsync(item => item.EnvironmentId == snapshot.EnvironmentId, cancellationToken)
            .ConfigureAwait(false);

        if (projection is not null && snapshot.Revision < checked((ulong)projection.Revision))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new InventoryReconciliationResult(
                ToSnapshot(projection),
                InventoryReconciliationOutcome.StaleBase,
                "stale_snapshot");
        }

        var isNewProjection = projection is null;
        projection ??= new InventoryProjectionEntity { EnvironmentId = snapshot.EnvironmentId };
        projection.Revision = checked((long)snapshot.Revision);
        projection.ObservedAtUtc = snapshot.Containers.Count == 0
            ? DateTimeOffset.UtcNow
            : snapshot.Containers.Values.Max(container => container.ObservedAtUtc);
        var existingContainers = projection.Containers.ToDictionary(
            container => container.ContainerId,
            StringComparer.Ordinal);
        foreach (var container in snapshot.Containers.Values)
        {
            if (existingContainers.Remove(container.ContainerId, out var entity))
            {
                ApplyToEntity(entity, container);
            }
            else
            {
                projection.Containers.Add(ToEntity(snapshot.EnvironmentId, container));
            }
        }

        foreach (var removed in existingContainers.Values)
        {
            projection.Containers.Remove(removed);
        }

        if (isNewProjection)
        {
            dbContext.InventoryProjections.Add(projection);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new InventoryReconciliationResult(snapshot, InventoryReconciliationOutcome.Accepted);
    }

    private static InventoryContainerEntity ToEntity(Guid environmentId, ContainerInventory container)
    {
        var entity = new InventoryContainerEntity
        {
            EnvironmentId = environmentId,
            ContainerId = container.ContainerId,
        };
        ApplyToEntity(entity, container);
        return entity;
    }

    private static void ApplyToEntity(InventoryContainerEntity entity, ContainerInventory container)
    {
        entity.Name = container.Name;
        entity.ImageReference = container.ImageReference;
        entity.State = container.State;
        entity.Revision = container.Revision;
        entity.ObservedAtUtc = container.ObservedAtUtc;
        entity.CreatedAtUtc = container.CreatedAtUtc;
        entity.StartedAtUtc = container.StartedAtUtc;
        entity.Health = container.Health switch
        {
            ContainerHealth.None => "none",
            ContainerHealth.Starting => "starting",
            ContainerHealth.Healthy => "healthy",
            ContainerHealth.Unhealthy => "unhealthy",
            _ => null,
        };
        entity.RestartCount = container.RestartCount;
        entity.ExitCode = container.ExitCode;
        entity.OomKilled = container.OomKilled;
        entity.Ports = container.Ports.Count == 0
            ? null
            : JsonSerializer.Serialize(container.Ports.Take(ContainerPortBinding.MaxPerContainer));
        entity.DisplayStatus = ContainerDisplayStatuses.ToValue(container.DisplayStatus);
        entity.ImageId = string.IsNullOrEmpty(container.ImageId) || container.ImageId.Length > 160 ? null : container.ImageId;
    }

    internal static ContainerInventory ToDomain(InventoryContainerEntity container) =>
        new(
            container.ContainerId,
            container.Name,
            container.ImageReference,
            container.State,
            container.Revision,
            container.ObservedAtUtc)
        {
            CreatedAtUtc = container.CreatedAtUtc,
            StartedAtUtc = container.StartedAtUtc,
            Health = container.Health switch
            {
                "none" => ContainerHealth.None,
                "starting" => ContainerHealth.Starting,
                "healthy" => ContainerHealth.Healthy,
                "unhealthy" => ContainerHealth.Unhealthy,
                _ => null,
            },
            RestartCount = container.RestartCount,
            ExitCode = container.ExitCode,
            OomKilled = container.OomKilled,
            Ports = string.IsNullOrEmpty(container.Ports)
                ? []
                : JsonSerializer.Deserialize<ContainerPortBinding[]>(container.Ports) ?? [],
            ImageId = container.ImageId,
        };

    private static InventorySnapshot ToSnapshot(InventoryProjectionEntity projection) =>
        new(
            projection.EnvironmentId,
            checked((ulong)projection.Revision),
            projection.Containers.ToDictionary(
                container => container.ContainerId,
                ToDomain,
                StringComparer.Ordinal));
}