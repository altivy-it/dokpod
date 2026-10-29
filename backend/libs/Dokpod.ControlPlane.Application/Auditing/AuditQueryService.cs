using System.Globalization;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.Domain.Auditing;
using Dokpod.Domain.Environments;

namespace Dokpod.ControlPlane.Application.Auditing;

/// <summary>Quem consulta: ator, token e os ambientes que ele pode ler (`environment:read`).</summary>
public sealed record AuditCaller(AuthenticatedActor Actor, string AccessToken, Guid CorrelationId, IReadOnlyList<EnvironmentRegistration> Readable);

/// <summary>Filtro da tela (spec 017 FR-003); período nulo usa as últimas 24 horas.</summary>
public sealed record AuditFilter(
    DateTimeOffset? SinceUtc,
    DateTimeOffset? UntilUtc,
    EnvironmentStage? Stage,
    IReadOnlyCollection<Guid> EnvironmentIds,
    string? Actor,
    IReadOnlyCollection<string> Categories,
    IReadOnlyCollection<string> Actions,
    IReadOnlyCollection<AuditOutcome> Outcomes,
    string? Text);

public enum AuditQueryOutcome
{
    Ok,
    PeriodTooLong,
    InvalidPeriod,
    InvalidFilter,
    ExportTooLarge,
}

public sealed record AuditListResult(AuditQueryOutcome Outcome, AuditPage Page);

public sealed record AuditDetail(AuditEventRecord Event, IReadOnlyList<AuditEventRecord> Related);

public sealed record AuditSummary(int ReadableHosts, DateTimeOffset? OldestEventAtUtc);

public sealed record AuditExport(AuditQueryOutcome Outcome, IReadOnlyList<AuditEventRecord> Events, DateTimeOffset SinceUtc, DateTimeOffset UntilUtc);

