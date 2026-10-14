using System.Security.Claims;
using System.Text.Json;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Environments;
using Dokpod.ControlPlane.Application.Events;
using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.ControlPlane.Application.Inventory;
using Dokpod.ControlPlane.Application.Queries;
using Dokpod.Domain.Environments;

namespace Dokpod.ControlPlane.Api.Endpoints;

/// <summary>Detalhe, resumo, inspeção sanitizada e logs sob demanda (spec 011).</summary>
public static class ContainerDetailEndpoints
{
    private const string Base = "/api/v1/environments/{environmentId:guid}/containers/{containerId}";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Base, GetAsync).RequireAuthorization();
        endpoints.MapGet($"{Base}/summary", (Guid environmentId, string containerId, HttpContext context, ContainerInsightsService insights, CancellationToken ct) =>
            InspectAsync(environmentId, containerId, true, context, insights, ct)).RequireAuthorization();
        endpoints.MapGet($"{Base}/inspect", (Guid environmentId, string containerId, HttpContext context, ContainerInsightsService insights, CancellationToken ct) =>
            InspectAsync(environmentId, containerId, false, context, insights, ct)).RequireAuthorization();
        endpoints.MapGet($"{Base}/logs", LogsAsync).RequireAuthorization();
    }

    private static async Task<IResult> GetAsync(
        Guid environmentId,
        string containerId,
        HttpContext context,
        ContainerInsightsService insights,
        IEnvironmentRegistrationStore registrations,
        HostQueryService hostQuery,
        IOperationalEventStore events,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var error)) return error!;
        var access = await insights.AuthorizeAsync(environmentId, containerId, EnvironmentResourceScopes.Read, actor!, token!, CorrelationId(context), cancellationToken);
        if (access.Access != InsightAccess.Allowed) return AccessProblem(context, access);
        var registration = await registrations.GetAsync(environmentId, cancellationToken);
        if (registration is null) return NotFound(context);
        var host = (await hostQuery.DescribeAsync([registration], cancellationToken))[0];
        var restarts = await events.CountRestartsAsync(environmentId, containerId, timeProvider.GetUtcNow().AddHours(-24), cancellationToken);
        var item = HostResponses.ToContainerListItem(new ContainerListItem(host, access.Container!, ContainerSearchService.AllowedActions(host, access.Container!)));
        item["restartsLast24h"] = restarts;
        item["hostName"] = host.Descriptor?.HostName ?? registration.Host;
        item["capabilities"] = host.Descriptor?.Capabilities ?? [];
        return Results.Ok(item);
    }

    private static async Task<IResult> InspectAsync(
        Guid environmentId,
        string containerId,
        bool summary,
        HttpContext context,
        ContainerInsightsService insights,
        CancellationToken cancellationToken)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var error)) return error!;
        var correlationId = CorrelationId(context);
        var access = await insights.AuthorizeAsync(environmentId, containerId, EnvironmentResourceScopes.InspectContainer, actor!, token!, correlationId, cancellationToken);
        if (access.Access != InsightAccess.Allowed) return AccessProblem(context, access);
        var outcome = await insights.InspectAsync(environmentId, containerId, summary, cancellationToken);
        await insights.AuditAsync(
            "container.inspected", environmentId, containerId, actor!, correlationId, outcome.Kind == QueryOutcomeKind.Ok, outcome.FailureCode,
            new Dictionary<string, string> { ["kind"] = summary ? "summary" : "inspect" }, CancellationToken.None);
        if (outcome.Kind != QueryOutcomeKind.Ok) return OutcomeProblem(context, outcome);
        using var document = JsonDocument.Parse(outcome.Json);
        if (summary) return Results.Ok(document.RootElement.Clone());
        return Results.Ok(new
        {
            json = document.RootElement.Clone(),
            truncated = outcome.Truncated,
            redactedPaths = outcome.RedactedPaths,
        });
    }

    private static async Task<IResult> LogsAsync(
        Guid environmentId,
        string containerId,
        int? tail,
        DateTimeOffset? since,
        DateTimeOffset? until,
        string? contains,
        HttpContext context,
        ContainerInsightsService insights,
        CancellationToken cancellationToken)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var error)) return error!;
        if (tail is < 1 or > ContainerInsightsService.MaxTail || contains is { Length: > ContainerInsightsService.MaxContainsLength } ||
            (since is not null && until is not null && until <= since))
        {
            return Problem(context, StatusCodes.Status400BadRequest, "invalid_logs_query", "Os parâmetros da consulta de logs são inválidos.");
        }

        var correlationId = CorrelationId(context);
        var access = await insights.AuthorizeAsync(environmentId, containerId, EnvironmentResourceScopes.ReadContainerLogs, actor!, token!, correlationId, cancellationToken);
        if (access.Access != InsightAccess.Allowed) return AccessProblem(context, access);
        var outcome = await insights.ReadLogsAsync(environmentId, containerId, tail, since, until, string.IsNullOrEmpty(contains) ? null : contains, cancellationToken);
        var bytes = outcome.Lines.Sum(line => (long)line.Text.Length);
        await insights.AuditAsync(
            "container.logs.read", environmentId, containerId, actor!, correlationId, outcome.Kind == QueryOutcomeKind.Ok, outcome.FailureCode,
            new Dictionary<string, string>
            {
                ["tail"] = ContainerInsightsService.Number(tail ?? ContainerInsightsService.DefaultTail),
                ["filtered"] = string.IsNullOrEmpty(contains) ? "false" : "true",
                ["lines"] = ContainerInsightsService.Number(outcome.Lines.Count),
                ["bytes"] = ContainerInsightsService.Number(bytes),
                ["truncated"] = outcome.Truncated ? "true" : "false",
            },
            CancellationToken.None);
        if (outcome.Kind != QueryOutcomeKind.Ok) return OutcomeProblem(context, outcome);
        return Results.Ok(new
        {
            lines = outcome.Lines.Select(LogResponses.ToView),
            truncated = outcome.Truncated,
            omittedLines = 0,
        });
    }

    internal static IResult OutcomeProblem(HttpContext context, QueryOutcome outcome) => outcome.Kind switch
    {
        QueryOutcomeKind.NotFound => NotFound(context),
        QueryOutcomeKind.AgentDisconnected => Problem(context, StatusCodes.Status409Conflict, "agent_disconnected", "O agente do host está desconectado; nenhum dado é servido de cache."),
        QueryOutcomeKind.NotSupported => Problem(context, StatusCodes.Status422UnprocessableEntity, outcome.FailureCode ?? "not_supported", "O agente ou o driver de log não suporta esta consulta."),
        QueryOutcomeKind.TooManyRequests => Problem(context, StatusCodes.Status429TooManyRequests, "too_many_requests", "Muitas consultas simultâneas neste host. Tente novamente."),
        QueryOutcomeKind.Timeout => Problem(context, StatusCodes.Status504GatewayTimeout, "agent_timeout", "O agente não respondeu no prazo."),
        _ => Problem(context, StatusCodes.Status502BadGateway, outcome.FailureCode ?? "query_failed", "O agente não conseguiu concluir a consulta."),
    };

    private static IResult AccessProblem(HttpContext context, InsightAccessResult access) => access.Access switch
    {
        InsightAccess.NotFound => NotFound(context),
        InsightAccess.Unavailable => Problem(context, StatusCodes.Status503ServiceUnavailable, access.FailureCode ?? "authorization_unavailable", "A autorização não respondeu."),
        _ => Problem(context, StatusCodes.Status403Forbidden, access.FailureCode ?? "authorization_denied", "Você não tem permissão para esta consulta."),
    };

    private static IResult NotFound(HttpContext context) =>
        Problem(context, StatusCodes.Status404NotFound, "container_not_found", "O container não foi encontrado.");

    private static bool TryAuthenticate(HttpContext context, out AuthenticatedActor? actor, out string? token, out IResult? error)
    {
        var subject = context.User.FindFirstValue("sub");
        var authorization = context.Request.Headers.Authorization.ToString();
        token = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? authorization["Bearer ".Length..].Trim() : null;
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(token))
        {
            actor = null;
            error = Problem(context, StatusCodes.Status401Unauthorized, "authentication_required", "A autenticação do usuário é obrigatória.");
            return false;
        }

        actor = AuthenticatedActor.FromSubject(subject);
        error = null;
        return true;
    }

    private static Guid CorrelationId(HttpContext context) =>
        Guid.TryParse(context.Request.Headers["X-Correlation-Id"], out var id) ? id : Guid.NewGuid();

    private static IResult Problem(HttpContext context, int status, string code, string detail) =>
        Results.Problem(
            statusCode: status,
            title: status switch
            {
                StatusCodes.Status401Unauthorized => "Autenticação necessária",
                StatusCodes.Status403Forbidden => "Acesso negado",
                StatusCodes.Status404NotFound => "Container não encontrado",
                StatusCodes.Status409Conflict => "Agente desconectado",
                StatusCodes.Status429TooManyRequests => "Muitas requisições",
                StatusCodes.Status503ServiceUnavailable => "Dependência indisponível",
                StatusCodes.Status504GatewayTimeout => "Tempo esgotado",
                _ => "Requisição inválida",
            },
            detail: detail,
            instance: context.Request.Path,
            extensions: new Dictionary<string, object?> { ["code"] = code, ["traceId"] = context.TraceIdentifier });
}

internal static class LogResponses
{
    public static object ToView(Dokpod.Agent.Contracts.V1.LogLine line) => new
    {
        timestamp = line.Timestamp?.ToDateTimeOffset(),
        stream = line.Stream == Dokpod.Agent.Contracts.V1.LogStream.Stderr ? "stderr" : "stdout",
        level = string.IsNullOrEmpty(line.Level) ? null : line.Level,
        text = line.Text,
    };
}
