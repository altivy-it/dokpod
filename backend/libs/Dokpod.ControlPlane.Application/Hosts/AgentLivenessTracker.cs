using System.Collections.Concurrent;

namespace Dokpod.ControlPlane.Application.Hosts;

/// <summary>
/// Último heartbeat e revisões de inventário mantidos em memória pela instância que detém a sessão
/// (008 D-005). Nunca usa `last_seen_at` para calcular a situação.
/// </summary>
public sealed class AgentLivenessTracker(TimeProvider timeProvider)
{
    public static readonly TimeSpan DivergenceTolerance = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan LastSeenPersistInterval = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<Guid, Entry> entries = new();

    private sealed class Entry
    {
        public DateTimeOffset LastHeartbeatUtc;
        public ulong ReportedRevision;
        public ulong ProjectedRevision;
        public DateTimeOffset? DivergingSinceUtc;
        public DateTimeOffset LastSeenPersistedUtc = DateTimeOffset.MinValue;
    }

    /// <summary>Registra heartbeat; retorna true quando `last_seen_at` deve ser persistido (≤ 1/min).</summary>
    public bool RecordHeartbeat(Guid environmentId, ulong reportedRevision)
    {
        var now = timeProvider.GetUtcNow();
        var entry = entries.GetOrAdd(environmentId, _ => new Entry());
        lock (entry)
        {
            entry.LastHeartbeatUtc = now;
            entry.ReportedRevision = reportedRevision;
            UpdateDivergence(entry, now);
            if (now - entry.LastSeenPersistedUtc < LastSeenPersistInterval) return false;
            entry.LastSeenPersistedUtc = now;
            return true;
        }
    }

    public void RecordProjected(Guid environmentId, ulong projectedRevision)
    {
        var now = timeProvider.GetUtcNow();
        var entry = entries.GetOrAdd(environmentId, _ => new Entry());
        lock (entry)
        {
            entry.ProjectedRevision = projectedRevision;
            if (entry.ReportedRevision < projectedRevision) entry.ReportedRevision = projectedRevision;
            UpdateDivergence(entry, now);
        }
    }

    public void Forget(Guid environmentId) => entries.TryRemove(environmentId, out _);

    public bool IsDiverging(Guid environmentId)
    {
        if (!entries.TryGetValue(environmentId, out var entry)) return false;
        var now = timeProvider.GetUtcNow();
        lock (entry)
        {
            UpdateDivergence(entry, now);
            return entry.DivergingSinceUtc is not null && now - entry.DivergingSinceUtc.Value >= DivergenceTolerance;
        }
    }

    public DateTimeOffset? LastHeartbeat(Guid environmentId) =>
        entries.TryGetValue(environmentId, out var entry) ? entry.LastHeartbeatUtc : null;

    /// <summary>Maior tempo de divergência entre revisão relatada e projetada (métrica `dokpod.inventory.lag`).</summary>
    public TimeSpan MaxDivergence()
    {
        var now = timeProvider.GetUtcNow();
        var max = TimeSpan.Zero;
        foreach (var entry in entries.Values)
        {
            lock (entry)
            {
                UpdateDivergence(entry, now);
                if (entry.DivergingSinceUtc is { } since && now - since > max) max = now - since;
            }
        }

        return max;
    }

    /// <summary>Maior idade do último heartbeat (métrica `dokpod.agents.heartbeat.age`).</summary>
    public TimeSpan MaxHeartbeatAge()
    {
        var now = timeProvider.GetUtcNow();
        var max = TimeSpan.Zero;
        foreach (var entry in entries.Values)
        {
            lock (entry)
            {
                if (entry.LastHeartbeatUtc != default && now - entry.LastHeartbeatUtc > max) max = now - entry.LastHeartbeatUtc;
            }
        }

        return max;
    }

    private static void UpdateDivergence(Entry entry, DateTimeOffset now)
    {
        if (entry.ReportedRevision == entry.ProjectedRevision || entry.ProjectedRevision == 0 && entry.ReportedRevision == 0)
        {
            entry.DivergingSinceUtc = null;
        }
        else
        {
            entry.DivergingSinceUtc ??= now;
        }
    }
}
