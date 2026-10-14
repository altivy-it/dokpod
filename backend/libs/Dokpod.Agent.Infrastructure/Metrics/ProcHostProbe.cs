using System.Globalization;
using Dokpod.Agent.Application.Metrics;

namespace Dokpod.Agent.Infrastructure.Metrics;

/// <summary>
/// CPU e memória do host lidas de `/proc/stat` e `/proc/meminfo` do próprio container do agente
/// (não isolados por namespace; spec 009 D-003). Nunca lê `/proc/&lt;pid&gt;` nem monta diretórios do host.
/// </summary>
public sealed class ProcHostProbe(string procRoot = "/proc") : IHostResourceProbe
{
    public HostResourceReading? Read()
    {
        if (!OperatingSystem.IsLinux()) return null;
        try
        {
            var cpu = ParseStat(File.ReadLines(Path.Combine(procRoot, "stat")).FirstOrDefault());
            var memory = ParseMeminfo(File.ReadLines(Path.Combine(procRoot, "meminfo")));
            if (cpu is null || memory is null) return null;
            return new HostResourceReading(cpu.Value.Total, cpu.Value.Idle, memory.Value.Total, memory.Value.Available);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Linha agregada `cpu`: total = user..steal (guest já está em user), ocioso = idle + iowait.</summary>
    public static (ulong Total, ulong Idle)? ParseStat(string? line)
    {
        if (line is null || !line.StartsWith("cpu ", StringComparison.Ordinal)) return null;
        var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).Take(8).ToArray();
        if (fields.Length < 4) return null;
        var values = new ulong[fields.Length];
        for (var i = 0; i < fields.Length; i++)
        {
            if (!ulong.TryParse(fields[i], NumberStyles.None, CultureInfo.InvariantCulture, out values[i])) return null;
        }

        ulong total = 0;
        foreach (var value in values) total += value;
        var idle = values[3] + (values.Length > 4 ? values[4] : 0);
        return (total, idle);
    }

    /// <summary>`MemTotal` e `MemAvailable` (kB) convertidos para bytes.</summary>
    public static (ulong Total, ulong Available)? ParseMeminfo(IEnumerable<string> lines)
    {
        ulong? total = null;
        ulong? available = null;
        foreach (var line in lines)
        {
            if (line.StartsWith("MemTotal:", StringComparison.Ordinal)) total = Kilobytes(line);
            else if (line.StartsWith("MemAvailable:", StringComparison.Ordinal)) available = Kilobytes(line);
            if (total is not null && available is not null) break;
        }

        return total is > 0 && available is not null ? (total.Value, available.Value) : null;
    }

    private static ulong? Kilobytes(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && ulong.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var kb) ? kb * 1024 : null;
    }
}
