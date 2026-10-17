using System.Security.Cryptography.X509Certificates;
using Dokpod.Agent.Contracts.V1;
using Dokpod.ControlPlane.Application.Agents;
using Dokpod.ControlPlane.Application.Commands;
using Dokpod.ControlPlane.Application.Inventory;
using Dokpod.ControlPlane.Application.Realtime;
using Dokpod.ControlPlane.Api.Realtime;
using Dokpod.Domain.Inventory;
using Google.Protobuf.WellKnownTypes;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Dokpod.ControlPlane.Api.Agents;

public sealed class AgentControlService(
    AgentSessionNegotiator negotiator,
    IAgentSessionStore sessionStore,
    IAgentCommandDeliveryQueue commandDeliveryQueue,
    IAgentCommandStore commandStore,
    AgentCommandStatusService commandStatusService,
    InventoryProjectionService inventoryProjection,
    IHubContext<ControlPlaneHub> hubContext,
    AgentHostActivity hostActivity,
    IControlPlaneNotifier notifier,
    Dokpod.ControlPlane.Application.Queries.AgentQueryDispatcher queryDispatcher,
    Dokpod.ControlPlane.Application.Metrics.MetricsIngestionService metricsIngestion,
    Dokpod.ControlPlane.Application.Images.IImageInventoryStore imageStore,
    ILogger<AgentControlService> logger,
    Dokpod.ControlPlane.Application.Volumes.IVolumeInventoryStore? volumeStore = null,
    Dokpod.ControlPlane.Application.Networks.INetworkInventoryStore? networkStore = null,
    Dokpod.ControlPlane.Application.Registries.IRegistryInventoryStore? registryStore = null,
    Dokpod.ControlPlane.Application.Pods.IPodInventoryStore? podStore = null,
    Dokpod.ControlPlane.Application.Terminal.TerminalSessionRegistry? terminals = null,
    IAgentIdentityRegistry? identities = null) : AgentControl.AgentControlBase
{
    private const int MaximumMessageBytes = 1_048_576;
    private static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(90);

    public override async Task Connect(
        IAsyncStreamReader<AgentMessage> requestStream,
        IServerStreamWriter<ControlPlaneMessage> responseStream,
        ServerCallContext context)
    {
        logger.LogInformation("Agent control connection received");
        if (!await requestStream.MoveNext(context.CancellationToken) ||
            requestStream.Current.PayloadCase != AgentMessage.PayloadOneofCase.Hello)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "agent_hello_required"));
        }

        var certificate = await context.GetHttpContext().Connection.GetClientCertificateAsync(
            context.CancellationToken);
        if (certificate is null)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "client_certificate_required"));
        }

        AgentSession session;
        var hello = requestStream.Current.Hello;
        try
        {
            session = await negotiator.NegotiateAsync(
                certificate,
                hello,
                context.CancellationToken);
        }
        catch (AgentSessionRejectedException exception)
        {
            logger.LogWarning("Agent control negotiation rejected with {FailureCode}", exception.FailureCode);
            throw new RpcException(new Status(StatusCode.PermissionDenied, exception.FailureCode));
        }

        logger.LogInformation(
            "Agent control session negotiated for environment {EnvironmentId}",
            session.EnvironmentId);

        var invalidated = sessionStore.WaitUntilInactiveAsync(session, CancellationToken.None);
        using var sessionCancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        try
        {
            if (!sessionStore.IsActive(session))
            {
                throw new RpcException(new Status(StatusCode.Aborted, "agent_session_fenced"));
            }

            await responseStream.WriteAsync(new ControlPlaneMessage
            {
                Metadata = new MessageMetadata
                {
                    ProtocolVersion = AgentSessionNegotiator.ProtocolVersion,
                    SessionId = session.SessionId.ToString("D"),
                    FencingToken = session.FencingToken,
                    Sequence = 1,
                    OccurredAt = Timestamp.FromDateTime(DateTime.UtcNow),
                    CorrelationId = Guid.NewGuid().ToString("D"),
                },
                SessionEstablished = new SessionEstablished
                {
                    SelectedProtocolVersion = AgentSessionNegotiator.ProtocolVersion,
                    SessionId = session.SessionId.ToString("D"),
                    FencingToken = session.FencingToken,
                    HeartbeatIntervalSeconds = 15,
                    MaximumMessageBytes = MaximumMessageBytes,
                    // Specs 018/019: sem projeção (sem banco), o servidor não confirma as capabilities de volume e de rede.
                    AcceptedCapabilities =
                    {
                        AgentHostActivity.Accept(hello).Where(capability =>
                            (volumeStore is not null || capability is not (Capability.VolumeInventory or Capability.VolumeRemove)) &&
                            (networkStore is not null || capability is not (Capability.NetworkInventory or Capability.NetworkRemove)) &&
                            (registryStore is not null || capability is not Capability.RegistryInventory) &&
                            (podStore is not null || capability is not (Capability.PodInventory or Capability.PodActions))),
                    },
                },
            });
            await hostActivity.OnSessionStartedAsync(session, hello, context.CancellationToken);
            var sessionOutbound = System.Threading.Channels.Channel.CreateBounded<ControlPlaneMessage>(64);
            using var queryRegistration = queryDispatcher.Register(
                session.EnvironmentId,
                session.SessionId,
                hello.Capabilities,
                sessionOutbound.Writer);
            Task<ControlPlaneMessage>? nextOutbound = null;

            ulong lastSequence = 0;
            ulong serverSequence = 1;
            var snapshotAccumulator = new InventorySnapshotAccumulator(session.EnvironmentId);
            var imageAccumulator = new Dokpod.ControlPlane.Application.Images.ImageSnapshotAccumulator(session.EnvironmentId, TimeProvider.System);
            var imagesAccepted = hello.Capabilities.Contains(Capability.ImageInventory);
            ulong appliedImageRevision = 0;
            var lastImageSnapshotRequest = DateTimeOffset.MinValue;
            // Spec 018: inventário de volumes por snapshot, como as imagens.
            var volumeAccumulator = new Dokpod.ControlPlane.Application.Volumes.VolumeSnapshotAccumulator(session.EnvironmentId, TimeProvider.System);
            var volumesAccepted = volumeStore is not null && hello.Capabilities.Contains(Capability.VolumeInventory);
            ulong appliedVolumeRevision = 0;
            var lastVolumeSnapshotRequest = DateTimeOffset.MinValue;
            // Spec 019: inventário de redes por snapshot, como os volumes.
            var networkAccumulator = new Dokpod.ControlPlane.Application.Networks.NetworkSnapshotAccumulator(session.EnvironmentId, TimeProvider.System);
            var networksAccepted = networkStore is not null && hello.Capabilities.Contains(Capability.NetworkInventory);
            ulong appliedNetworkRevision = 0;
            var lastNetworkSnapshotRequest = DateTimeOffset.MinValue;
            // Spec 020: configuração de registros do host em uma mensagem (sem credenciais).
            var registriesAccepted = registryStore is not null && hello.Capabilities.Contains(Capability.RegistryInventory);
            // Spec 021: pods (só Podman; o snapshot informa quando o engine não tem pods).
            var podAccumulator = new Dokpod.ControlPlane.Application.Pods.PodSnapshotAccumulator(session.EnvironmentId, TimeProvider.System);
            var podsAccepted = podStore is not null && hello.Capabilities.Contains(Capability.PodInventory);
            ulong appliedPodRevision = 0;
            var lastPodSnapshotRequest = DateTimeOffset.MinValue;
            var recoveredCommands = new Queue<PersistedAgentCommand>(
                await commandStore.ClaimDispatchableAsync(
                    session.EnvironmentId,
                    (long)session.FencingToken,
                    DateTimeOffset.UtcNow,
                    context.CancellationToken));
            using var heartbeatTimeout = new CancellationTokenSource(HeartbeatTimeout);
            var heartbeatExpired = Task.Delay(Timeout.InfiniteTimeSpan, heartbeatTimeout.Token);
            var nextMessage = requestStream.MoveNext(sessionCancellation.Token);
            Task<PersistedAgentCommand>? nextCommand = null;
            while (true)
            {
                nextCommand ??= recoveredCommands.TryDequeue(out var recoveredCommand)
                    ? Task.FromResult(recoveredCommand)
                    : commandDeliveryQueue
                        .DequeueAsync(session.EnvironmentId, sessionCancellation.Token)
                        .AsTask();
                nextOutbound ??= sessionOutbound.Reader.ReadAsync(sessionCancellation.Token).AsTask();
                var completed = await Task.WhenAny(nextMessage, nextCommand, nextOutbound, invalidated, heartbeatExpired);
                if (completed == nextOutbound)
                {
                    var outgoing = await nextOutbound;
                    nextOutbound = null;
                    outgoing.Metadata = CreateServerMetadata(session, ++serverSequence);
                    await responseStream.WriteAsync(outgoing);
                    continue;
                }

                if (completed == invalidated)
                {
                    throw new RpcException(new Status(StatusCode.Aborted, "agent_session_fenced"));
                }

                if (completed == heartbeatExpired)
                {
                    throw new RpcException(new Status(StatusCode.DeadlineExceeded, "agent_heartbeat_timeout"));
                }

                if (completed == nextCommand)
                {
                    var command = await nextCommand;
                    nextCommand = null;
                    var nowUtc = DateTimeOffset.UtcNow;
                    if (command.Command.DeadlineUtc <= nowUtc)
                    {
                        await commandStore.ExpireNonTerminalAsync(nowUtc, context.CancellationToken);
                        continue;
                    }

                    if (command.Command.EnvironmentId != session.EnvironmentId ||
                        command.Command.FencingToken != (long)session.FencingToken)
                    {
                        continue;
                    }

                    if (command.State != ControlPlaneCommandState.Accepted)
                    {
                        await RequireStatusAppliedAsync(commandStatusService.MarkDispatchedAsync(
                            session.EnvironmentId,
                            command.Command.CommandId,
                            DateTimeOffset.UtcNow,
                            context.CancellationToken));
                    }
                    if (!AgentSupports(hello, command.Command.Kind))
                    {
                        // Regra de negociação (008 D-009): comando novo só vai a agente que anunciou a capability.
                        await commandStatusService.RecordAcceptanceAsync(
                            session.EnvironmentId,
                            command.Command.CommandId,
                            false,
                            "capability_not_supported",
                            DateTimeOffset.UtcNow,
                            context.CancellationToken);
                        continue;
                    }

                    if (command.Command.Parameters is Dokpod.Domain.Commands.CreateContainerParameters { Spec.ValuesAvailable: false } ||
                        (command.Command.Kind == Dokpod.Domain.Commands.AgentCommandKind.CreateContainer && command.Command.Parameters is null))
                    {
                        // Spec 013 D-002: valores purgados ou key ring indisponível; nunca cria container sem eles.
                        await commandStatusService.RecordResultAsync(
                            session.EnvironmentId,
                            command.Command.CommandId,
                            ControlPlaneCommandState.Failed,
                            "parameters_unavailable",
                            null,
                            DateTimeOffset.UtcNow,
                            context.CancellationToken);
                        continue;
                    }

                    await responseStream.WriteAsync(new ControlPlaneMessage
                    {
                        Metadata = CreateServerMetadata(session, ++serverSequence),
                        Command = MapCommand(command.Command),
                    });
                    continue;
                }

                if (!await nextMessage)
                {
                    break;
                }

                heartbeatTimeout.CancelAfter(HeartbeatTimeout);

                if (!sessionStore.IsActive(session))
                {
                    throw new RpcException(new Status(StatusCode.Aborted, "agent_session_fenced"));
                }

                var metadata = requestStream.Current.Metadata;
                if (metadata is null ||
                    metadata.ProtocolVersion != AgentSessionNegotiator.ProtocolVersion ||
                    metadata.SessionId != session.SessionId.ToString("D") ||
                    metadata.FencingToken != session.FencingToken ||
                    lastSequence == ulong.MaxValue ||
                    metadata.Sequence != lastSequence + 1 ||
                    string.IsNullOrWhiteSpace(metadata.CorrelationId) ||
                    metadata.CorrelationId.Length > 128 ||
                    !IsValidTimestamp(metadata.OccurredAt))
                {
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "agent_message_metadata_invalid"));
                }

                lastSequence = requestStream.Current.Metadata.Sequence;

                switch (requestStream.Current.PayloadCase)
                {
                    case AgentMessage.PayloadOneofCase.Heartbeat:
                        await hostActivity.OnHeartbeatAsync(
                            session.EnvironmentId,
                            requestStream.Current.Heartbeat.InventoryRevision,
                            context.CancellationToken);
                        // Spec 012 D-007: revisão de imagens divergente = snapshot perdido; pede só imagens (no máximo 1/min).
                        var imageRevision = requestStream.Current.Heartbeat.ImageRevision;
                        if (imagesAccepted && imageRevision != 0 && imageRevision != appliedImageRevision &&
                            DateTimeOffset.UtcNow - lastImageSnapshotRequest >= TimeSpan.FromMinutes(1))
                        {
                            lastImageSnapshotRequest = DateTimeOffset.UtcNow;
                            await responseStream.WriteAsync(new ControlPlaneMessage
                            {
                                Metadata = CreateServerMetadata(session, ++serverSequence),
                                SnapshotRequest = new SnapshotRequest { ReasonCode = "image_revision_mismatch", Scope = SnapshotScope.Images },
                            });
                        }

                        var volumeRevision = requestStream.Current.Heartbeat.VolumeRevision;
                        if (volumesAccepted && volumeRevision != 0 && volumeRevision != appliedVolumeRevision &&
                            DateTimeOffset.UtcNow - lastVolumeSnapshotRequest >= TimeSpan.FromMinutes(1))
                        {
                            lastVolumeSnapshotRequest = DateTimeOffset.UtcNow;
                            await responseStream.WriteAsync(new ControlPlaneMessage
                            {
                                Metadata = CreateServerMetadata(session, ++serverSequence),
                                SnapshotRequest = new SnapshotRequest { ReasonCode = "volume_revision_mismatch", Scope = SnapshotScope.Volumes },
                            });
                        }

                        var networkRevision = requestStream.Current.Heartbeat.NetworkRevision;
                        if (networksAccepted && networkRevision != 0 && networkRevision != appliedNetworkRevision &&
                            DateTimeOffset.UtcNow - lastNetworkSnapshotRequest >= TimeSpan.FromMinutes(1))
                        {
                            lastNetworkSnapshotRequest = DateTimeOffset.UtcNow;
                            await responseStream.WriteAsync(new ControlPlaneMessage
                            {
                                Metadata = CreateServerMetadata(session, ++serverSequence),
                                SnapshotRequest = new SnapshotRequest { ReasonCode = "network_revision_mismatch", Scope = SnapshotScope.Networks },
                            });
                        }

                        var podRevision = requestStream.Current.Heartbeat.PodRevision;
                        if (podsAccepted && podRevision != 0 && podRevision != appliedPodRevision &&
                            DateTimeOffset.UtcNow - lastPodSnapshotRequest >= TimeSpan.FromMinutes(1))
                        {
                            lastPodSnapshotRequest = DateTimeOffset.UtcNow;
                            await responseStream.WriteAsync(new ControlPlaneMessage
                            {
                                Metadata = CreateServerMetadata(session, ++serverSequence),
                                SnapshotRequest = new SnapshotRequest { ReasonCode = "pod_revision_mismatch", Scope = SnapshotScope.Pods },
                            });
                        }

                        break;

                    case AgentMessage.PayloadOneofCase.PodSnapshotPage when podsAccepted:
                        var podSnapshot = podAccumulator.AddPage(MapPodPage(requestStream.Current.PodSnapshotPage), out var podFailure);
                        if (podFailure is not null)
                        {
                            throw new RpcException(new Status(StatusCode.InvalidArgument, podFailure));
                        }

                        if (podSnapshot is not null && await podStore!.ReplaceSnapshotAsync(podSnapshot, context.CancellationToken))
                        {
                            appliedPodRevision = podSnapshot.Revision;
                            await notifier.NotifyAsync(session.EnvironmentId, InvalidationKinds.Pods, context.CancellationToken);
                        }

                        break;

                    case AgentMessage.PayloadOneofCase.RegistryInventory when registriesAccepted:
                        if (await registryStore!.ReplaceAsync(MapRegistryInventory(session.EnvironmentId, requestStream.Current.RegistryInventory), context.CancellationToken))
                        {
                            await notifier.NotifyAsync(session.EnvironmentId, InvalidationKinds.Registries, context.CancellationToken);
                        }

                        break;

                    case AgentMessage.PayloadOneofCase.NetworkSnapshotPage when networksAccepted:
                        var networkSnapshot = networkAccumulator.AddPage(MapNetworkPage(requestStream.Current.NetworkSnapshotPage), out var networkFailure);
                        if (networkFailure is not null)
                        {
                            throw new RpcException(new Status(StatusCode.InvalidArgument, networkFailure));
                        }

                        if (networkSnapshot is not null &&
                            await networkStore!.ReplaceSnapshotAsync(networkSnapshot, context.CancellationToken) == Dokpod.ControlPlane.Application.Networks.NetworkSnapshotOutcome.Accepted)
                        {
                            appliedNetworkRevision = networkSnapshot.Revision;
                            await notifier.NotifyAsync(session.EnvironmentId, InvalidationKinds.Networks, context.CancellationToken);
                        }

                        break;

                    case AgentMessage.PayloadOneofCase.VolumeSnapshotPage when volumesAccepted:
                        var volumeSnapshot = volumeAccumulator.AddPage(MapVolumePage(requestStream.Current.VolumeSnapshotPage), out var volumeFailure);
                        if (volumeFailure is not null)
                        {
                            throw new RpcException(new Status(StatusCode.InvalidArgument, volumeFailure));
                        }

                        if (volumeSnapshot is not null &&
                            await volumeStore!.ReplaceSnapshotAsync(volumeSnapshot, context.CancellationToken) == Dokpod.ControlPlane.Application.Volumes.VolumeSnapshotOutcome.Accepted)
                        {
                            appliedVolumeRevision = volumeSnapshot.Revision;
                            await notifier.NotifyAsync(session.EnvironmentId, InvalidationKinds.Volumes, context.CancellationToken);
                        }

                        break;

                    case AgentMessage.PayloadOneofCase.ImageSnapshotPage when imagesAccepted:
                        var imageSnapshot = imageAccumulator.AddPage(MapImagePage(requestStream.Current.ImageSnapshotPage), out var imageFailure);
                        if (imageFailure is not null)
                        {
                            throw new RpcException(new Status(StatusCode.InvalidArgument, imageFailure));
                        }

                        if (imageSnapshot is not null &&
                            await imageStore.ReplaceSnapshotAsync(imageSnapshot, context.CancellationToken) == Dokpod.ControlPlane.Application.Images.ImageSnapshotOutcome.Accepted)
                        {
                            appliedImageRevision = imageSnapshot.Revision;
                            await notifier.NotifyAsync(session.EnvironmentId, InvalidationKinds.Images, context.CancellationToken);
                        }

                        break;

                    case AgentMessage.PayloadOneofCase.ImageDelta when imagesAccepted:
                        // Este servidor projeta imagens só por snapshot; um delta vira pedido de snapshot de imagens.
                        await responseStream.WriteAsync(new ControlPlaneMessage
                        {
                            Metadata = CreateServerMetadata(session, ++serverSequence),
                            SnapshotRequest = new SnapshotRequest { ReasonCode = "image_snapshot_required", Scope = SnapshotScope.Images },
                        });
                        break;

                    case AgentMessage.PayloadOneofCase.ContainerQueryResponse
                        when hello.Capabilities.Contains(Capability.ContainerInspect) || hello.Capabilities.Contains(Capability.ContainerLogs) ||
                            hello.Capabilities.Contains(Capability.VolumeInventory):
                        queryDispatcher.OnResponse(session.EnvironmentId, requestStream.Current.ContainerQueryResponse);
                        break;

                    case AgentMessage.PayloadOneofCase.LogChunk
                        when hello.Capabilities.Contains(Capability.ContainerLogs):
                        queryDispatcher.OnLogChunk(session.EnvironmentId, requestStream.Current.LogChunk);
                        break;

                    case AgentMessage.PayloadOneofCase.EngineEvents
                        when hello.Capabilities.Contains(Capability.EngineEvents):
                        await hostActivity.OnEngineEventsAsync(
                            session.EnvironmentId,
                            requestStream.Current.EngineEvents,
                            context.CancellationToken);
                        break;

                    case AgentMessage.PayloadOneofCase.ResourceSamples
                        when hello.Capabilities.Contains(Capability.ResourceMetrics):
                        await metricsIngestion.IngestAsync(
                            session.EnvironmentId,
                            requestStream.Current.ResourceSamples,
                            context.CancellationToken);
                        break;

                    case AgentMessage.PayloadOneofCase.InventoryDelta:
                        var result = await inventoryProjection.ApplyDeltaAsync(
                            session.EnvironmentId,
                            MapDelta(requestStream.Current.InventoryDelta),
                            context.CancellationToken);
                        if (result.Outcome is InventoryReconciliationOutcome.StaleBase or InventoryReconciliationOutcome.SequenceGap)
                        {
                            await responseStream.WriteAsync(new ControlPlaneMessage
                            {
                                Metadata = CreateServerMetadata(session, ++serverSequence),
                                SnapshotRequest = new SnapshotRequest { ReasonCode = result.FailureCode ?? "inventory_reconciliation_required" },
                            });
                        }
                        else if (result.Outcome == InventoryReconciliationOutcome.InvalidDelta)
                        {
                            throw new RpcException(new Status(StatusCode.InvalidArgument, result.FailureCode ?? "inventory_delta_invalid"));
                        }
                        else
                        {
                            hostActivity.OnSnapshotApplied(session.EnvironmentId, result.Snapshot.Revision);
                            await NotifyInventoryChangedAsync(
                                session.EnvironmentId,
                                result.Snapshot.Revision,
                                context.CancellationToken);
                        }

                        break;

                    case AgentMessage.PayloadOneofCase.SnapshotPage:
                        var pageResult = snapshotAccumulator.AddPage(MapSnapshotPage(requestStream.Current.SnapshotPage));
                        if (pageResult.Outcome == InventorySnapshotPageOutcome.Invalid)
                        {
                            throw new RpcException(new Status(
                                StatusCode.InvalidArgument,
                                pageResult.FailureCode ?? "inventory_snapshot_invalid"));
                        }

                        if (pageResult.Outcome == InventorySnapshotPageOutcome.Completed)
                        {
                            var snapshotResult = await inventoryProjection.ReplaceSnapshotAsync(
                                pageResult.Snapshot!,
                                context.CancellationToken);
                            if (snapshotResult.Outcome != InventoryReconciliationOutcome.Accepted)
                            {
                                throw new RpcException(new Status(
                                    StatusCode.FailedPrecondition,
                                    snapshotResult.FailureCode ?? "inventory_snapshot_stale"));
                            }

                            hostActivity.OnSnapshotApplied(session.EnvironmentId, snapshotResult.Snapshot.Revision);
                            await NotifyInventoryChangedAsync(
                                session.EnvironmentId,
                                snapshotResult.Snapshot.Revision,
                                context.CancellationToken);
                        }

                        break;

                    case AgentMessage.PayloadOneofCase.CommandAccepted:
                        await HandleCommandAcceptedAsync(
                            session.EnvironmentId,
                            requestStream.Current.CommandAccepted,
                            metadata.OccurredAt.ToDateTimeOffset(),
                            context.CancellationToken);
                        break;

                    case AgentMessage.PayloadOneofCase.CommandResult:
                        await HandleCommandResultAsync(
                            session.EnvironmentId,
                            requestStream.Current.CommandResult,
                            context.CancellationToken);
                        if (Guid.TryParse(requestStream.Current.CommandResult.CommandId, out var completedCommandId))
                        {
                            await hostActivity.OnCommandResultAsync(
                                session.EnvironmentId,
                                completedCommandId,
                                context.CancellationToken);
                        }

                        break;

                    // Spec 023: mensagem de um agente mais novo (campo do oneof desconhecido neste servidor) é ignorada,
                    // sem derrubar a sessão; mensagens conhecidas fora da negociação continuam encerrando a sessão.
                    case AgentMessage.PayloadOneofCase.None:
                        logger.LogDebug("Ignored agent message with a payload unknown to this server");
                        break;

                    default:
                        throw new RpcException(new Status(StatusCode.Unimplemented, "agent_payload_not_supported"));
                }

                nextMessage = requestStream.MoveNext(sessionCancellation.Token);
            }
        }
        finally
        {
            await sessionCancellation.CancelAsync();
            var wasActive = sessionStore.IsActive(session);
            await sessionStore.DeactivateAsync(session, CancellationToken.None);
            var replaced = !wasActive &&
                await sessionStore.FindActiveAsync(session.EnvironmentId, CancellationToken.None) is not null;
            await hostActivity.OnSessionEndedAsync(session, replaced);
        }
    }

    /// <summary>
    /// Spec 014 D-001: canal do terminal aberto pelo agente. O primeiro frame vincula o RPC ao certificado, à sessão
    /// ativa (id e fencing) e a uma sessão de terminal pendente do mesmo ambiente; qualquer divergência encerra o RPC.
    /// </summary>
    public override async Task Exec(IAsyncStreamReader<ExecFrame> requestStream, IServerStreamWriter<ExecFrame> responseStream, ServerCallContext context)
    {
        if (terminals is null || identities is null) throw new RpcException(new Status(StatusCode.Unimplemented, "exec_not_supported"));
        var certificate = await context.GetHttpContext().Connection.GetClientCertificateAsync(context.CancellationToken);
        if (certificate is null) throw new RpcException(new Status(StatusCode.Unauthenticated, "client_certificate_required"));
        var fingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(certificate.RawData));
        var identity = await identities.FindByFingerprintAsync(fingerprint, context.CancellationToken)
            ?? throw new RpcException(new Status(StatusCode.PermissionDenied, "agent_certificate_unknown"));
        if (!await requestStream.MoveNext(context.CancellationToken) || requestStream.Current.FrameCase != ExecFrame.FrameOneofCase.Handshake)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "exec_handshake_required"));
        }

        var handshake = requestStream.Current.Handshake;
        var active = await sessionStore.FindActiveAsync(identity.EnvironmentId, context.CancellationToken);
        if (active is null || active.SessionId.ToString("D") != handshake.SessionId || active.FencingToken != handshake.FencingToken ||
            !Guid.TryParse(handshake.ExecSessionId, out var execSessionId))
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "exec_session_fenced"));
        }

        var session = terminals.Attach(execSessionId, identity.EnvironmentId)
            ?? throw new RpcException(new Status(StatusCode.NotFound, "exec_session_unknown"));
        using var end = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        var toAgent = Task.Run(async () =>
        {
            await foreach (var frame in session.ToAgent.Reader.ReadAllAsync(end.Token))
            {
                await responseStream.WriteAsync(frame, end.Token);
            }

            // A sessão terminou no plano de controle: encerra o RPC para o agente derrubar o shell mesmo sem `Closed`.
            await end.CancelAsync();
        }, CancellationToken.None);
        try
        {
            while (await requestStream.MoveNext(end.Token))
            {
                var frame = requestStream.Current;
                if (frame.FrameCase is ExecFrame.FrameOneofCase.Output or ExecFrame.FrameOneofCase.Status)
                {
                    await session.FromAgent.Writer.WriteAsync(frame, end.Token);
                }
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException or RpcException)
        {
        }
        finally
        {
            session.FromAgent.Writer.TryComplete();
            await end.CancelAsync();
            try
            {
                await toAgent;
            }
            catch (Exception exception) when (exception is OperationCanceledException or IOException or InvalidOperationException or System.Threading.Channels.ChannelClosedException)
            {
            }
        }
    }

    private static bool AgentSupports(AgentHello hello, Dokpod.Domain.Commands.AgentCommandKind kind) => kind switch
    {
        Dokpod.Domain.Commands.AgentCommandKind.PauseContainer => hello.Capabilities.Contains(Capability.ContainerPause),
        Dokpod.Domain.Commands.AgentCommandKind.UnpauseContainer => hello.Capabilities.Contains(Capability.ContainerUnpause),
        Dokpod.Domain.Commands.AgentCommandKind.PullImage => hello.Capabilities.Contains(Capability.ImagePull),
        Dokpod.Domain.Commands.AgentCommandKind.CreateContainer => hello.Capabilities.Contains(Capability.ContainerCreate),
        Dokpod.Domain.Commands.AgentCommandKind.RemoveVolume => hello.Capabilities.Contains(Capability.VolumeRemove),
        Dokpod.Domain.Commands.AgentCommandKind.RemoveNetwork => hello.Capabilities.Contains(Capability.NetworkRemove),
        Dokpod.Domain.Commands.AgentCommandKind.PodAction => hello.Capabilities.Contains(Capability.PodActions),
        _ => true,
    };

    private static bool IsContainerId(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static bool IsValidTimestamp(Timestamp? timestamp)
        => timestamp is not null &&
            timestamp.Seconds >= -62_135_596_800 &&
            timestamp.Seconds <= 253_402_300_799 &&
            timestamp.Nanos is >= 0 and <= 999_999_999;

    private static MessageMetadata CreateServerMetadata(AgentSession session, ulong sequence) => new()
    {
        ProtocolVersion = AgentSessionNegotiator.ProtocolVersion,
        SessionId = session.SessionId.ToString("D"),
        FencingToken = session.FencingToken,
        Sequence = sequence,
        OccurredAt = Timestamp.FromDateTime(DateTime.UtcNow),
        CorrelationId = Guid.NewGuid().ToString("D"),
    };

    private static Dokpod.Domain.Inventory.InventoryDelta MapDelta(
        Dokpod.Agent.Contracts.V1.InventoryDelta message)
    {
        if (message.InventoryRevision == 0)
        {
            return new Dokpod.Domain.Inventory.InventoryDelta(message.BaseRevision, message.InventoryRevision, []);
        }

        var changes = message.Changes.Select(change =>
        {
            var kind = change.Kind switch
            {
                ChangeKind.Upsert => InventoryChangeKind.Updated,
                ChangeKind.Remove => InventoryChangeKind.Removed,
                _ => (InventoryChangeKind?)null,
            };
            if (kind is null)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "inventory_change_kind_invalid"));
            }

            var containerId = string.IsNullOrWhiteSpace(change.ContainerId)
                ? change.Container?.ContainerId
                : change.ContainerId;
            if (string.IsNullOrWhiteSpace(containerId) ||
                (kind == InventoryChangeKind.Updated && change.Container is null))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "inventory_change_invalid"));
            }

            var container = change.Container is null
                ? null
                : MapContainerFields(containerId, change.Container);
            return new InventoryChange(kind.Value, containerId, container);
        }).ToArray();

        return new Dokpod.Domain.Inventory.InventoryDelta(message.BaseRevision, message.InventoryRevision, changes);
    }

    private static Dokpod.ControlPlane.Application.Images.ImageSnapshotPage MapImagePage(ImageSnapshotPage page) =>
        new(
            page.SnapshotId,
            page.ImageRevision,
            page.PageNumber,
            page.IsLastPage,
            page.Images.Select(image => new Dokpod.ControlPlane.Application.Images.ImageInventoryItem(
                image.ImageId,
                image.RepoTags.Take(64).Where(tag => tag.Length <= 255).ToArray(),
                image.RepoDigests.Take(64).Where(digest => digest.Length <= 255).ToArray(),
                (long)Math.Min(image.SizeBytes, long.MaxValue),
                IsValidTimestamp(image.CreatedAt) && image.CreatedAt.Seconds > 0 ? image.CreatedAt.ToDateTimeOffset() : null,
                image.Architecture,
                image.Os,
                image.Truncated || image.RepoTags.Count > 64 || image.RepoDigests.Count > 64)).ToArray());

    private static Dokpod.ControlPlane.Application.Volumes.VolumeSnapshotPage MapVolumePage(VolumeSnapshotPage page) =>
        new(
            page.SnapshotId,
            page.VolumeRevision,
            page.PageNumber,
            page.IsLastPage,
            page.Volumes.Select(volume => new Dokpod.ControlPlane.Application.Volumes.VolumeInventoryItem(
                volume.Name,
                Clip(volume.Driver),
                Clip(volume.Scope),
                IsValidTimestamp(volume.CreatedAt) && volume.CreatedAt.Seconds > 0 ? volume.CreatedAt.ToDateTimeOffset() : null,
                volume.Labels.Take(64).ToDictionary(label => Clip(label.Key), label => Clip(label.Value), StringComparer.Ordinal),
                volume.Mounts.Take(100)
                    .Where(mount => IsContainerId(mount.ContainerId))
                    .Select(mount => new Dokpod.ControlPlane.Application.Volumes.VolumeMount(mount.ContainerId, Clip(mount.ContainerName), Clip(mount.Destination), mount.ReadOnly))
                    .ToArray(),
                volume.Truncated || volume.Labels.Count > 64 || volume.Mounts.Count > 100)).ToArray());

    private static Dokpod.ControlPlane.Application.Networks.NetworkSnapshotPage MapNetworkPage(NetworkSnapshotPage page) =>
        new(
            page.SnapshotId,
            page.NetworkRevision,
            page.PageNumber,
            page.IsLastPage,
            page.Networks.Select(network => new Dokpod.ControlPlane.Application.Networks.NetworkInventoryItem(
                network.NetworkId,
                network.Name,
                Clip(network.Driver),
                Clip(network.Scope),
                network.Internal,
                network.Ipv6,
                network.Attachable,
                // O servidor também reconhece as redes do sistema pelo nome, sem depender só do agente.
                network.System || Dokpod.Domain.Networks.NetworkNames.SystemNames.Contains(network.Name),
                IsValidTimestamp(network.CreatedAt) && network.CreatedAt.Seconds > 0 ? network.CreatedAt.ToDateTimeOffset() : null,
                network.Subnets.Take(16).Select(subnet => new Dokpod.ControlPlane.Application.Networks.NetworkSubnet(
                    Clip(subnet.Subnet), subnet.Gateway.Length == 0 ? null : Clip(subnet.Gateway))).ToArray(),
                network.Labels.Take(64).ToDictionary(label => Clip(label.Key), label => Clip(label.Value), StringComparer.Ordinal),
                network.Containers.Take(200)
                    .Where(container => IsContainerId(container.ContainerId))
                    .Select(container => new Dokpod.ControlPlane.Application.Networks.NetworkConnection(
                        container.ContainerId,
                        Clip(container.ContainerName),
                        container.Ipv4Address.Length == 0 ? null : Clip(container.Ipv4Address),
                        container.Ipv6Address.Length == 0 ? null : Clip(container.Ipv6Address)))
                    .ToArray(),
                network.Truncated || network.Subnets.Count > 16 || network.Labels.Count > 64 || network.Containers.Count > 200)).ToArray());

    private static Dokpod.ControlPlane.Application.Pods.PodSnapshotPage MapPodPage(PodSnapshotPage page) =>
        new(
            page.SnapshotId,
            page.PodRevision,
            page.PageNumber,
            page.IsLastPage,
            !page.EngineWithoutPods,
            page.Pods.Select(pod => new Dokpod.ControlPlane.Application.Pods.PodInventoryItem(
                pod.PodId,
                pod.Name,
                Clip(pod.Status),
                IsValidTimestamp(pod.CreatedAt) && pod.CreatedAt.Seconds > 0 ? pod.CreatedAt.ToDateTimeOffset() : null,
                pod.Labels.Take(64).ToDictionary(label => Clip(label.Key), label => Clip(label.Value), StringComparer.Ordinal),
                pod.Networks.Take(16).Select(Clip).ToArray(),
                IsContainerId(pod.InfraId) ? pod.InfraId : null,
                pod.Containers.Take(100)
                    .Where(member => IsContainerId(member.ContainerId))
                    .Select(member => new Dokpod.ControlPlane.Application.Pods.PodMemberItem(member.ContainerId, Clip(member.Name), Clip(member.Status), (int)Math.Min(member.Restarts, int.MaxValue), member.Infra))
                    .ToArray(),
                pod.AgentPod,
                pod.Truncated || pod.Containers.Count > 100 || pod.Labels.Count > 64)).ToArray());

    /// <summary>Configuração de registros (spec 020): nomes fora da gramática são descartados; até 500 registros e 16 espelhos.</summary>
    private static Dokpod.ControlPlane.Application.Registries.RegistryInventorySnapshot MapRegistryInventory(Guid environmentId, RegistryInventory inventory)
    {
        if (inventory.Revision == 0) throw new RpcException(new Status(StatusCode.InvalidArgument, "registry_inventory_invalid"));
        static string? Valid(string value) => Dokpod.Domain.Images.RegistryNames.IsValid(value) ? value : null;
        var registries = inventory.Registries.Take(500)
            .Where(r => Valid(r.Registry) is not null)
            .DistinctBy(r => r.Registry, StringComparer.Ordinal)
            .Select(r => new Dokpod.ControlPlane.Application.Registries.RegistryHostConfig(
                r.Registry,
                r.Mirrors.Select(Valid).OfType<string>().Distinct(StringComparer.Ordinal).Take(16).ToArray(),
                Valid(r.MirrorOf),
                r.Insecure,
                r.Blocked,
                r.Search,
                r.Credential))
            .ToArray();
        return new Dokpod.ControlPlane.Application.Registries.RegistryInventorySnapshot(environmentId, inventory.Revision, inventory.ConfigAvailable, DateTimeOffset.UtcNow, registries);
    }

    private static string Clip(string value) => value.Length <= 255 ? value : value[..255];

    private static Dokpod.Agent.Contracts.V1.AgentCommand MapCommand(
        Dokpod.Domain.Commands.AgentCommand command)
    {
        var message = MapCommandEnvelope(command);
        if (command.Parameters is Dokpod.Domain.Commands.PullImageParameters pull)
        {
            message.PullImage = new Dokpod.Agent.Contracts.V1.PullImageParameters { Reference = pull.Reference.Canonical };
        }
        else if (command.Parameters is Dokpod.Domain.Commands.CreateContainerParameters create)
        {
            message.CreateContainer = ContainerCreationProtocol.ToMessage(create.Spec);
        }
        else if (command.Parameters is Dokpod.Domain.Commands.RemoveVolumeParameters volume)
        {
            message.RemoveVolume = new Dokpod.Agent.Contracts.V1.RemoveVolumeParameters { Name = volume.Name };
        }
        else if (command.Parameters is Dokpod.Domain.Commands.PodActionParameters pod)
        {
            message.PodAction = new Dokpod.Agent.Contracts.V1.PodActionParameters { PodId = pod.PodId, Name = pod.Name, Action = (PodAction)(int)pod.Action };
        }
        else if (command.Parameters is Dokpod.Domain.Commands.RemoveNetworkParameters network)
        {
            message.RemoveNetwork = new Dokpod.Agent.Contracts.V1.RemoveNetworkParameters { NetworkId = network.NetworkId, Name = network.Name };
        }

        return message;
    }

    private static Dokpod.Agent.Contracts.V1.AgentCommand MapCommandEnvelope(
        Dokpod.Domain.Commands.AgentCommand command) => new()
        {
            CommandId = command.CommandId.ToString("D"),
            Kind = command.Kind switch
            {
                Dokpod.Domain.Commands.AgentCommandKind.StartContainer => CommandKind.StartContainer,
                Dokpod.Domain.Commands.AgentCommandKind.StopContainer => CommandKind.StopContainer,
                Dokpod.Domain.Commands.AgentCommandKind.RestartContainer => CommandKind.RestartContainer,
                Dokpod.Domain.Commands.AgentCommandKind.DeleteContainer => CommandKind.DeleteContainer,
                Dokpod.Domain.Commands.AgentCommandKind.PauseContainer => CommandKind.PauseContainer,
                Dokpod.Domain.Commands.AgentCommandKind.UnpauseContainer => CommandKind.UnpauseContainer,
                Dokpod.Domain.Commands.AgentCommandKind.PullImage => CommandKind.PullImage,
                Dokpod.Domain.Commands.AgentCommandKind.CreateContainer => CommandKind.CreateContainer,
                Dokpod.Domain.Commands.AgentCommandKind.RemoveVolume => CommandKind.RemoveVolume,
                Dokpod.Domain.Commands.AgentCommandKind.RemoveNetwork => CommandKind.RemoveNetwork,
                Dokpod.Domain.Commands.AgentCommandKind.PodAction => CommandKind.PodAction,
                _ => CommandKind.Unspecified,
            },
            ContainerId = command.ContainerId,
            ExpectedContainerRevision = command.ExpectedContainerRevision,
            PayloadHash = ByteString.CopyFrom(Convert.FromHexString(command.PayloadHash)),
            Deadline = Timestamp.FromDateTimeOffset(command.DeadlineUtc),
        };

    private async Task HandleCommandAcceptedAsync(
        Guid environmentId,
        CommandAccepted message,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(message.CommandId, out var commandId) ||
            message.Acceptance is CommandAcceptance.Unspecified ||
            !System.Enum.IsDefined(message.Acceptance))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "command_acceptance_invalid"));
        }

        var accepted = message.Acceptance is CommandAcceptance.Accepted or CommandAcceptance.Duplicate;
        try
        {
            await RequireStatusAppliedAsync(commandStatusService.RecordAcceptanceAsync(
                environmentId,
                commandId,
                accepted,
                message.FailureCode,
                occurredAtUtc,
                cancellationToken));
        }
        catch (ArgumentException)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "command_acceptance_invalid"));
        }
    }

    private async Task HandleCommandResultAsync(
        Guid environmentId,
        CommandResult message,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(message.CommandId, out var commandId) ||
            !IsValidTimestamp(message.CompletedAt))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "command_result_invalid"));
        }

        var state = message.State switch
        {
            CommandResultState.Succeeded => ControlPlaneCommandState.Succeeded,
            CommandResultState.Failed => ControlPlaneCommandState.Failed,
            CommandResultState.Indeterminate => ControlPlaneCommandState.Indeterminate,
            _ => (ControlPlaneCommandState?)null,
        };
        if (state is null)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "command_result_invalid"));
        }

        try
        {
            await RequireStatusAppliedAsync(commandStatusService.RecordResultAsync(
                environmentId,
                commandId,
                state.Value,
                message.FailureCode,
                message.ObservedContainerRevision,
                message.CompletedAt.ToDateTimeOffset(),
                cancellationToken,
                string.IsNullOrEmpty(message.ResolvedImageDigest) && message.ImageSizeBytes == 0 && string.IsNullOrEmpty(message.CreatedContainerId) && message.FailedContainers.Count == 0
                    ? null
                    : new Dokpod.Domain.Commands.AgentCommandOutput(
                        string.IsNullOrEmpty(message.ResolvedImageDigest) ? null : message.ResolvedImageDigest,
                        message.ImageSizeBytes == 0 ? null : (long)Math.Min(message.ImageSizeBytes, long.MaxValue),
                        IsContainerId(message.CreatedContainerId) ? message.CreatedContainerId : null)
                    {
                        // Spec 021 FR-008: membros do pod que falharam (nomes fora da gramática são descartados).
                        FailedContainers = message.FailedContainers.Count == 0 ? null
                            : message.FailedContainers.Where(Dokpod.Domain.Pods.PodNames.IsValidName).Take(50).ToArray(),
                    }));
        }
        catch (ArgumentException)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "command_result_invalid"));
        }
    }

    private static async Task RequireStatusAppliedAsync(
        Task<AgentCommandStatusUpdateResult> update)
    {
        var result = await update;
        if (result is AgentCommandStatusUpdateResult.NotFound or AgentCommandStatusUpdateResult.InvalidTransition)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "command_status_rejected"));
        }
    }

    private static Dokpod.ControlPlane.Application.Inventory.InventorySnapshotPage MapSnapshotPage(
        Dokpod.Agent.Contracts.V1.InventorySnapshotPage message) =>
        new(
            message.SnapshotId,
            message.InventoryRevision,
            message.PageNumber,
            message.IsLastPage,
            message.Containers.Select(MapContainer).ToArray());

    private static ContainerInventory MapContainer(ContainerState container)
    {
        if (string.IsNullOrWhiteSpace(container.ContainerId) || !IsValidTimestamp(container.ObservedAt))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "inventory_container_invalid"));
        }

        return MapContainerFields(container.ContainerId, container);
    }

    private static ContainerInventory MapContainerFields(string containerId, ContainerState container) =>
        new(
            containerId,
            container.Name,
            container.ImageReference,
            container.State.ToString(),
            container.Revision,
            container.ObservedAt.ToDateTimeOffset())
        {
            CreatedAtUtc = IsValidTimestamp(container.CreatedAt) && container.CreatedAt.Seconds > 0 ? container.CreatedAt.ToDateTimeOffset() : null,
            StartedAtUtc = IsValidTimestamp(container.StartedAt) && container.StartedAt.Seconds > 0 ? container.StartedAt.ToDateTimeOffset() : null,
            Health = container.Health switch
            {
                Dokpod.Agent.Contracts.V1.ContainerHealth.None => Dokpod.Domain.Inventory.ContainerHealth.None,
                Dokpod.Agent.Contracts.V1.ContainerHealth.Starting => Dokpod.Domain.Inventory.ContainerHealth.Starting,
                Dokpod.Agent.Contracts.V1.ContainerHealth.Healthy => Dokpod.Domain.Inventory.ContainerHealth.Healthy,
                Dokpod.Agent.Contracts.V1.ContainerHealth.Unhealthy => Dokpod.Domain.Inventory.ContainerHealth.Unhealthy,
                _ => null,
            },
            RestartCount = container.RestartCount == 0 && container.CreatedAt is null ? null : (int)Math.Min(container.RestartCount, int.MaxValue),
            ExitCode = container.HasExitCode ? container.ExitCode : null,
            OomKilled = container.CreatedAt is null ? null : container.OomKilled,
            ImageId = string.IsNullOrEmpty(container.ImageId) ? null : container.ImageId,
            Ports = container.Ports
                .Take(ContainerPortBinding.MaxPerContainer)
                .Where(port => port.ContainerPort is > 0 and <= 65535 && port.HostPort <= 65535)
                .Select(port => new ContainerPortBinding(
                    string.IsNullOrWhiteSpace(port.HostIp) ? null : port.HostIp[..Math.Min(port.HostIp.Length, 64)],
                    (int)port.HostPort,
                    (int)port.ContainerPort,
                    port.Protocol switch
                    {
                        TransportProtocol.Udp => "udp",
                        TransportProtocol.Sctp => "sctp",
                        _ => "tcp",
                    }))
                .ToArray(),
        };

    private async Task NotifyInventoryChangedAsync(
        Guid environmentId,
        ulong revision,
        CancellationToken cancellationToken)
    {
        await notifier.NotifyAsync(environmentId, InvalidationKinds.Inventory, cancellationToken);
        await SendInventoryChangedAsync(environmentId, revision, cancellationToken);
    }

    private Task SendInventoryChangedAsync(
        Guid environmentId,
        ulong revision,
        CancellationToken cancellationToken) =>
        hubContext.Clients
            .Group(ControlPlaneHub.GroupFor(environmentId.ToString("D")))
            .SendAsync(
                "inventoryChanged",
                new InventoryChangedNotification(environmentId, revision),
                cancellationToken);
}