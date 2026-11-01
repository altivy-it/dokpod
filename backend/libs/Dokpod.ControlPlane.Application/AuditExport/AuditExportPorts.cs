using System.Text.RegularExpressions;
using Dokpod.ControlPlane.Application.Auditing;
using Dokpod.Domain.Auditing;

namespace Dokpod.ControlPlane.Application.AuditExport;

public static class AuditExportProtocols
{
    public const string Syslog = "syslog";
    public const string Otlp = "otlp";
    public const string DefaultOtlpPath = "/v1/logs";
}

/// <summary>Códigos de falha de envio gravados no cursor (data-model.md); nunca contêm host, mensagem do servidor ou segredo.</summary>
public static class AuditExportErrorCodes
{
    public const string TlsError = "tls_error";
    public const string ConnectionRefused = "connection_refused";
    public const string Timeout = "timeout";
    public const string SecretMissing = "secret_missing";
    public const string Rejected = "rejected";
    public const string AddressBlocked = "address_blocked";
}

public static class AuditExportStates
{
    public const string Disabled = "disabled";
    public const string Ok = "ok";
    public const string CatchingUp = "catching_up";
    public const string Failing = "failing";
    public const string Gap = "gap";
}

public static class AuditExportLimits
{
    public const int MaxDestinations = 5;
    public const int MaxBatch = 500;
    public const int MaxPendingCount = 10_000;
    public const int MaxTestsPerMinute = 5;
    public static readonly TimeSpan BatchWait = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan IdempotencyWindow = TimeSpan.FromHours(24);
    public static readonly TimeSpan GapWindow = TimeSpan.FromHours(72);
    public static readonly TimeSpan GapNoticeWindow = TimeSpan.FromHours(24);
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);
}

/// <summary>Posição na ordem de confirmação da trilha (D-001): <c>tx_id</c> (<c>xid8</c>) e <c>export_seq</c>.</summary>
public readonly record struct AuditExportPosition(ulong TxId, long Seq) : IComparable<AuditExportPosition>
{
    public int CompareTo(AuditExportPosition other) => TxId != other.TxId ? TxId.CompareTo(other.TxId) : Seq.CompareTo(other.Seq);
}

/// <summary>Dados do destino como o administrador os envia; <see cref="Validate"/> devolve a forma normalizada.</summary>
public sealed partial record AuditExportDestinationInput(
    string Name,
    string Protocol,
    string Host,
    int Port,
    string? Path,
    string? SecretRef,
    IReadOnlyCollection<string> Categories,
    bool Active)
{
    public const string InvalidDestination = "audit_export_invalid_destination";
    public const string InvalidSecretRef = "audit_export_invalid_secret_ref";

    /// <summary>
    /// Mesmas regras da migration: nome 1–64 sem espaço nas pontas, protocolo conhecido, host DNS ou IP até 253,
    /// porta 1–65535, <c>path</c> só no OTLP (padrão <c>/v1/logs</c>), <c>secretRef</c> em <c>[a-z0-9-]{1,64}</c> e
    /// categorias conhecidas, sempre com <c>audit_export</c> (threat model L7).
    /// </summary>
    public (AuditExportDestinationInput? Normalized, string? ErrorCode) Validate()
    {
        var name = Name?.Trim() ?? string.Empty;
        if (name.Length is < 1 or > 64 || name.Any(char.IsControl)) return (null, InvalidDestination);
        if (Protocol is not (AuditExportProtocols.Syslog or AuditExportProtocols.Otlp)) return (null, InvalidDestination);
        var host = Host?.Trim() ?? string.Empty;
        if (host.Length is < 1 or > 253 || Uri.CheckHostName(host) == UriHostNameType.Unknown) return (null, InvalidDestination);
        if (Port is < 1 or > 65535) return (null, InvalidDestination);

        string? path = null;
        if (Protocol == AuditExportProtocols.Otlp)
        {
            path = string.IsNullOrEmpty(Path) ? AuditExportProtocols.DefaultOtlpPath : Path;
            if (path.Length > 256 || !path.StartsWith('/') || path.IndexOfAny(['?', '#', ' ']) >= 0 || path.Any(char.IsControl)) return (null, InvalidDestination);
        }
        else if (Path is not null)
        {
            return (null, InvalidDestination);
        }

        if (SecretRef is not null && !SecretRefPattern().IsMatch(SecretRef)) return (null, InvalidSecretRef);
        if (Categories is null || Categories.Any(category => category is null || !AuditCategories.Actions.ContainsKey(category))) return (null, InvalidDestination);

        var categories = Categories.Append(AuditCategories.AuditExport).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        return (this with { Name = name, Host = host, Path = path, Categories = categories }, null);
    }

    [GeneratedRegex(@"\A[a-z0-9-]{1,64}\z")]
    private static partial Regex SecretRefPattern();
}

public sealed record AuditExportDestination(
    Guid DestinationId,
    string Name,
    string Protocol,
    string Host,
    int Port,
    string? Path,
    string? SecretRef,
    IReadOnlyList<string> Categories,
    bool Active,
    long Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    /// <summary>Ações da trilha cobertas pelas categorias do destino.</summary>
    public IReadOnlyList<string> Actions =>
        [.. Categories.SelectMany(category => AuditCategories.Actions.TryGetValue(category, out var actions) ? actions : []).Distinct(StringComparer.Ordinal)];
}

