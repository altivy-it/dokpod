using Dokpod.Agent.Contracts.V1;
using DomainCommand = Dokpod.Domain.Commands.AgentCommand;
using DomainCommandKind = Dokpod.Domain.Commands.AgentCommandKind;

namespace Dokpod.Agent.Application.Protocol;

public static class AgentCommandMapper
{
    public static bool TryMap(
        Guid environmentId,
        MessageMetadata metadata,
        Dokpod.Agent.Contracts.V1.AgentCommand message,
        out DomainCommand? command,
        out string failureCode)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(message);

        command = null;
        failureCode = string.Empty;

        if (environmentId == Guid.Empty || metadata.FencingToken == 0 || metadata.FencingToken > long.MaxValue)
        {
            failureCode = "command_metadata_invalid";
            return false;
        }

        if (!Guid.TryParse(message.CommandId, out var commandId))
        {
            failureCode = "command_id_invalid";
            return false;
        }

        var kind = MapKind(message.Kind);
        Dokpod.Domain.Commands.AgentCommandParameters? parameters = null;
        if (Dokpod.Domain.Commands.AgentCommandKinds.IsHostTarget(kind))
        {
            // Alvo de host (spec 012): sem container; referência revalidada no agente (defesa em profundidade).
            if (message.ContainerId.Length != 0 || message.ExpectedContainerRevision.Length != 0)
            {
                failureCode = "command_target_invalid";
                return false;
            }

            if (kind == DomainCommandKind.CreateContainer)
            {
                if (message.ParametersCase != Dokpod.Agent.Contracts.V1.AgentCommand.ParametersOneofCase.CreateContainer ||
                    Dokpod.Agent.Application.Containers.ContainerCreationMapper.FromMessage(message.CreateContainer, out failureCode) is not { } spec)
                {
                    failureCode = "invalid_spec";
                    return false;
                }

                parameters = new Dokpod.Domain.Commands.CreateContainerParameters(spec);
            }
            else if (kind == DomainCommandKind.RemoveVolume)
            {
                if (message.ParametersCase != Dokpod.Agent.Contracts.V1.AgentCommand.ParametersOneofCase.RemoveVolume ||
                    !Dokpod.Domain.Volumes.VolumeNames.IsValid(message.RemoveVolume.Name))
                {
                    failureCode = "volume_name_invalid";
                    return false;
                }

                parameters = new Dokpod.Domain.Commands.RemoveVolumeParameters(message.RemoveVolume.Name);
            }
            else if (kind == DomainCommandKind.RemoveNetwork)
            {
                if (message.ParametersCase != Dokpod.Agent.Contracts.V1.AgentCommand.ParametersOneofCase.RemoveNetwork ||
                    !Dokpod.Domain.Networks.NetworkNames.IsValidId(message.RemoveNetwork.NetworkId) ||
                    !Dokpod.Domain.Networks.NetworkNames.IsValidName(message.RemoveNetwork.Name))
                {
                    failureCode = "network_invalid";
                    return false;
                }

                parameters = new Dokpod.Domain.Commands.RemoveNetworkParameters(message.RemoveNetwork.NetworkId, message.RemoveNetwork.Name);
            }
            else if (message.ParametersCase != Dokpod.Agent.Contracts.V1.AgentCommand.ParametersOneofCase.PullImage ||
                !Dokpod.Domain.Images.ImageReference.TryParse(message.PullImage.Reference, out var reference))
            {
                failureCode = "invalid_reference";
                return false;
            }
            else
            {
                parameters = new Dokpod.Domain.Commands.PullImageParameters(reference);
            }
        }
        else
        {
            if (!IsImmutableContainerId(message.ContainerId))
            {
                failureCode = "container_id_invalid";
                return false;
            }

            if (string.IsNullOrWhiteSpace(message.ExpectedContainerRevision))
            {
                failureCode = "container_revision_invalid";
                return false;
            }
        }

        if (message.PayloadHash.Length != 32)
        {
            failureCode = "payload_hash_invalid";
            return false;
        }

        DateTimeOffset deadlineUtc;
        try
        {
            deadlineUtc = message.Deadline.ToDateTimeOffset();
        }
        catch (ArgumentException)
        {
            failureCode = "deadline_invalid";
            return false;
        }

        command = new DomainCommand(
            environmentId,
            commandId,
            kind,
            message.ContainerId,
            message.ExpectedContainerRevision,
            Convert.ToHexString(message.PayloadHash.Span),
            deadlineUtc,
            (long)metadata.FencingToken)
        {
            Parameters = parameters,
        };
        return true;
    }

    private static bool IsImmutableContainerId(string containerId) =>
        containerId.Length == 64 && containerId.All(Uri.IsHexDigit);

    private static DomainCommandKind MapKind(CommandKind kind) => kind switch
    {
        CommandKind.StartContainer => DomainCommandKind.StartContainer,
        CommandKind.StopContainer => DomainCommandKind.StopContainer,
        CommandKind.RestartContainer => DomainCommandKind.RestartContainer,
        CommandKind.DeleteContainer => DomainCommandKind.DeleteContainer,
        CommandKind.PauseContainer => DomainCommandKind.PauseContainer,
        CommandKind.UnpauseContainer => DomainCommandKind.UnpauseContainer,
        CommandKind.PullImage => DomainCommandKind.PullImage,
        CommandKind.CreateContainer => DomainCommandKind.CreateContainer,
        CommandKind.RemoveVolume => DomainCommandKind.RemoveVolume,
        CommandKind.RemoveNetwork => DomainCommandKind.RemoveNetwork,
        _ => DomainCommandKind.Unknown,
    };
}