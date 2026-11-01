using System.Data.Common;
using Dokpod.ControlPlane.Application.AuditExport;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using static Dokpod.ControlPlane.Infrastructure.AuditExport.AuditExportSql;

namespace Dokpod.ControlPlane.Infrastructure.AuditExport;

/// <summary>
/// Destinos, cursor e lease (spec 027 data-model.md, D-003, D-011). O limite de 5 destinos e a chave de idempotência
/// são conferidos sob lock da tabela; o cursor só avança pela réplica dona do lease e nunca para trás. Excluir o
/// destino apaga o cursor, e a réplica que enviava perde o lease na próxima operação.
/// </summary>
public sealed class AuditExportStore(ControlPlaneDbContext dbContext) : IAuditExportStore
{
    private const string Select =
        """
        SELECT d.destination_id, d.name, d.protocol, d.host, d.port, d.path, d.secret_ref, d.categories, d.active, d.version,
               d.created_at_utc, d.updated_at_utc,
               c.last_tx_id::text, c.last_seq, c.last_sent_at_utc, c.last_error_code, c.last_error_at_utc, c.last_gap_at_utc,
               c.lease_owner, c.lease_until_utc
        FROM dokpod.audit_export_destinations d
        JOIN dokpod.audit_export_cursors c ON c.destination_id = d.destination_id
        """;

    public async Task<IReadOnlyList<AuditExportDestinationState>> ListAsync(CancellationToken cancellationToken) =>
        await QueryAsync(dbContext, $"{Select} ORDER BY lower(d.name)", [], Read, cancellationToken).ConfigureAwait(false);

    public async Task<AuditExportDestinationState?> GetAsync(Guid destinationId, CancellationToken cancellationToken) =>
        (await QueryAsync(dbContext, $"{Select} WHERE d.destination_id = @id", [P("id", destinationId)], Read, cancellationToken).ConfigureAwait(false))
            .SingleOrDefault();

