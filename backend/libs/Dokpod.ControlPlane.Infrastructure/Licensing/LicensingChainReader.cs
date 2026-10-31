using Dokpod.ControlPlane.Application.Licensing;

namespace Dokpod.ControlPlane.Infrastructure.Licensing;

/// <summary>Cadeia em ordem de <c>chain_seq</c> até o evento do relatório (T060); <c>actor_id</c> e a chave HMAC nunca saem daqui.</summary>
public sealed class LicensingChainReader(ControlPlaneDbContext dbContext) : ILicensingChainReader
{
    public async Task<IReadOnlyList<LicensingChainRecord>> ReadChainAsync(Guid lastEventId, CancellationToken cancellationToken) =>
        await LicensingSql.QueryAsync(
            dbContext,
            """
            SELECT chain_seq, event_id, occurred_at_utc, actor_ref, correlation_id, action, details::text, prev_hash, record_hash
            FROM dokpod.licensing_events
            WHERE chain_seq <= (SELECT chain_seq FROM dokpod.licensing_events WHERE event_id = @lastEventId)
            ORDER BY chain_seq
            """,
            [new Npgsql.NpgsqlParameter("lastEventId", lastEventId)],
            reader => new LicensingChainRecord(
                reader.GetInt64(0),
                reader.GetGuid(1),
                LicensingSql.Instant(reader, 2)!.Value,
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetGuid(4),
                reader.GetString(5),
                LicensingChain.Canonical(System.Text.Json.Nodes.JsonNode.Parse(reader.GetString(6))),
                (byte[])reader.GetValue(7),
                (byte[])reader.GetValue(8)),
            cancellationToken).ConfigureAwait(false);
}
