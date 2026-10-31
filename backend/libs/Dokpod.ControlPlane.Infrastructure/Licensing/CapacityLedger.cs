using System.Data.Common;
using Dokpod.ControlPlane.Application.Licensing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace Dokpod.ControlPlane.Infrastructure.Licensing;

/// <summary>
/// Observações de capacidade (spec 031 T037, D-005). A conexão do agente grava uma observação no host do ambiente,
/// criando na primeira vez um host só dele; só a origem <c>engine</c> grava vCPUs, as demais ficam como medição
/// desconhecida (FR-013). A retenção passa pela função que grava as âncoras antes de remover observações.
/// </summary>
public sealed class CapacityLedger(ControlPlaneDbContext dbContext, LicensingHostStore hostStore) : ICapacityLedger, IAgentCapacityObserver
{
    public async Task OnAgentHelloAsync(
        Guid environmentId,
        string? hostName,
        int? cpuCount,
        string source,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        if (source is not (CapacitySource.Engine or CapacitySource.ProcessFallback or CapacitySource.Unknown))
        {
            throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown capacity source.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var dbTransaction = transaction.GetDbTransaction();
        var connection = dbTransaction.Connection!;
        await LicensingStateRow.EnsureAsync(connection, dbTransaction, observedAtUtc, cancellationToken).ConfigureAwait(false);
        await LicensingSql.ExecuteAsync(connection, dbTransaction, "SELECT 1 FROM dokpod.licensing_state FOR UPDATE", [], cancellationToken).ConfigureAwait(false);
        var hostId = await hostStore.EnsureHostForEnvironmentAsync(connection, dbTransaction, environmentId, hostName, observedAtUtc, cancellationToken).ConfigureAwait(false);
        var trusted = source == CapacitySource.Engine && cpuCount is > 0 ? cpuCount : null;
        await InsertAsync(
            connection,
            dbTransaction,
            new CapacityObservation(hostId, observedAtUtc, trusted, trusted is null ? (source == CapacitySource.Engine ? CapacitySource.Unknown : source) : source, environmentId),
            cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RecordAsync(CapacityObservation observation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observation);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var dbTransaction = transaction.GetDbTransaction();
        await InsertAsync(dbTransaction.Connection!, dbTransaction, observation, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<MeteringInput> ReadMeteringInputAsync(DateTimeOffset windowStartUtc, CancellationToken cancellationToken)
    {
        var start = new NpgsqlParameter("start", windowStartUtc);
        var hosts = await LicensingSql.QueryAsync(
            dbContext,
            "SELECT host_id, label, created_at_utc, removed_at_utc FROM dokpod.licensing_hosts WHERE removed_at_utc IS NULL OR removed_at_utc > @start",
            [start],
            reader => new LicensingHostRecord(reader.GetGuid(0), reader.GetString(1), LicensingSql.Instant(reader, 2)!.Value, LicensingSql.Instant(reader, 3)),
            cancellationToken).ConfigureAwait(false);
        var links = await LicensingSql.QueryAsync(
            dbContext,
            "SELECT host_id, environment_id, linked_at_utc, unlinked_at_utc FROM dokpod.licensing_host_environments WHERE unlinked_at_utc IS NULL OR unlinked_at_utc > @start",
            [start.Clone()],
            LicensingSql.ReadLink,
            cancellationToken).ConfigureAwait(false);

        // A janela inteira, a última observação retida de cada ambiente antes dela e, sem ela, a âncora do host.
        var observations = await LicensingSql.QueryAsync(
            dbContext,
            """
            WITH before_window AS (
                SELECT DISTINCT ON (environment_id) host_id, observed_at_utc, vcpus, source, environment_id
                FROM dokpod.licensing_capacity_observations
                WHERE observed_at_utc < @start
                ORDER BY environment_id, observed_at_utc DESC
            )
            SELECT host_id, observed_at_utc, vcpus, source, environment_id FROM dokpod.licensing_capacity_observations WHERE observed_at_utc >= @start
            UNION ALL
            SELECT host_id, observed_at_utc, vcpus, source, environment_id FROM before_window
            UNION ALL
            SELECT a.host_id, a.observed_at_utc, a.vcpus, a.source, a.environment_id
            FROM dokpod.licensing_capacity_anchors a
            WHERE a.observed_at_utc < @start AND NOT EXISTS (SELECT 1 FROM before_window b WHERE b.environment_id = a.environment_id)
            """,
            [start.Clone()],
            reader => new CapacityObservation(
                reader.GetGuid(0),
                LicensingSql.Instant(reader, 1)!.Value,
                reader.IsDBNull(2) ? null : reader.GetInt32(2),
                reader.GetString(3),
                reader.GetGuid(4)),
            cancellationToken).ConfigureAwait(false);
        return new MeteringInput(hosts, links, observations);
    }

    public async Task<long> ApplyRetentionAsync(TimeSpan retention, CancellationToken cancellationToken)
    {
        var rows = await LicensingSql.QueryAsync(
            dbContext,
            "SELECT dokpod.dokpod_retain_licensing_capacity(@retention)",
            [new NpgsqlParameter("retention", NpgsqlDbType.Interval) { Value = retention }],
            reader => reader.GetInt64(0),
            cancellationToken).ConfigureAwait(false);
        return rows[0];
    }

    private static Task<int> InsertAsync(DbConnection connection, DbTransaction transaction, CapacityObservation observation, CancellationToken cancellationToken) =>
        LicensingSql.ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO dokpod.licensing_capacity_observations (host_id, observed_at_utc, vcpus, source, environment_id)
            VALUES (@host, @at, @vcpus, @source, @environment)
            """,
            [
                new NpgsqlParameter("host", observation.HostId),
                new NpgsqlParameter("at", observation.ObservedAtUtc),
                new NpgsqlParameter("vcpus", NpgsqlDbType.Integer) { Value = (object?)observation.Vcpus ?? DBNull.Value },
                new NpgsqlParameter("source", observation.Source),
                new NpgsqlParameter("environment", observation.EnvironmentId),
            ],
            cancellationToken);
}

/// <summary>Acesso SQL compartilhado pelas stores de licenciamento (mesma conexão e transação do contexto EF).</summary>
internal static class LicensingSql
{
    internal static LicensingHostLink ReadLink(DbDataReader reader) =>
        new(reader.GetGuid(0), reader.GetGuid(1), Instant(reader, 2)!.Value, Instant(reader, 3));

    internal static DateTimeOffset? Instant(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(ordinal), DateTimeKind.Utc));

    internal static async Task<List<T>> QueryAsync<T>(
        ControlPlaneDbContext dbContext,
        string sql,
        IReadOnlyList<NpgsqlParameter> parameters,
        Func<DbDataReader, T> read,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var opened = connection.State != System.Data.ConnectionState.Open;
        if (opened) await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await QueryAsync(connection, dbContext.Database.CurrentTransaction?.GetDbTransaction(), sql, parameters, read, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (opened) await connection.CloseAsync().ConfigureAwait(false);
        }
    }

    internal static async Task<List<T>> QueryAsync<T>(
        DbConnection connection,
        DbTransaction? transaction,
        string sql,
        IReadOnlyList<NpgsqlParameter> parameters,
        Func<DbDataReader, T> read,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        foreach (var parameter in parameters) command.Parameters.Add(parameter);
        var results = new List<T>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) results.Add(read(reader));
        return results;
    }

    internal static async Task<T?> ScalarAsync<T>(
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
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull ? default : (T)value;
    }

    internal static async Task<int> ExecuteAsync(
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
