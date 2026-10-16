using System.Text.RegularExpressions;
using Dokpod.Domain.Environments;

namespace Dokpod.Domain.Pods;

public enum PodActionKind
{
    Start = 1,
    Stop = 2,
    Restart = 3,
    Pause = 4,
    Unpause = 5,
}

/// <summary>Identificador, nome e ações de pod do Podman (spec 021).</summary>
public static partial class PodNames
{
    public const int MaxLength = 255;

    public static bool IsValidId(string? id) => id is { Length: 64 } && IdPattern().IsMatch(id);

    public static bool IsValidName(string? name) => name is { Length: > 0 and <= MaxLength } && NamePattern().IsMatch(name);

    public static string ToValue(PodActionKind action) => action switch
    {
        PodActionKind.Start => "start",
        PodActionKind.Stop => "stop",
        PodActionKind.Restart => "restart",
        PodActionKind.Pause => "pause",
        PodActionKind.Unpause => "unpause",
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };

    public static bool TryParse(string? value, out PodActionKind action)
    {
        action = value switch
        {
            "start" => PodActionKind.Start,
            "stop" => PodActionKind.Stop,
            "restart" => PodActionKind.Restart,
            "pause" => PodActionKind.Pause,
            "unpause" => PodActionKind.Unpause,
            _ => 0,
        };
        return action != 0;
    }

    /// <summary>Agir no pod é agir nos containers: vale o scope de container da ação (retomar usa o de pausar).</summary>
    public static string ScopeFor(PodActionKind action) => action switch
    {
        PodActionKind.Start => EnvironmentResourceScopes.StartContainer,
        PodActionKind.Stop => EnvironmentResourceScopes.StopContainer,
        PodActionKind.Restart => EnvironmentResourceScopes.RestartContainer,
        _ => EnvironmentResourceScopes.PauseContainer,
    };

    /// <summary>Parar e pausar pedem justificativa conforme a política do estágio (spec 016).</summary>
    public static bool RequiresJustification(PodActionKind action) => action is PodActionKind.Stop or PodActionKind.Pause;

    /// <summary>Ações que derrubariam o próprio agente quando o pod o contém (FR-005).</summary>
    public static bool RefusedForAgentPod(PodActionKind action) => action is PodActionKind.Stop or PodActionKind.Pause or PodActionKind.Restart;

    public static string AuditAction(PodActionKind action) => "pod." + ToValue(action);

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex IdPattern();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_.-]*$")]
    private static partial Regex NamePattern();
}

/// <summary>Códigos de falha das ações de pod (spec 021 FR-008).</summary>
public static class PodActionCodes
{
    public const string AgentPod = "agent_pod";
    public const string NotFound = "pod_not_found";
    public const string PartialFailure = "pod_partial_failure";
    public const string StateConflict = "pod_state_conflict";
    public const string Failed = "pod_action_failed";
}
