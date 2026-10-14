using Dokpod.ControlPlane.Application.Inventory;
using Dokpod.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Dokpod.ControlPlane.Infrastructure;

/// <summary>Busca multi-host em `inventory_containers` com keyset estável (chave de ordenação, ambiente, container).</summary>
public sealed class PostgresContainerSearchStore(ControlPlaneDbContext dbContext) : IContainerSearchStore
{
    private const string CreatedKey = "coalesce(to_char(c.created_at_utc AT TIME ZONE 'UTC', 'YYYYMMDDHH24MISSUS'), '00000000000000000000')";

    public async Task<ContainerSearchResult> SearchAsync(ContainerSearchQuery query, CancellationToken cancellationToken)
    {
        var (sortKey, descending) = query.Sort switch
        {
            ContainerSort.NameDesc => ("lower(c.name)", true),
            ContainerSort.DisplayStatus => ("c.display_status || '|' || lower(c.name)", false),
            ContainerSort.Environment => ("lower(e.name) || '|' || lower(c.name)", false),
            ContainerSort.CreatedAtAsc => (CreatedKey, false),
            ContainerSort.CreatedAtDesc => (CreatedKey, true),
            _ => ("lower(c.name)", false),
        };
        var text = string.IsNullOrWhiteSpace(query.Text) ? null : $"%{EscapeLike(query.Text.Trim().ToLowerInvariant())}%";
        var parameters = new List<NpgsqlParameter>
        {
            new("envs", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = query.EnvironmentIds.ToArray() },
            new("statuses", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = query.DisplayStatuses.ToArray() },
            new("text", NpgsqlDbType.Text) { Value = (object?)text ?? DBNull.Value },
            new("limit", query.Limit + 1),
        };
        const string filters = """
            c.environment_id = ANY(@envs)
            AND (@text::text IS NULL
                 OR lower(c.name) LIKE @text ESCAPE '\'
                 OR lower(c.image_reference) LIKE @text ESCAPE '\'
                 OR lower(e.name) LIKE @text ESCAPE '\'
                 OR coalesce(c.ports::text, '') LIKE @text ESCAPE '\')
            """;
        var keyset = string.Empty;
        if (query.After is not null)
        {
            keyset = $"AND ({sortKey}, c.environment_id::text, c.container_id) {(descending ? "<" : ">")} (@k1, @k2, @k3)";
            parameters.Add(new NpgsqlParameter("k1", query.After.SortKey));
            parameters.Add(new NpgsqlParameter("k2", query.After.EnvironmentId.ToString("D")));
            parameters.Add(new NpgsqlParameter("k3", query.After.ContainerId));
        }

        var direction = descending ? "DESC" : "ASC";
        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var rows = new List<ContainerSearchRow>();
            await using (var command = new NpgsqlCommand(
                $"""
                SELECT c.environment_id, c.container_id, c.name, c.image_reference, c.state, c.revision, c.observed_at_utc,
                       c.created_at_utc, c.started_at_utc, c.health, c.restart_count, c.exit_code, c.oom_killed, c.ports::text,
                       {sortKey} AS sort_key
                FROM dokpod.inventory_containers c
                JOIN dokpod.environments e ON e.environment_id = c.environment_id
                WHERE {filters}
                  AND (cardinality(@statuses) = 0 OR c.display_status = ANY(@statuses))
                  {keyset}
                ORDER BY {sortKey} {direction}, c.environment_id::text {direction}, c.container_id {direction}
                LIMIT @limit
                """, connection))
            {
                command.Parameters.AddRange(parameters.ToArray());
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var entity = new InventoryContainerEntity
                    {
                        EnvironmentId = reader.GetGuid(0),
                        ContainerId = reader.GetString(1),
                        Name = reader.GetString(2),
                        ImageReference = reader.GetString(3),
                        State = reader.GetString(4),
                        Revision = reader.GetString(5),
                        ObservedAtUtc = reader.GetFieldValue<DateTimeOffset>(6),
                        CreatedAtUtc = reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
                        StartedAtUtc = reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8),
                        Health = reader.IsDBNull(9) ? null : reader.GetString(9),
                        RestartCount = reader.IsDBNull(10) ? null : reader.GetInt32(10),
                        ExitCode = reader.IsDBNull(11) ? null : reader.GetInt32(11),
                        OomKilled = reader.IsDBNull(12) ? null : reader.GetBoolean(12),
                        Ports = reader.IsDBNull(13) ? null : reader.GetString(13),
                    };
                    rows.Add(new ContainerSearchRow(entity.EnvironmentId, PostgresInventoryProjectionStore.ToDomain(entity), reader.GetString(14)));
                }
            }

            var summary = Enum.GetValues<ContainerDisplayStatus>().ToDictionary(ContainerDisplayStatuses.ToValue, _ => 0);
            await using (var command = new NpgsqlCommand(
                $"""
                SELECT c.display_status, count(*)::int
                FROM dokpod.inventory_containers c
                JOIN dokpod.environments e ON e.environment_id = c.environment_id
                WHERE {filters}
                GROUP BY c.display_status
                """, connection))
            {
                command.Parameters.Add(new NpgsqlParameter("envs", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = query.EnvironmentIds.ToArray() });
                command.Parameters.Add(new NpgsqlParameter("text", NpgsqlDbType.Text) { Value = (object?)text ?? DBNull.Value });
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    summary[reader.GetString(0)] = reader.GetInt32(1);
                }
            }

            DateTimeOffset? oldest;
            await using (var command = new NpgsqlCommand(
                "SELECT min(observed_at_utc) FROM dokpod.inventory_projections WHERE environment_id = ANY(@envs)", connection))
            {
                command.Parameters.Add(new NpgsqlParameter("envs", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = query.EnvironmentIds.ToArray() });
                var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                oldest = value is DateTime dateTime ? new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)) : value as DateTimeOffset?;
            }

            var hasMore = rows.Count > query.Limit;
            if (hasMore) rows.RemoveAt(rows.Count - 1);
            var last = rows.LastOrDefault();
            return new ContainerSearchResult(
                rows,
                summary,
                oldest,
                hasMore && last is not null ? new ContainerSearchCursor(last.SortKey, last.EnvironmentId, last.Container.ContainerId) : null);
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync().ConfigureAwait(false);
        }
    }

    public async Task<ContainerInventory?> GetAsync(Guid environmentId, string containerId, CancellationToken cancellationToken)
    {
        var entity = await dbContext.InventoryContainers.AsNoTracking()
            .SingleOrDefaultAsync(item => item.EnvironmentId == environmentId && item.ContainerId == containerId, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : PostgresInventoryProjectionStore.ToDomain(entity);
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
}
