using Dokpod.Agent.Application.Engines;
using Dokpod.Domain.Commands;
using System.Collections.Concurrent;

namespace Dokpod.Agent.Application.Commands;

public sealed class AgentCommandProcessor(
    AgentCommandGate gate,
    ICommandJournal journal,
    IContainerEngine engine,
    TimeProvider timeProvider,
    Dokpod.Agent.Application.Containers.ContainerCreator? creator = null,
    Dokpod.Agent.Application.Pods.ISelfContainer? self = null)
{
    private readonly ConcurrentDictionary<(Guid EnvironmentId, string ContainerId), SemaphoreSlim> mutationLocks = [];

    public async Task<JournaledCommandResult> ProcessAsync(
        AgentCommand command,
        long activeFencingToken,
        CancellationToken cancellationToken) =>
        await ProcessAsync(
            command,
            activeFencingToken,
            static (_, _, _) => Task.CompletedTask,
            cancellationToken);

    public async Task<JournaledCommandResult> ProcessAsync(
        AgentCommand command,
        long activeFencingToken,
        Func<CommandAdmission, string?, CancellationToken, Task> admissionCallback,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(admissionCallback);

        var admission = await gate.AdmitAsync(command, activeFencingToken, cancellationToken);
        var admissionFailureCode = admission is CommandAdmission.Accepted or CommandAdmission.Duplicate
            ? null
            : MapAdmissionFailure(admission);
        await admissionCallback(admission, admissionFailureCode, cancellationToken);

        if (admission == CommandAdmission.Duplicate)
        {
            var persistedResult = await journal.FindResultAsync(
                command.EnvironmentId,
                command.CommandId,
                cancellationToken);
            if (persistedResult is not null)
            {
                return persistedResult;
            }
        }

        if (admission is not (CommandAdmission.Accepted or CommandAdmission.Duplicate))
        {
            var rejection = CreateResult(
                command,
                CommandExecutionState.Failed,
                admissionFailureCode,
                null);
            if (admission != CommandAdmission.ConflictingPayload)
            {
                await journal.SaveResultAsync(rejection, CancellationToken.None);
            }

            return rejection;
        }

        var lockKey = command.Parameters switch
        {
            PullImageParameters pull => "image:" + pull.Reference.Name,
            CreateContainerParameters create => "create:" + create.Spec.Name,
            RemoveVolumeParameters volume => "volume:" + volume.Name,
            RemoveNetworkParameters network => "network:" + network.NetworkId,
            PodActionParameters pod => "pod:" + pod.PodId,
            _ => command.ContainerId,
        };
        var mutationLock = mutationLocks.GetOrAdd(
            (command.EnvironmentId, lockKey),
            static _ => new SemaphoreSlim(1, 1));
        await mutationLock.WaitAsync(cancellationToken);

        try
        {
            if (command.DeadlineUtc <= timeProvider.GetUtcNow())
            {
                return await SaveAsync(CreateResult(command, CommandExecutionState.Failed, "expired_command", null));
            }

            if (command.Kind == AgentCommandKind.PullImage)
            {
                // Pull é idempotente: um comando já aceito e sem resultado (queda após o aceite) é refeito (012 D-003).
                if (command.Parameters is not PullImageParameters parameters)
                {
                    return await SaveAsync(CreateResult(command, CommandExecutionState.Failed, "invalid_reference", null));
                }

                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                deadline.CancelAfter(command.DeadlineUtc - timeProvider.GetUtcNow());
                try
                {
                    var pulled = await engine.PullImageAsync(parameters.Reference, deadline.Token);
                    return await SaveAsync(CreateResult(
                        command,
                        pulled.Succeeded ? CommandExecutionState.Succeeded : CommandExecutionState.Failed,
                        pulled.FailureCode,
                        null) with
                    {
                        Output = pulled.Succeeded ? new AgentCommandOutput(pulled.ResolvedDigest, pulled.SizeBytes) : null,
                    });
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    return await SaveAsync(CreateResult(command, CommandExecutionState.Failed, "deadline_exceeded", null));
                }
                catch (Exception exception) when (exception is HttpRequestException or IOException)
                {
                    return await SaveAsync(CreateResult(command, CommandExecutionState.Failed, "engine_unavailable", null));
                }
            }

            if (command.Kind == AgentCommandKind.RemoveVolume)
            {
                if (command.Parameters is not RemoveVolumeParameters volume)
                {
                    return await SaveAsync(CreateResult(command, CommandExecutionState.Failed, "volume_name_invalid", null));
                }

                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                deadline.CancelAfter(command.DeadlineUtc - timeProvider.GetUtcNow());
                try
                {
                    // Spec 018 D-005: confere o uso imediatamente antes (inclusive containers parados) e nunca força.
                    var containers = await engine.ListContainersAsync(deadline.Token);
                    if (containers.Any(container => container.Mounts.Any(mount =>
                            string.Equals(mount.Type, "volume", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(mount.Name, volume.Name, StringComparison.Ordinal))))
                    {
                        return await SaveAsync(CreateResult(command, CommandExecutionState.Failed, Dokpod.Domain.Volumes.VolumeRemovalCodes.InUse, null));
                    }

                    var failure = await engine.RemoveVolumeAsync(volume.Name, deadline.Token);
                    return await SaveAsync(CreateResult(
                        command,
                        failure is null ? CommandExecutionState.Succeeded : CommandExecutionState.Failed,
                        failure,
                        null));
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    return await SaveAsync(CreateResult(command, CommandExecutionState.Indeterminate, "deadline_exceeded", null));
                }
                catch (Exception exception) when (exception is HttpRequestException or IOException or System.Text.Json.JsonException or InvalidDataException)
                {
                    return await SaveAsync(CreateResult(command, CommandExecutionState.Indeterminate, "engine_unavailable", null));
                }
            }

            if (command.Kind == AgentCommandKind.PodAction)
            {
                if (command.Parameters is not PodActionParameters pod)
                {
                    return await SaveAsync(CreateResult(command, CommandExecutionState.Failed, "pod_invalid", null));
                }

                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                deadline.CancelAfter(command.DeadlineUtc - timeProvider.GetUtcNow());
                try
                {
                    var pods = await engine.ListPodsAsync(deadline.Token);
                    if (pods is null) return await SaveAsync(CreateResult(command, CommandExecutionState.Failed, "not_supported", null));
                    var current = pods.FirstOrDefault(p => p.PodId == pod.PodId);
                    if (current is null) return await SaveAsync(CreateResult(command, CommandExecutionState.Failed, Dokpod.Domain.Pods.PodActionCodes.NotFound, null));
                    // Spec 021 FR-005: o pod do próprio agente não é parado, pausado nem reiniciado (o host ficaria sem agente).
                    if (Dokpod.Domain.Pods.PodNames.RefusedForAgentPod(pod.Action) &&
                        Dokpod.Agent.Application.Pods.PodStates.IsAgentPod(current, self ?? new Dokpod.Agent.Application.Pods.NoSelfContainer()))
                    {
                        return await SaveAsync(CreateResult(command, CommandExecutionState.Failed, Dokpod.Domain.Pods.PodActionCodes.AgentPod, null));
                    }

                    var failure = await engine.PodActionAsync(pod.PodId, pod.Action, deadline.Token);
                    if (failure == Dokpod.Domain.Pods.PodActionCodes.NotFound) return await SaveAsync(CreateResult(command, CommandExecutionState.Failed, failure, null));
                    // Releitura: membros fora do estado esperado indicam falha parcial (FR-008).
                    var after = (await engine.ListPodsAsync(deadline.Token))?.FirstOrDefault(p => p.PodId == pod.PodId);
                    var failed = after is null ? [] : Dokpod.Agent.Application.Pods.PodStates.Failed(after, pod.Action);
                    // 409 com todos os membros no estado esperado na releitura conta como sucesso (o engine convergiu).
                    var code = failed.Count > 0 ? Dokpod.Domain.Pods.PodActionCodes.PartialFailure
                        : failure == Dokpod.Domain.Pods.PodActionCodes.PartialFailure ? null : failure;
                    return await SaveAsync(CreateResult(command, code is null ? CommandExecutionState.Succeeded : CommandExecutionState.Failed, code, null) with
                    {
                        Output = failed.Count > 0 ? new AgentCommandOutput { FailedContainers = failed } : null,
                    });
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    return await SaveAsync(CreateResult(command, CommandExecutionState.Indeterminate, "deadline_exceeded", null));
                }
                catch (Exception exception) when (exception is HttpRequestException or IOException or System.Text.Json.JsonException or InvalidDataException)
                {
                    return await SaveAsync(CreateResult(command, CommandExecutionState.Indeterminate, "engine_unavailable", null));
                }
            }

            if (command.Kind == AgentCommandKind.RemoveNetwork)
            {
                if (command.Parameters is not RemoveNetworkParameters network)
                {
                    return await SaveAsync(CreateResult(command, CommandExecutionState.Failed, "network_invalid", null));
                }

                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                deadline.CancelAfter(command.DeadlineUtc - timeProvider.GetUtcNow());
                try
                {
                    // Spec 019 D-004: confere sistema, escopo e uso (inclusive containers parados) imediatamente antes; nunca desconecta.
                    var current = (await engine.ListNetworksAsync(deadline.Token)).FirstOrDefault(n => n.NetworkId == network.NetworkId);
                    var refusal = current switch
                    {
                        null => Dokpod.Domain.Networks.NetworkRemovalCodes.NotFound,
                        _ when Dokpod.Domain.Networks.NetworkNames.IsSystem(current.Name, current.Ingress) => Dokpod.Domain.Networks.NetworkRemovalCodes.System,
                        _ when !string.Equals(current.Scope, "local", StringComparison.OrdinalIgnoreCase) => Dokpod.Domain.Networks.NetworkRemovalCodes.NotLocal,
                        _ => null,
                    };
                    if (refusal is null)
                    {
                        var containers = await engine.ListContainersAsync(deadline.Token);
                        if (Dokpod.Agent.Application.Networks.NetworkInventoryBuilder.ConnectedTo(current!, containers).Any())
                        {
                            refusal = Dokpod.Domain.Networks.NetworkRemovalCodes.InUse;
                        }
                    }

                    if (refusal is not null) return await SaveAsync(CreateResult(command, CommandExecutionState.Failed, refusal, null));
                    var failure = await engine.RemoveNetworkAsync(network.NetworkId, deadline.Token);
                    return await SaveAsync(CreateResult(
                        command,
                        failure is null ? CommandExecutionState.Succeeded : CommandExecutionState.Failed,
                        failure,
                        null));
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    return await SaveAsync(CreateResult(command, CommandExecutionState.Indeterminate, "deadline_exceeded", null));
                }
                catch (Exception exception) when (exception is HttpRequestException or IOException or System.Text.Json.JsonException or InvalidDataException)
                {
                    return await SaveAsync(CreateResult(command, CommandExecutionState.Indeterminate, "engine_unavailable", null));
                }
            }

            if (command.Kind == AgentCommandKind.CreateContainer)
            {
                if (command.Parameters is not CreateContainerParameters create || creator is null)
                {
                    return await SaveAsync(CreateResult(command, CommandExecutionState.Failed, creator is null ? "unsupported_command" : "invalid_spec", null));
                }

                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                deadline.CancelAfter(command.DeadlineUtc - timeProvider.GetUtcNow());
                try
                {
                    var (state, failure, containerId) = await creator.CreateAsync(command, create.Spec, deadline.Token);
                    return await SaveAsync(CreateResult(command, state, failure, null) with
                    {
                        Output = containerId is null ? null : new AgentCommandOutput(CreatedContainerId: containerId),
                    });
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    return await SaveAsync(CreateResult(command, CommandExecutionState.Indeterminate, "deadline_exceeded", null));
                }
                catch (Exception exception) when (exception is HttpRequestException or IOException or System.Text.Json.JsonException or InvalidDataException)
                {
                    // Efeito desconhecido: indeterminado; o label `dokpod.command-id` permite reconciliar no host.
                    var unknown = CreateResult(command, CommandExecutionState.Indeterminate, "engine_result_unknown", null);
                    await journal.SaveResultAsync(unknown, CancellationToken.None);
                    return unknown;
                }
            }

            var container = await engine.InspectContainerAsync(command.ContainerId, cancellationToken);
            if (container is null)
            {
                return await SaveAsync(CreateResult(command, CommandExecutionState.Failed, "target_not_found", null));
            }

            if (!string.Equals(container.Revision, command.ExpectedContainerRevision, StringComparison.Ordinal))
            {
                return await SaveAsync(CreateResult(command, CommandExecutionState.Failed, "stale_target", container.Revision));
            }

            try
            {
                var mutation = await engine.ExecuteAsync(command.Kind, command.ContainerId, cancellationToken);
                return await SaveAsync(CreateResult(
                    command,
                    mutation.Succeeded ? CommandExecutionState.Succeeded : CommandExecutionState.Failed,
                    mutation.FailureCode,
                    container.Revision));
            }
            catch (Exception exception) when (exception is not ArgumentException)
            {
                var result = CreateResult(
                    command,
                    CommandExecutionState.Indeterminate,
                    "engine_result_unknown",
                    container.Revision);
                await journal.SaveResultAsync(result, CancellationToken.None);
                return result;
            }
        }
        finally
        {
            mutationLock.Release();
        }

        async Task<JournaledCommandResult> SaveAsync(JournaledCommandResult result)
        {
            await journal.SaveResultAsync(result, CancellationToken.None);
            return result;
        }
    }

    private JournaledCommandResult CreateResult(
        AgentCommand command,
        CommandExecutionState state,
        string? failureCode,
        string? observedRevision) =>
        new(
            command.EnvironmentId,
            command.CommandId,
            state,
            failureCode,
            observedRevision,
            timeProvider.GetUtcNow());

    private static string MapAdmissionFailure(CommandAdmission admission) => admission switch
    {
        CommandAdmission.Expired => "expired_command",
        CommandAdmission.StaleSession => "stale_session",
        CommandAdmission.ConflictingPayload => "conflicting_payload",
        CommandAdmission.Unsupported => "unsupported_command",
        _ => "command_rejected",
    };
}