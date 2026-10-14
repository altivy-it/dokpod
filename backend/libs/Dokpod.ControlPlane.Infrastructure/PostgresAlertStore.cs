using System.Data.Common;
using Dokpod.ControlPlane.Application.Alerts;
using Dokpod.Domain.Alerts;
using Dokpod.Domain.Environments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Dokpod.ControlPlane.Infrastructure;

/// <summary>Regras, alertas, linha do tempo e silenciamentos (spec 016, migration 202610140001).</summary>
public sealed class PostgresAlertStore(ControlPlaneDbContext dbContext) : IAlertStore
{
    private const string AlertColumns = """
        a.alert_id, a.rule_id, r.name, a.environment_id, a.container_id, a.container_name, a.severity, a.state,
        a.condition_text, a.opened_at_utc, a.last_occurred_at_utc, a.occurrences, a.assignee, a.silence_id,
        a.resolved_at_utc, a.resolution_reason
        """;

    public async Task<IReadOnlyList<AlertRule>> ListRulesAsync(CancellationToken cancellationToken) =>
        await QueryAsync("SELECT * FROM dokpod.alert_rules ORDER BY built_in DESC, name", [], ReadRule, cancellationToken).ConfigureAwait(false);

    public async Task<AlertRule?> GetRuleAsync(Guid ruleId, CancellationToken cancellationToken) =>
        (await QueryAsync("SELECT * FROM dokpod.alert_rules WHERE rule_id = @id", [P("id", ruleId)], ReadRule, cancellationToken).ConfigureAwait(false)).FirstOrDefault();

    public async Task<bool> SaveRuleAsync(AlertRule rule, string actor, int? expectedVersion, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var parameters = new List<NpgsqlParameter>
        {
            P("id", rule.RuleId), P("name", rule.Name.Trim()), P("description", rule.Description), P("kind", rule.ConditionKind == AlertConditionKind.Event ? "event" : "metric"),
            P("eventType", rule.EventType), P("metric", rule.Metric is { } metric ? AlertValues.Metric(metric) : null),
            P("threshold", rule.ThresholdPercent is { } threshold ? (decimal)threshold : null), P("duration", rule.DurationSeconds), P("occurrences", rule.MinOccurrences),
            P("severity", AlertValues.Severity(rule.Severity)), P("stage", rule.Scope.Stage is { } stage ? EnvironmentStages.ToValue(stage) : null),
            P("environment", rule.Scope.EnvironmentId), P("pattern", rule.Scope.ContainerPattern), P("enabled", rule.Enabled), P("builtIn", rule.BuiltIn),
            P("actor", actor), P("at", at),
        };
        if (expectedVersion is null)
        {
            return await ExecuteAsync(
                """
                INSERT INTO dokpod.alert_rules (rule_id, name, description, condition_kind, event_type, metric, threshold_percent, duration_seconds,
                    min_occurrences, severity, scope_stage, scope_environment_id, scope_container_pattern, enabled, built_in, version, updated_by, updated_at_utc)
                VALUES (@id, @name, @description, @kind, @eventType, @metric, @threshold, @duration, @occurrences, @severity, @stage, @environment, @pattern,
                    @enabled, @builtIn, 1, @actor, @at)
                ON CONFLICT (rule_id) DO NOTHING
                """,
                parameters,
                cancellationToken).ConfigureAwait(false) == 1;
        }

        parameters.Add(P("version", expectedVersion.Value));
        return await ExecuteAsync(
            """
            UPDATE dokpod.alert_rules SET name = @name, description = @description, condition_kind = @kind, event_type = @eventType, metric = @metric,
                threshold_percent = @threshold, duration_seconds = @duration, min_occurrences = @occurrences, severity = @severity, scope_stage = @stage,
                scope_environment_id = @environment, scope_container_pattern = @pattern, enabled = @enabled, version = version + 1,
                updated_by = @actor, updated_at_utc = @at
            WHERE rule_id = @id AND version = @version
            """,
            parameters,
            cancellationToken).ConfigureAwait(false) == 1;
    }

