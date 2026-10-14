using Dokpod.Agent.Contracts.V1;
using Dokpod.Domain.Containers;
using Dokpod.Domain.Images;
using DomainHealthOnFailure = Dokpod.Domain.Containers.HealthCheckOnFailure;
using DomainSecretKind = Dokpod.Domain.Containers.HostSecretKind;
using ProtoHealthOnFailure = Dokpod.Agent.Contracts.V1.HealthCheckOnFailure;
using ProtoSecretKind = Dokpod.Agent.Contracts.V1.HostSecretKind;

namespace Dokpod.Agent.Application.Containers;

/// <summary>Converte `CreateContainerParameters` em especificação de domínio e revalida a allowlist no agente (T010).</summary>
public static class ContainerCreationMapper
{
    public static ContainerCreationSpec? FromMessage(CreateContainerParameters message, out string failureCode)
    {
        failureCode = "invalid_spec";
        if (!ImageReference.TryParse(message.Image, out var image)) return null;
        var environment = new List<EnvironmentSpec>();
        foreach (var variable in message.Environment)
        {
            HostSecretReference? secret = null;
            string? value = null;
            switch (variable.SourceCase)
            {
                case EnvironmentVariable.SourceOneofCase.Value:
                    value = variable.Value;
                    break;
                case EnvironmentVariable.SourceOneofCase.Secret when variable.Secret.Kind is ProtoSecretKind.PodmanSecret or ProtoSecretKind.File:
                    secret = new HostSecretReference(variable.Secret.Kind == ProtoSecretKind.PodmanSecret ? DomainSecretKind.PodmanSecret : DomainSecretKind.File, variable.Secret.Name);
                    break;
                default:
                    return null;
            }

            environment.Add(new EnvironmentSpec(variable.Name, value, secret, variable.Sensitive));
        }

        var volumes = new List<VolumeSpec>();
        foreach (var volume in message.Volumes)
        {
            if (volume.Type is not (VolumeMountType.Volume or VolumeMountType.Bind)) return null;
            volumes.Add(new VolumeSpec(volume.Type == VolumeMountType.Bind ? VolumeKind.Bind : VolumeKind.Volume, volume.Source, volume.Target, volume.ReadOnly));
        }

        var restart = message.RestartPolicy?.Name switch
        {
            null or RestartPolicyName.Unspecified or RestartPolicyName.No => RestartPolicyKind.No,
            RestartPolicyName.OnFailure => RestartPolicyKind.OnFailure,
            RestartPolicyName.Always => RestartPolicyKind.Always,
            RestartPolicyName.UnlessStopped => RestartPolicyKind.UnlessStopped,
            _ => (RestartPolicyKind?)null,
        };
        if (restart is null) return null;

        HealthCheckSpec? health = null;
        if (message.Healthcheck is { } check)
        {
            health = new HealthCheckSpec(
                check.Test.ToArray(),
                (int)Math.Min(check.IntervalSeconds, int.MaxValue),
                (int)Math.Min(check.TimeoutSeconds, int.MaxValue),
                (int)Math.Min(check.Retries, int.MaxValue),
                (int)Math.Min(check.StartPeriodSeconds, int.MaxValue),
                check.OnFailure == ProtoHealthOnFailure.Restart ? DomainHealthOnFailure.Restart : DomainHealthOnFailure.None);
        }

        var spec = new ContainerCreationSpec(
            image,
            message.Name,
            message.Ports.Select(p => new PortSpec(
                string.IsNullOrEmpty(p.HostIp) ? null : p.HostIp,
                (int)Math.Min(p.HostPort, int.MaxValue),
                (int)Math.Min(p.ContainerPort, int.MaxValue),
                p.Protocol switch { TransportProtocol.Udp => "udp", TransportProtocol.Tcp => "tcp", _ => "invalid" })).ToArray(),
            environment,
            volumes,
            string.IsNullOrEmpty(message.Network) ? null : message.Network,
            message.HasCpuLimitCores ? message.CpuLimitCores : null,
            message.HasMemoryLimitBytes ? (long)Math.Min(message.MemoryLimitBytes, long.MaxValue) : null,
            new RestartSpec(restart.Value, (int)Math.Min(message.RestartPolicy?.MaximumRetryCount ?? 0, int.MaxValue)),
            health,
            message.AutoRemove);
        if (spec.Validate().Count > 0) return null;
        failureCode = string.Empty;
        return spec;
    }
}
