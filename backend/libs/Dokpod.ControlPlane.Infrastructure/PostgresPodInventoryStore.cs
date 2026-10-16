using System.Data.Common;
using System.Text.Json;
using Dokpod.ControlPlane.Application.Pods;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace Dokpod.ControlPlane.Infrastructure;

/// <summary>Projeção do inventário de pods por host (spec 021 D-003): substituída por snapshot e protegida por revisão.</summary>
public sealed class PostgresPodInventoryStore(ControlPlaneDbContext dbContext) : IPodInventoryStore
{
    private const string Columns = "environment_id, pod_id, name, status, created_at, labels, networks, infra_id, containers, agent_pod, truncated, observed_at";

    /// <summary>Situação normalizada em SQL (mesma regra de `PodStatuses.Normalize`).</summary>
    private const string NormalizedStatus =
        "CASE lower(status) WHEN 'running' THEN 'running' WHEN 'degraded' THEN 'degraded' WHEN 'exited' THEN 'exited' WHEN 'stopped' THEN 'exited' " +
        "WHEN 'paused' THEN 'paused' WHEN 'created' THEN 'created' ELSE 'error' END";

    public async Task<bool> ReplaceSnapshotAsync(PodSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var revision = checked((long)snapshot.Revision);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var current = await ScalarAsync<long?>(
            "SELECT revision FROM dokpod.inventory_pod_projections WHERE environment_id = @environment FOR UPDATE",
            [P("environment", snapshot.EnvironmentId)],
            cancellationToken).ConfigureAwait(false);
        if (current is { } existing && existing > revision) return false;

        await ExecuteAsync(
            """
            INSERT INTO dokpod.inventory_pod_projections (environment_id, revision, pods_supported, observed_at_utc)
            VALUES (@environment, @revision, @supported, @observed)
            ON CONFLICT (environment_id) DO UPDATE SET revision = EXCLUDED.revision, pods_supported = EXCLUDED.pods_supported,
                observed_at_utc = EXCLUDED.observed_at_utc
            """,
            [P("environment", snapshot.EnvironmentId), P("revision", revision), P("supported", snapshot.PodsSupported), P("observed", snapshot.ObservedAtUtc)],
            cancellationToken).ConfigureAwait(false);
        await ExecuteAsync("DELETE FROM dokpod.inventory_pods WHERE environment_id = @environment", [P("environment", snapshot.EnvironmentId)], cancellationToken).ConfigureAwait(false);
        foreach (var chunk in snapshot.Pods.Chunk(500))
        {
            await ExecuteAsync(
                """
                INSERT INTO dokpod.inventory_pods (environment_id, pod_id, name, status, created_at, labels, networks, infra_id, containers,
                    container_count, running_count, agent_pod, truncated, observed_at)
                SELECT @environment, p.pod_id, p.name, p.status, p.created_at, p.labels, p.networks, p.infra_id, p.containers,
                    p.container_count, p.running_count, p.agent_pod, p.truncated, @observed
                FROM jsonb_to_recordset(@pods) AS p(pod_id text, name text, status text, created_at timestamptz, labels jsonb, networks jsonb,
                    infra_id text, containers jsonb, container_count integer, running_count integer, agent_pod boolean, truncated boolean)
                """,
                [
                    P("environment", snapshot.EnvironmentId),
                    P("observed", snapshot.ObservedAtUtc),
                    new NpgsqlParameter("pods", NpgsqlDbType.Jsonb)
                    {
                        Value = JsonSerializer.Serialize(chunk.Select(p => new
                        {
                            pod_id = p.PodId,
                            name = p.Name,
                            status = p.Status,
                            created_at = p.CreatedAtUtc,
                            labels = p.Labels,
                            networks = p.Networks,
                            infra_id = p.InfraId,
                            containers = p.Containers.Select(c => new { containerId = c.ContainerId, name = c.Name, status = c.Status, restarts = c.Restarts, infra = c.Infra }),
                            container_count = p.Containers.Count(c => !c.Infra),
                            running_count = p.RunningContainers,
                            agent_pod = p.AgentPod,
                            truncated = p.Truncated,
                        })),
                    },
                ],
                cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<IReadOnlyList<PodProjectionState>> ListProjectionsAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken)
    {
        if (environmentIds.Count == 0) return [];
        return await QueryAsync(
            "SELECT environment_id, pods_supported FROM dokpod.inventory_pod_projections WHERE environment_id = ANY(@environments)",
            [Environments(environmentIds)],
            reader => new PodProjectionState(reader.GetGuid(0), reader.GetBoolean(1)),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<PodSearchResult> SearchAsync(PodSearchQuery query, CancellationToken cancellationToken)
    {
        if (query.EnvironmentIds.Count == 0) return new PodSearchResult([], new PodSummary(0, new Dictionary<string, int>()), false);
        var where = "environment_id = ANY(@environments)";
        var parameters = new List<NpgsqlParameter> { Environments(query.EnvironmentIds) };
        if (query.ContainerId is { Length: 64 } containerId)
        {
            where += " AND containers @> @member";
            parameters.Add(new NpgsqlParameter("member", NpgsqlDbType.Jsonb) { Value = JsonSerializer.Serialize(new[] { new { containerId } }) });
        }

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            // Nome, labels (chave ou valor) e nomes dos containers (D-003).
            where += " AND (name ILIKE @text ESCAPE '\\' OR labels::text ILIKE @text ESCAPE '\\' OR containers::text ILIKE @text ESCAPE '\\')";
            parameters.Add(P("text", "%" + query.Text.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%"));
        }

        var byStatus = (await QueryAsync(
            $"SELECT {NormalizedStatus} AS s, count(*)::int FROM dokpod.inventory_pods WHERE {where} GROUP BY 1",
            parameters,
            reader => (Status: reader.GetString(0), Count: reader.GetInt32(1)),
            cancellationToken).ConfigureAwait(false)).ToDictionary(r => r.Status, r => r.Count, StringComparer.Ordinal);
        var summary = new PodSummary(byStatus.Values.Sum(), byStatus);
        if (query.Statuses.Count > 0)
        {
            where += $" AND {NormalizedStatus} = ANY(@statuses)";
            parameters.Add(new NpgsqlParameter("statuses", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = query.Statuses.ToArray() });
        }

        var order = query.Sort switch
        {
            PodSort.NameDesc => "name DESC, environment_id, pod_id",
            PodSort.CreatedAtDesc => "created_at DESC NULLS LAST, name, environment_id, pod_id",
            PodSort.CreatedAtAsc => "created_at ASC NULLS LAST, name, environment_id, pod_id",
            PodSort.Environment => "environment_id, name, pod_id",
            _ => "name, environment_id, pod_id",
        };
        parameters.Add(P("offset", query.Offset));
        parameters.Add(P("limit", query.Limit + 1));
        var rows = await QueryAsync(
            $"SELECT {Columns} FROM dokpod.inventory_pods WHERE {where} ORDER BY {order} OFFSET @offset LIMIT @limit",
            parameters,
            Read,
            cancellationToken).ConfigureAwait(false);
        return new PodSearchResult(rows.Take(query.Limit).ToArray(), summary, rows.Count > query.Limit);
    }

    public async Task<PodSearchRow?> GetAsync(Guid environmentId, string podIdOrName, CancellationToken cancellationToken) =>
        (await QueryAsync(
            $"SELECT {Columns} FROM dokpod.inventory_pods WHERE environment_id = @environment AND (pod_id = @key OR name = @key) " +
            "ORDER BY (pod_id = @key) DESC, pod_id LIMIT 1",
            [P("environment", environmentId), P("key", podIdOrName)],
            Read,
            cancellationToken).ConfigureAwait(false)).FirstOrDefault();

    private static PodSearchRow Read(DbDataReader reader)
    {
        var labels = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(5)) ?? [];
        var networks = JsonSerializer.Deserialize<string[]>(reader.GetString(6)) ?? [];
        using var containersDocument = JsonDocument.Parse(reader.GetString(8));
        var containers = containersDocument.RootElement.EnumerateArray()
            .Select(c => new PodMemberItem(
                c.GetProperty("containerId").GetString() ?? string.Empty,
                c.GetProperty("name").GetString() ?? string.Empty,
                c.GetProperty("status").GetString() ?? string.Empty,
                c.GetProperty("restarts").GetInt32(),
                c.GetProperty("infra").GetBoolean()))
            .ToArray();
        return new PodSearchRow(
            reader.GetGuid(0),
            new PodInventoryItem(
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(4), DateTimeKind.Utc)),
                labels,
                networks,
                reader.IsDBNull(7) ? null : reader.GetString(7),
                containers,
                reader.GetBoolean(9),
                reader.GetBoolean(10)),
            new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(11), DateTimeKind.Utc)));
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
