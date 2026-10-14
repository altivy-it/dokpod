using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Hosts;
using Dokpod.Domain.Inventory;

namespace Dokpod.ControlPlane.Api.Endpoints;

/// <summary>Formas JSON dos ambientes/hosts (contrato OpenAPI v1, spec 008).</summary>
internal static class HostResponses
{
    public static object ToEnvironment(HostView view)
    {
        var registration = view.Registration;
        var descriptor = view.Descriptor;
        return new
        {
            environmentId = registration.EnvironmentId,
            name = registration.Name,
            host = registration.Host,
            enabled = registration.Enabled,
            scopes = registration.Scopes.Order(StringComparer.Ordinal),
            stage = registration.Stage is null ? null : EnvironmentStages.ToValue(registration.Stage.Value),
            situation = HostSituations.ToValue(view.Situation),
            containerCount = view.Inventory?.Total,
            agent = descriptor is null && view.Connection is null
                ? null
                : new
                {
                    connected = view.Connected,
                    sessionStartedAt = view.Connection?.SessionStartedAtUtc,
                    lastSeenAt = view.Connection?.LastSeenAtUtc,
                    agentVersion = descriptor?.AgentVersion,
                    engine = descriptor?.EngineKind,
                    engineVersion = descriptor?.EngineVersion,
                    engineApiVersion = descriptor?.EngineApiVersion,
                    engineRootless = descriptor?.EngineRootless,
                    operatingSystem = descriptor?.OperatingSystem,
                    operatingSystemVersion = descriptor?.OperatingSystemVersion,
                    architecture = descriptor?.Architecture,
                    hostName = descriptor?.HostName,
                    cpuCount = descriptor?.CpuCount,
                    memoryBytes = descriptor?.MemoryBytes,
                    capabilities = descriptor?.Capabilities ?? [],
                },
        };
    }

    public static IDictionary<string, object?> ToContainerListItem(Dokpod.ControlPlane.Application.Inventory.ContainerListItem item)
    {
        var json = new Dictionary<string, object?>(ToContainerFields(item.Container))
        {
            ["environmentId"] = item.Host.Registration.EnvironmentId,
            ["environmentName"] = item.Host.Registration.Name,
            ["stage"] = item.Host.Registration.Stage is null ? null : EnvironmentStages.ToValue(item.Host.Registration.Stage.Value),
            ["engine"] = item.Host.Descriptor?.EngineKind,
            ["engineVersion"] = item.Host.Descriptor?.EngineVersion,
            ["hostConnected"] = item.Host.Connected,
            ["allowedActions"] = item.AllowedActions,
        };
        return json;
    }

    public static object ToContainer(ContainerInventory container) => ToContainerFields(container);

    /// <summary>Recursos atuais (spec 009 T012); container parado ou sem amostra recente não recebe o campo.</summary>
    public static IDictionary<string, object?> WithResources(
        IDictionary<string, object?> item,
        ContainerDisplayStatus status,
        Dokpod.ControlPlane.Application.Metrics.ContainerMetricsSample? sample)
    {
        if (sample is null || status is ContainerDisplayStatus.Stopped or ContainerDisplayStatus.Failed or ContainerDisplayStatus.Paused) return item;
        item["resources"] = new
        {
            cpuPercent = sample.CpuPercentOfLimit is { } ofLimit ? Math.Min(100, ofLimit) : sample.CpuPercentOfHost,
            cpuPercentOfHost = sample.CpuPercentOfHost,
            cpuLimitCores = sample.CpuLimitCores,
            memoryUsedBytes = sample.MemoryUsedBytes,
            memoryLimitBytes = sample.MemoryLimitBytes,
            sampledAt = sample.SampledAtUtc,
        };
        return item;
    }

    private static Dictionary<string, object?> ToContainerFields(ContainerInventory container) => new()
    {
        ["containerId"] = container.ContainerId,
        ["name"] = container.Name,
        ["imageReference"] = container.ImageReference,
        ["state"] = container.State,
        ["revision"] = container.Revision,
        ["observedAt"] = container.ObservedAtUtc,
        ["displayStatus"] = ContainerDisplayStatuses.ToValue(container.DisplayStatus),
        ["createdAt"] = container.CreatedAtUtc,
        ["startedAt"] = container.StartedAtUtc,
        ["health"] = container.Health?.ToString().ToLowerInvariant(),
        ["restartCount"] = container.RestartCount,
        ["exitCode"] = container.ExitCode,
        ["oomKilled"] = container.OomKilled,
        ["ports"] = container.Ports.Select(port => new
        {
            hostIp = port.HostIp,
            hostPort = port.HostPort,
            containerPort = port.ContainerPort,
            protocol = port.Protocol,
        }).ToArray(),
    };
}