    public async Task<bool> DeleteRuleAsync(Guid ruleId, CancellationToken cancellationToken)
    {
        // Alertas resolvidos da regra continuam no histórico; a FK impede apagar a regra com alertas, então o histórico
        // dela é removido junto (os ativos já foram resolvidos pelo serviço com o motivo "regra removida").
        await ExecuteAsync("DELETE FROM dokpod.alerts WHERE rule_id = @id AND state = 'resolved'", [P("id", ruleId)], cancellationToken).ConfigureAwait(false);
        return await ExecuteAsync("DELETE FROM dokpod.alert_rules WHERE rule_id = @id", [P("id", ruleId)], cancellationToken).ConfigureAwait(false) == 1;
    }

    public async Task<(AlertRecord Alert, bool Opened)> OpenOrOccurAsync(AlertOccurrence occurrence, CancellationToken cancellationToken)
    {
        var targetKey = AlertRecord.TargetKey(occurrence.EnvironmentId, occurrence.ContainerId);
        var state = occurrence.SilenceId is null ? "open" : "silenced";
        var rows = await QueryAsync(
            """
            INSERT INTO dokpod.alerts AS a (alert_id, rule_id, environment_id, container_id, container_name, target_key, severity, state, condition_text,
                opened_at_utc, last_occurred_at_utc, occurrences, silence_id)
            VALUES (@id, @rule, @environment, @container, @containerName, @target, @severity, @state, @condition, @at, @at, 1, @silence)
            ON CONFLICT (rule_id, target_key) WHERE state <> 'resolved'
            DO UPDATE SET occurrences = a.occurrences + 1, last_occurred_at_utc = GREATEST(a.last_occurred_at_utc, EXCLUDED.last_occurred_at_utc),
                condition_text = EXCLUDED.condition_text, container_name = COALESCE(EXCLUDED.container_name, a.container_name)
            RETURNING a.alert_id, (xmax = 0) AS inserted
            """,
            [
                P("id", Guid.NewGuid()), P("rule", occurrence.Rule.RuleId), P("environment", occurrence.EnvironmentId), P("container", occurrence.ContainerId),
                P("containerName", occurrence.ContainerName), P("target", targetKey), P("severity", AlertValues.Severity(occurrence.Rule.Severity)), P("state", state),
                P("condition", Truncate(occurrence.ConditionText, 300)), P("at", occurrence.OccurredAtUtc), P("silence", occurrence.SilenceId),
            ],
            reader => (Id: reader.GetGuid(0), Inserted: reader.GetBoolean(1)),
            cancellationToken).ConfigureAwait(false);
        var (alertId, inserted) = rows[0];
        await AddTimelineAsync(alertId, occurrence.OccurredAtUtc, inserted ? AlertTimelineKinds.Opened : AlertTimelineKinds.Occurred, null, inserted && occurrence.SilenceId is not null ? "Aberto durante silenciamento." : null, cancellationToken).ConfigureAwait(false);
        return ((await GetAsync(alertId, cancellationToken).ConfigureAwait(false))!, inserted);
    }

    public async Task<IReadOnlyList<AlertRecord>> ListActiveAsync(CancellationToken cancellationToken) =>
        await QueryAsync($"SELECT {AlertColumns} FROM dokpod.alerts a JOIN dokpod.alert_rules r ON r.rule_id = a.rule_id WHERE a.state <> 'resolved'", [], ReadAlert, cancellationToken).ConfigureAwait(false);

