using Dokpod.Domain.Commands;

namespace Dokpod.Agent.Application.Engines;

public sealed record EngineDescriptor(string EngineVersion, string ApiVersion);

public sealed record EngineContainer(
    string ContainerId,
    string Name,
    string ImageReference,
    string State,
    string Revision)
{
    public DateTimeOffset? CreatedAtUtc { get; init; }
    public DateTimeOffset? StartedAtUtc { get; init; }
    /// <summary>`none`, `starting`, `healthy` ou `unhealthy`; nulo quando desconhecido.</summary>
    public string? Health { get; init; }
    public int? RestartCount { get; init; }
    public int? ExitCode { get; init; }
    public bool? OomKilled { get; init; }
    public IReadOnlyList<EnginePortBinding> Ports { get; init; } = [];
    public double? CpuLimitCores { get; init; }
    /// <summary>`ImageID` do engine (spec 012), base de "imagem em uso".</summary>
    public string? ImageId { get; init; }
    /// <summary>Montagens da listagem (spec 018): base de "volume em uso".</summary>
    public IReadOnlyList<EngineMount> Mounts { get; init; } = [];
    /// <summary>Conexões de rede da listagem (spec 019): base de "rede em uso", inclusive parados.</summary>
    public IReadOnlyList<EngineNetworkAttachment> Networks { get; init; } = [];
}

/// <summary>Conexão de um container a uma rede; endereços vazios quando o container não está em execução.</summary>
public sealed record EngineNetworkAttachment(string Name, string? NetworkId, string? IPv4Address, string? IPv6Address);

/// <summary>Rede do engine (spec 019); as opções do driver nunca são lidas para fora do agente.</summary>
public sealed record EngineNetwork(
    string NetworkId,
    string Name,
    string Driver,
    string Scope,
    bool Internal,
    bool IPv6,
    bool Attachable,
    bool Ingress,
    DateTimeOffset? CreatedAtUtc,
    IReadOnlyList<EngineNetworkSubnet> Subnets,
    IReadOnlyDictionary<string, string> Labels);

public sealed record EngineNetworkSubnet(string Subnet, string? Gateway);

/// <summary>Pod do Podman (spec 021); membros com situação e reinícios.</summary>
public sealed record EnginePod(
    string PodId,
    string Name,
    string Status,
    DateTimeOffset? CreatedAtUtc,
    IReadOnlyDictionary<string, string> Labels,
    IReadOnlyList<string> Networks,
    string? InfraId,
    IReadOnlyList<EnginePodMember> Members);

public sealed record EnginePodMember(string ContainerId, string Name, string Status, int Restarts);

/// <summary>Registro na configuração do engine (spec 020); `MirrorOf` quando é espelho de outro registro.</summary>
public sealed record EngineRegistry(string Registry, IReadOnlyList<string> Mirrors, bool Insecure, bool Blocked, bool Search);

/// <summary>Configuração de registros do host: do engine (quando informada) e nomes com credencial no agente (nunca valores).</summary>
public sealed record EngineRegistryConfig(bool ConfigAvailable, IReadOnlyList<EngineRegistry> Registries, IReadOnlyList<string> CredentialRegistries);

/// <summary>Montagem de um container: `volume` (com nome), `bind`, `tmpfs` etc.</summary>
public sealed record EngineMount(string Type, string? Name, string Destination, bool ReadOnly);

/// <summary>Volume do engine (spec 018); o caminho no host (`Mountpoint`) nunca é lido para fora do agente.</summary>
public sealed record EngineVolume(string Name, string Driver, string Scope, DateTimeOffset? CreatedAtUtc, IReadOnlyDictionary<string, string> Labels);

/// <summary>Tamanho e referências de um volume segundo o engine (`system df`); nulos quando não informados.</summary>
public sealed record EngineVolumeUsage(long? SizeBytes, long? RefCount);

/// <summary>Imagem local do engine (spec 012); somente metadados, nunca camadas ou conteúdo.</summary>
public sealed record EngineImage(
    string ImageId,
    IReadOnlyList<string> RepoTags,
    IReadOnlyList<string> RepoDigests,
    long SizeBytes,
    DateTimeOffset? CreatedAtUtc);

/// <summary>
/// Pedido de criação já resolvido no agente (spec 013): valores literais e de segredos em arquivo em `Environment`;
/// segredos do Podman só por nome em `PodmanSecrets` (variável → segredo), resolvidos pelo próprio engine.
/// </summary>
public sealed record EngineCreateRequest(
    Dokpod.Domain.Containers.ContainerCreationSpec Spec,
    IReadOnlyList<KeyValuePair<string, string>> Environment,
    IReadOnlyDictionary<string, string> PodmanSecrets,
    Guid CommandId);

public sealed record EngineCreateResult(string? ContainerId, string? FailureCode);

