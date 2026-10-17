using Dokpod.Domain.Environments;
using Dokpod.Domain.Hosts;

namespace Dokpod.ControlPlane.Application.Hosts;

public sealed record AgentDescriptor(
    Guid EnvironmentId,
    string EngineKind,
    string EngineVersion,
    string EngineApiVersion,
    bool? EngineRootless,
    string OperatingSystem,
    string OperatingSystemVersion,
    string Architecture,
    string AgentVersion,
    IReadOnlyList<string> Capabilities,
    string HostName,
    int? CpuCount,
    long? MemoryBytes,
    DateTimeOffset UpdatedAtUtc);

public sealed record AgentConnectionState(
    Guid EnvironmentId,
    bool Connected,
    DateTimeOffset? SessionStartedAtUtc,
    DateTimeOffset? LastSeenAtUtc,
    DateTimeOffset? DisconnectedAtUtc);

public sealed record InventoryStatistics(
    Guid EnvironmentId,
    IReadOnlyDictionary<string, int> ByDisplayStatus,
    DateTimeOffset? ObservedAtUtc,
    ulong Revision)
{
    public int Total => ByDisplayStatus.Values.Sum();
}

public sealed record HostView(
    EnvironmentRegistration Registration,
    AgentDescriptor? Descriptor,
    AgentConnectionState? Connection,
    bool Connected,
    HostSituation Situation,
    InventoryStatistics? Inventory)
{
    /// <summary>
    /// O agente do host publica este inventário (spec 023): com um agente anterior, a projeção gravada por uma versão
    /// mais nova é antiga e o host aparece como "inventário indisponível", não com dados velhos.
    /// </summary>
    public bool Publishes(string capability) => Descriptor?.Capabilities.Contains(capability) ?? false;
}

public interface IAgentHostStore
{
    Task UpsertDescriptorAsync(AgentDescriptor descriptor, CancellationToken cancellationToken);

    /// <summary>Grava conexão; retorna o estado anterior de `connected` (null se não existia).</summary>
    Task<bool?> SetConnectionAsync(Guid environmentId, bool connected, DateTimeOffset atUtc, CancellationToken cancellationToken);

    Task TouchLastSeenAsync(Guid environmentId, DateTimeOffset atUtc, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, AgentDescriptor>> GetDescriptorsAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, AgentConnectionState>> GetConnectionsAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> ListConnectedAsync(CancellationToken cancellationToken);
}

public interface IInventoryStatisticsStore
{
    Task<IReadOnlyDictionary<Guid, InventoryStatistics>> GetStatisticsAsync(
        IReadOnlyCollection<Guid> environmentIds,
        CancellationToken cancellationToken);
}

public interface IEnvironmentStageStore
{
    /// <summary>Atualiza o estágio; retorna o estágio anterior, ou `NotFound` quando o ambiente não existe.</summary>
    Task<(bool Found, EnvironmentStage? Previous)> SetStageAsync(
        Guid environmentId,
        EnvironmentStage? stage,
        CancellationToken cancellationToken);
}

public sealed class EngineSupportOptions
{
    public const string SectionName = "EngineSupport";

    public string DockerMinimumVersion { get; set; } = "27.0.0";
    public string PodmanMinimumVersion { get; set; } = "5.0.0";

    public string? MinimumFor(string? engineKind) => engineKind switch
    {
        "docker" => DockerMinimumVersion,
        "podman" => PodmanMinimumVersion,
        _ => null,
    };

    public bool IsValid() =>
        EngineVersion.TryParse(DockerMinimumVersion, out _) && EngineVersion.TryParse(PodmanMinimumVersion, out _);
}
