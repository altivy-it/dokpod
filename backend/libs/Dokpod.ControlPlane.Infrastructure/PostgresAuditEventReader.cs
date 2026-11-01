using System.Data.Common;
using System.Text;
using System.Text.Json;
using Dokpod.ControlPlane.Application.Auditing;
using Dokpod.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace Dokpod.ControlPlane.Infrastructure;

/// <summary>Leitura da trilha de auditoria (spec 017 D-002): sempre por período, com cursor por chave.</summary>
public sealed class PostgresAuditEventReader(ControlPlaneDbContext dbContext) : IAuditEventReader
{
    internal const string Columns =
        "event_id, occurred_at_utc, correlation_id, actor_kind, actor_id, actor_name, action, environment_id, outcome, failure_code, subject_id, command_id, details";

    public async Task<AuditPage> ListAsync(AuditQuery query, CancellationToken cancellationToken)
    {
        if (query.EnvironmentIds.Count == 0) return new AuditPage([], null);
        var (where, parameters) = Filter(query);
        if (query.After is { } after)
        {
            where.Append(" AND (occurred_at_utc, event_id) < (@afterAt, @afterId)");
            parameters.Add(P("afterAt", after.At));
            parameters.Add(P("afterId", after.EventId));
        }

        parameters.Add(P("limit", query.Limit + 1));
        var rows = await QueryAsync(
            $"SELECT {Columns} FROM dokpod.audit_events WHERE {where} ORDER BY occurred_at_utc DESC, event_id DESC LIMIT @limit",
            parameters,
            cancellationToken).ConfigureAwait(false);
        var page = rows.Take(query.Limit).ToArray();
        return new AuditPage(page, rows.Count > query.Limit ? new AuditCursor(page[^1].OccurredAtUtc, page[^1].EventId) : null);
    }