/// <summary>Imagem local: presença e destinos `VOLUME` declarados (regra de remoção automática, FR-011).</summary>
public sealed record EngineImageConfig(bool Present, IReadOnlyList<string> Volumes);

/// <summary>Objeto do host para o formulário de criação: segredo do Podman, rede ou volume (somente nomes).</summary>
public sealed record EngineHostObject(string Name, string? Driver = null);

public enum EngineHostObjectKind
{
    PodmanSecrets,
    Networks,
    Volumes,
}

/// <summary>
/// Exec interativo iniciado (spec 014): `Stream` é a conexão sequestrada do engine (TTY bidirecional).
/// Fechar o stream fecha o TTY e o shell recebe SIGHUP.
/// </summary>
public sealed record EngineExecSession(string ExecId, Stream Stream) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Stream.DisposeAsync();
}

public sealed record EngineExecState(bool Running, int? ExitCode);

/// <summary>Falha ao criar/iniciar o exec; `ExecutableNotFound` permite cair de `/bin/bash` para `/bin/sh`.</summary>
public sealed class EngineExecException(string message, bool executableNotFound) : Exception(message)
{
    public bool ExecutableNotFound { get; } = executableNotFound;
}

/// <summary>Resultado do pull: digest resolvido e tamanho, ou código de falha do contrato da 012.</summary>
public sealed record ImagePullResult(bool Succeeded, string? FailureCode, string? ResolvedDigest = null, long? SizeBytes = null);

public sealed record EnginePortBinding(string? HostIp, int HostPort, int ContainerPort, string Protocol);

/// <summary>Descritor do host (spec 008): engine detectado, nome, CPUs, memória e modo rootless.</summary>
public sealed record EngineHostInfo(string Engine, string? HostName, int? CpuCount, long? MemoryBytes, bool? Rootless);

/// <summary>Evento bruto do stream `/events` do engine, já reduzido à allowlist de atributos.</summary>
public sealed record EngineEventRecord(
    string Type,
    string Action,
    string SubjectId,
    string? Name,
    string? Image,
    int? ExitCode,
    DateTimeOffset OccurredAtUtc,
    long TimeNano);

public sealed record ContainerMutationResult(bool Succeeded, string? FailureCode);

public interface IContainerEngine
{
    Task<EngineDescriptor> InspectAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<EngineContainer>> ListContainersAsync(CancellationToken cancellationToken);

    Task<EngineContainer?> InspectContainerAsync(string containerId, CancellationToken cancellationToken);

    Task<ContainerMutationResult> ExecuteAsync(
        AgentCommandKind commandKind,
        string containerId,
        CancellationToken cancellationToken);

    Task<EngineHostInfo?> DescribeHostAsync(CancellationToken cancellationToken) =>
        Task.FromResult<EngineHostInfo?>(null);

    IAsyncEnumerable<EngineEventRecord> WatchEventsAsync(CancellationToken cancellationToken) =>
        AsyncEnumerable.Empty<EngineEventRecord>();

    /// <summary>JSON bruto de `/containers/{id}/json` (nulo quando o container não existe). Nunca sai do agente sem sanitização.</summary>
    Task<string?> GetContainerJsonAsync(string containerId, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);

    /// <summary>Linhas de log (com timestamp do engine) de stdout e stderr; `Follow` mantém o stream aberto.</summary>
    IAsyncEnumerable<EngineLogLine> ReadLogsAsync(string containerId, EngineLogRequest request, CancellationToken cancellationToken) =>
        AsyncEnumerable.Empty<EngineLogLine>();

    /// <summary>Imagens locais (`/images/json`, spec 012).</summary>
    Task<IReadOnlyList<EngineImage>> ListImagesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EngineImage>>([]);

    /// <summary>Baixa a imagem (`/images/create`); credenciais, se houver, ficam no host (spec 012).</summary>
    Task<ImagePullResult> PullImageAsync(Dokpod.Domain.Images.ImageReference reference, CancellationToken cancellationToken) =>
        Task.FromResult(new ImagePullResult(false, "not_supported"));

    /// <summary>Inspeção da imagem local (`/images/{ref}/json`) para presença e `Config.Volumes`.</summary>
    Task<EngineImageConfig> InspectImageConfigAsync(Dokpod.Domain.Images.ImageReference reference, CancellationToken cancellationToken) =>
        Task.FromResult(new EngineImageConfig(false, []));

    /// <summary>Container com o label informado (reconciliação por `dokpod.command-id`, D-004).</summary>
    Task<EngineContainer?> FindByLabelAsync(string label, string value, CancellationToken cancellationToken) =>
        Task.FromResult<EngineContainer?>(null);

    /// <summary>`POST /containers/create` (ou Libpod no Podman, quando exigido); não inicia o container.</summary>
    Task<EngineCreateResult> CreateContainerAsync(EngineCreateRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new EngineCreateResult(null, "not_supported"));