    public async Task<AlertPage> ListAsync(AlertQuery query, CancellationToken cancellationToken)
    {
        var where = new List<string> { "a.environment_id = ANY(@environments)", query.History ? "a.state = 'resolved'" : "a.state <> 'resolved'" };
        var parameters = new List<NpgsqlParameter> { P("environments", query.EnvironmentIds.ToArray()), P("limit", query.Limit + 1) };
        if (query.Severities.Count > 0)
        {
            where.Add("a.severity = ANY(@severities)");
            parameters.Add(P("severities", query.Severities.Select(AlertValues.Severity).ToArray()));
        }

        if (query.States.Count > 0)
        {
            where.Add("a.state = ANY(@states)");
            parameters.Add(P("states", query.States.Select(AlertValues.State).ToArray()));
        }

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            where.Add("(r.name ILIKE @text ESCAPE '\\' OR a.container_name ILIKE @text ESCAPE '\\' OR a.condition_text ILIKE @text ESCAPE '\\')");
            parameters.Add(P("text", "%" + query.Text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%"));
        }

        if (query.RuleId is { } rule)
        {
            where.Add("a.rule_id = @rule");
            parameters.Add(P("rule", rule));
        }

        // Ordem: abertos pela última ocorrência; histórico pela resolução; ambos decrescentes com desempate por id.
        var orderColumn = query.History ? "a.resolved_at_utc" : "a.last_occurred_at_utc";
        if (query.SinceUtc is { } since)
        {
            where.Add($"{orderColumn} >= @since");
            parameters.Add(P("since", since));
        }

        if (query.UntilUtc is { } until)
        {
            where.Add($"{orderColumn} < @until");
            parameters.Add(P("until", until));
        }

        if (query.After is { } after)
        {
            where.Add($"({orderColumn}, a.alert_id) < (@afterAt, @afterId)");
            parameters.Add(P("afterAt", after.At));
            parameters.Add(P("afterId", after.AlertId));
        }

        var rows = await QueryAsync(
            $"SELECT {AlertColumns} FROM dokpod.alerts a JOIN dokpod.alert_rules r ON r.rule_id = a.rule_id WHERE {string.Join(" AND ", where)} ORDER BY {orderColumn} DESC, a.alert_id DESC LIMIT @limit",
            parameters,
            ReadAlert,
            cancellationToken).ConfigureAwait(false);
        var page = rows.Take(query.Limit).ToArray();
        var next = rows.Count > query.Limit
            ? new AlertCursor(query.History ? page[^1].ResolvedAtUtc!.Value : page[^1].LastOccurredAtUtc, page[^1].AlertId)
            : null;
        return new AlertPage(page, next);
    }

    public async Task<AlertRecord?> GetAsync(Guid alertId, CancellationToken cancellationToken) =>
        (await QueryAsync($"SELECT {AlertColumns} FROM dokpod.alerts a JOIN dokpod.alert_rules r ON r.rule_id = a.rule_id WHERE a.alert_id = @id", [P("id", alertId)], ReadAlert, cancellationToken).ConfigureAwait(false)).FirstOrDefault();

    public async Task<IReadOnlyList<AlertTimelineEntry>> GetTimelineAsync(Guid alertId, CancellationToken cancellationToken) =>
        await QueryAsync(
            "SELECT occurred_at_utc, kind, actor, comment FROM dokpod.alert_timeline WHERE alert_id = @id ORDER BY occurred_at_utc, entry_id",
            [P("id", alertId)],
            reader => new AlertTimelineEntry(reader.GetFieldValue<DateTimeOffset>(0), reader.GetString(1), Nullable(reader, 2), Nullable(reader, 3)),
            cancellationToken).ConfigureAwait(false);

    public async Task<bool> TransitionAsync(
        Guid alertId,
        AlertState state,
        string? assignee,
        Guid? silenceId,
        string? resolutionReason,
        DateTimeOffset at,
        string timelineKind,
        string? actor,
        string? comment,
        CancellationToken cancellationToken)
    {
        var changed = await ExecuteAsync(
            """
            UPDATE dokpod.alerts SET state = @state, assignee = COALESCE(@assignee, assignee), silence_id = @silence,
                resolved_at_utc = CASE WHEN @state = 'resolved' THEN @at ELSE NULL END,
                resolution_reason = CASE WHEN @state = 'resolved' THEN @reason ELSE NULL END
            WHERE alert_id = @id AND state <> 'resolved'
            """,
            [P("id", alertId), P("state", AlertValues.State(state)), P("assignee", assignee), P("silence", silenceId), P("at", at), P("reason", resolutionReason)],
            cancellationToken).ConfigureAwait(false);
        if (changed == 1) await AddTimelineAsync(alertId, at, timelineKind, actor, comment, cancellationToken).ConfigureAwait(false);
        return changed == 1;
    }

    public async Task<int> CountEventsAsync(Guid environmentId, string eventType, string? subjectId, DateTimeOffset sinceUtc, CancellationToken cancellationToken) =>
        (await QueryAsync(
            """
            SELECT count(*)::int FROM dokpod.environment_events
            WHERE environment_id = @environment AND type = @type AND occurred_at >= @since AND (@subject::text IS NULL OR subject_id = @subject)
            """,
            [P("environment", environmentId), P("type", eventType), P("since", sinceUtc), P("subject", subjectId)],
            reader => reader.GetInt32(0),
            cancellationToken).ConfigureAwait(false))[0];

    public async Task<IReadOnlyList<AlertSilence>> ListSilencesAsync(bool activeOnly, DateTimeOffset now, CancellationToken cancellationToken) =>
        await QueryAsync(
            activeOnly
                ? "SELECT * FROM dokpod.alert_silences WHERE ended_at_utc IS NULL AND starts_at_utc <= @now AND ends_at_utc > @now ORDER BY ends_at_utc"
                : "SELECT * FROM dokpod.alert_silences ORDER BY created_at_utc DESC LIMIT 500",
            [P("now", now)],
            ReadSilence,
            cancellationToken).ConfigureAwait(false);

    public async Task<AlertSilence?> GetSilenceAsync(Guid silenceId, CancellationToken cancellationToken) =>
        (await QueryAsync("SELECT * FROM dokpod.alert_silences WHERE silence_id = @id", [P("id", silenceId)], ReadSilence, cancellationToken).ConfigureAwait(false)).FirstOrDefault();

    public Task CreateSilenceAsync(AlertSilence silence, CancellationToken cancellationToken) =>
        ExecuteAsync(
            """
            INSERT INTO dokpod.alert_silences (silence_id, scope_stage, scope_environment_id, scope_container_pattern, alert_id, starts_at_utc, ends_at_utc,
                justification, created_by, created_at_utc)
            VALUES (@id, @stage, @environment, @pattern, @alert, @starts, @ends, @justification, @author, @created)
            """,
            [
                P("id", silence.SilenceId), P("stage", silence.Scope.Stage is { } stage ? EnvironmentStages.ToValue(stage) : null), P("environment", silence.Scope.EnvironmentId),
                P("pattern", silence.Scope.ContainerPattern), P("alert", silence.AlertId), P("starts", silence.StartsAtUtc), P("ends", silence.EndsAtUtc),
                P("justification", silence.Justification), P("author", silence.CreatedBy), P("created", silence.StartsAtUtc),
            ],
            cancellationToken);

    public async Task<bool> EndSilenceAsync(Guid silenceId, DateTimeOffset at, CancellationToken cancellationToken) =>
        await ExecuteAsync("UPDATE dokpod.alert_silences SET ended_at_utc = @at WHERE silence_id = @id AND ended_at_utc IS NULL", [P("id", silenceId), P("at", at)], cancellationToken).ConfigureAwait(false) == 1;

    public Task<int> DeleteResolvedBeforeAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken) =>
        ExecuteAsync(
            "DELETE FROM dokpod.alerts WHERE alert_id IN (SELECT alert_id FROM dokpod.alerts WHERE state = 'resolved' AND resolved_at_utc < @cutoff LIMIT 5000)",
            [P("cutoff", cutoffUtc)],
            cancellationToken);