    public async Task<int> CountAsync(AuditQuery query, int cap, CancellationToken cancellationToken)
    {
        if (query.EnvironmentIds.Count == 0) return 0;
        var (where, parameters) = Filter(query);
        parameters.Add(P("cap", cap));
        var connection = dbContext.Database.GetDbConnection();
        var opened = connection.State != System.Data.ConnectionState.Open;
        if (opened) await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT count(*) FROM (SELECT 1 FROM dokpod.audit_events WHERE {where} LIMIT @cap) AS capped";
            command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
            foreach (var parameter in parameters) command.Parameters.Add(parameter.Clone());
            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture);
        }
        finally
        {
            if (opened) await connection.CloseAsync().ConfigureAwait(false);
        }
    }

    public async Task<AuditEventRecord?> GetAsync(Guid eventId, IReadOnlyCollection<Guid> environmentIds, DateTimeOffset? at, DateTimeOffset notBefore, CancellationToken cancellationToken)
    {
        if (environmentIds.Count == 0) return null;
        var time = at is null ? "occurred_at_utc >= @notBefore" : "occurred_at_utc = @at";
        var rows = await QueryAsync(
            $"SELECT {Columns} FROM dokpod.audit_events WHERE event_id = @id AND environment_id = ANY(@environments) AND {time} LIMIT 1",
            [P("id", eventId), Environments(environmentIds), P("at", at), P("notBefore", notBefore)],
            cancellationToken).ConfigureAwait(false);
        return rows.FirstOrDefault();
    }

    public async Task<IReadOnlyList<AuditEventRecord>> RelatedAsync(AuditEventRecord audited, IReadOnlyCollection<Guid> environmentIds, TimeSpan window, int limit, CancellationToken cancellationToken)
    {
        if (environmentIds.Count == 0) return [];
        return await QueryAsync(
            $"""
            SELECT {Columns} FROM dokpod.audit_events
            WHERE environment_id = ANY(@environments)
                AND occurred_at_utc BETWEEN @from AND @to
                AND event_id <> @id
                AND (correlation_id = @correlation OR (@command::uuid IS NOT NULL AND command_id = @command))
            ORDER BY occurred_at_utc, event_id
            LIMIT @limit
            """,
            [
                Environments(environmentIds), P("from", audited.OccurredAtUtc - window), P("to", audited.OccurredAtUtc + window),
                P("id", audited.EventId), P("correlation", audited.CorrelationId),
                new NpgsqlParameter("command", NpgsqlDbType.Uuid) { Value = (object?)audited.CommandId ?? DBNull.Value }, P("limit", limit),
            ],
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<DateTimeOffset?> OldestAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken)
    {
        if (environmentIds.Count == 0) return null;
        var connection = dbContext.Database.GetDbConnection();
        var opened = connection.State != System.Data.ConnectionState.Open;
        if (opened) await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT min(occurred_at_utc) FROM dokpod.audit_events WHERE environment_id = ANY(@environments)";
            command.Parameters.Add(Environments(environmentIds));
            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return value is DateTime utc ? new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)) : value as DateTimeOffset?;
        }
        finally
        {
            if (opened) await connection.CloseAsync().ConfigureAwait(false);
        }
    }

    private static (StringBuilder Where, List<NpgsqlParameter> Parameters) Filter(AuditQuery query)
    {
        var where = new StringBuilder("environment_id = ANY(@environments) AND occurred_at_utc >= @since AND occurred_at_utc < @until");
        var parameters = new List<NpgsqlParameter> { Environments(query.EnvironmentIds), P("since", query.SinceUtc), P("until", query.UntilUtc) };
        if (query.Actions.Count > 0)
        {
            where.Append(" AND action = ANY(@actions)");
            parameters.Add(new NpgsqlParameter("actions", NpgsqlDbType.Array | NpgsqlDbType.Varchar) { Value = query.Actions.ToArray() });
        }

        if (query.Outcomes.Count > 0)
        {
            where.Append(" AND outcome = ANY(@outcomes)");
            parameters.Add(new NpgsqlParameter("outcomes", NpgsqlDbType.Array | NpgsqlDbType.Smallint) { Value = query.Outcomes.Select(o => (short)o).ToArray() });
        }

        if (!string.IsNullOrWhiteSpace(query.Actor))
        {
            where.Append(" AND (actor_id = @actor OR actor_name ILIKE @actorPrefix ESCAPE '\\')");
            parameters.Add(P("actor", query.Actor.Trim()));
            parameters.Add(P("actorPrefix", Like(query.Actor.Trim()) + "%"));
        }

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            var text = query.Text.Trim();
            if (Guid.TryParse(text, out var id))
            {
                where.Append(" AND (correlation_id = @textId OR command_id = @textId OR event_id = @textId OR subject_id = @text)");
                parameters.Add(P("textId", id));
                parameters.Add(P("text", text));
            }
            else
            {
                // D-008: prefixo do alvo (ids de container) ou busca textual no índice GIN (ator, ação, alvo, falha e detalhes).
                where.Append(
                    " AND (subject_id LIKE @textPrefix ESCAPE '\\'" +
                    " OR dokpod.dokpod_audit_search_document(action, actor_id, actor_name, subject_id, failure_code, details)" +
                    " @@ dokpod.dokpod_audit_search_query(@text))");
                // LIKE (e não ILIKE) usa o índice varchar_pattern_ops; os ids gravados como alvo são minúsculos.
                parameters.Add(P("textPrefix", Like(text.ToLowerInvariant()) + "%"));
                parameters.Add(P("text", text));
            }
        }

        return (where, parameters);
    }

    private static string Like(string value) => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static NpgsqlParameter Environments(IReadOnlyCollection<Guid> ids) =>
        new("environments", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = ids.ToArray() };

    private static NpgsqlParameter P(string name, object? value) => new(name, value ?? DBNull.Value);

    private async Task<List<AuditEventRecord>> QueryAsync(string sql, IReadOnlyList<NpgsqlParameter> parameters, CancellationToken cancellationToken)
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
            var results = new List<AuditEventRecord>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) results.Add(Read(reader));
            return results;
        }
        finally
        {
            if (opened) await connection.CloseAsync().ConfigureAwait(false);
        }
    }

    internal static AuditEventRecord Read(DbDataReader reader)
    {
        string? Text(int i) => reader.IsDBNull(i) ? null : reader.GetString(i);
        var details = reader.IsDBNull(12) ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(12));
        return new AuditEventRecord(
            reader.GetGuid(0),
            new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Utc)),
            reader.GetGuid(2),
            (AuditActorKind)reader.GetInt16(3),
            reader.GetString(4),
            Text(5),
            reader.GetString(6),
            reader.GetGuid(7),
            (AuditOutcome)reader.GetInt16(8),
            Text(9),
            Text(10),
            reader.IsDBNull(11) ? null : reader.GetGuid(11),
            details is { Count: > 0 } ? details : null);
    }
}
