using System.Text.RegularExpressions;

namespace Dokpod.Domain.Volumes;

/// <summary>Nomes de volume aceitos pelo engine (Docker e Podman) e volumes anônimos (spec 018).</summary>
public static partial class VolumeNames
{
    public const int MaxLength = 255;

    public static bool IsValid(string? name) => name is { Length: > 0 and <= MaxLength } && Pattern().IsMatch(name);

    /// <summary>Volume anônimo: nome gerado pelo engine (64 caracteres hexadecimais).</summary>
    public static bool IsAnonymous(string name) => name.Length == 64 && AnonymousPattern().IsMatch(name);

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_.-]*$")]
    private static partial Regex Pattern();

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex AnonymousPattern();
}

/// <summary>Códigos de falha da remoção de volume (spec 018 FR-008).</summary>
public static class VolumeRemovalCodes
{
    public const string InUse = "volume_in_use";
    public const string NotFound = "volume_not_found";
    public const string Failed = "volume_remove_failed";
}
