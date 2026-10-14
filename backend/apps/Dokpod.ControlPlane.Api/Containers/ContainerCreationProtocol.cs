using Dokpod.Agent.Contracts.V1;
using Dokpod.Domain.Containers;
using DomainHealthOnFailure = Dokpod.Domain.Containers.HealthCheckOnFailure;
using DomainSecretKind = Dokpod.Domain.Containers.HostSecretKind;
using ProtoHealthOnFailure = Dokpod.Agent.Contracts.V1.HealthCheckOnFailure;
using ProtoSecretKind = Dokpod.Agent.Contracts.V1.HostSecretKind;

namespace Dokpod.ControlPlane.Api;

/// <summary>Tradução da especificação de domínio para `CreateContainerParameters` (spec 013); valores só no canal mTLS.</summary>
public static class ContainerCreationProtocol
{
    public static CreateContainerParameters ToMessage(ContainerCreationSpec spec)
    {
        var message = new CreateContainerParameters
        {
            Image = spec.Image.Canonical,
            Name = spec.Name,
            Network = spec.Network ?? string.Empty,
            AutoRemove = spec.AutoRemove,
            RestartPolicy = new RestartPolicy
            {
                Name = spec.Restart.Name switch
                {
                    RestartPolicyKind.OnFailure => RestartPolicyName.OnFailure,
                    RestartPolicyKind.Always => RestartPolicyName.Always,
                    RestartPolicyKind.UnlessStopped => RestartPolicyName.UnlessStopped,
                    _ => RestartPolicyName.No,
                },
                MaximumRetryCount = (uint)Math.Max(0, spec.Restart.MaximumRetryCount),
            },
        };
        if (spec.CpuLimitCores is { } cpu) message.CpuLimitCores = cpu;
        if (spec.MemoryLimitBytes is { } memory) message.MemoryLimitBytes = (ulong)memory;
        message.Ports.AddRange(spec.Ports.Select(p => new PortBinding
        {
            HostIp = p.HostIp ?? string.Empty,
            HostPort = (uint)p.HostPort,
            ContainerPort = (uint)p.ContainerPort,
            Protocol = p.Protocol == "udp" ? TransportProtocol.Udp : TransportProtocol.Tcp,
        }));
        foreach (var variable in spec.Environment)
        {
            var entry = new EnvironmentVariable { Name = variable.Name, Sensitive = variable.Sensitive };
            if (variable.Secret is { } secret)
            {
                entry.Secret = new HostSecretRef
                {
                    Kind = secret.Kind == DomainSecretKind.PodmanSecret ? ProtoSecretKind.PodmanSecret : ProtoSecretKind.File,
                    Name = secret.Name,
                };
            }
            else
            {
                entry.Value = variable.Value ?? string.Empty;
            }

            message.Environment.Add(entry);
        }

        message.Volumes.AddRange(spec.Volumes.Select(v => new VolumeMount
        {
            Type = v.Kind == VolumeKind.Bind ? VolumeMountType.Bind : VolumeMountType.Volume,
            Source = v.Source,
            Target = v.Target,
            ReadOnly = v.ReadOnly,
        }));
        if (spec.HealthCheck is { } health)
        {
            message.Healthcheck = new HealthCheck
            {
                IntervalSeconds = (uint)health.IntervalSeconds,
                TimeoutSeconds = (uint)health.TimeoutSeconds,
                Retries = (uint)health.Retries,
                StartPeriodSeconds = (uint)health.StartPeriodSeconds,
                OnFailure = health.OnFailure == DomainHealthOnFailure.Restart ? ProtoHealthOnFailure.Restart : ProtoHealthOnFailure.None,
            };
            message.Healthcheck.Test.AddRange(health.Test);
        }

        return message;
    }
}
