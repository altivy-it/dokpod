using System.Text.Json.Serialization;
using Dokpod.Domain.Containers;
using Dokpod.Domain.Images;

namespace Dokpod.ControlPlane.Application.Containers;

/// <summary>
/// Corpo de `createContainer` (spec 013): espelho da allowlist. Qualquer propriedade desconhecida
/// (privileged, capAdd, devices, pidMode, securityOpt, sysctls, userns...) faz a desserialização falhar → 400.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ContainerCreationRequest(
    string? Image,
    string? Name,
    IReadOnlyList<PortRequest>? Ports,
    IReadOnlyList<EnvironmentRequest>? Environment,
    IReadOnlyList<VolumeRequest>? Volumes,
    string? Network,
    double? CpuLimitCores,
    long? MemoryLimitBytes,
    RestartPolicyRequest? RestartPolicy,
    HealthCheckRequest? Healthcheck,
    bool AutoRemove,
    DateTimeOffset? DeadlineUtc)
{
    /// <summary>Forma JSON da especificação (sem `deadlineUtc`); usada para persistir parâmetros sem valores.</summary>
    public static ContainerCreationRequest FromSpec(ContainerCreationSpec spec) => new(
        spec.Image.Canonical,
        spec.Name,
        spec.Ports.Select(p => new PortRequest(p.HostIp, p.HostPort, p.ContainerPort, p.Protocol)).ToArray(),
        spec.Environment.Select(e => new EnvironmentRequest(
            e.Name,
            e.Value,
            e.Secret is null ? null : new SecretRefRequest(e.Secret.Kind == HostSecretKind.PodmanSecret ? "podman_secret" : "file", e.Secret.Name),
            e.Sensitive)).ToArray(),
        spec.Volumes.Select(v => new VolumeRequest(v.Kind == VolumeKind.Bind ? "bind" : "volume", v.Source, v.Target, v.ReadOnly)).ToArray(),
        spec.Network,
        spec.CpuLimitCores,
        spec.MemoryLimitBytes,
        new RestartPolicyRequest(
            spec.Restart.Name switch
            {
                RestartPolicyKind.OnFailure => "on-failure",
                RestartPolicyKind.Always => "always",
                RestartPolicyKind.UnlessStopped => "unless-stopped",
                _ => "no",
            },
            spec.Restart.MaximumRetryCount),
        spec.HealthCheck is null
            ? null
            : new HealthCheckRequest(spec.HealthCheck.Test, spec.HealthCheck.IntervalSeconds, spec.HealthCheck.TimeoutSeconds, spec.HealthCheck.Retries, spec.HealthCheck.StartPeriodSeconds, spec.HealthCheck.OnFailure == HealthCheckOnFailure.Restart ? "restart" : "none"),
        spec.AutoRemove,
        null);

    /// <summary>Converte em especificação de domínio; erros de forma (enumerações, referência) viram erros de campo.</summary>
    public (ContainerCreationSpec? Spec, IReadOnlyList<FieldError> Errors) ToSpec()
    {
        var errors = new List<FieldError>();
        if (!ImageReference.TryParse(Image, out var image)) errors.Add(new FieldError("image", "invalid_reference"));

        var environment = new List<EnvironmentSpec>();
        foreach (var (variable, index) in (Environment ?? []).Select((v, i) => (v, i)))
        {
            HostSecretReference? secret = null;
            if (variable.Secret is { } reference)
            {
                HostSecretKind? kind = reference.Kind switch
                {
                    "podman_secret" => HostSecretKind.PodmanSecret,
                    "file" => HostSecretKind.File,
                    _ => null,
                };
                if (kind is null) errors.Add(new FieldError($"environment[{index}].secret.kind", "invalid_kind"));
                else secret = new HostSecretReference(kind.Value, reference.Name ?? string.Empty);
            }

            environment.Add(new EnvironmentSpec(variable.Name ?? string.Empty, variable.Value, secret, variable.Sensitive));
        }

        var volumes = new List<VolumeSpec>();
        foreach (var (volume, index) in (Volumes ?? []).Select((v, i) => (v, i)))
        {
            VolumeKind? kind = volume.Type switch { "volume" => VolumeKind.Volume, "bind" => VolumeKind.Bind, _ => null };
            if (kind is null) errors.Add(new FieldError($"volumes[{index}].type", "invalid_type"));
            else volumes.Add(new VolumeSpec(kind.Value, volume.Source ?? string.Empty, volume.Target ?? string.Empty, volume.ReadOnly));
        }

        RestartPolicyKind? restart = (RestartPolicy?.Name ?? "no") switch
        {
            "no" => RestartPolicyKind.No,
            "on-failure" => RestartPolicyKind.OnFailure,
            "always" => RestartPolicyKind.Always,
            "unless-stopped" => RestartPolicyKind.UnlessStopped,
            _ => null,
        };
        if (restart is null) errors.Add(new FieldError("restartPolicy.name", "invalid_policy"));

        HealthCheckSpec? health = null;
        if (Healthcheck is { } check)
        {
            HealthCheckOnFailure? onFailure = (check.OnFailure ?? "none") switch
            {
                "none" => HealthCheckOnFailure.None,
                "restart" => HealthCheckOnFailure.Restart,
                _ => null,
            };
            if (onFailure is null) errors.Add(new FieldError("healthcheck.onFailure", "invalid_option"));
            health = new HealthCheckSpec(check.Test ?? [], check.IntervalSeconds, check.TimeoutSeconds, check.Retries, check.StartPeriodSeconds, onFailure ?? HealthCheckOnFailure.None);
        }

        if (errors.Count > 0 || image is null || restart is null)
        {
            return (null, errors);
        }

        var spec = new ContainerCreationSpec(
            image,
            Name ?? string.Empty,
            (Ports ?? []).Select(p => new PortSpec(string.IsNullOrWhiteSpace(p.HostIp) ? null : p.HostIp, p.HostPort, p.ContainerPort, p.Protocol ?? "tcp")).ToArray(),
            environment,
            volumes,
            string.IsNullOrWhiteSpace(Network) ? null : Network,
            CpuLimitCores,
            MemoryLimitBytes,
            new RestartSpec(restart.Value, RestartPolicy?.MaximumRetryCount ?? 0),
            health,
            AutoRemove);
        return (spec, []);
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PortRequest(string? HostIp, int HostPort, int ContainerPort, string? Protocol);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EnvironmentRequest(string? Name, string? Value, SecretRefRequest? Secret, bool Sensitive);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SecretRefRequest(string? Kind, string? Name);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VolumeRequest(string? Type, string? Source, string? Target, bool ReadOnly);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RestartPolicyRequest(string? Name, int? MaximumRetryCount);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HealthCheckRequest(IReadOnlyList<string>? Test, int IntervalSeconds, int TimeoutSeconds, int Retries, int StartPeriodSeconds, string? OnFailure);
