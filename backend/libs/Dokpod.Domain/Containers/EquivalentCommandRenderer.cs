using System.Globalization;
using System.Text;

namespace Dokpod.Domain.Containers;

/// <summary>
/// Comando equivalente (`docker run`/`podman run`) para o formulário e a auditoria (spec 013 D-005, FR-008):
/// valores literais viram `***` e segredos aparecem só por referência. O frontend gera o mesmo texto
/// (fixtures de paridade em `frontend/web/src/testing/equivalent-command.fixtures.json`).
/// </summary>
public static class EquivalentCommandRenderer
{
    public static string Render(ContainerCreationSpec spec, string engine)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var parts = new List<string> { engine == "podman" ? "podman" : "docker", "run", "-d", "--name", Quote(spec.Name) };
        foreach (var port in spec.Ports)
        {
            var mapping = port.HostPort == 0
                ? port.ContainerPort.ToString(CultureInfo.InvariantCulture)
                : $"{(string.IsNullOrEmpty(port.HostIp) ? string.Empty : (port.HostIp.Contains(':') ? $"[{port.HostIp}]" : port.HostIp) + ":")}{port.HostPort}:{port.ContainerPort}";
            parts.Add("-p");
            parts.Add(mapping + (port.Protocol == "udp" ? "/udp" : string.Empty));
        }

        foreach (var variable in spec.Environment)
        {
            if (variable.Secret is { Kind: HostSecretKind.PodmanSecret } podman)
            {
                parts.Add("--secret");
                parts.Add(Quote($"{podman.Name},type=env,target={variable.Name}"));
            }
            else if (variable.Secret is { } file)
            {
                parts.Add("-e");
                parts.Add(Quote($"{variable.Name}=<segredo:{file.Name}>"));
            }
            else
            {
                parts.Add("-e");
                parts.Add($"{variable.Name}=***");
            }
        }

        foreach (var volume in spec.Volumes)
        {
            parts.Add("-v");
            parts.Add(Quote($"{volume.Source}:{volume.Target}{(volume.ReadOnly ? ":ro" : string.Empty)}"));
        }

        if (!string.IsNullOrEmpty(spec.Network))
        {
            parts.Add("--network");
            parts.Add(Quote(spec.Network));
        }

        if (spec.CpuLimitCores is { } cpu)
        {
            parts.Add("--cpus");
            parts.Add(cpu.ToString("0.##", CultureInfo.InvariantCulture));
        }

        if (spec.MemoryLimitBytes is { } memory)
        {
            parts.Add("--memory");
            parts.Add(memory % (1024 * 1024) == 0 ? $"{memory / (1024 * 1024)}m" : $"{memory}b");
        }

        if (spec.Restart.Name != RestartPolicyKind.No)
        {
            parts.Add("--restart");
            parts.Add(spec.Restart.Name switch
            {
                RestartPolicyKind.OnFailure => spec.Restart.MaximumRetryCount > 0 ? $"on-failure:{spec.Restart.MaximumRetryCount}" : "on-failure",
                RestartPolicyKind.Always => "always",
                _ => "unless-stopped",
            });
        }

        if (spec.HealthCheck is { } health)
        {
            parts.Add("--health-cmd");
            parts.Add(Quote(health.Test[0] == "CMD-SHELL" ? health.Test[1] : string.Join(' ', health.Test.Skip(1))));
            parts.Add($"--health-interval={health.IntervalSeconds}s");
            parts.Add($"--health-timeout={health.TimeoutSeconds}s");
            parts.Add($"--health-retries={health.Retries}");
            if (health.StartPeriodSeconds > 0) parts.Add($"--health-start-period={health.StartPeriodSeconds}s");
            if (health.OnFailure == HealthCheckOnFailure.Restart) parts.Add("--health-on-failure=restart");
        }

        if (spec.AutoRemove) parts.Add("--rm");
        parts.Add(spec.Image.Canonical);
        return string.Join(' ', parts);
    }

    /// <summary>Aspas simples POSIX somente quando necessário.</summary>
    public static string Quote(string value)
    {
        if (value.Length > 0 && value.All(c => char.IsAsciiLetterOrDigit(c) || "-_./:=,@%+".Contains(c))) return value;
        var builder = new StringBuilder("'");
        foreach (var c in value)
        {
            builder.Append(c == '\'' ? "'\\''" : c.ToString());
        }

        return builder.Append('\'').ToString();
    }
}
