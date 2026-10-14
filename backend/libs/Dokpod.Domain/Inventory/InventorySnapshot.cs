using System.Collections.ObjectModel;

namespace Dokpod.Domain.Inventory;

public enum InventoryChangeKind
{
    Added,
    Removed,
    Updated,
}

public sealed record ContainerInventory(
    string ContainerId,
    string Name,
    string ImageReference,
    string State,
    string Revision,
    DateTimeOffset ObservedAtUtc)
{
    public DateTimeOffset? CreatedAtUtc { get; init; }
    public DateTimeOffset? StartedAtUtc { get; init; }
    public ContainerHealth? Health { get; init; }
    public int? RestartCount { get; init; }
    public int? ExitCode { get; init; }
    public bool? OomKilled { get; init; }
    public IReadOnlyList<ContainerPortBinding> Ports { get; init; } = [];

    public ContainerDisplayStatus DisplayStatus =>
        ContainerDisplayStatuses.Evaluate(State, Health, ExitCode, OomKilled);
}

public enum ContainerHealth
{
    None,
    Starting,
    Healthy,
    Unhealthy,
}

public sealed record ContainerPortBinding(string? HostIp, int HostPort, int ContainerPort, string Protocol)
{
    public const int MaxPerContainer = 64;
}

public sealed record InventoryChange(
    InventoryChangeKind Kind,
    string ContainerId,
    ContainerInventory? Container);

public sealed record InventoryDelta(
    ulong BaseRevision,
    ulong InventoryRevision,
    IReadOnlyList<InventoryChange> Changes);

public enum InventoryReconciliationOutcome
{
    Accepted,
    StaleBase,
    SequenceGap,
    InvalidDelta,
}

public sealed record InventoryReconciliationResult(
    InventorySnapshot Snapshot,
    InventoryReconciliationOutcome Outcome,
    string? FailureCode = null);

public sealed record InventorySnapshot(
    Guid EnvironmentId,
    ulong Revision,
    IReadOnlyDictionary<string, ContainerInventory> Containers)
{
    public static InventorySnapshot Empty(Guid environmentId, ulong revision) =>
        new(
            environmentId,
            revision,
            new ReadOnlyDictionary<string, ContainerInventory>(new Dictionary<string, ContainerInventory>(StringComparer.Ordinal)));

    public InventorySnapshot WithUpdatedRevision(ulong newRevision) =>
        this with { Revision = newRevision };
}

public static class InventoryReconciler
{
    public static InventoryReconciliationResult Apply(
        InventorySnapshot current,
        InventoryDelta delta)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(delta);

        if (delta.InventoryRevision == 0 || delta.BaseRevision > delta.InventoryRevision)
        {
            return new InventoryReconciliationResult(current, InventoryReconciliationOutcome.InvalidDelta, "invalid_delta");
        }

        if (delta.BaseRevision != current.Revision)
        {
            return new InventoryReconciliationResult(current, InventoryReconciliationOutcome.StaleBase, "stale_base");
        }

        if (delta.InventoryRevision <= current.Revision)
        {
            return new InventoryReconciliationResult(current, InventoryReconciliationOutcome.InvalidDelta, "non_monotonic_revision");
        }

        if (delta.InventoryRevision > current.Revision + 1)
        {
            return new InventoryReconciliationResult(current, InventoryReconciliationOutcome.SequenceGap, "sequence_gap");
        }

        var updated = new Dictionary<string, ContainerInventory>(current.Containers, StringComparer.Ordinal);

        foreach (var change in delta.Changes)
        {
            switch (change.Kind)
            {
                case InventoryChangeKind.Added:
                case InventoryChangeKind.Updated:
                    if (change.Container is null)
                    {
                        return new InventoryReconciliationResult(current, InventoryReconciliationOutcome.InvalidDelta, "missing_container_payload");
                    }

                    updated[change.ContainerId] = change.Container;
                    break;

                case InventoryChangeKind.Removed:
                    updated.Remove(change.ContainerId);
                    break;

                default:
                    return new InventoryReconciliationResult(current, InventoryReconciliationOutcome.InvalidDelta, "unsupported_change");
            }
        }

        var next = new InventorySnapshot(
            current.EnvironmentId,
            delta.InventoryRevision,
            new ReadOnlyDictionary<string, ContainerInventory>(updated));

        return new InventoryReconciliationResult(next, InventoryReconciliationOutcome.Accepted);
    }
}
