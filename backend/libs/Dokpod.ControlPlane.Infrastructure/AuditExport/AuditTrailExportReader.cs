using Dokpod.ControlPlane.Application.AuditExport;
using Npgsql;
using NpgsqlTypes;
using static Dokpod.ControlPlane.Infrastructure.AuditExport.AuditExportSql;

namespace Dokpod.ControlPlane.Infrastructure.AuditExport;

/// <summary>
/// Trilha na ordem de confirmação (spec 027 D-001). A leitura só devolve linhas com <c>tx_id</c> abaixo do
/// <c>xmin</c> do snapshot: nenhuma transação que ainda possa confirmar fica para trás do cursor, então nada se perde
/// mesmo quando transações concorrentes confirmam fora da ordem de <c>occurred_at_utc</c>.
/// </summary>
public sealed class AuditTrailExportReader(ControlPlaneDbContext dbContext) : IAuditTrailExportReader
{
    public async Task<AuditExportPosition> HorizonAsync(CancellationToken cancellationToken) =>
        (await QueryAsync(
            dbContext,
            "SELECT pg_snapshot_xmin(pg_current_snapshot())::text",
            [],
            reader => new AuditExportPosition(Xid8(reader, 0), 0),
            cancellationToken).ConfigureAwait(false))[0];

    public async Task<IReadOnlyList<AuditTrailExportItem>> ReadAsync(
        AuditExportPosition after, IReadOnlyCollection<string> actions, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        if (actions.Count == 0) return [];
        return await QueryAsync(
            dbContext,
            $"""
            SELECT {PostgresAuditEventReader.Columns}, tx_id::text, export_seq
            FROM dokpod.audit_events
            WHERE tx_id IS NOT NULL
              AND (tx_id, export_seq) > (@tx::text::xid8, @seq)
              AND tx_id < pg_snapshot_xmin(pg_current_snapshot())
              AND action = ANY(@actions)
            ORDER BY tx_id, export_seq
            LIMIT @limit
            """,
            [Xid8Parameter("tx", after.TxId), P("seq", after.Seq), Actions(actions), P("limit", limit)],
            reader => new AuditTrailExportItem(new AuditExportPosition(Xid8(reader, 13), reader.GetInt64(14)), PostgresAuditEventReader.Read(reader)),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Pendentes já visíveis (confirmados) após o cursor, contados até <paramref name="cap"/>.</summary>
    public async Task<AuditExportPending> PendingAsync(AuditExportPosition after, IReadOnlyCollection<string> actions, int cap, CancellationToken cancellationToken)
    {
        if (actions.Count == 0) return new AuditExportPending(0, false, null);
        IReadOnlyList<NpgsqlParameter> parameters = [Xid8Parameter("tx", after.TxId), P("seq", after.Seq), Actions(actions), P("cap", cap + 1)];
        const string Pending = "tx_id IS NOT NULL AND (tx_id, export_seq) > (@tx::text::xid8, @seq) AND action = ANY(@actions)";
        var count = (await QueryAsync(
            dbContext,
            $"SELECT count(*) FROM (SELECT 1 FROM dokpod.audit_events WHERE {Pending} LIMIT @cap) AS capped",
            parameters,
            reader => reader.GetInt64(0),
            cancellationToken).ConfigureAwait(false))[0];
        var oldest = (await QueryAsync(
            dbContext,
            $"SELECT occurred_at_utc FROM dokpod.audit_events WHERE {Pending} ORDER BY tx_id, export_seq LIMIT 1",
            parameters,
            reader => Instant(reader, 0),
            cancellationToken).ConfigureAwait(false)).FirstOrDefault();
        return new AuditExportPending(Math.Min(count, cap), count > cap, oldest);
    }

    public async Task<AuditTrailGap?> GapAsync(
        AuditExportPosition after, IReadOnlyCollection<string> actions, DateTimeOffset cutoffUtc, CancellationToken cancellationToken)
    {
        if (actions.Count == 0) return null;
        // Uma instrução, um snapshot: só o que já está abaixo do xmin e antes do primeiro evento dentro da janela.
        var rows = await QueryAsync(
            dbContext,
            """
            WITH pending AS (
                SELECT tx_id, export_seq, occurred_at_utc
                FROM dokpod.audit_events
                WHERE tx_id IS NOT NULL
                  AND (tx_id, export_seq) > (@tx::text::xid8, @seq)
                  AND tx_id < pg_snapshot_xmin(pg_current_snapshot())
                  AND action = ANY(@actions)
            ),
            first_kept AS (
                SELECT tx_id, export_seq FROM pending WHERE occurred_at_utc >= @cutoff ORDER BY tx_id, export_seq LIMIT 1
            ),
            skipped AS (
                SELECT * FROM pending
                WHERE NOT EXISTS (SELECT 1 FROM first_kept) OR (tx_id, export_seq) < (SELECT tx_id, export_seq FROM first_kept)
            ),
            last_skipped AS (
                SELECT tx_id, export_seq FROM skipped ORDER BY tx_id DESC, export_seq DESC LIMIT 1
            )
            SELECT count(*), min(s.occurred_at_utc), max(s.occurred_at_utc),
                   (SELECT tx_id::text FROM last_skipped), (SELECT export_seq FROM last_skipped)
            FROM skipped s
            """,
            [Xid8Parameter("tx", after.TxId), P("seq", after.Seq), Actions(actions), P("cutoff", cutoffUtc)],
            reader => reader.GetInt64(0) == 0
                ? null
                : new AuditTrailGap(new AuditExportPosition(Xid8(reader, 3), reader.GetInt64(4)), reader.GetInt64(0), Instant(reader, 1)!.Value, Instant(reader, 2)!.Value),
            cancellationToken).ConfigureAwait(false);
        return rows[0];
    }

    private static NpgsqlParameter Actions(IReadOnlyCollection<string> actions) =>
        new("actions", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = actions.ToArray() };
}
