namespace Dokpod.Agent.Infrastructure.Engines;

/// <summary>
/// Seleciona o endpoint do engine local (spec 008 T011). Um caminho configurado sempre vence;
/// sem configuração, no Linux procura Docker, Podman rootless e Podman rootful nessa ordem.
/// O engine efetivo (Docker ou Podman) é detectado depois pela API compatível (`/version`).
/// </summary>
public static class EngineSocketResolver
{
    public const string WindowsDefault = "docker_engine";
    public const string LinuxDefault = "/run/docker.sock";

    public static IReadOnlyList<string> LinuxCandidates(string? runtimeDirectory)
    {
        var candidates = new List<string> { LinuxDefault, "/var/run/docker.sock" };
        if (!string.IsNullOrWhiteSpace(runtimeDirectory))
        {
            candidates.Add(Path.Combine(runtimeDirectory, "podman", "podman.sock"));
        }

        candidates.Add("/run/podman/podman.sock");
        return candidates;
    }

    public static string Resolve(string? configured, bool isWindows, string? runtimeDirectory, Func<string, bool> exists)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        if (isWindows)
        {
            return WindowsDefault;
        }

        return LinuxCandidates(runtimeDirectory).FirstOrDefault(exists) ?? LinuxDefault;
    }
}
