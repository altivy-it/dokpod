using Dokpod.Domain.Auditing;

namespace Dokpod.ControlPlane.Application.Auditing;

/// <summary>Evento da trilha como foi gravado (spec 017); detalhes já mascarados na origem.</summary>
public sealed record AuditEventRecord(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid CorrelationId,
    AuditActorKind ActorKind,
    string ActorId,
    string? ActorName,
    string Action,
    Guid EnvironmentId,
    AuditOutcome Outcome,
    string? FailureCode,
    string? SubjectId,
    Guid? CommandId,
    IReadOnlyDictionary<string, string>? Details);

public sealed record AuditCursor(DateTimeOffset At, Guid EventId);

/// <summary>Consulta por período (D-002); <see cref="Actions"/> vazio significa todas as ações.</summary>
public sealed record AuditQuery(
    IReadOnlyCollection<Guid> EnvironmentIds,
    DateTimeOffset SinceUtc,
    DateTimeOffset UntilUtc,
    string? Actor,
    IReadOnlyCollection<string> Actions,
    IReadOnlyCollection<AuditOutcome> Outcomes,
    string? Text,
    AuditCursor? After,
    int Limit);

public sealed record AuditPage(IReadOnlyList<AuditEventRecord> Events, AuditCursor? Next);

public interface IAuditEventReader
{
    Task<AuditPage> ListAsync(AuditQuery query, CancellationToken cancellationToken);

    /// <summary>Quantidade de eventos do filtro, contando no máximo <paramref name="cap"/> (exportação).</summary>
    Task<int> CountAsync(AuditQuery query, int cap, CancellationToken cancellationToken);

    /// <summary>Evento por id nos hosts informados; <paramref name="at"/> localiza a partição quando conhecido.</summary>
    Task<AuditEventRecord?> GetAsync(Guid eventId, IReadOnlyCollection<Guid> environmentIds, DateTimeOffset? at, DateTimeOffset notBefore, CancellationToken cancellationToken);

    /// <summary>Eventos da mesma correlação ou do mesmo comando, nos hosts informados, em ordem cronológica.</summary>
    Task<IReadOnlyList<AuditEventRecord>> RelatedAsync(AuditEventRecord audited, IReadOnlyCollection<Guid> environmentIds, TimeSpan window, int limit, CancellationToken cancellationToken);

    Task<DateTimeOffset?> OldestAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken);
}
