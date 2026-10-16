using System.Text.RegularExpressions;
using Dokpod.Agent.Application.Pods;

namespace Dokpod.Agent.Infrastructure.Engines;

/// <summary>
/// Identificador do container em que o agente roda (spec 021 D-002): identificadores de 64 hex em `/proc/self/mountinfo`
/// e o hostname de 12 caracteres hex que o Podman e o Docker usam por padrão.
/// </summary>
public sealed partial class SelfContainerIdentity : ISelfContainer
{
    private readonly HashSet<string> ids;
    private readonly string? hostnamePrefix;

    public SelfContainerIdentity(string mountInfoPath = "/proc/self/mountinfo", string hostnamePath = "/etc/hostname")
    {
        ids = Read(mountInfoPath) is { } mounts ? Hex64().Matches(mounts).Select(m => m.Value).ToHashSet(StringComparer.Ordinal) : [];
        var hostname = Read(hostnamePath)?.Trim();
        hostnamePrefix = hostname is { Length: 12 } && hostname.All(Uri.IsHexDigit) ? hostname.ToLowerInvariant() : null;
    }

    public bool IsSelf(string containerId) =>
        ids.Contains(containerId) || (hostnamePrefix is not null && containerId.StartsWith(hostnamePrefix, StringComparison.Ordinal));

    private static string? Read(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    [GeneratedRegex("(?<![0-9a-f])[0-9a-f]{64}(?![0-9a-f])")]
    private static partial Regex Hex64();
}