public sealed record AuditExportCursor(
    Guid DestinationId,
    AuditExportPosition Position,
    DateTimeOffset? LastSentAtUtc,
    string? LastErrorCode,
    DateTimeOffset? LastErrorAtUtc,
    DateTimeOffset? LastGapAtUtc,
    string? LeaseOwner,
    DateTimeOffset? LeaseUntilUtc);

public sealed record AuditExportDestinationState(AuditExportDestination Destination, AuditExportCursor Cursor);

public sealed record AuditExportPending(long Count, bool Capped, DateTimeOffset? OldestAtUtc);

public sealed record AuditTrailExportItem(AuditExportPosition Position, AuditEventRecord Event);

public enum AuditExportWriteOutcome
{
    Created,
    Replayed,
    Updated,
    Deleted,
    NotFound,
    VersionConflict,
    NameTaken,
    LimitReached,
}

public sealed record AuditExportWriteResult(AuditExportWriteOutcome Outcome, AuditExportDestination? Destination);

/// <summary>Leitura da trilha na ordem de confirmação, só abaixo do <c>xmin</c> do snapshot (D-001).</summary>
public interface IAuditTrailExportReader
{
    /// <summary>Posição inicial de um destino novo: nada confirmado antes dela será enviado.</summary>
    Task<AuditExportPosition> HorizonAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditTrailExportItem>> ReadAsync(AuditExportPosition after, IReadOnlyCollection<string> actions, int limit, CancellationToken cancellationToken);

    Task<AuditExportPending> PendingAsync(AuditExportPosition after, IReadOnlyCollection<string> actions, int cap, CancellationToken cancellationToken);
}

/// <summary>Destinos, cursor e lease (data-model.md); o lease garante um só remetente por destino entre réplicas (D-003).</summary>
public interface IAuditExportStore
{
    Task<IReadOnlyList<AuditExportDestinationState>> ListAsync(CancellationToken cancellationToken);

    Task<AuditExportDestinationState?> GetAsync(Guid destinationId, CancellationToken cancellationToken);

    Task<AuditExportWriteResult> CreateAsync(AuditExportDestinationInput input, string idempotencyKey, DateTimeOffset nowUtc, CancellationToken cancellationToken);

    Task<AuditExportWriteResult> UpdateAsync(Guid destinationId, AuditExportDestinationInput input, long expectedVersion, DateTimeOffset nowUtc, CancellationToken cancellationToken);

    Task<AuditExportWriteOutcome> DeleteAsync(Guid destinationId, long expectedVersion, CancellationToken cancellationToken);

    Task<bool> TryAcquireLeaseAsync(Guid destinationId, string owner, DateTimeOffset nowUtc, TimeSpan duration, CancellationToken cancellationToken);

    Task<bool> AdvanceAsync(Guid destinationId, string owner, AuditExportPosition position, DateTimeOffset sentAtUtc, CancellationToken cancellationToken);

    Task<bool> RecordFailureAsync(Guid destinationId, string owner, string errorCode, DateTimeOffset atUtc, CancellationToken cancellationToken);

    Task ReleaseLeaseAsync(Guid destinationId, string owner, CancellationToken cancellationToken);
}

public sealed record AuditExportSendResult(bool Delivered, string? ErrorCode)
{
    public static AuditExportSendResult Ok { get; } = new(true, null);

    public static AuditExportSendResult Failed(string errorCode) => new(false, errorCode);
}

/// <summary>Transporte de um protocolo; confirma o lote inteiro ou devolve um código de falha sem detalhes do servidor.</summary>
public interface IAuditExportTransport
{
    string Protocol { get; }

    Task<AuditExportSendResult> SendAsync(AuditExportDestination destination, IReadOnlyList<AuditExportRecord> batch, CancellationToken cancellationToken);
}

/// <summary>Quais arquivos existem no secret do destino; o conteúdo nunca sai da API (D-006).</summary>
public sealed record AuditExportSecretFiles(bool Ca, bool ClientCertificate, bool Token);

/// <summary>Conteúdo lido para uma conexão; <see cref="ErrorCode"/> é <c>secret_missing</c> com um motivo curto sem conteúdo.</summary>
public sealed record AuditExportSecret(string? CaPem, string? ClientCertificatePem, string? ClientKeyPem, string? Token, string? ErrorCode, string? Reason)
{
    public static AuditExportSecret None { get; } = new(null, null, null, null, null, null);

    public static AuditExportSecret Missing(string reason) => new(null, null, null, null, AuditExportErrorCodes.SecretMissing, reason);
}

/// <summary>
/// Secret montado do destino (D-006; threat model B3, L4, L5, L9). Lido a cada conexão, sem cache, para que a troca de
/// certificado ou token valha na conexão seguinte.
/// </summary>
public interface IAuditExportSecretReader
{
    AuditExportSecretFiles Describe(string? secretRef);

    AuditExportSecret Read(string? secretRef);
}
