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

/// <summary>Saída de comandos parametrizados: digest e tamanho do pull (012), container criado (013).</summary>
public sealed record AgentCommandOutput(string? ResolvedImageDigest = null, long? ImageSizeBytes = null, string? CreatedContainerId = null);

public static class AgentCommandKinds
{
    /// <summary>Comandos cujo alvo é o host (sem container): `container_id` nulo no armazenamento.</summary>
    public static bool IsHostTarget(AgentCommandKind kind) => kind == AgentCommandKind.PullImage;

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