/// <summary>Consulta da trilha de auditoria (spec 017) restrita aos hosts com `audit:read` (D-003).</summary>
public sealed class AuditQueryService(
    IAuditEventReader reader,
    IEnvironmentGrantReader grantReader,
    IAuditEventWriter writer,
    TimeProvider timeProvider)
{
    public static readonly TimeSpan DefaultPeriod = TimeSpan.FromHours(24);
    public static readonly TimeSpan MaxPeriod = TimeSpan.FromDays(90);
    public static readonly TimeSpan RelatedWindow = TimeSpan.FromDays(7);
    public const int MaxExport = 10_000;
    public const int MaxRelated = 50;

    // Autorização indisponível conta como negada: a trilha nunca é revelada sem decisão.
    public Task<IReadOnlyList<EnvironmentRegistration>> ReadableAsync(AuditCaller caller, IEnumerable<EnvironmentRegistration>? subset, CancellationToken cancellationToken) =>
        grantReader.WithScopeAsync(subset ?? caller.Readable, EnvironmentResourceScopes.AuditRead, caller.Actor, caller.AccessToken, caller.CorrelationId, cancellationToken);

    public async Task<AuditSummary> SummarizeAsync(AuditCaller caller, CancellationToken cancellationToken)
    {
        var readable = await ReadableAsync(caller, null, cancellationToken).ConfigureAwait(false);
        var oldest = await reader.OldestAsync(readable.Select(r => r.EnvironmentId).ToArray(), cancellationToken).ConfigureAwait(false);
        return new AuditSummary(readable.Count, oldest);
    }

    public async Task<AuditListResult> ListAsync(AuditCaller caller, AuditFilter filter, AuditCursor? after, int limit, CancellationToken cancellationToken)
    {
        var (outcome, query) = await QueryAsync(caller, filter, after, limit, cancellationToken).ConfigureAwait(false);
        if (outcome != AuditQueryOutcome.Ok) return new AuditListResult(outcome, new AuditPage([], null));
        return new AuditListResult(AuditQueryOutcome.Ok, await reader.ListAsync(query!, cancellationToken).ConfigureAwait(false));
    }

    public async Task<AuditDetail?> GetAsync(AuditCaller caller, Guid eventId, DateTimeOffset? at, CancellationToken cancellationToken)
    {
        var readable = (await ReadableAsync(caller, null, cancellationToken).ConfigureAwait(false)).Select(r => r.EnvironmentId).ToArray();
        // Sem `audit:read` no host do evento: indistinguível de inexistente (FR-010).
        var found = await reader.GetAsync(eventId, readable, at, timeProvider.GetUtcNow() - MaxPeriod, cancellationToken).ConfigureAwait(false);
        if (found is null) return null;
        var related = await reader.RelatedAsync(found, readable, RelatedWindow, MaxRelated, cancellationToken).ConfigureAwait(false);
        return new AuditDetail(found, related);
    }

    /// <summary>Exportação de todo o filtro (até 10.000 eventos), registrada como `audit.exported` em cada host abrangido (FR-009).</summary>
    public async Task<AuditExport> ExportAsync(AuditCaller caller, AuditFilter filter, CancellationToken cancellationToken)
    {
        var (outcome, query) = await QueryAsync(caller, filter, null, MaxExport, cancellationToken).ConfigureAwait(false);
        var (since, until) = Period(filter);
        if (outcome != AuditQueryOutcome.Ok) return new AuditExport(outcome, [], since, until);
        var count = await reader.CountAsync(query!, MaxExport + 1, cancellationToken).ConfigureAwait(false);
        if (count > MaxExport) return new AuditExport(AuditQueryOutcome.ExportTooLarge, [], since, until);
        var events = count == 0 ? [] : (await reader.ListAsync(query!, cancellationToken).ConfigureAwait(false)).Events;
        var details = new Dictionary<string, string>
        {
            ["since"] = since.ToString("O", CultureInfo.InvariantCulture),
            ["until"] = until.ToString("O", CultureInfo.InvariantCulture),
            ["count"] = events.Count.ToString(CultureInfo.InvariantCulture),
        };
        foreach (var environmentId in query!.EnvironmentIds)
        {
            var exported = AuditEvent.Create(Guid.NewGuid(), timeProvider.GetUtcNow(), caller.CorrelationId, AuditActorKind.User, caller.Actor.Subject, "audit.exported", environmentId, AuditOutcome.Succeeded)
                .WithActorName(caller.Actor.DisplayName)
                .WithSubject("audit", details);
            await writer.AppendAsync(exported, cancellationToken).ConfigureAwait(false);
        }

        return new AuditExport(AuditQueryOutcome.Ok, events, since, until);
    }

    private (DateTimeOffset Since, DateTimeOffset Until) Period(AuditFilter filter)
    {
        var until = filter.UntilUtc ?? timeProvider.GetUtcNow();
        return (filter.SinceUtc ?? until - DefaultPeriod, until);
    }

    private async Task<(AuditQueryOutcome Outcome, AuditQuery? Query)> QueryAsync(AuditCaller caller, AuditFilter filter, AuditCursor? after, int limit, CancellationToken cancellationToken)
    {
        var (since, until) = Period(filter);
        if (until <= since) return (AuditQueryOutcome.InvalidPeriod, null);
        if (until - since > MaxPeriod) return (AuditQueryOutcome.PeriodTooLong, null);
        var actions = new HashSet<string>(filter.Actions, StringComparer.Ordinal);
        foreach (var category in filter.Categories)
        {
            if (!AuditCategories.Actions.TryGetValue(category, out var inCategory)) return (AuditQueryOutcome.InvalidFilter, null);
            actions.UnionWith(inCategory);
        }

        if (actions.Any(action => !AuditEvent.Actions.Contains(action))) return (AuditQueryOutcome.InvalidFilter, null);
        var candidates = caller.Readable.Where(r =>
            (filter.Stage is null || r.Stage == filter.Stage) &&
            (filter.EnvironmentIds.Count == 0 || filter.EnvironmentIds.Contains(r.EnvironmentId)));
        var readable = await ReadableAsync(caller, candidates, cancellationToken).ConfigureAwait(false);
        return (AuditQueryOutcome.Ok, new AuditQuery(
            readable.Select(r => r.EnvironmentId).ToArray(), since, until, filter.Actor, actions, filter.Outcomes, filter.Text, after, limit));
    }
}
