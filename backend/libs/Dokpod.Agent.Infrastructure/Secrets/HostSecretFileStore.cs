using System.Text;
using System.Text.RegularExpressions;
using Dokpod.Agent.Application.Containers;

namespace Dokpod.Agent.Infrastructure.Secrets;

/// <summary>
/// Segredos em arquivo do host (spec 013 D-001/FR-005): `&lt;data&gt;/secrets/&lt;nome&gt;`, nome restrito, sem links
/// simbólicos e, no Linux, sem leitura por "outros". O valor nunca é registrado nem enviado ao plano de controle.
/// </summary>
public sealed partial class HostSecretFileStore(string directory) : IHostSecretFiles
{
    public const int MaxBytes = 32 * 1024;

    [GeneratedRegex("^[a-zA-Z0-9][a-zA-Z0-9_.-]{0,127}$")]
    private static partial Regex NamePattern();

    public async Task<string?> ReadAsync(string name, CancellationToken cancellationToken)
    {
        var path = PathFor(name);
        if (path is null) return null;
        var info = new FileInfo(path);
        if (!info.Exists || info.LinkTarget is not null || info.Length > MaxBytes) return null;
        if (!OperatingSystem.IsWindows() && (info.UnixFileMode & (UnixFileMode.OtherRead | UnixFileMode.OtherWrite)) != 0) return null;
        var value = await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken);
        return value.TrimEnd('\r', '\n');
    }

    public IReadOnlyList<string> List()
    {
        if (!Directory.Exists(directory)) return [];
        return Directory.EnumerateFiles(directory)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(name => NamePattern().IsMatch(name))
            .Order(StringComparer.Ordinal)
            .Take(500)
            .ToArray();
    }

    private string? PathFor(string name)
    {
        if (!NamePattern().IsMatch(name ?? string.Empty)) return null;
        var full = Path.GetFullPath(Path.Combine(directory, name!));
        return Path.GetDirectoryName(full) == Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) ? full : null;
    }
}
