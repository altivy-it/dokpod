using System.Data.Common;
using System.Text.Json;
using Dokpod.ControlPlane.Application.Registries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace Dokpod.ControlPlane.Infrastructure;

/// <summary>Política de registros permitidos por estágio editada no console (spec 020 D-004).</summary>
public sealed class PostgresRegistryPolicyStore(ControlPlaneDbContext dbContext) : IRegistryPolicyStore
{
    public async Task<IReadOnlyList<StoredRegistryPolicy>> ListAsync(CancellationToken cancellationToken) =>
        await QueryAsync(
            "SELECT stage, registries, updated_by, updated_at FROM dokpod.registry_policies ORDER BY stage",
            [],
            reader =>
            {
                RegistryPolicyStages.TryParse(reader.GetString(0), out var stage);
                return new StoredRegistryPolicy(
                    stage,
                    JsonSerializer.Deserialize<string[]>(reader.GetString(1)) ?? [],
                    reader.GetString(2),
                    new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc)));
            },
            cancellationToken).ConfigureAwait(false);

    public async Task SaveAsync(StoredRegistryPolicy policy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(policy);
        await ExecuteAsync(
            """
            INSERT INTO dokpod.registry_policies (stage, registries, updated_by, updated_at)
            VALUES (@stage, @registries, @by, @at)
            ON CONFLICT (stage) DO UPDATE SET registries = EXCLUDED.registries, updated_by = EXCLUDED.updated_by, updated_at = EXCLUDED.updated_at
            """,
            [
                P("stage", RegistryPolicyStages.Key(policy.Stage)),
                new NpgsqlParameter("registries", NpgsqlDbType.Jsonb) { Value = JsonSerializer.Serialize(policy.Registries) },
                P("by", policy.UpdatedBy.Length <= 255 ? policy.UpdatedBy : policy.UpdatedBy[..255]),
                P("at", policy.UpdatedAt),
            ],
            cancellationToken).ConfigureAwait(false);
    }

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
