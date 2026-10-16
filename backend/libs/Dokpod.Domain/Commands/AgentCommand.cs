namespace Dokpod.Domain.Commands;

public enum AgentCommandKind
{
    Unknown = 0,
    StartContainer = 1,
    StopContainer = 2,
    RestartContainer = 3,
    DeleteContainer = 4,
    PauseContainer = 5,
    UnpauseContainer = 6,
    PullImage = 7,
    CreateContainer = 8,
    RemoveVolume = 9,
    RemoveNetwork = 10,
    PodAction = 11,
}

/// <summary>Parâmetros de comandos com alvo de host (spec 012 D-001); a 013 acrescenta criação de container.</summary>
public abstract record AgentCommandParameters
{
    /// <summary>Forma canônica (texto UTF-8, campos em ordem fixa) usada no hash `v2`.</summary>
    public abstract string Canonical { get; }
}

public sealed record PullImageParameters(Dokpod.Domain.Images.ImageReference Reference) : AgentCommandParameters
{
    public override string Canonical => $"reference={Reference.Canonical}";
}

/// <summary>Criação de container (spec 013); valores literais só existem em memória e no canal mTLS.</summary>
public sealed record CreateContainerParameters(Dokpod.Domain.Containers.ContainerCreationSpec Spec) : AgentCommandParameters
{
    public override string Canonical => Spec.Canonical();
}

/// <summary>Remoção de volume sem uso (spec 018); o agente nunca força.</summary>
public sealed record RemoveVolumeParameters : AgentCommandParameters
{
    public RemoveVolumeParameters(string name)
    {
        if (!Dokpod.Domain.Volumes.VolumeNames.IsValid(name)) throw new ArgumentException("Volume name is invalid.", nameof(name));
        Name = name;
    }

    public string Name { get; }

    public override string Canonical => $"volume={Name}";
}

/// <summary>Remoção de rede sem uso (spec 019); o agente nunca desconecta containers.</summary>
public sealed record RemoveNetworkParameters : AgentCommandParameters
{
    public RemoveNetworkParameters(string networkId, string name)
    {
        if (!Dokpod.Domain.Networks.NetworkNames.IsValidId(networkId)) throw new ArgumentException("Network id is invalid.", nameof(networkId));
        if (!Dokpod.Domain.Networks.NetworkNames.IsValidName(name)) throw new ArgumentException("Network name is invalid.", nameof(name));
        NetworkId = networkId;
        Name = name;
    }

    public string NetworkId { get; }

    /// <summary>Nome para a auditoria e a tela; fora do hash (o alvo é o identificador).</summary>
    public string Name { get; }

    public override string Canonical => $"network={NetworkId}";
}

/// <summary>Ação sobre o pod inteiro (spec 021); o agente recusa parar, pausar e reiniciar o pod do agente.</summary>
public sealed record PodActionParameters : AgentCommandParameters
{
    public PodActionParameters(string podId, string name, Dokpod.Domain.Pods.PodActionKind action)
    {
        if (!Dokpod.Domain.Pods.PodNames.IsValidId(podId)) throw new ArgumentException("Pod id is invalid.", nameof(podId));
        if (!Dokpod.Domain.Pods.PodNames.IsValidName(name)) throw new ArgumentException("Pod name is invalid.", nameof(name));
        if (!Enum.IsDefined(action)) throw new ArgumentException("Pod action is invalid.", nameof(action));
        PodId = podId;
        Name = name;
        Action = action;
    }

    public string PodId { get; }

    /// <summary>Nome para a auditoria e a tela; fora do hash (o alvo é o identificador).</summary>
    public string Name { get; }

    public Dokpod.Domain.Pods.PodActionKind Action { get; }

    public override string Canonical => $"pod={PodId};action={Dokpod.Domain.Pods.PodNames.ToValue(Action)}";
}

/// <summary>Saída de comandos parametrizados: digest e tamanho do pull (012), container criado (013), membros do pod que falharam (021).</summary>
public sealed record AgentCommandOutput(string? ResolvedImageDigest = null, long? ImageSizeBytes = null, string? CreatedContainerId = null)
{
    public IReadOnlyList<string>? FailedContainers { get; init; }
}

