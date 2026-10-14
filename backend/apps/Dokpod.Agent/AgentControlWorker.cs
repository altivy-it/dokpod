using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Channels;
using Dokpod.Agent.Application.Engines;
using Dokpod.Agent.Application.Events;
using Dokpod.Agent.Application.Protocol;
using Dokpod.Agent.Contracts.V1;
using AgentGrpcClient = Dokpod.Agent.Infrastructure.Protocol.AgentControlClient;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dokpod.Agent;

public sealed class AgentControlWorker(
    AgentOptions options,
    IContainerEngine engine,
    AgentCommandProtocolHandler commandHandler,
    Dokpod.Agent.Application.Metrics.IHostResourceProbe hostProbe,
    ILogger<AgentControlWorker> logger) : BackgroundService
{
    // Containers em execução do último snapshot (alvo da amostragem de recursos, spec 009).
    private IReadOnlyList<Dokpod.Agent.Application.Metrics.SampleTarget> runningTargets = [];
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);
    private const int SnapshotPageSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var endpoint = options.ControlPlaneEndpoint
            ?? throw new InvalidOperationException("Control plane endpoint is required.");
        var environmentId = options.EnvironmentId
            ?? throw new InvalidOperationException("Agent environment ID is required.");
        if (!File.Exists(options.ClientCertificatePath))
        {
            throw new InvalidOperationException("Agent client certificate was not found.");
        }

        using var certificate = X509CertificateLoader.LoadPkcs12FromFile(
            options.ClientCertificatePath,
            options.ClientCertificatePassword,
            System.OperatingSystem.IsWindows()
                ? X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet
                : X509KeyStorageFlags.EphemeralKeySet);
        using var serverCaCertificate = options.ServerCaCertificatePath is null
            ? null
            : X509CertificateLoader.LoadCertificateFromFile(options.ServerCaCertificatePath);
        await using var client = AgentGrpcClient.Create(endpoint, certificate, serverCaCertificate);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunSessionAsync(client.Connect, environmentId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception) when (exception is RpcException or HttpRequestException or IOException)
            {
                logger.LogWarning(
                    exception,
                    "Agent control session disconnected with status {Status}",
                    GetStatus(exception));
            }

            await Task.Delay(ReconnectDelay, stoppingToken);
        }
    }

    internal async Task RunSessionAsync(
        Func<CancellationToken, AsyncDuplexStreamingCall<AgentMessage, ControlPlaneMessage>> connect,
        Guid environmentId,
        CancellationToken stoppingToken)
    {
        var descriptor = await engine.InspectAsync(stoppingToken);
        var hostInfo = await TryDescribeHostAsync(stoppingToken);
        using var call = connect(stoppingToken);
        await call.RequestStream.WriteAsync(CreateHello(descriptor, hostInfo), stoppingToken);
        if (!await call.ResponseStream.MoveNext(stoppingToken) ||
            call.ResponseStream.Current.PayloadCase != ControlPlaneMessage.PayloadOneofCase.SessionEstablished)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "agent_session_not_established"));
        }

        var establishedMessage = call.ResponseStream.Current;
        var established = establishedMessage.SessionEstablished;
        if (!Guid.TryParse(established.SessionId, out var sessionId) ||
            established.FencingToken == 0 ||
            established.FencingToken > long.MaxValue ||
            established.HeartbeatIntervalSeconds == 0 ||
            !IsValidServerMetadata(establishedMessage.Metadata, established.SessionId, established.FencingToken, 1))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "control_plane_session_invalid"));
        }

        logger.LogInformation("Agent control session established");
        using var sessionCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var outbound = Channel.CreateBounded<AgentMessage>(new BoundedChannelOptions(256)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
        });
        // Fila de baixa prioridade para consultas e logs (spec 011 D-002): comandos, heartbeat e inventário saem antes.
        var background = Channel.CreateBounded<AgentMessage>(new BoundedChannelOptions(64)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
        });
        var writer = WriteMessagesAsync(
            call.RequestStream,
            outbound.Reader,
            background.Reader,
            sessionId,
            established.FencingToken,
            sessionCancellation.Token);
        var inventory = new InventoryPublisher(this, outbound.Writer);
        await inventory.SendAsync(sessionCancellation.Token);
        var heartbeat = SendHeartbeatsAsync(
            outbound.Writer,
            () => inventory.Revision,
            TimeSpan.FromSeconds(established.HeartbeatIntervalSeconds),
            sessionCancellation.Token);
        // Agente N só envia payloads novos com capability confirmada pelo servidor (008 D-009).
        var eventsAccepted = established.AcceptedCapabilities.Contains(Capability.EngineEvents);
        var events = WatchEngineEventsAsync(outbound.Writer, inventory, eventsAccepted, sessionCancellation.Token);
        var metricsAccepted = established.AcceptedCapabilities.Contains(Capability.ResourceMetrics);
        var metrics = metricsAccepted
            ? SendResourceSamplesAsync(background.Writer, sessionCancellation.Token)
            : Task.CompletedTask;
        var queries = new Dokpod.Agent.Application.Queries.ContainerQueryHandler(engine, TimeProvider.System);
        var logStreams = new Dokpod.Agent.Application.Queries.LogStreamManager(engine, TimeProvider.System);
        Task SendBackground(AgentMessage outgoing) => background.Writer.WriteAsync(outgoing, sessionCancellation.Token).AsTask();

        ulong serverSequence = 1;
        try
        {
            while (await call.ResponseStream.MoveNext(sessionCancellation.Token))
            {
                var message = call.ResponseStream.Current;
                if (!IsValidServerMetadata(
                        message.Metadata,
                        established.SessionId,
                        established.FencingToken,
                        ++serverSequence))
                {
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "control_plane_metadata_invalid"));
                }

                if (message.PayloadCase == ControlPlaneMessage.PayloadOneofCase.Command)
                {
                    await commandHandler.HandleAsync(
                        environmentId,
                        message.Metadata,
                        message.Command,
                        (acceptance, cancellationToken) => outbound.Writer.WriteAsync(
                            new AgentMessage { CommandAccepted = acceptance },
                            cancellationToken).AsTask(),
                        (result, cancellationToken) => outbound.Writer.WriteAsync(
                            new AgentMessage { CommandResult = result },
                            cancellationToken).AsTask(),
                        sessionCancellation.Token);
                    await inventory.SendAsync(sessionCancellation.Token);
                }
                else if (message.PayloadCase == ControlPlaneMessage.PayloadOneofCase.SnapshotRequest)
                {
                    await inventory.SendAsync(sessionCancellation.Token);
                }
                else if (message.PayloadCase == ControlPlaneMessage.PayloadOneofCase.ContainerQuery)
                {
                    var request = message.ContainerQuery;
                    _ = Task.Run(() => IgnoreCancellationAsync(queries.HandleAsync(request, SendBackground, sessionCancellation.Token)), CancellationToken.None);
                }
                else if (message.PayloadCase == ControlPlaneMessage.PayloadOneofCase.LogStreamControl)
                {
                    await logStreams.HandleAsync(message.LogStreamControl, SendBackground, sessionCancellation.Token);
                }
            }
        }
        finally
        {
            await sessionCancellation.CancelAsync();
            outbound.Writer.TryComplete();
            background.Writer.TryComplete();
            await IgnoreCancellationAsync(writer);
            await IgnoreCancellationAsync(heartbeat);
            await IgnoreCancellationAsync(events);
            await IgnoreCancellationAsync(metrics);
        }
    }

    /// <summary>
    /// Uma amostra por intervalo na fila de baixa prioridade (spec 009 FR-002): nunca espera por espaço,
    /// para não atrasar comandos e inventário; lote que não cabe é descartado e contado no próximo.
    /// </summary>
    private async Task SendResourceSamplesAsync(ChannelWriter<AgentMessage> background, CancellationToken cancellationToken)
    {
        var sampler = new Dokpod.Agent.Application.Metrics.ResourceSampler(engine, hostProbe, TimeProvider.System);
        sampler.EngineMemoryTotalBytes = (await TryDescribeHostAsync(cancellationToken))?.MemoryBytes;
        uint lost = 0;
        using var timer = new PeriodicTimer(options.EffectiveMetricsInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            IReadOnlyList<ResourceSampleBatch> batches;
            try
            {
                batches = await sampler.SampleAsync(Volatile.Read(ref runningTargets), cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException)
            {
                logger.LogDebug(exception, "Resource sampling skipped");
                continue;
            }

            batches[0].DroppedCount += lost;
            lost = 0;
            foreach (var batch in batches)
            {
                if (!background.TryWrite(new AgentMessage { ResourceSamples = batch }))
                {
                    lost += (uint)batch.Containers.Count;
                }
            }
        }
    }

    private static async Task WriteMessagesAsync(
        IClientStreamWriter<AgentMessage> requestStream,
        ChannelReader<AgentMessage> messages,
        ChannelReader<AgentMessage> background,
        Guid sessionId,
        ulong fencingToken,
        CancellationToken cancellationToken)
    {
        ulong sequence = 0;
        while (true)
        {
            // Alta prioridade sempre drena primeiro; a fila de consultas/logs só é lida quando a outra está vazia.
            if (!messages.TryRead(out var message) && !background.TryRead(out message))
            {
                var high = messages.WaitToReadAsync(cancellationToken).AsTask();
                var low = background.WaitToReadAsync(cancellationToken).AsTask();
                await Task.WhenAny(high, low);
                if (high.IsCompleted && !await high && (!low.IsCompleted || !await low)) return;
                continue;
            }

            message.Metadata = new MessageMetadata
            {
                ProtocolVersion = "1",
                SessionId = sessionId.ToString("D"),
                FencingToken = fencingToken,
                Sequence = ++sequence,
                OccurredAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
                CorrelationId = Guid.NewGuid().ToString("D"),
            };
            await requestStream.WriteAsync(message, cancellationToken);
        }
    }

    private static async Task SendHeartbeatsAsync(
        ChannelWriter<AgentMessage> messages,
        Func<ulong> getInventoryRevision,
        TimeSpan interval,
        CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await messages.WriteAsync(
                new AgentMessage
                {
                    Heartbeat = new Heartbeat { InventoryRevision = getInventoryRevision() },
                },
                cancellationToken);
        }
    }

    private async Task<ulong> SendInventorySnapshotAsync(
        ChannelWriter<AgentMessage> messages,
        ulong previousRevision,
        CancellationToken cancellationToken)
    {
        var containers = await engine.ListContainersAsync(cancellationToken);
        Volatile.Write(ref runningTargets, containers
            .Where(container => string.Equals(container.State, "running", StringComparison.OrdinalIgnoreCase))
            .Select(container => new Dokpod.Agent.Application.Metrics.SampleTarget(container.ContainerId, container.CpuLimitCores))
            .ToArray());
        var observedAt = DateTimeOffset.UtcNow;
        var clockRevision = checked((ulong)observedAt.ToUnixTimeMilliseconds());
        var revision = Math.Max(previousRevision + 1, clockRevision);
        var snapshotId = Guid.NewGuid().ToString("D");
        var orderedContainers = containers
            .OrderBy(container => container.ContainerId, StringComparer.Ordinal)
            .ToArray();
        var pageCount = Math.Max(1, (orderedContainers.Length + SnapshotPageSize - 1) / SnapshotPageSize);

        for (var pageNumber = 0; pageNumber < pageCount; pageNumber++)
        {
            var page = new InventorySnapshotPage
            {
                SnapshotId = snapshotId,
                InventoryRevision = revision,
                PageNumber = checked((uint)pageNumber),
                IsLastPage = pageNumber == pageCount - 1,
            };
            page.Containers.AddRange(orderedContainers
                .Skip(pageNumber * SnapshotPageSize)
                .Take(SnapshotPageSize)
                .Select(container => MapContainer(container, observedAt)));
            await messages.WriteAsync(new AgentMessage { SnapshotPage = page }, cancellationToken);
        }

        return revision;
    }

    private static ContainerState MapContainer(EngineContainer container, DateTimeOffset observedAt)
    {
        var state = new ContainerState
        {
            ContainerId = container.ContainerId,
            Name = container.Name,
            ImageReference = container.ImageReference,
            State = MapContainerState(container.State),
            Revision = container.Revision,
            ObservedAt = Timestamp.FromDateTimeOffset(observedAt),
            Health = container.Health switch
            {
                "none" => ContainerHealth.None,
                "starting" => ContainerHealth.Starting,
                "healthy" => ContainerHealth.Healthy,
                "unhealthy" => ContainerHealth.Unhealthy,
                _ => ContainerHealth.Unspecified,
            },
            RestartCount = container.RestartCount is > 0 ? (uint)container.RestartCount.Value : 0,
            OomKilled = container.OomKilled ?? false,
        };
        if (container.CreatedAtUtc is not null) state.CreatedAt = Timestamp.FromDateTimeOffset(container.CreatedAtUtc.Value);
        if (container.StartedAtUtc is not null) state.StartedAt = Timestamp.FromDateTimeOffset(container.StartedAtUtc.Value);
        if (container.ExitCode is not null) state.ExitCode = container.ExitCode.Value;
        state.Ports.AddRange(container.Ports.Take(64).Select(port => new PortBinding
        {
            HostIp = port.HostIp ?? string.Empty,
            HostPort = (uint)Math.Clamp(port.HostPort, 0, 65535),
            ContainerPort = (uint)Math.Clamp(port.ContainerPort, 0, 65535),
            Protocol = port.Protocol switch
            {
                "udp" => TransportProtocol.Udp,
                "sctp" => TransportProtocol.Sctp,
                _ => TransportProtocol.Tcp,
            },
        }));
        return state;
    }

    private async Task<EngineHostInfo?> TryDescribeHostAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await engine.DescribeHostAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException or System.Text.Json.JsonException)
        {
            logger.LogWarning(exception, "Engine host description unavailable");
            return null;
        }
    }

    /// <summary>
    /// Assina eventos do engine (reconectando), envia lotes a cada segundo quando aceitos e
    /// republica o inventário 2 s depois da última mudança de container.
    /// </summary>
    private async Task WatchEngineEventsAsync(
        ChannelWriter<AgentMessage> messages,
        InventoryPublisher inventory,
        bool sendEvents,
        CancellationToken cancellationToken)
    {
        var publisher = new EngineEventPublisher(TimeProvider.System);
        long dirtySince = 0;
        var pump = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var batch = publisher.Drain();
                if (sendEvents && batch is not null)
                {
                    await messages.WriteAsync(new AgentMessage { EngineEvents = batch }, cancellationToken);
                }

                var since = Interlocked.Read(ref dirtySince);
                if (since != 0 && Environment.TickCount64 - since >= 2000 &&
                    Interlocked.CompareExchange(ref dirtySince, 0, since) == since)
                {
                    await inventory.SendAsync(cancellationToken);
                }
            }
        }, cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var record in engine.WatchEventsAsync(cancellationToken))
                {
                    if (publisher.Observe(record))
                    {
                        Interlocked.Exchange(ref dirtySince, Environment.TickCount64);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException or NotSupportedException)
            {
                logger.LogWarning(exception, "Engine event stream interrupted");
            }

            await Task.Delay(ReconnectDelay, cancellationToken);
        }

        await IgnoreCancellationAsync(pump);
    }

    /// <summary>Serializa os envios de snapshot (comandos, pedidos do servidor e eventos).</summary>
    private sealed class InventoryPublisher(AgentControlWorker worker, ChannelWriter<AgentMessage> messages)
    {
        private readonly SemaphoreSlim gate = new(1, 1);
        private ulong revision;

        public ulong Revision => Interlocked.Read(ref revision);

        public async Task SendAsync(CancellationToken cancellationToken)
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                var next = await worker.SendInventorySnapshotAsync(messages, Revision, cancellationToken);
                Interlocked.Exchange(ref revision, next);
            }
            finally
            {
                gate.Release();
            }
        }
    }

    private static ContainerLifecycleState MapContainerState(string state) => state.ToLowerInvariant() switch
    {
        "created" => ContainerLifecycleState.Created,
        "running" => ContainerLifecycleState.Running,
        "paused" => ContainerLifecycleState.Paused,
        "restarting" => ContainerLifecycleState.Restarting,
        "exited" => ContainerLifecycleState.Exited,
        "dead" => ContainerLifecycleState.Dead,
        _ => ContainerLifecycleState.Unspecified,
    };

    private static AgentMessage CreateHello(EngineDescriptor descriptor, EngineHostInfo? hostInfo)
    {
        var hello = new AgentHello
        {
            AgentVersion = typeof(AgentControlWorker).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            Engine = hostInfo?.Engine == "podman" ? EngineKind.Podman : EngineKind.Docker,
            EngineVersion = descriptor.EngineVersion,
            EngineApiVersion = descriptor.ApiVersion,
            EngineRootless = hostInfo?.Rootless ?? false,
            HostName = hostInfo?.HostName ?? System.Environment.MachineName,
            CpuCount = hostInfo?.CpuCount is > 0 ? (uint)hostInfo.CpuCount.Value : (uint)System.Environment.ProcessorCount,
            MemoryBytes = hostInfo?.MemoryBytes is > 0 ? (ulong)hostInfo.MemoryBytes.Value : 0,
            OperatingSystem = System.OperatingSystem.IsWindows()
                ? Dokpod.Agent.Contracts.V1.OperatingSystem.Windows
                : Dokpod.Agent.Contracts.V1.OperatingSystem.Linux,
            OperatingSystemVersion = System.Environment.OSVersion.VersionString,
            Architecture = RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64
                ? Dokpod.Agent.Contracts.V1.Architecture.Arm64
                : Dokpod.Agent.Contracts.V1.Architecture.Amd64,
            Capabilities =
            {
                Capability.Inventory,
                Capability.ContainerStart,
                Capability.ContainerStop,
                Capability.ContainerRestart,
                Capability.ContainerDelete,
                Capability.EngineEvents,
                Capability.ContainerPause,
                Capability.ContainerUnpause,
                Capability.ContainerInspect,
                Capability.ContainerLogs,
            },
            SupportedProtocolVersions = { "1" },
        };
        // Métricas de host dependem de `/proc` do Linux; o agente Windows não anuncia (spec 009 FR-010).
        if (!System.OperatingSystem.IsWindows()) hello.Capabilities.Add(Capability.ResourceMetrics);
        return new AgentMessage { Hello = hello };
    }

    private static bool IsValidServerMetadata(
        MessageMetadata? metadata,
        string sessionId,
        ulong fencingToken,
        ulong expectedSequence) =>
        metadata is not null &&
        metadata.ProtocolVersion == "1" &&
        metadata.SessionId == sessionId &&
        metadata.FencingToken == fencingToken &&
        metadata.Sequence == expectedSequence &&
        metadata.OccurredAt is not null;

    private static async Task IgnoreCancellationAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static string GetStatus(Exception exception) => exception is RpcException rpcException
        ? rpcException.StatusCode.ToString()
        : exception.GetType().Name;
}