using System.Data.Common;
using System.Text.Json;
using Dokpod.ControlPlane.Application.Registries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace Dokpod.ControlPlane.Infrastructure;

/// <summary>Configuração de registros por host (spec 020): substituída por mensagem do agente e protegida por revisão.</summary>
public sealed class PostgresRegistryInventoryStore(ControlPlaneDbContext dbContext) : IRegistryInventoryStore
{
    public async Task<bool> ReplaceAsync(RegistryInventorySnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var revision = checked((long)snapshot.Revision);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var current = await ScalarAsync<long?>(
            "SELECT revision FROM dokpod.inventory_registry_projections WHERE environment_id = @environment FOR UPDATE",
            [P("environment", snapshot.EnvironmentId)],
            cancellationToken).ConfigureAwait(false);
        if (current is { } existing && existing > revision) return false;

        await ExecuteAsync(
            """
            INSERT INTO dokpod.inventory_registry_projections (environment_id, revision, config_available, observed_at_utc)
            VALUES (@environment, @revision, @available, @observed)
            ON CONFLICT (environment_id) DO UPDATE SET revision = EXCLUDED.revision, config_available = EXCLUDED.config_available,
                observed_at_utc = EXCLUDED.observed_at_utc
            """,
            [P("environment", snapshot.EnvironmentId), P("revision", revision), P("available", snapshot.ConfigAvailable), P("observed", snapshot.ObservedAtUtc)],
            cancellationToken).ConfigureAwait(false);
        await ExecuteAsync("DELETE FROM dokpod.inventory_registries WHERE environment_id = @environment", [P("environment", snapshot.EnvironmentId)], cancellationToken).ConfigureAwait(false);
        if (snapshot.Registries.Count > 0)
        {
            await ExecuteAsync(
                """
                INSERT INTO dokpod.inventory_registries (environment_id, registry, mirrors, mirror_of, insecure, blocked, search, credential, observed_at)
                SELECT @environment, r.registry, r.mirrors, r.mirror_of, r.insecure, r.blocked, r.search, r.credential, @observed
                FROM jsonb_to_recordset(@registries) AS r(registry text, mirrors jsonb, mirror_of text, insecure boolean, blocked boolean, search boolean, credential boolean)
                """,
                [
                    P("environment", snapshot.EnvironmentId),
                    P("observed", snapshot.ObservedAtUtc),
                    new NpgsqlParameter("registries", NpgsqlDbType.Jsonb)
                    {
                        Value = JsonSerializer.Serialize(snapshot.Registries.Select(r => new
                        {
                            registry = r.Registry,
                            mirrors = r.Mirrors,
                            mirror_of = r.MirrorOf,
                            insecure = r.Insecure,
                            blocked = r.Blocked,
                            search = r.Search,
                            credential = r.Credential,
                        })),
                    },
                ],
                cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<IReadOnlySet<Guid>> ListConfigAvailableAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken)
    {
        if (environmentIds.Count == 0) return new HashSet<Guid>();
        var ids = await QueryAsync(
            "SELECT environment_id FROM dokpod.inventory_registry_projections WHERE environment_id = ANY(@environments) AND config_available",
            [Environments(environmentIds)],
            reader => reader.GetGuid(0),
            cancellationToken).ConfigureAwait(false);
        return ids.ToHashSet();
    }

    public async Task<IReadOnlyList<RegistryHostConfigRow>> ListAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken)
    {
        if (environmentIds.Count == 0) return [];
        return await QueryAsync(
            """
            SELECT environment_id, registry, mirrors, mirror_of, insecure, blocked, search, credential
            FROM dokpod.inventory_registries WHERE environment_id = ANY(@environments)
            """,
            [Environments(environmentIds)],
            reader => new RegistryHostConfigRow(
                reader.GetGuid(0),
                new RegistryHostConfig(
                    reader.GetString(1),
                    JsonSerializer.Deserialize<string[]>(reader.GetString(2)) ?? [],
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.GetBoolean(4),
                    reader.GetBoolean(5),
                    reader.GetBoolean(6),
                    reader.GetBoolean(7))),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RegistryImageSource>> ListImageSourcesAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken)
    {
        if (environmentIds.Count == 0) return [];
        // Só a primeira tag e o primeiro digest: bastam para o registro de origem (D-003).
        return await QueryAsync(
            "SELECT environment_id, repo_tags[1:1], repo_digests[1:1], size_bytes FROM dokpod.inventory_images WHERE environment_id = ANY(@environments)",
            [Environments(environmentIds)],
            reader => new RegistryImageSource(reader.GetGuid(0), reader.GetFieldValue<string[]>(1), reader.GetFieldValue<string[]>(2), reader.GetInt64(3)),
            cancellationToken).ConfigureAwait(false);
    }

    private static NpgsqlParameter Environments(IReadOnlyCollection<Guid> ids) =>
        new("environments", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = ids.ToArray() };

    private static NpgsqlParameter P(string name, object? value) => new(name, value ?? DBNull.Value);

    private async Task ExecuteAsync(string sql, IReadOnlyList<NpgsqlParameter> parameters, CancellationToken cancellationToken) =>
        await dbContext.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken).ConfigureAwait(false);

    private async Task<T?> ScalarAsync<T>(string sql, IReadOnlyList<NpgsqlParameter> parameters, CancellationToken cancellationToken)
    {
        var rows = await QueryAsync(sql, parameters, reader => reader.IsDBNull(0) ? default : (T)reader.GetValue(0), cancellationToken).ConfigureAwait(false);
        return rows.FirstOrDefault();
    }

    private async Task<List<T>> QueryAsync<T>(string sql, IReadOnlyList<NpgsqlParameter> parameters, Func<DbDataReader, T> read, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var opened = connection.State != System.Data.ConnectionState.Open;
        if (opened) await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
            foreach (var parameter in parameters) command.Parameters.Add(parameter.Clone());
            var results = new List<T>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) results.Add(read(reader));
            return results;
        }
        finally
        {
            if (opened) await connection.CloseAsync().ConfigureAwait(false);
        }
    }
}
