using Dokpod.Agent.Application.Engines;
using Dokpod.Agent.Contracts.V1;
using Google.Protobuf.WellKnownTypes;

namespace Dokpod.Agent.Application.Events;

/// <summary>
/// Converte eventos brutos do engine na allowlist da spec 008 (D-002/D-003): agrega por 10 s
/// (tipo, sujeito), funde `die` posterior a `oom` do mesmo container, limita a 120 eventos por
/// minuto e informa o excedente em `dropped_count`. Também indica quando o inventário mudou.
/// </summary>
public sealed class EngineEventPublisher(TimeProvider timeProvider)
{
    public static readonly TimeSpan AggregationWindow = TimeSpan.FromSeconds(10);
    public const int MaxEventsPerMinute = 120;
    public const int MaxEventsPerBatch = 100;
    private const int MaxAttributeLength = 256;

    private static readonly HashSet<string> InventoryActions = new(StringComparer.Ordinal)
    {
        "create", "start", "stop", "die", "kill", "pause", "unpause", "restart", "destroy", "rename", "oom", "update",
    };

    private readonly Lock gate = new();
    private readonly List<EngineEvent> pending = [];
    private readonly Dictionary<(EngineEventType, string), (EngineEvent Event, DateTimeOffset At)> recent = [];
    private readonly Dictionary<string, DateTimeOffset> oomAt = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> lastHealth = new(StringComparer.Ordinal);
    private readonly Queue<DateTimeOffset> emitted = new();
    private uint dropped;

    /// <summary>Registra um evento bruto; retorna true quando ele altera o inventário de containers.</summary>
    public bool Observe(EngineEventRecord record)
    {
        var inventoryChanged = record.Type == "container" &&
            (InventoryActions.Contains(record.Action) || record.Action.StartsWith("health_status", StringComparison.Ordinal));
        var type = Map(record);
        if (type is null) return inventoryChanged;

        var now = timeProvider.GetUtcNow();
        lock (gate)
        {
            Prune(now);
            var key = (type.Value, record.SubjectId);
            if (type == EngineEventType.ContainerDied && oomAt.TryGetValue(record.SubjectId, out var oomTime) && now - oomTime <= AggregationWindow)
            {
                // OOM seguido de die do mesmo container na janela é um único evento de erro.
                return inventoryChanged;
            }

            if (recent.TryGetValue(key, out var existing) && now - existing.At <= AggregationWindow)
            {
                existing.Event.AggregatedCount++;
                return inventoryChanged;
            }

            if (emitted.Count >= MaxEventsPerMinute)
            {
                dropped++;
                return inventoryChanged;
            }

            var engineEvent = new EngineEvent
            {
                SourceKey = Truncate($"{record.SubjectId}:{record.TimeNano}:{record.Action}"),
                Type = type.Value,
                OccurredAt = Timestamp.FromDateTimeOffset(record.OccurredAtUtc),
                SubjectId = Truncate(record.SubjectId),
                SubjectName = Truncate(record.Name ?? string.Empty),
                AggregatedCount = 1,
            };
            if (!string.IsNullOrEmpty(record.Name)) engineEvent.Attributes["name"] = Truncate(record.Name);
            if (!string.IsNullOrEmpty(record.Image)) engineEvent.Attributes["image"] = Truncate(record.Image);
            if (record.ExitCode is not null) engineEvent.Attributes["exitCode"] = record.ExitCode.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (type == EngineEventType.ContainerOomKilled) oomAt[record.SubjectId] = now;
            recent[key] = (engineEvent, now);
            emitted.Enqueue(now);
            pending.Add(engineEvent);
        }

        return inventoryChanged;
    }

    /// <summary>Retira o lote pendente (até 100 eventos) com o total descartado desde o último lote.</summary>
    public EngineEventBatch? Drain()
    {
        lock (gate)
        {
            if (pending.Count == 0 && dropped == 0) return null;
            var batch = new EngineEventBatch { DroppedCount = dropped };
            var take = Math.Min(pending.Count, MaxEventsPerBatch);
            batch.Events.AddRange(pending.Take(take));
            pending.RemoveRange(0, take);
            dropped = 0;
            return batch;
        }
    }

    private EngineEventType? Map(EngineEventRecord record)
    {
        switch (record.Type)
        {
            case "container":
                if (record.Action == "die")
                {
                    return record.ExitCode is not null and not 0 ? EngineEventType.ContainerDied : null;
                }

                if (record.Action.StartsWith("health_status", StringComparison.Ordinal))
                {
                    var status = record.Action.Contains("unhealthy", StringComparison.Ordinal) ? "unhealthy"
                        : record.Action.Contains("healthy", StringComparison.Ordinal) ? "healthy"
                        : "starting";
                    lock (gate)
                    {
                        lastHealth.TryGetValue(record.SubjectId, out var previous);
                        lastHealth[record.SubjectId] = status;
                        if (status == "unhealthy" && previous != "unhealthy") return EngineEventType.ContainerUnhealthy;
                        if (status == "healthy" && previous == "unhealthy") return EngineEventType.ContainerRecovered;
                    }

                    return null;
                }

                return record.Action switch
                {
                    "oom" => EngineEventType.ContainerOomKilled,
                    "restart" => EngineEventType.ContainerAutoRestarted,
                    "create" => EngineEventType.ContainerCreated,
                    "destroy" => EngineEventType.ContainerRemoved,
                    _ => null,
                };
            case "image":
                return record.Action switch
                {
                    "pull" => EngineEventType.ImagePulled,
                    "delete" or "remove" => EngineEventType.ImageRemoved,
                    _ => null,
                };
            case "volume":
                return record.Action is "destroy" or "remove" ? EngineEventType.VolumeRemoved : null;
            default:
                return null;
        }
    }

    private void Prune(DateTimeOffset now)
    {
        while (emitted.Count > 0 && now - emitted.Peek() > TimeSpan.FromMinutes(1)) emitted.Dequeue();
        foreach (var key in recent.Where(entry => now - entry.Value.At > AggregationWindow).Select(entry => entry.Key).ToArray())
        {
            recent.Remove(key);
        }

        foreach (var key in oomAt.Where(entry => now - entry.Value > AggregationWindow).Select(entry => entry.Key).ToArray())
        {
            oomAt.Remove(key);
        }
    }

    private static string Truncate(string value) => value.Length <= MaxAttributeLength ? value : value[..MaxAttributeLength];
}
