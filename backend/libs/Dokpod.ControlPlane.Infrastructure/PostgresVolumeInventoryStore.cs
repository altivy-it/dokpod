using System.Data.Common;
using System.Text.Json;
using Dokpod.ControlPlane.Application.Volumes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace Dokpod.ControlPlane.Infrastructure;

/// <summary>Projeção do inventário de volumes por host (spec 018 D-003): substituída inteira por snapshot e protegida por revisão.</summary>
public sealed class PostgresVolumeInventoryStore(ControlPlaneDbContext dbContext) : IVolumeInventoryStore
{
    private const string Columns = "environment_id, name, driver, scope, created_at, labels, mounts, truncated, observed_at";

    public async Task<VolumeSnapshotOutcome> ReplaceSnapshotAsync(VolumeSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var revision = checked((long)snapshot.Revision);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var current = await ScalarAsync<long?>(
            "SELECT revision FROM dokpod.inventory_volume_projections WHERE environment_id = @environment FOR UPDATE",
            [P("environment", snapshot.EnvironmentId)],
            cancellationToken).ConfigureAwait(false);
        if (current is { } existing && existing > revision) return VolumeSnapshotOutcome.Stale;

        await ExecuteAsync(
            """
            INSERT INTO dokpod.inventory_volume_projections (environment_id, revision, observed_at_utc)
            VALUES (@environment, @revision, @observed)
            ON CONFLICT (environment_id) DO UPDATE SET revision = EXCLUDED.revision, observed_at_utc = EXCLUDED.observed_at_utc
            """,
            [P("environment", snapshot.EnvironmentId), P("revision", revision), P("observed", snapshot.ObservedAtUtc)],
            cancellationToken).ConfigureAwait(false);
        await ExecuteAsync("DELETE FROM dokpod.inventory_volumes WHERE environment_id = @environment", [P("environment", snapshot.EnvironmentId)], cancellationToken).ConfigureAwait(false);
        foreach (var chunk in snapshot.Volumes.Chunk(500))
        {
            await ExecuteAsync(
                """
                INSERT INTO dokpod.inventory_volumes (environment_id, name, anonymous, driver, scope, created_at, labels, mounts, mount_count, truncated, observed_at)
                SELECT @environment, v.name, v.anonymous, v.driver, v.scope, v.created_at, v.labels, v.mounts, jsonb_array_length(v.mounts), v.truncated, @observed
                FROM jsonb_to_recordset(@volumes) AS v(name text, anonymous boolean, driver text, scope text, created_at timestamptz, labels jsonb, mounts jsonb, truncated boolean)
                """,
                [
                    P("environment", snapshot.EnvironmentId),
                    P("observed", snapshot.ObservedAtUtc),
                    new NpgsqlParameter("volumes", NpgsqlDbType.Jsonb)
                    {
                        Value = JsonSerializer.Serialize(chunk.Select(v => new
                        {
                            name = v.Name,
                            anonymous = v.Anonymous,
                            driver = v.Driver,
                            scope = v.Scope,
                            created_at = v.CreatedAtUtc,
                            labels = v.Labels,
                            mounts = v.Mounts.Select(m => new { containerId = m.ContainerId, containerName = m.ContainerName, destination = m.Destination, readOnly = m.ReadOnly }),
                            truncated = v.Truncated,
                        })),
                    },
                ],
                cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return VolumeSnapshotOutcome.Accepted;
    }

    public async Task<ulong?> GetRevisionAsync(Guid environmentId, CancellationToken cancellationToken)
    {
        var revision = await ScalarAsync<long?>(
            "SELECT revision FROM dokpod.inventory_volume_projections WHERE environment_id = @environment",
            [P("environment", environmentId)],
            cancellationToken).ConfigureAwait(false);
        return revision is { } value ? (ulong)value : null;
    }

    public async Task<IReadOnlySet<Guid>> ListProjectedAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken)
    {
        if (environmentIds.Count == 0) return new HashSet<Guid>();
        var ids = await QueryAsync(
            "SELECT environment_id FROM dokpod.inventory_volume_projections WHERE environment_id = ANY(@environments)",
            [Environments(environmentIds)],
            reader => reader.GetGuid(0),
            cancellationToken).ConfigureAwait(false);
        return ids.ToHashSet();
    }

    public async Task<VolumeSearchResult> SearchAsync(VolumeSearchQuery query, CancellationToken cancellationToken)
    {
        if (query.EnvironmentIds.Count == 0) return new VolumeSearchResult([], new VolumeSummary(0, 0), false);
        var where = "environment_id = ANY(@environments)";
        var parameters = new List<NpgsqlParameter> { Environments(query.EnvironmentIds) };
        if (query.Anonymous is { } anonymous)
        {
            where += " AND anonymous = @anonymous";
            parameters.Add(P("anonymous", anonymous));
        }

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            // Nome, labels (chave ou valor) e containers que montam o volume.
            where += " AND (name ILIKE @text ESCAPE '\\' OR labels::text ILIKE @text ESCAPE '\\' OR mounts::text ILIKE @text ESCAPE '\\')";
            parameters.Add(P("text", "%" + query.Text.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%"));
        }

        var summary = (await QueryAsync(
            $"SELECT count(*)::int, count(*) FILTER (WHERE mount_count = 0)::int FROM dokpod.inventory_volumes WHERE {where}",
            parameters,
            reader => new VolumeSummary(reader.GetInt32(0), reader.GetInt32(1)),
            cancellationToken).ConfigureAwait(false)).Single();
        if (query.UnusedOnly) where += " AND mount_count = 0";
        var order = query.Sort switch
        {
            VolumeSort.NameDesc => "name DESC, environment_id",
            VolumeSort.CreatedAtDesc => "created_at DESC NULLS LAST, name, environment_id",
            VolumeSort.CreatedAtAsc => "created_at ASC NULLS LAST, name, environment_id",
            VolumeSort.Environment => "environment_id, name",
            _ => "name, environment_id",
        };
        parameters.Add(P("offset", query.Offset));
        parameters.Add(P("limit", query.Limit + 1));
        var rows = await QueryAsync(
            $"SELECT {Columns} FROM dokpod.inventory_volumes WHERE {where} ORDER BY {order} OFFSET @offset LIMIT @limit",
            parameters,
            Read,
            cancellationToken).ConfigureAwait(false);
        return new VolumeSearchResult(rows.Take(query.Limit).ToArray(), summary, rows.Count > query.Limit);
    }

    public async Task<VolumeSearchRow?> GetAsync(Guid environmentId, string name, CancellationToken cancellationToken) =>
        (await QueryAsync(
            $"SELECT {Columns} FROM dokpod.inventory_volumes WHERE environment_id = @environment AND name = @name",
            [P("environment", environmentId), P("name", name)],
            Read,
            cancellationToken).ConfigureAwait(false)).FirstOrDefault();

    private static VolumeSearchRow Read(DbDataReader reader)
    {
        var labels = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(5)) ?? [];
        using var mountsDocument = JsonDocument.Parse(reader.GetString(6));
        var mounts = mountsDocument.RootElement.EnumerateArray().Select(m => new VolumeMount(
            m.GetProperty("containerId").GetString() ?? string.Empty,
            m.GetProperty("containerName").GetString() ?? string.Empty,
            m.GetProperty("destination").GetString() ?? string.Empty,
            m.GetProperty("readOnly").GetBoolean())).ToArray();
        return new VolumeSearchRow(
            reader.GetGuid(0),
            new VolumeInventoryItem(
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(4), DateTimeKind.Utc)),
                labels,
                mounts,
                reader.GetBoolean(7)),
            new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(8), DateTimeKind.Utc)));
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
