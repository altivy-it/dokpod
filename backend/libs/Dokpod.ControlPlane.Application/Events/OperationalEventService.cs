using Dokpod.ControlPlane.Application.Realtime;
using Dokpod.Domain.Events;

namespace Dokpod.ControlPlane.Application.Events;

public sealed record OperationalEventCursor(DateTimeOffset OccurredAtUtc, Guid EventId);

public sealed record OperationalEventQuery(
    IReadOnlyCollection<Guid> EnvironmentIds,
    string? Severity,
    DateTimeOffset SinceUtc,
    DateTimeOffset? UntilUtc,
    OperationalEventCursor? After,
    int Limit);

public sealed record OperationalEventPage(IReadOnlyList<OperationalEvent> Events, OperationalEventCursor? Next);

public interface IOperationalEventStore
{
    /// <summary>Grava eventos; duplicatas por `source_key` são ignoradas. Retorna quantos foram gravados.</summary>
    Task<int> AppendAsync(IReadOnlyCollection<OperationalEvent> events, CancellationToken cancellationToken);

    Task<OperationalEventPage> ListAsync(OperationalEventQuery query, CancellationToken cancellationToken);

    Task<int> CountAttentionAsync(IReadOnlyCollection<Guid> environmentIds, DateTimeOffset sinceUtc, CancellationToken cancellationToken);

    Task<long> ApplyRetentionAsync(TimeSpan retention, CancellationToken cancellationToken);

    /// <summary>Reinícios do container desde o horário (auto_restarted + comandos de restart concluídos; 011 D-005).</summary>
    Task<int> CountRestartsAsync(Guid environmentId, string containerId, DateTimeOffset sinceUtc, CancellationToken cancellationToken) =>
        Task.FromResult(0);
}

public sealed class OperationalEventService(
    IOperationalEventStore store,
    IControlPlaneNotifier notifier,
    TimeProvider timeProvider)
{
    public async Task RecordAsync(
        Guid environmentId,
        string type,
        string? subjectKind,
        string? subjectId,
        string? subjectName,
        IReadOnlyDictionary<string, string>? parameters,
        CancellationToken cancellationToken,
        DateTimeOffset? occurredAtUtc = null)
    {
        var now = timeProvider.GetUtcNow();
        var operationalEvent = OperationalEvent.Create(
            environmentId, type, occurredAtUtc ?? now, now, subjectKind, subjectId, subjectName, parameters);
        await AppendAsync(environmentId, [operationalEvent], cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> AppendAsync(
        Guid environmentId,
        IReadOnlyCollection<OperationalEvent> events,
        CancellationToken cancellationToken)
    {
        if (events.Count == 0) return 0;
        var written = await store.AppendAsync(events, cancellationToken).ConfigureAwait(false);
        if (written > 0)
        {
            await notifier.NotifyAsync(environmentId, InvalidationKinds.Events, cancellationToken).ConfigureAwait(false);
        }

        return written;
    }
}
