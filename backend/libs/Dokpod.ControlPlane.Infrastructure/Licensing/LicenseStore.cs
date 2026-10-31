using System.Data.Common;
using Dokpod.ControlPlane.Application.Licensing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace Dokpod.ControlPlane.Infrastructure.Licensing;

/// <summary>
/// Revisões importadas e estado ativo (spec 031 T025). Ativação e recusa acontecem numa única transação junto com os
/// eventos da cadeia; a troca de <c>active_revision_id</c> só ocorre se o <c>version</c> esperado conferir.
/// </summary>
public sealed class LicenseStore(ControlPlaneDbContext dbContext, ILicensingAuditWriter auditWriter) : ILicenseStore, ILicenseStateReader
{
    public async Task<LicensingStateRecord?> ReadAsync(CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var opened = await OpenAsync(connection, cancellationToken).ConfigureAwait(false);
        try
        {
            await using var command = CreateCommand(
                connection,
                """
                SELECT installation_id, installation_slot_id, active_revision_id, revocation_sequence, expiry_grace_until_utc,
                       excess_started_at_utc, excess_grace_until_utc, last_trusted_instant_utc, chain_head_seq, version
                FROM dokpod.licensing_state
                """);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
            return new LicensingStateRecord(
                reader.GetGuid(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetGuid(2),
                reader.GetInt64(3),
                Instant(reader, 4),
                Instant(reader, 5),
                Instant(reader, 6),
                Instant(reader, 7)!.Value,
                reader.GetInt64(8),
                reader.GetInt64(9));
        }
        finally
        {
            if (opened) await connection.CloseAsync().ConfigureAwait(false);
        }
    }

    public async Task<LicenseRevisionRecord?> GetActiveAsync(CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var opened = await OpenAsync(connection, cancellationToken).ConfigureAwait(false);
        try
        {
            await using var command = CreateCommand(
                connection,
                """
                SELECT r.revision_id, r.document_type, r.received_at_utc, r.received_by, r.content_sha256, r.content,
                       r.license_id, r.revision, r.outcome, r.failure_code
                FROM dokpod.licensing_state s
                JOIN dokpod.license_revisions r ON r.revision_id = s.active_revision_id
                """);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
            return new LicenseRevisionRecord(
                reader.GetGuid(0),
                reader.GetString(1),
                Instant(reader, 2)!.Value,
                reader.GetString(3),
                (byte[])reader.GetValue(4),
                reader.IsDBNull(5) ? null : (byte[])reader.GetValue(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetInt32(7),
                reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9));
        }
        finally
        {
            if (opened) await connection.CloseAsync().ConfigureAwait(false);
        }
    }

    public async Task<LicenseActivationOutcome> ActivateAsync(
        LicenseRevisionRecord? revision,
        string slotId,
        long expectedVersion,
        IReadOnlyList<LicensingEvent> events,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        ArgumentNullException.ThrowIfNull(events);
        if (revision is not null && (revision.Outcome != "accepted" || revision.Content is null))
        {
            throw new ArgumentException("Only accepted revisions with content can be activated.", nameof(revision));
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var dbTransaction = transaction.GetDbTransaction();
        var connection = dbTransaction.Connection!;
        await LicensingStateRow.EnsureAsync(connection, dbTransaction, revision?.ReceivedAtUtc ?? DateTimeOffset.UnixEpoch, cancellationToken)
            .ConfigureAwait(false);

        // Trava a linha antes de gravar a revisão: duas importações com o mesmo If-Match nunca passam juntas.
        await using (var lockCommand = CreateCommand(connection, "SELECT version FROM dokpod.licensing_state FOR UPDATE", dbTransaction))
        {
            var current = (long)(await lockCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
            if (current != expectedVersion)
            {
                return LicenseActivationOutcome.Conflict;
            }
        }

        if (revision is not null)
        {
            try
            {
                await InsertRevisionAsync(connection, dbTransaction, revision, cancellationToken).ConfigureAwait(false);
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                return LicenseActivationOutcome.Duplicate;
            }
        }

        var updated = await ExecuteAsync(
            connection,
            dbTransaction,
            """
            UPDATE dokpod.licensing_state
            SET active_revision_id = COALESCE(@revision, active_revision_id), installation_slot_id = @slot, version = version + 1,
                -- Licença nova tem expiração própria; o prazo da anterior não vale para ela (reimportação idêntica não chega aqui).
                expiry_grace_until_utc = CASE WHEN @revision IS NULL THEN expiry_grace_until_utc ELSE NULL END
            WHERE version = @expected
            """,
            [
                new NpgsqlParameter("revision", NpgsqlDbType.Uuid) { Value = (object?)revision?.RevisionId ?? DBNull.Value },
                new NpgsqlParameter("slot", slotId),
                new NpgsqlParameter("expected", expectedVersion),
            ],
            cancellationToken).ConfigureAwait(false);
        if (updated != 1)
        {
            return LicenseActivationOutcome.Conflict;
        }

        foreach (var licensingEvent in events)
        {
            await auditWriter.AppendAsync(licensingEvent, cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return LicenseActivationOutcome.Activated;
    }

    public async Task RecordRejectedAsync(LicenseRevisionRecord revision, LicensingEvent rejected, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentNullException.ThrowIfNull(rejected);
        if (revision.Outcome != "rejected" || revision.Content is not null)
        {
            throw new ArgumentException("Rejected revisions keep only hash, code and author, never content.", nameof(revision));
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var dbTransaction = transaction.GetDbTransaction();
        await InsertRevisionAsync(dbTransaction.Connection!, dbTransaction, revision, cancellationToken).ConfigureAwait(false);
        await auditWriter.AppendAsync(rejected, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Task<int> InsertRevisionAsync(DbConnection connection, DbTransaction transaction, LicenseRevisionRecord revision, CancellationToken cancellationToken) =>
        ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO dokpod.license_revisions (
                revision_id, document_type, received_at_utc, received_by, content_sha256, content,
                license_id, revision, outcome, failure_code)
            VALUES (@id, @type, @at, @by, @sha, @content, @license, @revision, @outcome, @failure)
            """,
            [
                new NpgsqlParameter("id", revision.RevisionId),
                new NpgsqlParameter("type", revision.DocumentType),
                new NpgsqlParameter("at", revision.ReceivedAtUtc),
                new NpgsqlParameter("by", revision.ReceivedBy.Length <= 255 ? revision.ReceivedBy : revision.ReceivedBy[..255]),
                new NpgsqlParameter("sha", NpgsqlDbType.Bytea) { Value = revision.ContentSha256 },
                new NpgsqlParameter("content", NpgsqlDbType.Bytea) { Value = (object?)revision.Content ?? DBNull.Value },
                new NpgsqlParameter("license", NpgsqlDbType.Text) { Value = (object?)revision.LicenseId ?? DBNull.Value },
                new NpgsqlParameter("revision", NpgsqlDbType.Integer) { Value = (object?)revision.Revision ?? DBNull.Value },
                new NpgsqlParameter("outcome", revision.Outcome),
                new NpgsqlParameter("failure", NpgsqlDbType.Text) { Value = (object?)revision.FailureCode ?? DBNull.Value },
            ],
            cancellationToken);

    private static DateTimeOffset? Instant(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(ordinal), DateTimeKind.Utc));

    private static async Task<bool> OpenAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (connection.State == System.Data.ConnectionState.Open) return false;
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private DbCommand CreateCommand(DbConnection connection, string sql, DbTransaction? transaction = null)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction ?? dbContext.Database.CurrentTransaction?.GetDbTransaction();
        return command;
    }

    private static async Task<int> ExecuteAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        IReadOnlyList<NpgsqlParameter> parameters,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        foreach (var parameter in parameters) command.Parameters.Add(parameter);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
