using System.Data.Common;
using System.Text.Json.Nodes;
using Dokpod.ControlPlane.Application.Licensing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Dokpod.ControlPlane.Infrastructure.Licensing;

/// <summary>
/// Hosts de licenciamento e associações (spec 031 T039). Toda escrita trava a linha de <c>licensing_state</c>, o que
/// serializa as mudanças de licenciamento (volume baixo) e garante um ambiente em no máximo um host ativo.
/// </summary>
public sealed class LicensingHostStore(ControlPlaneDbContext dbContext, ILicensingAuditWriter auditWriter) : ILicensingHostStore
{
    public async Task<IReadOnlyList<LicensingHostRecord>> ListHostsAsync(CancellationToken cancellationToken) =>
        await LicensingSql.QueryAsync(
            dbContext,
            "SELECT host_id, label, created_at_utc, removed_at_utc FROM dokpod.licensing_hosts",
            [],
            reader => new LicensingHostRecord(reader.GetGuid(0), reader.GetString(1), LicensingSql.Instant(reader, 2)!.Value, LicensingSql.Instant(reader, 3)),
            cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<LicensingHostLink>> ListActiveLinksAsync(CancellationToken cancellationToken) =>
        await LicensingSql.QueryAsync(
            dbContext,
            "SELECT host_id, environment_id, linked_at_utc, unlinked_at_utc FROM dokpod.licensing_host_environments WHERE unlinked_at_utc IS NULL",
            [],
            LicensingSql.ReadLink,
            cancellationToken).ConfigureAwait(false);

    public Task<LicensingHostChange> CreateHostAsync(Guid hostId, string label, string? actorId, Guid correlationId, DateTimeOffset now, CancellationToken cancellationToken) =>
        InTransactionAsync(now, async (connection, transaction) =>
        {
            var existing = await HostStateAsync(connection, transaction, hostId, cancellationToken).ConfigureAwait(false);
            if (existing is not null) return existing.Value ? LicensingHostChange.Removed : LicensingHostChange.Unchanged;
            await InsertHostAsync(connection, transaction, hostId, label, now, cancellationToken).ConfigureAwait(false);
            return LicensingHostChange.Applied;
        }, cancellationToken);

    public Task<LicensingHostChange> LinkAsync(Guid hostId, Guid environmentId, string? actorId, Guid correlationId, DateTimeOffset now, CancellationToken cancellationToken) =>
        InTransactionAsync(now, async (connection, transaction) =>
        {
            var removed = await HostStateAsync(connection, transaction, hostId, cancellationToken).ConfigureAwait(false);
            if (removed is null) return LicensingHostChange.NotFound;
            if (removed.Value) return LicensingHostChange.Removed;
            return await LinkEnvironmentAsync(connection, transaction, hostId, environmentId, actorId, correlationId, now, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    public Task<LicensingHostChange> UnlinkAsync(Guid hostId, Guid environmentId, string? actorId, Guid correlationId, DateTimeOffset now, CancellationToken cancellationToken) =>
        InTransactionAsync(now, async (connection, transaction) =>
        {
            if (await HostStateAsync(connection, transaction, hostId, cancellationToken).ConfigureAwait(false) is null) return LicensingHostChange.NotFound;
            var closed = await CloseLinksAsync(connection, transaction, hostId, environmentId, now, cancellationToken).ConfigureAwait(false);
            foreach (var environment in closed)
            {
                await AppendAsync(LicensingEventAction.HostUnlinked, actorId, correlationId, now, new JsonObject { ["hostId"] = hostId.ToString("D"), ["environmentId"] = environment.ToString("D") }, cancellationToken)
                    .ConfigureAwait(false);
            }

            return closed.Count == 0 ? LicensingHostChange.Unchanged : LicensingHostChange.Applied;
        }, cancellationToken);

    public Task<LicensingHostChange> RemoveHostAsync(Guid hostId, string? actorId, Guid correlationId, DateTimeOffset now, CancellationToken cancellationToken) =>
        InTransactionAsync(now, async (connection, transaction) =>
        {
            var removed = await HostStateAsync(connection, transaction, hostId, cancellationToken).ConfigureAwait(false);
            if (removed is null) return LicensingHostChange.NotFound;
            if (removed.Value) return LicensingHostChange.Unchanged;
            foreach (var environment in await CloseLinksAsync(connection, transaction, hostId, null, now, cancellationToken).ConfigureAwait(false))
            {
                await AppendAsync(LicensingEventAction.HostUnlinked, actorId, correlationId, now, new JsonObject { ["hostId"] = hostId.ToString("D"), ["environmentId"] = environment.ToString("D") }, cancellationToken)
                    .ConfigureAwait(false);
            }

            await LicensingSql.ExecuteAsync(
                connection,
                transaction,
                "UPDATE dokpod.licensing_hosts SET removed_at_utc = @now WHERE host_id = @host",
                [new NpgsqlParameter("now", now), new NpgsqlParameter("host", hostId)],
                cancellationToken).ConfigureAwait(false);
            await AppendAsync(LicensingEventAction.HostRemoved, actorId, correlationId, now, new JsonObject { ["hostId"] = hostId.ToString("D") }, cancellationToken)
                .ConfigureAwait(false);
            return LicensingHostChange.Applied;
        }, cancellationToken);

    /// <summary>Host do ambiente: o associado ou, na primeira conexão, um host novo só dele, associado pelo sistema.</summary>
    internal async Task<Guid> EnsureHostForEnvironmentAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid environmentId,
        string? hostName,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existing = await LicensingSql.ScalarAsync<Guid?>(
            connection,
            transaction,
            "SELECT host_id FROM dokpod.licensing_host_environments WHERE environment_id = @environment AND unlinked_at_utc IS NULL",
            [new NpgsqlParameter("environment", environmentId)],
            cancellationToken).ConfigureAwait(false);
        if (existing is { } hostId) return hostId;

        var created = Guid.NewGuid();
        var label = string.IsNullOrWhiteSpace(hostName) ? $"host-{environmentId:N}"[..17] : hostName.Trim();
        await InsertHostAsync(connection, transaction, created, label.Length <= LicensingHostService.MaxLabelLength ? label : label[..LicensingHostService.MaxLabelLength], now, cancellationToken)
            .ConfigureAwait(false);
        await LinkEnvironmentAsync(connection, transaction, created, environmentId, null, Guid.NewGuid(), now, cancellationToken).ConfigureAwait(false);
        return created;
    }

    private async Task<LicensingHostChange> LinkEnvironmentAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid hostId,
        Guid environmentId,
        string? actorId,
        Guid correlationId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var current = await LicensingSql.ScalarAsync<Guid?>(
            connection,
            transaction,
            "SELECT host_id FROM dokpod.licensing_host_environments WHERE environment_id = @environment AND unlinked_at_utc IS NULL",
            [new NpgsqlParameter("environment", environmentId)],
            cancellationToken).ConfigureAwait(false);
        if (current == hostId) return LicensingHostChange.Unchanged;
        if (current is { } previous)
        {
            await CloseLinksAsync(connection, transaction, previous, environmentId, now, cancellationToken).ConfigureAwait(false);
            await AppendAsync(LicensingEventAction.HostUnlinked, actorId, correlationId, now, new JsonObject { ["hostId"] = previous.ToString("D"), ["environmentId"] = environmentId.ToString("D") }, cancellationToken)
                .ConfigureAwait(false);
        }

        await LicensingSql.ExecuteAsync(
            connection,
            transaction,
            "INSERT INTO dokpod.licensing_host_environments (host_id, environment_id, linked_at_utc) VALUES (@host, @environment, @now)",
            [new NpgsqlParameter("host", hostId), new NpgsqlParameter("environment", environmentId), new NpgsqlParameter("now", now)],
            cancellationToken).ConfigureAwait(false);
        await AppendAsync(LicensingEventAction.HostLinked, actorId, correlationId, now, new JsonObject { ["hostId"] = hostId.ToString("D"), ["environmentId"] = environmentId.ToString("D") }, cancellationToken)
            .ConfigureAwait(false);
        return LicensingHostChange.Applied;
    }

    private static async Task<IReadOnlyList<Guid>> CloseLinksAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid hostId,
        Guid? environmentId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE dokpod.licensing_host_environments SET unlinked_at_utc = @now
            WHERE host_id = @host AND unlinked_at_utc IS NULL AND (@environment::uuid IS NULL OR environment_id = @environment::uuid)
            RETURNING environment_id
            """;
        command.Parameters.Add(new NpgsqlParameter("now", now));
        command.Parameters.Add(new NpgsqlParameter("host", hostId));
        command.Parameters.Add(new NpgsqlParameter("environment", NpgsqlTypes.NpgsqlDbType.Uuid) { Value = (object?)environmentId ?? DBNull.Value });
        var closed = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) closed.Add(reader.GetGuid(0));
        return closed;
    }

    private static Task<int> InsertHostAsync(DbConnection connection, DbTransaction transaction, Guid hostId, string label, DateTimeOffset now, CancellationToken cancellationToken) =>
        LicensingSql.ExecuteAsync(
            connection,
            transaction,
            "INSERT INTO dokpod.licensing_hosts (host_id, label, created_at_utc) VALUES (@host, @label, @now)",
            [new NpgsqlParameter("host", hostId), new NpgsqlParameter("label", label), new NpgsqlParameter("now", now)],
            cancellationToken);

    /// <summary>null se o host não existe; senão, se ele foi removido.</summary>
    private static async Task<bool?> HostStateAsync(DbConnection connection, DbTransaction transaction, Guid hostId, CancellationToken cancellationToken)
    {
        var rows = await LicensingSql.QueryAsync(
            connection,
            transaction,
            "SELECT removed_at_utc IS NOT NULL FROM dokpod.licensing_hosts WHERE host_id = @host",
            [new NpgsqlParameter("host", hostId)],
            reader => reader.GetBoolean(0),
            cancellationToken).ConfigureAwait(false);
        return rows.Count == 0 ? null : rows[0];
    }

    private Task AppendAsync(string action, string? actorId, Guid correlationId, DateTimeOffset now, JsonObject details, CancellationToken cancellationToken) =>
        auditWriter.AppendAsync(new LicensingEvent(Guid.NewGuid(), now, actorId, correlationId, action, details), cancellationToken);

    private async Task<LicensingHostChange> InTransactionAsync(
        DateTimeOffset now,
        Func<DbConnection, DbTransaction, Task<LicensingHostChange>> action,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var dbTransaction = transaction.GetDbTransaction();
        var connection = dbTransaction.Connection!;
        await LicensingStateRow.EnsureAsync(connection, dbTransaction, now, cancellationToken).ConfigureAwait(false);
        await LicensingSql.ExecuteAsync(connection, dbTransaction, "SELECT 1 FROM dokpod.licensing_state FOR UPDATE", [], cancellationToken).ConfigureAwait(false);
        var result = await action(connection, dbTransaction).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }
}
