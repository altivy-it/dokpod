using Dokpod.Agent.Application.Engines;
using Dokpod.Domain.Commands;
using System.Collections.Concurrent;

namespace Dokpod.Agent.Application.Commands;

public sealed class AgentCommandProcessor(
    AgentCommandGate gate,
    ICommandJournal journal,
    IContainerEngine engine,
    TimeProvider timeProvider,
    Dokpod.Agent.Application.Containers.ContainerCreator? creator = null)
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