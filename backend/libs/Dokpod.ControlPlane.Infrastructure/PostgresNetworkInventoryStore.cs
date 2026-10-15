using System.Data.Common;
using System.Text.Json;
using Dokpod.ControlPlane.Application.Networks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace Dokpod.ControlPlane.Infrastructure;

/// <summary>Projeção do inventário de redes por host (spec 019 D-003): substituída inteira por snapshot e protegida por revisão.</summary>
public sealed class PostgresNetworkInventoryStore(ControlPlaneDbContext dbContext) : INetworkInventoryStore
{
    private const string Columns =
        "environment_id, network_id, name, driver, scope, internal, ipv6, attachable, system, created_at, subnets, labels, containers, truncated, observed_at";

    public async Task<NetworkSnapshotOutcome> ReplaceSnapshotAsync(NetworkSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var revision = checked((long)snapshot.Revision);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var current = await ScalarAsync<long?>(
            "SELECT revision FROM dokpod.inventory_network_projections WHERE environment_id = @environment FOR UPDATE",
            [P("environment", snapshot.EnvironmentId)],
            cancellationToken).ConfigureAwait(false);
        if (current is { } existing && existing > revision) return NetworkSnapshotOutcome.Stale;

        await ExecuteAsync(
            """
            INSERT INTO dokpod.inventory_network_projections (environment_id, revision, observed_at_utc)
            VALUES (@environment, @revision, @observed)
            ON CONFLICT (environment_id) DO UPDATE SET revision = EXCLUDED.revision, observed_at_utc = EXCLUDED.observed_at_utc
            """,
            [P("environment", snapshot.EnvironmentId), P("revision", revision), P("observed", snapshot.ObservedAtUtc)],
            cancellationToken).ConfigureAwait(false);
        await ExecuteAsync("DELETE FROM dokpod.inventory_networks WHERE environment_id = @environment", [P("environment", snapshot.EnvironmentId)], cancellationToken).ConfigureAwait(false);
        foreach (var chunk in snapshot.Networks.Chunk(500))
        {
            await ExecuteAsync(
                """
                INSERT INTO dokpod.inventory_networks (environment_id, network_id, name, driver, scope, internal, ipv6, attachable, system, created_at,
                    subnets, labels, containers, container_count, truncated, observed_at)
                SELECT @environment, n.network_id, n.name, n.driver, n.scope, n.internal, n.ipv6, n.attachable, n.system, n.created_at,
                    n.subnets, n.labels, n.containers, jsonb_array_length(n.containers), n.truncated, @observed
                FROM jsonb_to_recordset(@networks) AS n(network_id text, name text, driver text, scope text, internal boolean, ipv6 boolean,
                    attachable boolean, system boolean, created_at timestamptz, subnets jsonb, labels jsonb, containers jsonb, truncated boolean)
                """,
                [
                    P("environment", snapshot.EnvironmentId),
                    P("observed", snapshot.ObservedAtUtc),
                    new NpgsqlParameter("networks", NpgsqlDbType.Jsonb)
                    {
                        Value = JsonSerializer.Serialize(chunk.Select(n => new
                        {
                            network_id = n.NetworkId,
                            name = n.Name,
                            driver = n.Driver,
                            scope = n.Scope,
                            @internal = n.Internal,
                            ipv6 = n.IPv6,
                            attachable = n.Attachable,
                            system = n.System,
                            created_at = n.CreatedAtUtc,
                            subnets = n.Subnets.Select(s => new { subnet = s.Subnet, gateway = s.Gateway }),
                            labels = n.Labels,
                            containers = n.Containers.Select(c => new { containerId = c.ContainerId, containerName = c.ContainerName, ipv4 = c.IPv4Address, ipv6 = c.IPv6Address }),
                            truncated = n.Truncated,
                        })),
                    },
                ],
                cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return NetworkSnapshotOutcome.Accepted;
    }

    public async Task<IReadOnlySet<Guid>> ListProjectedAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken)
    {
        if (environmentIds.Count == 0) return new HashSet<Guid>();
        var ids = await QueryAsync(
            "SELECT environment_id FROM dokpod.inventory_network_projections WHERE environment_id = ANY(@environments)",
            [Environments(environmentIds)],
            reader => reader.GetGuid(0),
            cancellationToken).ConfigureAwait(false);
        return ids.ToHashSet();
    }

    public async Task<NetworkSearchResult> SearchAsync(NetworkSearchQuery query, CancellationToken cancellationToken)
    {
        if (query.EnvironmentIds.Count == 0) return new NetworkSearchResult([], new NetworkSummary(0, 0), false);
        var where = "environment_id = ANY(@environments)";
        var parameters = new List<NpgsqlParameter> { Environments(query.EnvironmentIds) };
        if (query.System is { } system)
        {
            where += " AND system = @system";
            parameters.Add(P("system", system));
        }

        if (query.Drivers.Count > 0)
        {
            where += " AND driver = ANY(@drivers)";
            parameters.Add(new NpgsqlParameter("drivers", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = query.Drivers.ToArray() });
        }

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            // Nome, identificador, sub-redes, labels (chave ou valor) e containers conectados (D-003).
            where += " AND (name ILIKE @text ESCAPE '\\' OR network_id LIKE @prefix OR subnets::text ILIKE @text ESCAPE '\\'" +
                " OR labels::text ILIKE @text ESCAPE '\\' OR containers::text ILIKE @text ESCAPE '\\')";
            var text = query.Text.Trim();
            parameters.Add(P("text", "%" + text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%"));
            parameters.Add(P("prefix", text.All(Uri.IsHexDigit) ? text.ToLowerInvariant() + "%" : "-"));
        }

        var summary = (await QueryAsync(
            $"SELECT count(*)::int, count(*) FILTER (WHERE container_count = 0 AND NOT system)::int FROM dokpod.inventory_networks WHERE {where}",
            parameters,
            reader => new NetworkSummary(reader.GetInt32(0), reader.GetInt32(1)),
            cancellationToken).ConfigureAwait(false)).Single();
        if (query.UnusedOnly) where += " AND container_count = 0 AND NOT system";
        var order = query.Sort switch
        {
            NetworkSort.NameDesc => "name DESC, environment_id, network_id",
            NetworkSort.CreatedAtDesc => "created_at DESC NULLS LAST, name, environment_id, network_id",
            NetworkSort.CreatedAtAsc => "created_at ASC NULLS LAST, name, environment_id, network_id",
            NetworkSort.Environment => "environment_id, name, network_id",
            _ => "name, environment_id, network_id",
        };
        parameters.Add(P("offset", query.Offset));
        parameters.Add(P("limit", query.Limit + 1));
        var rows = await QueryAsync(
            $"SELECT {Columns} FROM dokpod.inventory_networks WHERE {where} ORDER BY {order} OFFSET @offset LIMIT @limit",
            parameters,
            Read,
            cancellationToken).ConfigureAwait(false);
        return new NetworkSearchResult(rows.Take(query.Limit).ToArray(), summary, rows.Count > query.Limit);
    }

    public async Task<NetworkSearchRow?> GetAsync(Guid environmentId, string networkIdOrName, CancellationToken cancellationToken) =>
        (await QueryAsync(
            $"SELECT {Columns} FROM dokpod.inventory_networks WHERE environment_id = @environment AND (network_id = @key OR name = @key) " +
            "ORDER BY (network_id = @key) DESC, network_id LIMIT 1",
            [P("environment", environmentId), P("key", networkIdOrName)],
            Read,
            cancellationToken).ConfigureAwait(false)).FirstOrDefault();

    private static NetworkSearchRow Read(DbDataReader reader)
    {
        static string? Optional(JsonElement element, string property) =>
            element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text : null;
        using var subnetsDocument = JsonDocument.Parse(reader.GetString(10));
        var subnets = subnetsDocument.RootElement.EnumerateArray()
            .Select(s => new NetworkSubnet(s.GetProperty("subnet").GetString() ?? string.Empty, Optional(s, "gateway")))
            .ToArray();
        var labels = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(11)) ?? [];
        using var containersDocument = JsonDocument.Parse(reader.GetString(12));
        var containers = containersDocument.RootElement.EnumerateArray()
            .Select(c => new NetworkConnection(
                c.GetProperty("containerId").GetString() ?? string.Empty,
                c.GetProperty("containerName").GetString() ?? string.Empty,
                Optional(c, "ipv4"),
                Optional(c, "ipv6")))
            .ToArray();
        return new NetworkSearchRow(
            reader.GetGuid(0),
            new NetworkInventoryItem(
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetBoolean(5),
                reader.GetBoolean(6),
                reader.GetBoolean(7),
                reader.GetBoolean(8),
                reader.IsDBNull(9) ? null : new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(9), DateTimeKind.Utc)),
                subnets,
                labels,
                containers,
                reader.GetBoolean(13)),
            new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(14), DateTimeKind.Utc)));
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
