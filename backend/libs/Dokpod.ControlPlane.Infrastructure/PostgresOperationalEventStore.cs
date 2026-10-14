using System.Text.Json;
using Dokpod.ControlPlane.Application.Events;
using Dokpod.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Dokpod.ControlPlane.Infrastructure;

public sealed class PostgresOperationalEventStore(ControlPlaneDbContext dbContext) : IOperationalEventStore
{
    public async Task<int> AppendAsync(IReadOnlyCollection<OperationalEvent> events, CancellationToken cancellationToken) =>
        (await AppendNewAsync(events, cancellationToken).ConfigureAwait(false)).Count;

    public async Task<IReadOnlyList<OperationalEvent>> AppendNewAsync(IReadOnlyCollection<OperationalEvent> events, CancellationToken cancellationToken)
    {
        var written = new List<OperationalEvent>(events.Count);
        foreach (var item in events)
        {
            // ON CONFLICT ignora reenvio do mesmo evento do engine (dedup por source_key).
            var inserted = await dbContext.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO dokpod.environment_events
                    (event_id, environment_id, type, severity, occurred_at, received_at, subject_kind, subject_id, subject_name, parameters, aggregated_count, source_key)
                VALUES (@id, @env, @type, @severity, @occurred, @received, @kind, @subjectId, @subjectName, @parameters, @count, @sourceKey)
                ON CONFLICT DO NOTHING
                """,
                [
                    new NpgsqlParameter("id", item.EventId),
                    new NpgsqlParameter("env", item.EnvironmentId),
                    new NpgsqlParameter("type", item.Type),
                    new NpgsqlParameter("severity", item.Severity),
                    new NpgsqlParameter("occurred", item.OccurredAtUtc),
                    new NpgsqlParameter("received", item.ReceivedAtUtc),
                    new NpgsqlParameter("kind", (object?)item.SubjectKind ?? DBNull.Value),
                    new NpgsqlParameter("subjectId", (object?)item.SubjectId ?? DBNull.Value),
                    new NpgsqlParameter("subjectName", (object?)item.SubjectName ?? DBNull.Value),
                    new NpgsqlParameter("parameters", NpgsqlDbType.Jsonb) { Value = JsonSerializer.Serialize(item.Parameters) },
                    new NpgsqlParameter("count", item.AggregatedCount),
                    new NpgsqlParameter("sourceKey", (object?)item.SourceKey ?? DBNull.Value),
                ],
                cancellationToken).ConfigureAwait(false);
            if (inserted == 1) written.Add(item);
        }

        return written;
    }

    public async Task<OperationalEventPage> ListAsync(OperationalEventQuery query, CancellationToken cancellationToken)
    {
        var ids = query.EnvironmentIds.ToArray();
        var source = dbContext.EnvironmentEvents.AsNoTracking()
            .Where(item => ids.Contains(item.EnvironmentId) && item.OccurredAt >= query.SinceUtc);
        if (query.UntilUtc is not null) source = source.Where(item => item.OccurredAt < query.UntilUtc.Value);
        if (!string.IsNullOrWhiteSpace(query.Severity)) source = source.Where(item => item.Severity == query.Severity);
        if (query.After is not null)
        {
            var at = query.After.OccurredAtUtc;
            var id = query.After.EventId;
            source = source.Where(item => item.OccurredAt < at || (item.OccurredAt == at && item.EventId.CompareTo(id) < 0));
        }

        var entities = await source
            .OrderByDescending(item => item.OccurredAt)
            .ThenByDescending(item => item.EventId)
            .Take(query.Limit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var hasMore = entities.Count > query.Limit;
        var page = entities.Take(query.Limit).Select(ToDomain).ToArray();
        return new OperationalEventPage(
            page,
            hasMore ? new OperationalEventCursor(page[^1].OccurredAtUtc, page[^1].EventId) : null);
    }

    public async Task<int> CountAttentionAsync(IReadOnlyCollection<Guid> environmentIds, DateTimeOffset sinceUtc, CancellationToken cancellationToken)
    {
        var ids = environmentIds.ToArray();
        return await dbContext.EnvironmentEvents.AsNoTracking()
            .Where(item => ids.Contains(item.EnvironmentId) && item.OccurredAt >= sinceUtc &&
                (item.Severity == OperationalEventSeverities.Warning || item.Severity == OperationalEventSeverities.Error))
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<long> ApplyRetentionAsync(TimeSpan retention, CancellationToken cancellationToken)
    {
        var result = await dbContext.Database
            .SqlQueryRaw<long>(
                "SELECT dokpod.dokpod_apply_temporal_retention('dokpod.environment_events'::regclass, 'occurred_at', @retention) AS \"Value\"",
                new NpgsqlParameter("retention", retention))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return result.SingleOrDefault();
    }

    public async Task<int> CountRestartsAsync(Guid environmentId, string containerId, DateTimeOffset sinceUtc, CancellationToken cancellationToken)
    {
        var events = await dbContext.EnvironmentEvents.AsNoTracking()
            .Where(item => item.EnvironmentId == environmentId && item.SubjectId == containerId && item.OccurredAt >= sinceUtc &&
                (item.Type == OperationalEventTypes.ContainerAutoRestarted || item.Type == OperationalEventTypes.CommandSucceeded))
            .Select(item => new { item.Type, item.Parameters, item.AggregatedCount })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return events.Sum(item => item.Type == OperationalEventTypes.ContainerAutoRestarted
            ? item.AggregatedCount
            : item.Parameters.Contains("\"RestartContainer\"", StringComparison.Ordinal) ? 1 : 0);
    }

    private static OperationalEvent ToDomain(EnvironmentEventEntity entity) =>
        new(
            entity.EventId,
            entity.EnvironmentId,
            entity.Type,
            entity.Severity,
            entity.OccurredAt,
            entity.ReceivedAt,
            entity.SubjectKind,
            entity.SubjectId,
            entity.SubjectName,
            JsonSerializer.Deserialize<Dictionary<string, string>>(entity.Parameters) ?? [],
            entity.AggregatedCount,
            entity.SourceKey);
}
