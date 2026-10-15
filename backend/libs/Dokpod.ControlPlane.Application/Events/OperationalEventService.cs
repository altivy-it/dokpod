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

    /// <summary>Grava e devolve só os eventos novos (spec 016: reenvio deduplicado não reabre alerta).</summary>
    async Task<IReadOnlyList<OperationalEvent>> AppendNewAsync(IReadOnlyCollection<OperationalEvent> events, CancellationToken cancellationToken) =>
        await AppendAsync(events, cancellationToken).ConfigureAwait(false) == events.Count ? events.ToArray() : [];

    Task<OperationalEventPage> ListAsync(OperationalEventQuery query, CancellationToken cancellationToken);

    Task<int> CountAttentionAsync(IReadOnlyCollection<Guid> environmentIds, DateTimeOffset sinceUtc, CancellationToken cancellationToken);

    Task<long> ApplyRetentionAsync(TimeSpan retention, CancellationToken cancellationToken);

    /// <summary>Reinícios do container desde o horário (auto_restarted + comandos de restart concluídos; 011 D-005).</summary>
    Task<int> CountRestartsAsync(Guid environmentId, string containerId, DateTimeOffset sinceUtc, CancellationToken cancellationToken) =>
        Task.FromResult(0);
}

/// <summary>Recebe os eventos efetivamente gravados (spec 016: avaliação de alertas).</summary>
public interface IOperationalEventObserver
{
    Task OnEventsAsync(Guid environmentId, IReadOnlyCollection<OperationalEvent> events, CancellationToken cancellationToken);
}

public sealed class OperationalEventService(
    IOperationalEventStore store,
    IControlPlaneNotifier notifier,
    TimeProvider timeProvider,
    IOperationalEventObserver? observer = null)
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
        var written = await store.AppendNewAsync(events, cancellationToken).ConfigureAwait(false);
        if (written.Count > 0)
        {
            await notifier.NotifyAsync(environmentId, InvalidationKinds.Events, cancellationToken).ConfigureAwait(false);
            // Avaliação de alertas depois da gravação e da notificação: uma falha aqui não perde o evento (D-002).
            if (observer is not null) await observer.OnEventsAsync(environmentId, written, cancellationToken).ConfigureAwait(false);
        }

        return written.Count;
    }
}
