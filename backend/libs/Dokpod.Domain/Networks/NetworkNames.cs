using System.Text.RegularExpressions;

namespace Dokpod.Domain.Networks;

/// <summary>Identificador, nome e redes do sistema (spec 019).</summary>
public static partial class NetworkNames
{
    public const int MaxLength = 255;

    /// <summary>Redes criadas pelo próprio engine (Docker, Swarm e Podman); nunca removidas pelo console.</summary>
    public static IReadOnlySet<string> SystemNames { get; } = new HashSet<string>(["bridge", "host", "none", "docker_gwbridge", "podman"], StringComparer.Ordinal);

    public static bool IsValidId(string? id) => id is { Length: 64 } && IdPattern().IsMatch(id);

    public static bool IsValidName(string? name) => name is { Length: > 0 and <= MaxLength } && NamePattern().IsMatch(name);

    public static bool IsSystem(string name, bool ingress) => ingress || SystemNames.Contains(name);

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex IdPattern();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_.-]*$")]
    private static partial Regex NamePattern();
}

/// <summary>Códigos de falha da remoção de rede (spec 019 FR-008).</summary>
public static class NetworkRemovalCodes
{
    public const string InUse = "network_in_use";
    public const string NotFound = "network_not_found";
    public const string System = "network_system";
    public const string NotLocal = "network_not_local";
    public const string Failed = "network_remove_failed";
}
