using System.Globalization;
using Dokpod.Agent.Contracts.V1;
using Dokpod.ControlPlane.Application.Auditing;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Inventory;
using Dokpod.Domain.Auditing;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Inventory;

namespace Dokpod.ControlPlane.Application.Queries;

public enum InsightAccess
{
    Allowed,
    NotFound,
    Denied,
    Unavailable,
}

public sealed record InsightAccessResult(InsightAccess Access, ContainerInventory? Container, string? FailureCode);

/// <summary>
/// Detalhe, resumo, inspeção e logs sob demanda (spec 011): autorização por scope a cada chamada,
/// 404 indistinguível para container inexistente ou ambiente não autorizado, e auditoria só com metadados.
/// </summary>
public sealed class ContainerInsightsService(
    IEnvironmentAuthorizationDecider authorizationDecider,
    IContainerSearchStore containers,
    IAuditEventWriter auditWriter,
    AgentQueryDispatcher dispatcher,
    TimeProvider timeProvider)
{
    public const int MaxContainsLength = 128;
    public const int DefaultTail = 500;
    public const int MaxTail = 5000;

    public async Task<InsightAccessResult> AuthorizeAsync(
        Guid environmentId,
        string containerId,
        string scope,
        AuthenticatedActor actor,
        string accessToken,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        if (environmentId == Guid.Empty || string.IsNullOrWhiteSpace(containerId) || containerId.Length > 128)
        {
            return new InsightAccessResult(InsightAccess.NotFound, null, "container_not_found");
        }

        var read = await DecideAsync(environmentId, EnvironmentResourceScopes.Read, actor, accessToken, correlationId, cancellationToken).ConfigureAwait(false);
        if (read.Outcome == AuthorizationDecisionOutcome.Indeterminate) return new InsightAccessResult(InsightAccess.Unavailable, null, read.FailureCode);
        // Sem leitura no ambiente: responde como inexistente para não revelar o container (US1-4).
        if (!read.Allowed) return new InsightAccessResult(InsightAccess.NotFound, null, "container_not_found");

        var container = await containers.GetAsync(environmentId, containerId, cancellationToken).ConfigureAwait(false);
        if (container is null) return new InsightAccessResult(InsightAccess.NotFound, null, "container_not_found");
        if (scope == EnvironmentResourceScopes.Read) return new InsightAccessResult(InsightAccess.Allowed, container, null);

        var specific = await DecideAsync(environmentId, scope, actor, accessToken, correlationId, cancellationToken).ConfigureAwait(false);
        return specific.Allowed
            ? new InsightAccessResult(InsightAccess.Allowed, container, null)
            : new InsightAccessResult(
                specific.Outcome == AuthorizationDecisionOutcome.Indeterminate ? InsightAccess.Unavailable : InsightAccess.Denied,
                null,
                specific.FailureCode ?? "authorization_denied");
    }

    public Task<QueryOutcome> InspectAsync(Guid environmentId, string containerId, bool summary, CancellationToken cancellationToken) =>
        dispatcher.QueryAsync(environmentId, summary ? ContainerQueryKind.Summary : ContainerQueryKind.Inspect, containerId, null, cancellationToken);

    public Task<QueryOutcome> ReadLogsAsync(
        Guid environmentId,
        string containerId,
        int? tail,
        DateTimeOffset? since,
        DateTimeOffset? until,
        string? contains,
        CancellationToken cancellationToken)
    {
        var query = new LogQuery
        {
            Tail = (uint)Math.Clamp(tail ?? DefaultTail, 1, MaxTail),
            Contains = contains ?? string.Empty,
            MaxBytes = AgentQueryDispatcher.MaxResponseBytes,
        };
        if (since is not null) query.Since = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(since.Value);
        if (until is not null) query.Until = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(until.Value);
        return dispatcher.QueryAsync(environmentId, ContainerQueryKind.Logs, containerId, query, cancellationToken);
    }

    public Task AuditAsync(
        string action,
        Guid environmentId,
        string containerId,
        AuthenticatedActor actor,
        Guid correlationId,
        bool succeeded,
        string? failureCode,
        IReadOnlyDictionary<string, string> details,
        CancellationToken cancellationToken)
    {
        var auditEvent = AuditEvent.Create(
            Guid.NewGuid(),
            timeProvider.GetUtcNow(),
            correlationId,
            AuditActorKind.User,
            actor.Subject,
            action,
            environmentId,
            succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            succeeded ? null : failureCode ?? "query_failed").WithSubject(containerId, details);
        return auditWriter.AppendAsync(auditEvent, cancellationToken);
    }

    public static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    private async Task<EnvironmentAuthorizationDecision> DecideAsync(
        Guid environmentId,
        string scope,
        AuthenticatedActor actor,
        string accessToken,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await authorizationDecider.DecideAsync(
                $"urn:dokpod:environment:{environmentId:D}", scope, actor, accessToken, correlationId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return new EnvironmentAuthorizationDecision(AuthorizationDecisionOutcome.Indeterminate, "authorization_unavailable");
        }
    }
}