    /// <summary>Inicia o container recém-criado; devolve código de falha do contrato da 013 ou nulo.</summary>
    Task<string?> StartCreatedContainerAsync(string containerId, CancellationToken cancellationToken) =>
        Task.FromResult<string?>("not_supported");

    /// <summary>Remove o container recém-criado (rollback de conflito de porta); nunca remove volumes.</summary>
    Task RemoveCreatedContainerAsync(string containerId, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Volumes do host (`/volumes`, spec 018).</summary>
    Task<IReadOnlyList<EngineVolume>> ListVolumesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EngineVolume>>([]);

    /// <summary>`DELETE /volumes/{name}` sem `force`; devolve código de falha da spec 018 ou nulo quando removido.</summary>
    Task<string?> RemoveVolumeAsync(string name, CancellationToken cancellationToken) =>
        Task.FromResult<string?>("not_supported");

    /// <summary>Tamanho e referências do volume por `system df`; nulo quando o engine não informa.</summary>
    Task<EngineVolumeUsage?> GetVolumeUsageAsync(string name, CancellationToken cancellationToken) =>
        Task.FromResult<EngineVolumeUsage?>(null);

    /// <summary>Configuração de registros (`/libpod/info` no Podman, `/info` no Docker) e registros com credencial (spec 020).</summary>
    Task<EngineRegistryConfig> GetRegistryConfigAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new EngineRegistryConfig(false, [], []));

    /// <summary>Pods do host (`/libpod/pods/json`, spec 021); nulo quando o engine não tem pods (Docker).</summary>
    Task<IReadOnlyList<EnginePod>?> ListPodsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EnginePod>?>(null);

    /// <summary>`POST /libpod/pods/{id}/{ação}`; código de falha da spec 021 ou nulo (304 = já no estado).</summary>
    Task<string?> PodActionAsync(string podId, Dokpod.Domain.Pods.PodActionKind action, CancellationToken cancellationToken) =>
        Task.FromResult<string?>("not_supported");

    /// <summary>Redes do host (`/networks`, spec 019).</summary>
    Task<IReadOnlyList<EngineNetwork>> ListNetworksAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EngineNetwork>>([]);

    /// <summary>`DELETE /networks/{id}`; devolve código de falha da spec 019 ou nulo quando removida. Nunca desconecta containers.</summary>
    Task<string?> RemoveNetworkAsync(string networkId, CancellationToken cancellationToken) =>
        Task.FromResult<string?>("not_supported");

    /// <summary>Nomes de segredos do Podman, redes ou volumes do host (sem valores).</summary>
    Task<IReadOnlyList<EngineHostObject>> ListHostObjectsAsync(EngineHostObjectKind kind, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EngineHostObject>>([]);

    /// <summary>Cria e inicia exec com TTY e comando fixo (spec 014); conexão sequestrada (`Upgrade: tcp`).</summary>
    Task<EngineExecSession> StartExecAsync(string containerId, IReadOnlyList<string> command, int cols, int rows, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Exec is not supported by this engine adapter.");

    /// <summary>`GET /exec/{id}/json`: nulo se o exec não existe mais.</summary>
    Task<EngineExecState?> InspectExecAsync(string execId, CancellationToken cancellationToken) =>
        Task.FromResult<EngineExecState?>(null);

    Task ResizeExecAsync(string execId, int cols, int rows, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Envia sinal ao processo do shell da sessão (PID no namespace do container) por um exec destacado e fixo
    /// (`kill -HUP|-KILL &lt;pid&gt;`): o engine não encerra execs ao fechar a conexão (spec 014 D-007).
    /// </summary>
    Task SignalContainerProcessAsync(string containerId, int pid, string signal, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>`docker` ou `podman`, detectado na negociação.</summary>
    Task<string> GetEngineKindAsync(CancellationToken cancellationToken) => Task.FromResult("docker");

    /// <summary>Contadores brutos de `/containers/{id}/stats?stream=false` (spec 009); nulo se indisponível.</summary>
    Task<EngineContainerStats?> GetContainerStatsAsync(string containerId, CancellationToken cancellationToken) =>
        Task.FromResult<EngineContainerStats?>(null);
}

public sealed record EngineContainerStats(
    ulong CpuTotalNanos,
    ulong SystemCpuNanos,
    int OnlineCpus,
    ulong MemoryUsedBytes,
    ulong? MemoryLimitBytes,
    ulong NetRxBytes,
    ulong NetTxBytes,
    ulong BlockReadBytes,
    ulong BlockWriteBytes);

public sealed record EngineLogRequest(int? Tail, DateTimeOffset? Since, DateTimeOffset? Until, bool Follow);

/// <summary>Stream 1 = stdout, 2 = stderr; `Raw` começa com o timestamp RFC 3339 do engine.</summary>
public sealed record EngineLogLine(int Stream, string Raw);