public static class AgentCommandKinds
{
    /// <summary>Comandos cujo alvo é o host (sem container): `container_id` nulo no armazenamento.</summary>
    public static bool IsHostTarget(AgentCommandKind kind) => kind is AgentCommandKind.PullImage or AgentCommandKind.CreateContainer or AgentCommandKind.RemoveVolume or AgentCommandKind.RemoveNetwork or AgentCommandKind.PodAction;

    /// <summary>
    /// Hash `v1` (tipos 1-6, sem prazo): `v1\n{kind}\n{container}\n{revisão}`; hash `v2` (tipos parametrizados):
    /// `v2\n{kind}\nhost\n{parâmetros canônicos}`. O prazo fica fora para retry com prazo novo não virar conflito.
    /// </summary>
    public static string PayloadHash(AgentCommandKind kind, string containerId, string expectedRevision, AgentCommandParameters? parameters)
    {
        var text = IsHostTarget(kind)
            ? $"v2\n{(int)kind}\nhost\n{parameters?.Canonical ?? throw new ArgumentException("Parameters are required.", nameof(parameters))}"
            : $"v1\n{(int)kind}\n{containerId}\n{expectedRevision}";
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
    }
}

public sealed record AgentCommand(
    Guid EnvironmentId,
    Guid CommandId,
    AgentCommandKind Kind,
    string ContainerId,
    string ExpectedContainerRevision,
    string PayloadHash,
    DateTimeOffset DeadlineUtc,
    long FencingToken)
{
    /// <summary>Parâmetros de comandos com alvo de host; nulo para os tipos 1-6.</summary>
    public AgentCommandParameters? Parameters { get; init; }
}

public enum CommandAdmission
{
    Accepted,
    Duplicate,
    Expired,
    StaleSession,
    ConflictingPayload,
    Unsupported,
}

public sealed record JournaledCommand(
    Guid EnvironmentId,
    Guid CommandId,
    string PayloadHash,
    CommandAdmission Admission);

public enum CommandExecutionState
{
    Succeeded,
    Failed,
    Indeterminate,
}

public sealed record JournaledCommandResult(
    Guid EnvironmentId,
    Guid CommandId,
    CommandExecutionState State,
    string? FailureCode,
    string? ObservedContainerRevision,
    DateTimeOffset CompletedAtUtc)
{
    public AgentCommandOutput? Output { get; init; }
}

public interface ICommandJournal
{
    Task<JournaledCommand?> AppendIfAbsentAsync(
        JournaledCommand command,
        CancellationToken cancellationToken);

    Task<JournaledCommandResult?> FindResultAsync(
        Guid environmentId,
        Guid commandId,
        CancellationToken cancellationToken);

    Task SaveResultAsync(
        JournaledCommandResult result,
        CancellationToken cancellationToken);
}

public sealed class AgentCommandGate(ICommandJournal journal, TimeProvider timeProvider)
{
    public async Task<CommandAdmission> AdmitAsync(
        AgentCommand command,
        long activeFencingToken,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.FencingToken != activeFencingToken)
        {
            return CommandAdmission.StaleSession;
        }

        var admission = command.Kind switch
        {
            AgentCommandKind.Unknown => CommandAdmission.Unsupported,
            _ when command.DeadlineUtc <= timeProvider.GetUtcNow() => CommandAdmission.Expired,
            _ => CommandAdmission.Accepted,
        };

        var existing = await journal.AppendIfAbsentAsync(
            new JournaledCommand(command.EnvironmentId, command.CommandId, command.PayloadHash, admission),
            cancellationToken);

        if (existing is null)
        {
            return admission;
        }

        if (!string.Equals(existing.PayloadHash, command.PayloadHash, StringComparison.Ordinal))
        {
            return CommandAdmission.ConflictingPayload;
        }

        return existing.Admission == CommandAdmission.Accepted
            ? CommandAdmission.Duplicate
            : existing.Admission;
    }
}