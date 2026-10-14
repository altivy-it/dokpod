namespace Dokpod.Domain.Hosts;

public enum HostSituation
{
    Disabled,
    Disconnected,
    Stale,
    UpdateRequired,
    Unknown,
    UpToDate,
}

public enum EngineKindValue
{
    Docker,
    Podman,
}

public static class HostSituations
{
    public static string ToValue(HostSituation situation) => situation switch
    {
        HostSituation.Disabled => "disabled",
        HostSituation.Disconnected => "disconnected",
        HostSituation.Stale => "stale",
        HostSituation.UpdateRequired => "update_required",
        HostSituation.Unknown => "unknown",
        HostSituation.UpToDate => "up_to_date",
        _ => throw new ArgumentOutOfRangeException(nameof(situation)),
    };

    public static bool TryParse(string? value, out HostSituation situation)
    {
        foreach (var candidate in Enum.GetValues<HostSituation>())
        {
            if (ToValue(candidate) == value)
            {
                situation = candidate;
                return true;
            }
        }

        situation = default;
        return false;
    }

    /// <summary>
    /// Avalia a situação na ordem de FR-004 (spec 008): Desabilitado, Desconectado, Desatualizado,
    /// Atualizar engine, Desconhecido e Em dia. Host estável sem deltas permanece Em dia.
    /// </summary>
    public static HostSituation Evaluate(
        bool enabled,
        bool connected,
        bool inventoryDiverging,
        string? engineVersion,
        string? minimumVersion)
    {
        if (!enabled) return HostSituation.Disabled;
        if (!connected) return HostSituation.Disconnected;
        if (inventoryDiverging) return HostSituation.Stale;
        if (string.IsNullOrWhiteSpace(engineVersion) || !EngineVersion.TryParse(engineVersion, out var current))
        {
            return HostSituation.Unknown;
        }

        if (!string.IsNullOrWhiteSpace(minimumVersion) &&
            EngineVersion.TryParse(minimumVersion, out var minimum) &&
            current.CompareTo(minimum) < 0)
        {
            return HostSituation.UpdateRequired;
        }

        return HostSituation.UpToDate;
    }
}

/// <summary>Versão SemVer tolerante: "27.3.1", "5.2.3-dev", "26.1"; sufixos ignorados.</summary>
public readonly record struct EngineVersion(int Major, int Minor, int Patch) : IComparable<EngineVersion>
{
    public static bool TryParse(string? value, out EngineVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var core = value.Trim().TrimStart('v', 'V');
        var end = 0;
        while (end < core.Length && (char.IsDigit(core[end]) || core[end] == '.')) end++;
        var parts = core[..end].Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Length > 3) return false;
        var numbers = new int[3];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out numbers[i]))
            {
                return false;
            }
        }

        version = new EngineVersion(numbers[0], numbers[1], numbers[2]);
        return true;
    }

    public int CompareTo(EngineVersion other)
    {
        var major = Major.CompareTo(other.Major);
        if (major != 0) return major;
        var minor = Minor.CompareTo(other.Minor);
        return minor != 0 ? minor : Patch.CompareTo(other.Patch);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}
