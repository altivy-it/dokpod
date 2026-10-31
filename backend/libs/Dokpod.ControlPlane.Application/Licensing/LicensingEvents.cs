using System.Text.Json.Nodes;

namespace Dokpod.ControlPlane.Application.Licensing;

/// <summary>
/// Evento da trilha global de licenciamento. <paramref name="ActorId"/> é o sujeito do Keycloak, ou null para o
/// sistema; ele fica só na trilha local e entra na cadeia como HMAC (<c>actor_ref</c>). <paramref name="Details"/>
/// aceita só objetos, arrays, strings, inteiros, booleanos e null, nas chaves de <see cref="LicensingEventAction.AllowedDetails"/>.
/// </summary>
public sealed record LicensingEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    string? ActorId,
    Guid CorrelationId,
    string Action,
    JsonObject Details);

/// <summary>Elo gravado na cadeia (contracts/report-format.md).</summary>
public sealed record LicensingChainLink(long ChainSeq, byte[] RecordHash);

public interface ILicensingAuditWriter
{
    /// <summary>Grava o evento e avança a cabeça da cadeia na mesma transação; junta-se à transação corrente, se houver.</summary>
    Task<LicensingChainLink> AppendAsync(LicensingEvent licensingEvent, CancellationToken cancellationToken);
}
