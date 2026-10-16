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
    Dokpod.Agent.Application.Containers.IHostSecretFiles secretFiles,
    Dokpod.Agent.Application.Containers.ContainerCreationPolicy creationPolicy,
    ILogger<AgentControlWorker> logger,
    Dokpod.Agent.Application.Terminal.ExecSessionManager? execSessions = null,
    Dokpod.Agent.Application.Pods.ISelfContainer? selfContainer = null) : BackgroundService
{
    // Containers em execução do último snapshot (alvo da amostragem de recursos, spec 009).
    private IReadOnlyList<Dokpod.Agent.Application.Metrics.SampleTarget> runningTargets = [];
    /// <summary>Container do próprio agente (spec 021): marca o pod do agente.</summary>
    private readonly Dokpod.Agent.Application.Pods.ISelfContainer self = selfContainer ?? new Dokpod.Agent.Application.Pods.NoSelfContainer();

    /// <summary>Último inventário de containers (spec 018): montagens para o snapshot de volumes.</summary>
    private IReadOnlyList<EngineContainer> latestContainers = [];
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
                await RunSessionAsync(client.Connect, environmentId, stoppingToken, client.Exec);
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
        CancellationToken stoppingToken,
        Func<CancellationToken, AsyncDuplexStreamingCall<ExecFrame, ExecFrame>>? openExec = null)
    {
        var descriptor = await engine.InspectAsync(stoppingToken);
        var hostInfo = await TryDescribeHostAsync(stoppingToken);
        using var call = connect(stoppingToken);
        await call.RequestStream.WriteAsync(CreateHello(descriptor, hostInfo, execSessions is not null), stoppingToken);
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
        // Spec 012: inventário de imagens somente com a capability confirmada pelo servidor.
        var imagesAccepted = established.AcceptedCapabilities.Contains(Capability.ImageInventory);
        var images = new ImagePublisher(this, outbound.Writer, imagesAccepted);
        await images.SendAsync(force: true, sessionCancellation.Token);
        // Spec 018: inventário de volumes somente com a capability confirmada; usa as montagens do último inventário.
        var volumesAccepted = established.AcceptedCapabilities.Contains(Capability.VolumeInventory);
        var volumes = new VolumePublisher(this, outbound.Writer, volumesAccepted);
        await volumes.SendAsync(force: true, sessionCancellation.Token);
        // Spec 019: inventário de redes somente com a capability confirmada; usa as conexões do último inventário.
        var networksAccepted = established.AcceptedCapabilities.Contains(Capability.NetworkInventory);
        var networks = new NetworkPublisher(this, outbound.Writer, networksAccepted);
        await networks.SendAsync(force: true, sessionCancellation.Token);
        // Spec 020: configuração de registros na sessão nova e a cada 5 minutos, só quando muda.
        var registries = new RegistryPublisher(this, outbound.Writer, established.AcceptedCapabilities.Contains(Capability.RegistryInventory));
        await registries.SendAsync(force: true, sessionCancellation.Token);
        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
            while (await timer.WaitForNextTickAsync(sessionCancellation.Token)) await registries.SendAsync(force: false, sessionCancellation.Token);
        }, sessionCancellation.Token).ContinueWith(_ => { }, TaskScheduler.Default);
        // Spec 021: pods (só Podman; no Docker o snapshot informa "sem pods").
        var pods = new PodPublisher(this, outbound.Writer, established.AcceptedCapabilities.Contains(Capability.PodInventory));
        await pods.SendAsync(force: true, sessionCancellation.Token);
        var heartbeat = SendHeartbeatsAsync(
            outbound.Writer,
            () => inventory.Revision,
            () => images.Revision,
            () => volumes.Revision,
            () => networks.Revision,
            () => pods.Revision,
            TimeSpan.FromSeconds(established.HeartbeatIntervalSeconds),
            sessionCancellation.Token);
        // Agente N só envia payloads novos com capability confirmada pelo servidor (008 D-009).
        var eventsAccepted = established.AcceptedCapabilities.Contains(Capability.EngineEvents);
        var events = WatchEngineEventsAsync(outbound.Writer, inventory, images, volumes, networks, pods, eventsAccepted, sessionCancellation.Token);
        await ReportOrphanExecsAsync(outbound.Writer, eventsAccepted, sessionCancellation.Token);
        var metricsAccepted = established.AcceptedCapabilities.Contains(Capability.ResourceMetrics);
        var metrics = metricsAccepted
            ? SendResourceSamplesAsync(background.Writer, sessionCancellation.Token)
            : Task.CompletedTask;
        // A tarefa só é aguardada no fim da sessão: uma falha precisa aparecer no log quando acontece.
        _ = metrics.ContinueWith(
            task => logger.LogError(task.Exception, "Resource sampling stopped unexpectedly"),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
        var queries = new Dokpod.Agent.Application.Queries.ContainerQueryHandler(engine, TimeProvider.System, secretFiles, creationPolicy);
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
                    if (message.Command.Kind == CommandKind.PullImage)
                    {
                        await images.SendAsync(force: false, sessionCancellation.Token);
                        await registries.SendAsync(force: false, sessionCancellation.Token);
                    }
                    await volumes.SendAsync(force: false, sessionCancellation.Token);
                    await networks.SendAsync(force: false, sessionCancellation.Token);
                    await pods.SendAsync(force: false, sessionCancellation.Token);
                }
                else if (message.PayloadCase == ControlPlaneMessage.PayloadOneofCase.SnapshotRequest)
                {
                    if (message.SnapshotRequest.Scope == SnapshotScope.Images) await images.SendAsync(force: true, sessionCancellation.Token);
                    else if (message.SnapshotRequest.Scope == SnapshotScope.Volumes) await volumes.SendAsync(force: true, sessionCancellation.Token);
                    else if (message.SnapshotRequest.Scope == SnapshotScope.Networks) await networks.SendAsync(force: true, sessionCancellation.Token);
                    else if (message.SnapshotRequest.Scope == SnapshotScope.Registries) await registries.SendAsync(force: true, sessionCancellation.Token);
                    else if (message.SnapshotRequest.Scope == SnapshotScope.Pods) await pods.SendAsync(force: true, sessionCancellation.Token);
                    else await inventory.SendAsync(sessionCancellation.Token);
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
                else if (message.PayloadCase == ControlPlaneMessage.PayloadOneofCase.ExecOpen && execSessions is not null && openExec is not null)
                {
                    // Spec 014: terminal no RPC `Exec` separado, vinculado à sessão e ao fencing desta conexão.
                    var request = message.ExecOpen;
                    var handshake = new ExecHandshake { ExecSessionId = request.ExecSessionId, SessionId = established.SessionId, FencingToken = established.FencingToken };
                    _ = Task.Run(
                        () => IgnoreCancellationAsync(execSessions.RunAsync(
                            request,
                            token => Task.FromResult<Dokpod.Agent.Application.Terminal.IExecChannel>(new Dokpod.Agent.Infrastructure.Protocol.GrpcExecChannel(openExec(token))),
                            handshake,
                            sessionCancellation.Token)),
                        CancellationToken.None);
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
        Func<ulong> getImageRevision,
        Func<ulong> getVolumeRevision,
        Func<ulong> getNetworkRevision,
        Func<ulong> getPodRevision,
        TimeSpan interval,
        CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await messages.WriteAsync(
                new AgentMessage
                {
                    Heartbeat = new Heartbeat { InventoryRevision = getInventoryRevision(), ImageRevision = getImageRevision(), VolumeRevision = getVolumeRevision(), NetworkRevision = getNetworkRevision(), PodRevision = getPodRevision() },
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
        Volatile.Write(ref latestContainers, containers);
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
            ImageId = container.ImageId ?? string.Empty,
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
        ImagePublisher images,
        VolumePublisher volumes,
        NetworkPublisher networks,
        PodPublisher pods,
        bool sendEvents,
        CancellationToken cancellationToken)
    {
        var publisher = new EngineEventPublisher(TimeProvider.System);
        long dirtySince = 0;
        long imagesDirtySince = 0;
        long volumesDirtySince = 0;
        long networksDirtySince = 0;
        long podsDirtySince = 0;
        long podsFirstDirty = 0;
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
                    // Montagens mudam com containers criados ou removidos: o snapshot de volumes só sai se o conjunto mudou.
                    await volumes.SendAsync(force: false, cancellationToken);
                    await networks.SendAsync(force: false, cancellationToken);
                }

                // Pods: 2 s sem eventos ou, com eventos contínuos, no máximo 5 s após a primeira mudança.
                var podsSince = Interlocked.Read(ref podsDirtySince);
                var podsFirst = Interlocked.Read(ref podsFirstDirty);
                if (podsSince != 0 &&
                    (Environment.TickCount64 - podsSince >= 2000 || Environment.TickCount64 - podsFirst >= 5000) &&
                    Interlocked.CompareExchange(ref podsDirtySince, 0, podsSince) == podsSince)
                {
                    Interlocked.Exchange(ref podsFirstDirty, 0);
                    await pods.SendAsync(force: false, cancellationToken);
                }

                var networksSince = Interlocked.Read(ref networksDirtySince);
                if (networksSince != 0 && Environment.TickCount64 - networksSince >= 2000 &&
                    Interlocked.CompareExchange(ref networksDirtySince, 0, networksSince) == networksSince)
                {
                    await networks.SendAsync(force: false, cancellationToken);
                }

                var volumesSince = Interlocked.Read(ref volumesDirtySince);
                if (volumesSince != 0 && Environment.TickCount64 - volumesSince >= 2000 &&
                    Interlocked.CompareExchange(ref volumesDirtySince, 0, volumesSince) == volumesSince)
                {
                    await volumes.SendAsync(force: false, cancellationToken);
                }

                var imagesSince = Interlocked.Read(ref imagesDirtySince);
                if (imagesSince != 0 && Environment.TickCount64 - imagesSince >= 2000 &&
                    Interlocked.CompareExchange(ref imagesDirtySince, 0, imagesSince) == imagesSince)
                {
                    await images.SendAsync(force: false, cancellationToken);
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

                    if (record.Type == "image")
                    {
                        Interlocked.Exchange(ref imagesDirtySince, Environment.TickCount64);
                    }

                    if (record.Type == "volume")
                    {
                        Interlocked.Exchange(ref volumesDirtySince, Environment.TickCount64);
                    }

                    // A situação do pod deriva dos containers: eventos de pod e de container republicam os pods. O health check
                    // não muda a situação e chega a cada poucos segundos: contá-lo adiaria o envio indefinidamente.
                    if (record.Type is "pod" or "container" && !record.Action.StartsWith("health_status", StringComparison.Ordinal))
                    {
                        Interlocked.CompareExchange(ref podsFirstDirty, Environment.TickCount64, 0);
                        Interlocked.Exchange(ref podsDirtySince, Environment.TickCount64);
                    }

                    if (record.Type == "network")
                    {
                        // Conectar/desconectar muda as conexões dos containers: o inventário é relido antes do snapshot de redes.
                        Interlocked.Exchange(ref dirtySince, Environment.TickCount64);
                        Interlocked.Exchange(ref networksDirtySince, Environment.TickCount64);
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
    private int orphansChecked;

    /// <summary>Spec 014 D-007: uma vez por processo, encerra e reporta execs de terminal deixados por um reinício.</summary>
    private async Task ReportOrphanExecsAsync(ChannelWriter<AgentMessage> messages, bool eventsAccepted, CancellationToken cancellationToken)
    {
        if (execSessions is null || Interlocked.Exchange(ref orphansChecked, 1) == 1) return;
        try
        {
            var orphans = await execSessions.RecoverOrphansAsync(cancellationToken);
            if (orphans.Count == 0) return;
            logger.LogWarning("{OrphanCount} terminal exec sessions still running after agent restart", orphans.Count);
            if (!eventsAccepted) return;
            var batch = new EngineEventBatch();
            batch.Events.AddRange(orphans.Select(orphan => new EngineEvent
            {
                SourceKey = $"exec:{orphan.ExecSessionId}",
                Type = EngineEventType.ExecOrphanSuspected,
                OccurredAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
                SubjectId = orphan.ContainerId,
            }));
            await messages.WriteAsync(new AgentMessage { EngineEvents = batch }, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            logger.LogWarning(exception, "Terminal orphan check failed");
        }
    }

    private Task<IReadOnlyList<EngineImage>> ListImagesAsync(CancellationToken cancellationToken) => engine.ListImagesAsync(cancellationToken);

    private void LogImageInventoryUnavailable(Exception exception) => logger.LogWarning(exception, "Engine image inventory unavailable");

    /// <summary>Snapshot do inventário de imagens quando o conjunto muda (spec 012); revisão vai no heartbeat.</summary>
    private sealed class ImagePublisher(AgentControlWorker worker, ChannelWriter<AgentMessage> messages, bool enabled)
    {
        private readonly SemaphoreSlim gate = new(1, 1);
        private readonly Dokpod.Agent.Application.Images.ImageInventoryBuilder builder = new(TimeProvider.System);

        public ulong Revision => enabled ? builder.Revision : 0;

        public async Task SendAsync(bool force, CancellationToken cancellationToken)
        {
            if (!enabled) return;
            await gate.WaitAsync(cancellationToken);
            try
            {
                IReadOnlyList<EngineImage> list;
                try
                {
                    list = await worker.ListImagesAsync(cancellationToken);
                }
                catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException or System.Text.Json.JsonException)
                {
                    worker.LogImageInventoryUnavailable(exception);
                    return;
                }

                foreach (var page in builder.Build(list, force))
                {
                    await messages.WriteAsync(new AgentMessage { ImageSnapshotPage = page }, cancellationToken);
                }
            }
            finally
            {
                gate.Release();
            }
        }
    }

    private Task<IReadOnlyList<EngineVolume>> ListVolumesAsync(CancellationToken cancellationToken) => engine.ListVolumesAsync(cancellationToken);

    private void LogVolumeInventoryUnavailable(Exception exception) => logger.LogWarning(exception, "Engine volume inventory unavailable");

    /// <summary>Snapshot do inventário de volumes quando o conjunto ou as montagens mudam (spec 018); revisão vai no heartbeat.</summary>
    private sealed class VolumePublisher(AgentControlWorker worker, ChannelWriter<AgentMessage> messages, bool enabled)
    {
        private readonly SemaphoreSlim gate = new(1, 1);
        private readonly Dokpod.Agent.Application.Volumes.VolumeInventoryBuilder builder = new(TimeProvider.System);

        public ulong Revision => enabled ? builder.Revision : 0;

        public async Task SendAsync(bool force, CancellationToken cancellationToken)
        {
            if (!enabled) return;
            await gate.WaitAsync(cancellationToken);
            try
            {
                IReadOnlyList<EngineVolume> list;
                try
                {
                    list = await worker.ListVolumesAsync(cancellationToken);
                }
                catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException or System.Text.Json.JsonException)
                {
                    worker.LogVolumeInventoryUnavailable(exception);
                    return;
                }

                foreach (var page in builder.Build(list, Volatile.Read(ref worker.latestContainers), force))
                {
                    await messages.WriteAsync(new AgentMessage { VolumeSnapshotPage = page }, cancellationToken);
                }
            }
            finally
            {
                gate.Release();
            }
        }
    }

    private Task<IReadOnlyList<EngineNetwork>> ListNetworksAsync(CancellationToken cancellationToken) => engine.ListNetworksAsync(cancellationToken);

    private Task<IReadOnlyList<EnginePod>?> ListPodsAsync(CancellationToken cancellationToken) => engine.ListPodsAsync(cancellationToken);

    private void LogPodInventoryUnavailable(Exception exception) => logger.LogWarning(exception, "Engine pod inventory unavailable");

    /// <summary>Snapshot do inventário de pods quando o conjunto ou a situação dos membros muda (spec 021).</summary>
    private sealed class PodPublisher(AgentControlWorker worker, ChannelWriter<AgentMessage> messages, bool enabled)
    {
        private readonly SemaphoreSlim gate = new(1, 1);
        private readonly Dokpod.Agent.Application.Pods.PodInventoryBuilder builder = new(TimeProvider.System, worker.self);

        public ulong Revision => enabled ? builder.Revision : 0;

        public async Task SendAsync(bool force, CancellationToken cancellationToken)
        {
            if (!enabled) return;
            await gate.WaitAsync(cancellationToken);
            try
            {
                IReadOnlyList<EnginePod>? list;
                try
                {
                    list = await worker.ListPodsAsync(cancellationToken);
                }
                catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException or System.Text.Json.JsonException)
                {
                    worker.LogPodInventoryUnavailable(exception);
                    return;
                }

                foreach (var page in builder.Build(list, force))
                {
                    await messages.WriteAsync(new AgentMessage { PodSnapshotPage = page }, cancellationToken);
                }
            }
            finally
            {
                gate.Release();
            }
        }
    }

    private Task<EngineRegistryConfig> GetRegistryConfigAsync(CancellationToken cancellationToken) => engine.GetRegistryConfigAsync(cancellationToken);

    private void LogRegistryInventoryUnavailable(Exception exception) => logger.LogWarning(exception, "Engine registry configuration unavailable");

    /// <summary>Configuração de registros do host (spec 020): enviada quando muda; credenciais só como indicação.</summary>
    private sealed class RegistryPublisher(AgentControlWorker worker, ChannelWriter<AgentMessage> messages, bool enabled)
    {
        private readonly SemaphoreSlim gate = new(1, 1);
        private readonly Dokpod.Agent.Application.Registries.RegistryInventoryBuilder builder = new(TimeProvider.System);

        public async Task SendAsync(bool force, CancellationToken cancellationToken)
        {
            if (!enabled) return;
            await gate.WaitAsync(cancellationToken);
            try
            {
                EngineRegistryConfig config;
                try
                {
                    config = await worker.GetRegistryConfigAsync(cancellationToken);
                }
                catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException or System.Text.Json.JsonException)
                {
                    worker.LogRegistryInventoryUnavailable(exception);
                    return;
                }

                if (builder.Build(config, force) is { } inventory)
                {
                    await messages.WriteAsync(new AgentMessage { RegistryInventory = inventory }, cancellationToken);
                }
            }
            finally
            {
                gate.Release();
            }
        }
    }

    private void LogNetworkInventoryUnavailable(Exception exception) => logger.LogWarning(exception, "Engine network inventory unavailable");

    /// <summary>Snapshot do inventário de redes quando o conjunto ou as conexões mudam (spec 019); revisão vai no heartbeat.</summary>
    private sealed class NetworkPublisher(AgentControlWorker worker, ChannelWriter<AgentMessage> messages, bool enabled)
    {
        private readonly SemaphoreSlim gate = new(1, 1);
        private readonly Dokpod.Agent.Application.Networks.NetworkInventoryBuilder builder = new(TimeProvider.System);

        public ulong Revision => enabled ? builder.Revision : 0;

        public async Task SendAsync(bool force, CancellationToken cancellationToken)
        {
            if (!enabled) return;
            await gate.WaitAsync(cancellationToken);
            try
            {
                IReadOnlyList<EngineNetwork> list;
                try
                {
                    list = await worker.ListNetworksAsync(cancellationToken);
                }
                catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException or System.Text.Json.JsonException)
                {
                    worker.LogNetworkInventoryUnavailable(exception);
                    return;
                }

                foreach (var page in builder.Build(list, Volatile.Read(ref worker.latestContainers), force))
                {
                    await messages.WriteAsync(new AgentMessage { NetworkSnapshotPage = page }, cancellationToken);
                }
            }
            finally
            {
                gate.Release();
            }
        }
    }

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

    private static AgentMessage CreateHello(EngineDescriptor descriptor, EngineHostInfo? hostInfo, bool execAvailable)
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
                Capability.ImageInventory,
                Capability.ImagePull,
                Capability.VolumeInventory,
                Capability.VolumeRemove,
                Capability.NetworkInventory,
                Capability.NetworkRemove,
                Capability.RegistryInventory,
                Capability.PodInventory,
                Capability.PodActions,
            },
            SupportedProtocolVersions = { "1" },
        };
        // Spec 013 T011 / 014 T007: criação e terminal só no Linux (Windows sem qualificação própria).
        if (!System.OperatingSystem.IsWindows())
        {
            hello.Capabilities.Add(Capability.ContainerCreate);
            if (execAvailable) hello.Capabilities.Add(Capability.ContainerExec);
        }
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