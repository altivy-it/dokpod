using System.Data.Common;
using System.Text.Json.Nodes;
using Dokpod.ControlPlane.Application.Licensing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace Dokpod.ControlPlane.Infrastructure.Licensing;

/// <summary>
/// Grava a trilha global de licenciamento como cadeia de hashes (spec 031 T009, FR-035). A linha de
/// <c>licensing_state</c> é travada com <c>FOR UPDATE</c> e a cabeça só avança se ainda for a lida, então dois
/// eventos nunca recebem o mesmo <c>chain_seq</c>. O <c>version</c> do estado não muda aqui: ele é o
/// <c>If-Match</c> das operações administrativas, e um evento do sistema não deve invalidá-lo.
/// </summary>
public sealed class LicensingAuditWriter(ControlPlaneDbContext dbContext) : ILicensingAuditWriter
{
    private const int MaxActorIdLength = 255;

    public async Task<LicensingChainLink> AppendAsync(LicensingEvent licensingEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(licensingEvent);
        Validate(licensingEvent);

        var occurredAtUtc = LicensingChain.ToStoredInstant(licensingEvent.OccurredAtUtc);
        var ownsTransaction = dbContext.Database.CurrentTransaction is null;
        await using var ownedTransaction = ownsTransaction
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;
        var transaction = dbContext.Database.CurrentTransaction!.GetDbTransaction();
        var connection = transaction.Connection!;

        await LicensingStateRow.EnsureAsync(connection, transaction, occurredAtUtc, cancellationToken).ConfigureAwait(false);

        Guid installationId;
        byte[] hmacKey;
        long headSeq;
        byte[]? headHash;
        await using (var command = CreateCommand(
            connection,
            transaction,
            "SELECT installation_id, actor_hmac_key, chain_head_seq, chain_head_hash FROM dokpod.licensing_state FOR UPDATE",
            []))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("Licensing state row is missing.");
            }

            installationId = reader.GetGuid(0);
            hmacKey = (byte[])reader.GetValue(1);
            headSeq = reader.GetInt64(2);
            headHash = reader.IsDBNull(3) ? null : (byte[])reader.GetValue(3);
        }

        var chainSeq = headSeq + 1;
        var previousHash = headHash ?? LicensingChain.GenesisHash(installationId);
        var actorRef = LicensingChain.ActorReference(hmacKey, licensingEvent.ActorId);
        var recordHash = LicensingChain.RecordHash(
            previousHash,
            chainSeq,
            licensingEvent.EventId,
            occurredAtUtc,
            actorRef,
            licensingEvent.CorrelationId,
            licensingEvent.Action,
            licensingEvent.Details);

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO dokpod.licensing_events (
                event_id, chain_seq, prev_hash, record_hash, occurred_at_utc,
                actor_id, actor_ref, correlation_id, action, details)
            VALUES (@event, @seq, @prev, @hash, @at, @actor, @actorRef, @correlation, @action, @details)
            """,
            [
                new NpgsqlParameter("event", licensingEvent.EventId),
                new NpgsqlParameter("seq", chainSeq),
                new NpgsqlParameter("prev", NpgsqlDbType.Bytea) { Value = previousHash },
                new NpgsqlParameter("hash", NpgsqlDbType.Bytea) { Value = recordHash },
                new NpgsqlParameter("at", occurredAtUtc),
                new NpgsqlParameter("actor", NpgsqlDbType.Text) { Value = (object?)licensingEvent.ActorId ?? DBNull.Value },
                new NpgsqlParameter("actorRef", NpgsqlDbType.Text) { Value = (object?)actorRef ?? DBNull.Value },
                new NpgsqlParameter("correlation", licensingEvent.CorrelationId),
                new NpgsqlParameter("action", licensingEvent.Action),
                new NpgsqlParameter("details", NpgsqlDbType.Jsonb) { Value = LicensingChain.Canonical(licensingEvent.Details) },
            ],
            cancellationToken).ConfigureAwait(false);

        var advanced = await ExecuteAsync(
            connection,
            transaction,
            """
            UPDATE dokpod.licensing_state
            SET chain_head_seq = @seq, chain_head_hash = @hash
            WHERE chain_head_seq = @expected
            """,
            [
                new NpgsqlParameter("seq", chainSeq),
                new NpgsqlParameter("hash", NpgsqlDbType.Bytea) { Value = recordHash },
                new NpgsqlParameter("expected", headSeq),
            ],
            cancellationToken).ConfigureAwait(false);
        if (advanced != 1)
        {
            throw new InvalidOperationException("Licensing chain head changed while appending an event.");
        }

        if (ownedTransaction is not null)
        {
            await ownedTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return new LicensingChainLink(chainSeq, recordHash);
    }

    private static void Validate(LicensingEvent licensingEvent)
    {
        if (!LicensingEventAction.AllowedDetails.TryGetValue(licensingEvent.Action, out var allowedKeys))
        {
            throw new ArgumentException($"Unknown licensing action '{licensingEvent.Action}'.", nameof(licensingEvent));
        }

        ArgumentNullException.ThrowIfNull(licensingEvent.Details);
        var unexpected = licensingEvent.Details.Select(property => property.Key).Where(key => !allowedKeys.Contains(key)).ToArray();
        if (unexpected.Length > 0)
        {
            throw new ArgumentException(
                $"Details keys not allowed for '{licensingEvent.Action}': {string.Join(", ", unexpected)}.",
                nameof(licensingEvent));
        }

        if (licensingEvent.ActorId is { } actorId && (actorId.Length == 0 || actorId.Length > MaxActorIdLength))
        {
            throw new ArgumentException("Actor id must have 1 to 255 characters.", nameof(licensingEvent));
        }

        // Valida o tipo de cada valor antes de abrir a transação.
        _ = LicensingChain.Canonical(licensingEvent.Details);
    }

    private static DbCommand CreateCommand(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        IReadOnlyList<NpgsqlParameter> parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        foreach (var parameter in parameters) command.Parameters.Add(parameter);
        return command;
    }

    private static async Task<int> ExecuteAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        IReadOnlyList<NpgsqlParameter> parameters,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, sql, parameters);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
