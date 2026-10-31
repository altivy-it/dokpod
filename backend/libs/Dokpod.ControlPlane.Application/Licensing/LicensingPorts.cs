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

public interface ILicenseStore
{
    Task<LicenseRevisionRecord?> GetActiveAsync(CancellationToken cancellationToken);

    /// <summary>Grava a revisão aceita e a torna ativa se <paramref name="expectedVersion"/> conferir; false no conflito.</summary>
    Task<bool> ActivateAsync(LicenseRevisionRecord revision, long expectedVersion, CancellationToken cancellationToken);

    /// <summary>Registra o documento recusado sem tocar o estado (FR-009).</summary>
    Task RecordRejectedAsync(LicenseRevisionRecord revision, CancellationToken cancellationToken);
}

public static class CapacitySource
{
    public const string Engine = "engine";
    public const string ProcessFallback = "process_fallback";
    public const string Unknown = "unknown";
}

/// <summary>Observação de capacidade de um host; <c>Vcpus</c> é null quando desconhecida (FR-013).</summary>
public sealed record CapacityObservation(Guid HostId, DateTimeOffset ObservedAtUtc, int? Vcpus, string Source, Guid EnvironmentId);

public interface ICapacityLedger
{
    /// <summary>Grava só quando a capacidade confiável muda ou na conexão do agente (D-005).</summary>
    Task RecordAsync(CapacityObservation observation, CancellationToken cancellationToken);

    /// <summary>Âncora de cada host mais as observações de [<paramref name="fromUtc"/>, <paramref name="untilUtc"/>).</summary>
    Task<IReadOnlyList<CapacityObservation>> ReadWindowAsync(DateTimeOffset fromUtc, DateTimeOffset untilUtc, CancellationToken cancellationToken);
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