    public async Task<AuditExportWriteResult> CreateAsync(
        AuditExportDestinationInput input, string idempotencyKey, DateTimeOffset nowUtc, CancellationToken cancellationToken,
        Func<AuditExportDestination, CancellationToken, Task>? withinTransaction = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        if (idempotencyKey.Length > 128) throw new ArgumentException("Idempotency key is too long.", nameof(idempotencyKey));
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(dbContext, "LOCK TABLE dokpod.audit_export_destinations IN SHARE ROW EXCLUSIVE MODE", [], cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(
            dbContext,
            "UPDATE dokpod.audit_export_destinations SET idempotency_key = NULL, idempotency_expires_at_utc = NULL WHERE idempotency_expires_at_utc <= @now",
            [P("now", nowUtc)],
            cancellationToken).ConfigureAwait(false);

        var replayed = (await QueryAsync(
            dbContext, $"{Select} WHERE d.idempotency_key = @key", [P("key", idempotencyKey)], Read, cancellationToken).ConfigureAwait(false)).SingleOrDefault();
        if (replayed is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new AuditExportWriteResult(AuditExportWriteOutcome.Replayed, replayed.Destination);
        }

        var (count, nameTaken) = (await QueryAsync(
            dbContext,
            "SELECT count(*), count(*) FILTER (WHERE lower(name) = lower(@name)) FROM dokpod.audit_export_destinations",
            [P("name", input.Name)],
            reader => (reader.GetInt64(0), reader.GetInt64(1) > 0),
            cancellationToken).ConfigureAwait(false))[0];
        if (nameTaken) return new AuditExportWriteResult(AuditExportWriteOutcome.NameTaken, null);
        if (count >= AuditExportLimits.MaxDestinations) return new AuditExportWriteResult(AuditExportWriteOutcome.LimitReached, null);

        var destinationId = Guid.NewGuid();
        await ExecuteAsync(
            dbContext,
            """
            INSERT INTO dokpod.audit_export_destinations (
                destination_id, name, protocol, host, port, path, secret_ref, categories, active, version,
                idempotency_key, idempotency_expires_at_utc, created_at_utc, updated_at_utc)
            VALUES (@id, @name, @protocol, @host, @port, @path, @secret, @categories, @active, 1, @key, @expires, @now, @now);
            -- D-001: o destino nasce no fim da trilha; nada confirmado antes do xmin atual é enviado.
            INSERT INTO dokpod.audit_export_cursors (destination_id, last_tx_id, last_seq)
            VALUES (@id, pg_snapshot_xmin(pg_current_snapshot()), 0);
            """,
            [.. Parameters(destinationId, input), P("key", idempotencyKey), P("expires", nowUtc + AuditExportLimits.IdempotencyWindow), P("now", nowUtc)],
            cancellationToken).ConfigureAwait(false);
        var created = (await GetAsync(destinationId, cancellationToken).ConfigureAwait(false))!.Destination;
        if (withinTransaction is not null) await withinTransaction(created, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new AuditExportWriteResult(AuditExportWriteOutcome.Created, created);
    }

    public async Task<AuditExportWriteResult> UpdateAsync(
        Guid destinationId, AuditExportDestinationInput input, long expectedVersion, DateTimeOffset nowUtc, CancellationToken cancellationToken,
        Func<AuditExportDestination, CancellationToken, Task>? withinTransaction = null)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        int updated;
        try
        {
            updated = await ExecuteAsync(
                dbContext,
                """
                UPDATE dokpod.audit_export_destinations
                SET name = @name, protocol = @protocol, host = @host, port = @port, path = @path, secret_ref = @secret,
                    categories = @categories, active = @active, version = version + 1, updated_at_utc = @now
                WHERE destination_id = @id AND version = @version
                """,
                [.. Parameters(destinationId, input), P("version", expectedVersion), P("now", nowUtc)],
                cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return new AuditExportWriteResult(AuditExportWriteOutcome.NameTaken, null);
        }

        if (updated != 1)
        {
            return new AuditExportWriteResult(await ExistsAsync(destinationId, cancellationToken).ConfigureAwait(false) ? AuditExportWriteOutcome.VersionConflict : AuditExportWriteOutcome.NotFound, null);
        }

        var destination = (await GetAsync(destinationId, cancellationToken).ConfigureAwait(false))!.Destination;
        if (withinTransaction is not null) await withinTransaction(destination, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new AuditExportWriteResult(AuditExportWriteOutcome.Updated, destination);
    }

    public async Task<AuditExportWriteOutcome> DeleteAsync(
        Guid destinationId, long expectedVersion, CancellationToken cancellationToken,
        Func<AuditExportDestination, CancellationToken, Task>? withinTransaction = null)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var existing = (await QueryAsync(
            dbContext, $"{Select} WHERE d.destination_id = @id FOR UPDATE OF d", [P("id", destinationId)], Read, cancellationToken).ConfigureAwait(false)).SingleOrDefault();
        if (existing is null) return AuditExportWriteOutcome.NotFound;
        if (existing.Destination.Version != expectedVersion) return AuditExportWriteOutcome.VersionConflict;

        await ExecuteAsync(
            dbContext, "DELETE FROM dokpod.audit_export_destinations WHERE destination_id = @id", [P("id", destinationId)], cancellationToken).ConfigureAwait(false);
        if (withinTransaction is not null) await withinTransaction(existing.Destination, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return AuditExportWriteOutcome.Deleted;
    }

    public async Task<bool> TryAcquireLeaseAsync(Guid destinationId, string owner, DateTimeOffset nowUtc, TimeSpan duration, CancellationToken cancellationToken) =>
        await ExecuteAsync(
            dbContext,
            """
            UPDATE dokpod.audit_export_cursors SET lease_owner = @owner, lease_until_utc = @until
            WHERE destination_id = @id AND (lease_owner IS NULL OR lease_owner = @owner OR lease_until_utc <= @now)
            """,
            [P("id", destinationId), P("owner", owner), P("now", nowUtc), P("until", nowUtc + duration)],
            cancellationToken).ConfigureAwait(false) == 1;

    public async Task<bool> AdvanceAsync(Guid destinationId, string owner, AuditExportPosition position, DateTimeOffset? sentAtUtc, CancellationToken cancellationToken) =>
        await ExecuteAsync(
            dbContext,
            """
            UPDATE dokpod.audit_export_cursors
            SET last_tx_id = @tx::text::xid8, last_seq = @seq,
                last_sent_at_utc = COALESCE(@sent, last_sent_at_utc),
                last_error_code = CASE WHEN @sent IS NULL THEN last_error_code END,
                last_error_at_utc = CASE WHEN @sent IS NULL THEN last_error_at_utc END
            WHERE destination_id = @id AND lease_owner = @owner AND (last_tx_id, last_seq) < (@tx::text::xid8, @seq)
            """,
            [
                P("id", destinationId), P("owner", owner), Xid8Parameter("tx", position.TxId), P("seq", position.Seq),
                new NpgsqlParameter("sent", NpgsqlDbType.TimestampTz) { Value = (object?)sentAtUtc ?? DBNull.Value },
            ],
            cancellationToken).ConfigureAwait(false) == 1;

    public async Task<bool> RecordFailureAsync(Guid destinationId, string owner, string errorCode, DateTimeOffset atUtc, CancellationToken cancellationToken) =>
        await ExecuteAsync(
            dbContext,
            "UPDATE dokpod.audit_export_cursors SET last_error_code = @code, last_error_at_utc = @at WHERE destination_id = @id AND lease_owner = @owner",
            [P("id", destinationId), P("owner", owner), P("code", errorCode), P("at", atUtc)],
            cancellationToken).ConfigureAwait(false) == 1;

    public async Task ReleaseLeaseAsync(Guid destinationId, string owner, CancellationToken cancellationToken) =>
        await ExecuteAsync(
            dbContext,
            "UPDATE dokpod.audit_export_cursors SET lease_owner = NULL, lease_until_utc = NULL WHERE destination_id = @id AND lease_owner = @owner",
            [P("id", destinationId), P("owner", owner)],
            cancellationToken).ConfigureAwait(false);

    private async Task<bool> ExistsAsync(Guid destinationId, CancellationToken cancellationToken) =>
        (await QueryAsync(
            dbContext, "SELECT 1 FROM dokpod.audit_export_destinations WHERE destination_id = @id", [P("id", destinationId)], _ => true, cancellationToken)
            .ConfigureAwait(false)).Count > 0;

    private static NpgsqlParameter[] Parameters(Guid destinationId, AuditExportDestinationInput input) =>
    [
        P("id", destinationId),
        P("name", input.Name),
        P("protocol", input.Protocol),
        P("host", input.Host),
        P("port", input.Port),
        new("path", NpgsqlDbType.Varchar) { Value = (object?)input.Path ?? DBNull.Value },
        new("secret", NpgsqlDbType.Varchar) { Value = (object?)input.SecretRef ?? DBNull.Value },
        new("categories", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = input.Categories.ToArray() },
        P("active", input.Active),
    ];

    private static AuditExportDestinationState Read(DbDataReader reader)
    {
        string? Text(int i) => reader.IsDBNull(i) ? null : reader.GetString(i);
        var destination = new AuditExportDestination(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetInt32(4),
            Text(5),
            Text(6),
            reader.GetFieldValue<string[]>(7),
            reader.GetBoolean(8),
            reader.GetInt64(9),
            Instant(reader, 10)!.Value,
            Instant(reader, 11)!.Value);
        var cursor = new AuditExportCursor(
            destination.DestinationId,
            new AuditExportPosition(Xid8(reader, 12), reader.GetInt64(13)),
            Instant(reader, 14),
            Text(15),
            Instant(reader, 16),
            Instant(reader, 17),
            Text(18),
            Instant(reader, 19));
        return new AuditExportDestinationState(destination, cursor);
    }
}
