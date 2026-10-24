using Dokpod.Agent.Contracts.V1;
using Dokpod.ControlPlane.Application.Agents;
using Dokpod.ControlPlane.Application.Commands;
using Dokpod.ControlPlane.Application.Events;
using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.ControlPlane.Application.Realtime;
using Dokpod.Domain.Events;
using Dokpod.Domain.Hosts;
using Google.Protobuf.WellKnownTypes;

namespace Dokpod.ControlPlane.Api.Agents;

/// <summary>
/// Efeitos da spec 008 sobre a sessão do agente: descritor, conexão, heartbeat em memória,
/// eventos operacionais e invalidações. Falhas aqui nunca derrubam a sessão de comandos.
/// </summary>
public sealed class AgentHostActivity(
    IAgentHostStore hostStore,
    AgentLivenessTracker liveness,
    OperationalEventService events,
    IAgentCommandStore commandStore,
    IControlPlaneNotifier notifier,
    EngineSupportOptions engineSupport,
    TimeProvider timeProvider,
    ILogger<AgentHostActivity> logger)
{
    public const int MaxEventsPerBatch = 100;

    // Registro do início da sessão que falhou (por exemplo, banco sem conexões livres no restart da API);
    // refeito a cada heartbeat até gravar, para o host não ficar desconectado com a sessão ativa (spec 005 T016).
    private Func<CancellationToken, Task>? pendingSessionStart;

    /// <summary>Capabilities do Hello que este servidor processa (008 D-009).</summary>
    public static IReadOnlyList<Capability> ServerCapabilities { get; } =
    [
        Capability.Inventory,
        Capability.ContainerStart,
        Capability.ContainerStop,
        Capability.ContainerRestart,
        Capability.ContainerDelete,
        Capability.EngineEvents,
        Capability.ResourceMetrics,
        Capability.ContainerPause,
        Capability.ContainerUnpause,
        Capability.ContainerInspect,
        Capability.ContainerLogs,
        Capability.ImageInventory,
        Capability.ImagePull,
        Capability.ContainerCreate,
        Capability.ContainerExec,
        Capability.VolumeInventory,
        Capability.VolumeRemove,
        Capability.NetworkInventory,
        Capability.NetworkRemove,
        Capability.RegistryInventory,
        Capability.PodInventory,
        Capability.PodActions,
    ];

    public static IEnumerable<Capability> Accept(AgentHello hello) =>
        hello.Capabilities.Where(ServerCapabilities.Contains).Distinct();

    public async Task OnSessionStartedAsync(AgentSession session, AgentHello hello, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var engine = hello.Engine == EngineKind.Podman ? "podman" : "docker";
        var descriptor = new AgentDescriptor(
            session.EnvironmentId,
            engine,
            hello.EngineVersion,
            hello.EngineApiVersion,
            hello.EngineRootless,
            hello.OperatingSystem == Dokpod.Agent.Contracts.V1.OperatingSystem.Windows ? "windows" : "linux",
            hello.OperatingSystemVersion,
            hello.Architecture == Architecture.Arm64 ? "arm64" : "amd64",
            hello.AgentVersion,
            hello.Capabilities.Select(c => c.ToString()).Distinct().ToArray(),
            hello.HostName,
            hello.CpuCount == 0 ? null : checked((int)Math.Min(hello.CpuCount, int.MaxValue)),
            hello.MemoryBytes == 0 ? null : checked((long)Math.Min(hello.MemoryBytes, long.MaxValue)),
            now);
        async Task RegisterAsync(CancellationToken cancellationToken)
        {
            await hostStore.UpsertDescriptorAsync(descriptor, cancellationToken);
            await hostStore.SetConnectionAsync(session.EnvironmentId, true, now, cancellationToken);
            await events.RecordAsync(
                session.EnvironmentId,
                OperationalEventTypes.AgentConnected,
                "host",
                null,
                hello.HostName,
                new Dictionary<string, string> { ["engine"] = engine, ["version"] = hello.EngineVersion },
                cancellationToken);
            var minimum = engineSupport.MinimumFor(engine);
            if (HostSituations.Evaluate(true, true, false, hello.EngineVersion, minimum) == HostSituation.UpdateRequired)
            {
                await events.RecordAsync(
                    session.EnvironmentId,
                    OperationalEventTypes.EngineVersionOutdated,
                    "host",
                    null,
                    hello.HostName,
                    new Dictionary<string, string>
                    {
                        ["engine"] = engine,
                        ["version"] = hello.EngineVersion,
                        ["minimumVersion"] = minimum ?? string.Empty,
                    },
                    cancellationToken);
            }

            await notifier.NotifyAsync(session.EnvironmentId, InvalidationKinds.Host, cancellationToken);
        }

        if (!await Guard(() => RegisterAsync(cancellationToken), "session_started"))
        {
            pendingSessionStart = RegisterAsync;
        }
    }

    public async Task OnHeartbeatAsync(Guid environmentId, ulong inventoryRevision, CancellationToken cancellationToken)
    {
        if (pendingSessionStart is { } register && await Guard(() => register(cancellationToken), "session_started_retry"))
        {
            pendingSessionStart = null;
        }

        if (liveness.RecordHeartbeat(environmentId, inventoryRevision))
        {
            await Guard(() => hostStore.TouchLastSeenAsync(environmentId, timeProvider.GetUtcNow(), cancellationToken), "heartbeat");
        }
    }

    public void OnSnapshotApplied(Guid environmentId, ulong revision) => liveness.RecordProjected(environmentId, revision);

    public async Task OnEngineEventsAsync(Guid environmentId, EngineEventBatch batch, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var mapped = new List<OperationalEvent>(Math.Min(batch.Events.Count, MaxEventsPerBatch) + 1);
        var discarded = 0;
        foreach (var item in batch.Events.Take(MaxEventsPerBatch))
        {
            var type = MapType(item.Type);
            if (type is null || item.OccurredAt is null)
            {
                discarded++;
                continue;
            }

            mapped.Add(OperationalEvent.Create(
                environmentId,
                type,
                item.OccurredAt.ToDateTimeOffset(),
                now,
                SubjectKind(type),
                item.SubjectId,
                item.SubjectName,
                item.Attributes,
                (int)Math.Clamp(item.AggregatedCount, 1, int.MaxValue),
                item.SourceKey));
        }

        var dropped = batch.DroppedCount + (uint)discarded + (uint)Math.Max(0, batch.Events.Count - MaxEventsPerBatch);
        if (dropped > 0)
        {
            mapped.Add(OperationalEvent.Create(
                environmentId,
                OperationalEventTypes.EventsDropped,
                now,
                now,
                "host",
                null,
                null,
                new Dictionary<string, string> { ["droppedCount"] = dropped.ToString(System.Globalization.CultureInfo.InvariantCulture) }));
        }

        await Guard(() => events.AppendAsync(environmentId, mapped, cancellationToken), "engine_events");
    }

    public async Task OnCommandResultAsync(Guid environmentId, Guid commandId, CancellationToken cancellationToken)
    {
        await Guard(async () =>
        {
            var command = await commandStore.GetAsync(environmentId, commandId, cancellationToken);
            if (command is null) return;
            var type = command.State switch
            {
                ControlPlaneCommandState.Succeeded => OperationalEventTypes.CommandSucceeded,
                ControlPlaneCommandState.Failed => OperationalEventTypes.CommandFailed,
                ControlPlaneCommandState.Indeterminate => OperationalEventTypes.CommandIndeterminate,
                _ => null,
            };
            if (type is null) return;
            var parameters = new Dictionary<string, string>
            {
                ["action"] = command.Kind.ToString(),
                ["commandId"] = commandId.ToString("D"),
            };
            if (!string.IsNullOrWhiteSpace(command.FailureCode)) parameters["failureCode"] = command.FailureCode;
            await events.RecordAsync(environmentId, type, "command", command.ContainerId, null, parameters, cancellationToken);
        }, "command_result");
    }

    public async Task OnSessionEndedAsync(AgentSession session, bool stillActiveElsewhere)
    {
        if (stillActiveElsewhere) return;
        liveness.Forget(session.EnvironmentId);
        await Guard(async () =>
        {
            var now = timeProvider.GetUtcNow();
            var previous = await hostStore.SetConnectionAsync(session.EnvironmentId, false, now, CancellationToken.None);
            if (previous == true)
            {
                await events.RecordAsync(session.EnvironmentId, OperationalEventTypes.AgentDisconnected, "host", null, null, null, CancellationToken.None);
            }

            await notifier.NotifyAsync(session.EnvironmentId, InvalidationKinds.Host, CancellationToken.None);
        }, "session_ended");
    }

    private static string? MapType(EngineEventType type) => type switch
    {
        EngineEventType.ContainerDied => OperationalEventTypes.ContainerDied,
        EngineEventType.ContainerOomKilled => OperationalEventTypes.ContainerOomKilled,
        EngineEventType.ContainerUnhealthy => OperationalEventTypes.ContainerUnhealthy,
        EngineEventType.ContainerRecovered => OperationalEventTypes.ContainerRecovered,
        EngineEventType.ContainerAutoRestarted => OperationalEventTypes.ContainerAutoRestarted,
        EngineEventType.ContainerCreated => OperationalEventTypes.ContainerCreated,
        EngineEventType.ContainerRemoved => OperationalEventTypes.ContainerRemoved,
        EngineEventType.ImagePulled => OperationalEventTypes.ImagePulled,
        EngineEventType.ImageRemoved => OperationalEventTypes.ImageRemoved,
        EngineEventType.VolumeRemoved => OperationalEventTypes.VolumeRemoved,
        EngineEventType.ExecOrphanSuspected => OperationalEventTypes.ContainerExecOrphanSuspected,
        _ => null,
    };

    private static string SubjectKind(string type) =>
        type.StartsWith("container.", StringComparison.Ordinal) ? "container"
        : type.StartsWith("image.", StringComparison.Ordinal) ? "image"
        : type.StartsWith("volume.", StringComparison.Ordinal) ? "volume"
        : "host";

    private async Task<bool> Guard(Func<Task> action, string stage)
    {
        try
        {
            await action();
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Agent host activity failed at {Stage}", stage);
            return false;
        }
    }
}
