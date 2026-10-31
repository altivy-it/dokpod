namespace Dokpod.ControlPlane.Application.Licensing;

/// <summary>Linha única de <c>licensing_state</c>; prazos persistidos nunca recomeçam (D-006).</summary>
public sealed record LicensingStateRecord(
    Guid InstallationId,
    string? InstallationSlotId,
    Guid? ActiveRevisionId,
    long RevocationSequence,
    DateTimeOffset? ExpiryGraceUntilUtc,
    DateTimeOffset? ExcessStartedAtUtc,
    DateTimeOffset? ExcessGraceUntilUtc,
    DateTimeOffset LastTrustedInstantUtc,
    long ChainHeadSeq,
    long Version);

public interface ILicenseStateReader
{
    /// <summary>Null enquanto a composição Business nunca gravou estado.</summary>
    Task<LicensingStateRecord?> ReadAsync(CancellationToken cancellationToken);
}

/// <summary>Documento recebido (licença ou revogação). <c>Content</c> só existe em documentos aceitos (threat model L4).</summary>
public sealed record LicenseRevisionRecord(
    Guid RevisionId,
    string DocumentType,
    DateTimeOffset ReceivedAtUtc,
    string ReceivedBy,
    byte[] ContentSha256,
    byte[]? Content,
    string? LicenseId,
    int? Revision,
    string Outcome,
    string? FailureCode);

public enum LicenseActivationOutcome
{
    Activated,

    /// <summary>O <c>version</c> do estado não é mais o esperado (<c>If-Match</c>).</summary>
    Conflict,

    /// <summary>A mesma licença e revisão já foi aceita antes (índice único de revisões aceitas).</summary>
    Duplicate,
}

public interface ILicenseStore
{
    Task<LicenseRevisionRecord?> GetActiveAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Numa transação: grava <paramref name="revision"/> (null mantém a revisão ativa e só troca o slot), aponta o estado
    /// para ela e para <paramref name="slotId"/> se <paramref name="expectedVersion"/> conferir, e grava os eventos.
    /// </summary>
    Task<LicenseActivationOutcome> ActivateAsync(
        LicenseRevisionRecord? revision,
        string slotId,
        long expectedVersion,
        IReadOnlyList<LicensingEvent> events,
        CancellationToken cancellationToken);

    /// <summary>Registra o documento recusado e o evento, sem tocar o estado (FR-009).</summary>
    Task RecordRejectedAsync(LicenseRevisionRecord revision, LicensingEvent rejected, CancellationToken cancellationToken);
}

public static class CapacitySource
{
    public const string Engine = "engine";
    public const string ProcessFallback = "process_fallback";
    public const string Unknown = "unknown";
}

/// <summary>Observação de capacidade de um host; <c>Vcpus</c> é null quando a origem não é o engine (FR-013).</summary>
public sealed record CapacityObservation(Guid HostId, DateTimeOffset ObservedAtUtc, int? Vcpus, string Source, Guid EnvironmentId);

public sealed record LicensingHostRecord(Guid HostId, string Label, DateTimeOffset CreatedAtUtc, DateTimeOffset? RemovedAtUtc);

public sealed record LicensingHostLink(Guid HostId, Guid EnvironmentId, DateTimeOffset LinkedAtUtc, DateTimeOffset? UnlinkedAtUtc);

/// <summary>
/// Dados brutos da apuração (D-005): hosts e associações que tocam a janela, a última observação de cada ambiente
/// antes dela (observação retida ou âncora) e todas as observações da janela.
/// </summary>
public sealed record MeteringInput(
    IReadOnlyList<LicensingHostRecord> Hosts,
    IReadOnlyList<LicensingHostLink> Links,
    IReadOnlyList<CapacityObservation> Observations);

public interface ICapacityLedger
{
    /// <summary>Grava a observação da conexão do agente no host associado ao ambiente (D-005).</summary>
    Task RecordAsync(CapacityObservation observation, CancellationToken cancellationToken);

    Task<MeteringInput> ReadMeteringInputAsync(DateTimeOffset windowStartUtc, CancellationToken cancellationToken);

    /// <summary>Grava as âncoras e remove observações mais antigas que <paramref name="retention"/> (mínimo 35 dias).</summary>
    Task<long> ApplyRetentionAsync(TimeSpan retention, CancellationToken cancellationToken);
}

/// <summary>
/// Recebe a CPU informada no <c>AgentHello</c> (spec 031 T037). Só a composição Business registra uma implementação;
/// falha aqui nunca derruba a sessão do agente.
/// </summary>
public interface IAgentCapacityObserver
{
    Task OnAgentHelloAsync(Guid environmentId, string? hostName, int? cpuCount, string source, DateTimeOffset observedAtUtc, CancellationToken cancellationToken);
}

public enum LicensingHostChange
{
    Applied,
    Unchanged,
    NotFound,
    Removed,
}

/// <summary>Hosts locais de licenciamento e suas associações (D-004, FR-012); toda mudança gera evento na cadeia.</summary>
public interface ILicensingHostStore
{
    Task<IReadOnlyList<LicensingHostRecord>> ListHostsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<LicensingHostLink>> ListActiveLinksAsync(CancellationToken cancellationToken);

    /// <summary>Cria o host com o ID do cliente; repetir com o mesmo ID não muda nada (idempotente).</summary>
    Task<LicensingHostChange> CreateHostAsync(Guid hostId, string label, string? actorId, Guid correlationId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Associa o ambiente ao host; se ele estava em outro host ativo, desassocia de lá na mesma transação.</summary>
    Task<LicensingHostChange> LinkAsync(Guid hostId, Guid environmentId, string? actorId, Guid correlationId, DateTimeOffset now, CancellationToken cancellationToken);

    Task<LicensingHostChange> UnlinkAsync(Guid hostId, Guid environmentId, string? actorId, Guid correlationId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Encerra a participação futura do host sem apagar o histórico (FR-014).</summary>
    Task<LicensingHostChange> RemoveHostAsync(Guid hostId, string? actorId, Guid correlationId, DateTimeOffset now, CancellationToken cancellationToken);
}

/// <summary>Decisão comercial tipada; nunca substitui a autorização do Keycloak (FR-022) nem vira falha de autenticação (FR-024).</summary>
public sealed record BusinessCapabilityDecision(bool Allowed, string? DenialCode, IReadOnlyList<string> Reasons)
{
    public static BusinessCapabilityDecision Allow { get; } = new(true, null, []);

    public static BusinessCapabilityDecision Deny(string denialCode, IReadOnlyList<string> reasons) => new(false, denialCode, reasons);
}

/// <summary>Só existe na composição Business; casos de uso da CE nunca dependem dele (D-009).</summary>
public interface IBusinessCapabilityGate
{
    Task<BusinessCapabilityDecision> EnsureAsync(string capability, CancellationToken cancellationToken);
}