    private Task<int> AddTimelineAsync(Guid alertId, DateTimeOffset at, string kind, string? actor, string? comment, CancellationToken cancellationToken) =>
        ExecuteAsync(
            "INSERT INTO dokpod.alert_timeline (alert_id, occurred_at_utc, kind, actor, comment) VALUES (@id, @at, @kind, @actor, @comment)",
            [P("id", alertId), P("at", at), P("kind", kind), P("actor", actor), P("comment", comment is null ? null : Truncate(comment, 500))],
            cancellationToken);

    private static AlertRule ReadRule(DbDataReader reader)
    {
        var kind = reader.GetString(reader.GetOrdinal("condition_kind")) == "event" ? AlertConditionKind.Event : AlertConditionKind.Metric;
        AlertValues.TryParseMetric(Nullable(reader, reader.GetOrdinal("metric")), out var metric);
        AlertValues.TryParseSeverity(reader.GetString(reader.GetOrdinal("severity")), out var severity);
        var stageText = Nullable(reader, reader.GetOrdinal("scope_stage"));
        var thresholdOrdinal = reader.GetOrdinal("threshold_percent");
        var environmentOrdinal = reader.GetOrdinal("scope_environment_id");
        return new AlertRule(
            reader.GetGuid(reader.GetOrdinal("rule_id")),
            reader.GetString(reader.GetOrdinal("name")),
            Nullable(reader, reader.GetOrdinal("description")),
            kind,
            Nullable(reader, reader.GetOrdinal("event_type")),
            kind == AlertConditionKind.Metric ? metric : null,
            reader.IsDBNull(thresholdOrdinal) ? null : (double)reader.GetDecimal(thresholdOrdinal),
            reader.GetInt32(reader.GetOrdinal("duration_seconds")),
            severity,
            new AlertScope(
                stageText is not null && EnvironmentStages.TryParse(stageText, out var stage) ? stage : null,
                reader.IsDBNull(environmentOrdinal) ? null : reader.GetGuid(environmentOrdinal),
                Nullable(reader, reader.GetOrdinal("scope_container_pattern"))),
            reader.GetBoolean(reader.GetOrdinal("enabled")),
            reader.GetBoolean(reader.GetOrdinal("built_in")),
            reader.GetInt32(reader.GetOrdinal("version")))
        {
            MinOccurrences = reader.GetInt32(reader.GetOrdinal("min_occurrences")),
        };
    }

    private static AlertRecord ReadAlert(DbDataReader reader)
    {
        AlertValues.TryParseSeverity(reader.GetString(6), out var severity);
        AlertValues.TryParseState(reader.GetString(7), out var state);
        return new AlertRecord(
            reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetGuid(3), Nullable(reader, 4), Nullable(reader, 5), severity, state,
            reader.GetString(8), reader.GetFieldValue<DateTimeOffset>(9), reader.GetFieldValue<DateTimeOffset>(10), reader.GetInt32(11), Nullable(reader, 12),
            reader.IsDBNull(13) ? null : reader.GetGuid(13), reader.IsDBNull(14) ? null : reader.GetFieldValue<DateTimeOffset>(14), Nullable(reader, 15));
    }

    private static AlertSilence ReadSilence(DbDataReader reader)
    {
        var stageText = Nullable(reader, reader.GetOrdinal("scope_stage"));
        var environmentOrdinal = reader.GetOrdinal("scope_environment_id");
        var alertOrdinal = reader.GetOrdinal("alert_id");
        var endedOrdinal = reader.GetOrdinal("ended_at_utc");
        return new AlertSilence(
            reader.GetGuid(reader.GetOrdinal("silence_id")),
            new AlertScope(
                stageText is not null && EnvironmentStages.TryParse(stageText, out var stage) ? stage : null,
                reader.IsDBNull(environmentOrdinal) ? null : reader.GetGuid(environmentOrdinal),
                Nullable(reader, reader.GetOrdinal("scope_container_pattern"))),
            reader.IsDBNull(alertOrdinal) ? null : reader.GetGuid(alertOrdinal),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("starts_at_utc")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("ends_at_utc")),
            reader.IsDBNull(endedOrdinal) ? null : reader.GetFieldValue<DateTimeOffset>(endedOrdinal),
            reader.GetString(reader.GetOrdinal("justification")),
            reader.GetString(reader.GetOrdinal("created_by")));
    }

    private static string? Nullable(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    private static NpgsqlParameter P(string name, object? value) => new(name, value ?? DBNull.Value);

    private async Task<int> ExecuteAsync(string sql, IReadOnlyList<NpgsqlParameter> parameters, CancellationToken cancellationToken) =>
        await dbContext.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken).ConfigureAwait(false);

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
