using System.Globalization;
using System.Security.Claims;
using System.Text;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Events;
using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.ControlPlane.Application.Inventory;
using Dokpod.Domain.Inventory;
using Dokpod.ControlPlane.Application.Overview;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Events;
using Microsoft.AspNetCore.WebUtilities;

namespace Dokpod.ControlPlane.Api.Endpoints;

public sealed record SetEnvironmentStageRequest(string? Stage);

/// <summary>Visão geral, eventos operacionais e estágio (spec 008).</summary>
public static class ConsoleEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/overview", GetOverviewAsync).RequireAuthorization();
        endpoints.MapGet("/api/v1/events", ListEventsAsync).RequireAuthorization();
        endpoints.MapPut("/api/v1/environments/{environmentId:guid}/stage", SetStageAsync).RequireAuthorization();
        endpoints.MapGet("/api/v1/containers", ListContainersAsync).RequireAuthorization();
    }

    private static async Task<IResult> ListContainersAsync(
        HttpContext context,
        AuthorizedEnvironmentsService authorizedEnvironments,
        HostQueryService hostQuery,
        ContainerSearchService search,
        Dokpod.ControlPlane.Application.Metrics.MetricsQueryService metrics,
        CancellationToken cancellationToken)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return unauthorized!;
        var query = context.Request.Query;
        EnvironmentStage? stage = null;
        if (!string.IsNullOrEmpty(query["stage"]))
        {
            if (!EnvironmentStages.TryParse(query["stage"], out var parsedStage)) return InvalidContainerQuery(context);
            stage = parsedStage;
        }

        var environmentIds = new List<Guid>();
        foreach (var value in query["environmentId"])
        {
            if (!Guid.TryParse(value, out var id)) return InvalidContainerQuery(context);
            environmentIds.Add(id);
        }

        var engines = query["engine"].Where(v => !string.IsNullOrEmpty(v)).Select(v => v!).ToArray();
        var statuses = query["displayStatus"].Where(v => !string.IsNullOrEmpty(v)).Select(v => v!).ToArray();
        var text = query["q"].ToString();
        var limit = 50;
        if (engines.Any(e => e is not ("docker" or "podman")) ||
            statuses.Any(s => !ContainerDisplayStatuses.TryParse(s, out _)) ||
            (text.Length > 0 && (text.Length < 2 || text.Length > 128)) ||
            (!string.IsNullOrEmpty(query["limit"]) && (!int.TryParse(query["limit"], out limit) || limit is < 1 or > 100)) ||
            !TryParseSort(query["sort"], out var sort) ||
            !TryDecodeContainerCursor(query["cursor"], out var after))
        {
            return InvalidContainerQuery(context);
        }

        var authorized = await LoadAuthorizedAsync(context, authorizedEnvironments, actor!, token!, cancellationToken);
        if (authorized.Error is not null) return authorized.Error;
        var hosts = await hostQuery.DescribeAsync(authorized.Registrations, cancellationToken);
        var result = await search.SearchAsync(
            hosts,
            new ContainerListFilter(stage, environmentIds, engines, statuses, text.Length == 0 ? null : text, sort, after, limit),
            cancellationToken);
        var justificationStore = context.RequestServices.GetService<Dokpod.ControlPlane.Application.Alerts.IActionJustificationStore>();
        var intentional = justificationStore is null
            ? new Dictionary<(Guid EnvironmentId, string ContainerId), Dokpod.ControlPlane.Application.Alerts.ContainerActionJustification>()
            : await justificationStore.GetCurrentAsync(
                result.Items.Where(i => i.Container.DisplayStatus is ContainerDisplayStatus.Stopped or ContainerDisplayStatus.Paused)
                    .Select(i => (i.Host.Registration.EnvironmentId, i.Container.ContainerId)).ToArray(),
                cancellationToken);
        return Results.Ok(new
        {
            containers = result.Items.Select(item => HostResponses.WithIntentional(
                HostResponses.WithResources(
                    HostResponses.ToContainerListItem(item),
                    item.Container.DisplayStatus,
                    metrics.GetContainerResources(item.Host.Registration.EnvironmentId, item.Container.ContainerId)),
                item.Container.DisplayStatus,
                intentional.GetValueOrDefault((item.Host.Registration.EnvironmentId, item.Container.ContainerId)))),
            summary = new { total = result.Summary.Values.Sum(), byDisplayStatus = result.Summary },
            observedAt = result.ObservedAtUtc,
            nextCursor = EncodeContainerCursor(result.Next),
        });
    }

    private static bool TryParseSort(string? value, out ContainerSort sort)
    {
        sort = value switch
        {
            null or "" or "name" => ContainerSort.NameAsc,
            "-name" => ContainerSort.NameDesc,
            "displayStatus" => ContainerSort.DisplayStatus,
            "environment" => ContainerSort.Environment,
            "createdAt" => ContainerSort.CreatedAtAsc,
            "-createdAt" => ContainerSort.CreatedAtDesc,
            "cpu" => ContainerSort.CpuAsc,
            "-cpu" => ContainerSort.CpuDesc,
            "memory" => ContainerSort.MemoryAsc,
            "-memory" => ContainerSort.MemoryDesc,
            _ => (ContainerSort)(-1),
        };
        return Enum.IsDefined(sort);
    }

    private static bool TryDecodeContainerCursor(string? cursor, out ContainerSearchCursor? after)
    {
        after = null;
        if (string.IsNullOrWhiteSpace(cursor)) return true;
        try
        {
            var parts = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(cursor)).Split('\n');
            if (parts.Length != 3 || !Guid.TryParseExact(parts[1], "D", out var environmentId) || parts[2].Length is 0 or > 128 || parts[0].Length > 600)
            {
                return false;
            }

            after = new ContainerSearchCursor(parts[0], environmentId, parts[2]);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string? EncodeContainerCursor(ContainerSearchCursor? cursor) =>
        cursor is null
            ? null
            : WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes($"{cursor.SortKey}\n{cursor.EnvironmentId:D}\n{cursor.ContainerId}"));

    private static IResult InvalidContainerQuery(HttpContext context) =>
        Problem(context, StatusCodes.Status400BadRequest, "invalid_container_query", "Os parâmetros da consulta são inválidos.");

    private static async Task<IResult> GetOverviewAsync(
        string? stage,
        HttpContext context,
        AuthorizedEnvironmentsService authorizedEnvironments,
        HostQueryService hostQuery,
        OverviewQueryService overview,
        Dokpod.ControlPlane.Application.Images.IImageInventoryStore imageStore,
        CancellationToken cancellationToken)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return unauthorized!;
        EnvironmentStage? parsedStage = null;
        if (!string.IsNullOrEmpty(stage))
        {
            if (!EnvironmentStages.TryParse(stage, out var value))
            {
                return Problem(context, StatusCodes.Status400BadRequest, "invalid_overview_query", "O estágio informado é inválido.");
            }

            parsedStage = value;
        }

        var authorized = await LoadAuthorizedAsync(context, authorizedEnvironments, actor!, token!, cancellationToken);
        if (authorized.Error is not null) return authorized.Error;
        var hosts = await hostQuery.DescribeAsync(authorized.Registrations, cancellationToken);
        var summary = await overview.SummarizeAsync(parsedStage, hosts, cancellationToken);
        var imageHosts = hosts
            .Where(h => h.Registration.Enabled && (parsedStage is null || h.Registration.Stage == parsedStage))
            .Select(h => h.Registration.EnvironmentId)
            .ToArray();
        Dokpod.ControlPlane.Application.Images.ImageSummary images;
        try
        {
            images = await imageStore.SummarizeAsync(imageHosts, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            images = new Dokpod.ControlPlane.Application.Images.ImageSummary(0, 0, 0, true);
        }

        return Results.Ok(new
        {
            stage = summary.Stage is null ? null : EnvironmentStages.ToValue(summary.Stage.Value),
            observedAt = summary.ObservedAtUtc,
            hosts = new { total = summary.HostsTotal, bySituation = summary.HostsBySituation },
            containers = new { total = summary.ContainersTotal, byDisplayStatus = summary.ContainersByDisplayStatus },
            engines = summary.Engines.Select(engine => new
            {
                engine = engine.Engine,
                hosts = engine.Hosts,
                containers = engine.Containers,
                versions = engine.Versions.Select(v => new { version = v.Version, hosts = v.Hosts }),
            }),
            attentionEvents24h = summary.AttentionEvents24h,
            images = new { total = images.Total, unused = images.Unused, partial = images.Partial },
        });
    }

    private static async Task<IResult> ListEventsAsync(
        string? stage,
        Guid? environmentId,
        string? severity,
        DateTimeOffset? since,
        DateTimeOffset? until,
        string? cursor,
        int? limit,
        HttpContext context,
        AuthorizedEnvironmentsService authorizedEnvironments,
        IOperationalEventStore eventStore,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return unauthorized!;
        var pageLimit = limit ?? 50;
        EnvironmentStage? parsedStage = null;
        if (!string.IsNullOrEmpty(stage))
        {
            if (!EnvironmentStages.TryParse(stage, out var value)) return InvalidEventQuery(context);
            parsedStage = value;
        }

        if (pageLimit is < 1 or > 100 ||
            (severity is not null && !OperationalEventSeverities.All.Contains(severity)) ||
            !TryDecodeEventCursor(cursor, out var after))
        {
            return InvalidEventQuery(context);
        }

        var authorized = await LoadAuthorizedAsync(context, authorizedEnvironments, actor!, token!, cancellationToken);
        if (authorized.Error is not null) return authorized.Error;
        var scope = authorized.Registrations
            .Where(r => r.Enabled && (parsedStage is null || r.Stage == parsedStage) &&
                (environmentId is null || r.EnvironmentId == environmentId))
            .ToDictionary(r => r.EnvironmentId);
        if (environmentId is not null && scope.Count == 0)
        {
            return Problem(context, StatusCodes.Status403Forbidden, "authorization_denied", "Os eventos do ambiente não foram autorizados.");
        }

        if (scope.Count == 0)
        {
            return Results.Ok(new { events = Array.Empty<object>(), nextCursor = (string?)null });
        }

        var page = await eventStore.ListAsync(
            new OperationalEventQuery(
                scope.Keys.ToArray(),
                severity,
                (since ?? timeProvider.GetUtcNow().AddHours(-24)).ToUniversalTime(),
                until?.ToUniversalTime(),
                after,
                pageLimit),
            cancellationToken);
        return Results.Ok(new
        {
            events = page.Events.Select(item => new
            {
                eventId = item.EventId,
                environmentId = item.EnvironmentId,
                environmentName = scope.TryGetValue(item.EnvironmentId, out var registration) ? registration.Name : null,
                type = item.Type,
                severity = item.Severity,
                occurredAt = item.OccurredAtUtc,
                receivedAt = item.ReceivedAtUtc,
                clockSkewDetected = item.ClockSkewDetected,
                subjectKind = item.SubjectKind,
                subjectId = item.SubjectId,
                subjectName = item.SubjectName,
                parameters = item.Parameters,
                aggregatedCount = item.AggregatedCount,
            }),
            nextCursor = EncodeEventCursor(page.Next),
        });
    }

    private static async Task<IResult> SetStageAsync(
        Guid environmentId,
        SetEnvironmentStageRequest request,
        HttpContext context,
        EnvironmentStageService stageService,
        CancellationToken cancellationToken)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return unauthorized!;
        EnvironmentStage? stage = null;
        if (request.Stage is not null)
        {
            if (!EnvironmentStages.TryParse(request.Stage, out var value))
            {
                return Problem(context, StatusCodes.Status400BadRequest, "invalid_stage", "O estágio informado é inválido.");
            }

            stage = value;
        }

        StageChangeResult result;
        try
        {
            result = await stageService.ChangeAsync(environmentId, stage, actor!, token!, GetCorrelationId(context), cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return Problem(context, StatusCodes.Status503ServiceUnavailable, "persistence_unavailable", "O cadastro do ambiente está indisponível.");
        }

        return result.Outcome switch
        {
            StageChangeOutcome.Changed or StageChangeOutcome.Unchanged => Results.Ok(new
            {
                environmentId,
                stage = stage is null ? null : EnvironmentStages.ToValue(stage.Value),
                changed = result.Outcome == StageChangeOutcome.Changed,
            }),
            StageChangeOutcome.NotFound => Problem(context, StatusCodes.Status404NotFound, "environment_not_found", "O ambiente não foi encontrado."),
            StageChangeOutcome.Unavailable => Problem(context, StatusCodes.Status503ServiceUnavailable, result.FailureCode ?? "authorization_unavailable", "A alteração de estágio não pôde ser autorizada."),
            _ => Problem(context, StatusCodes.Status403Forbidden, result.FailureCode ?? "authorization_denied", "A alteração de estágio não foi autorizada."),
        };
    }

    internal static async Task<(IReadOnlyList<EnvironmentRegistration> Registrations, IResult? Error)> LoadAuthorizedAsync(
        HttpContext context,
        AuthorizedEnvironmentsService authorizedEnvironments,
        AuthenticatedActor actor,
        string token,
        CancellationToken cancellationToken)
    {
        try
        {
            var authorized = await authorizedEnvironments.ListAllAsync(actor, token, GetCorrelationId(context), cancellationToken);
            return authorized.Available
                ? (authorized.Registrations, null)
                : ([], Problem(context, StatusCodes.Status503ServiceUnavailable, authorized.FailureCode ?? "authorization_unavailable", "As permissões não puderam ser avaliadas."));
        }
        catch (InvalidOperationException)
        {
            return ([], Problem(context, StatusCodes.Status503ServiceUnavailable, "persistence_unavailable", "O catálogo de ambientes está indisponível."));
        }
    }

    internal static bool TryAuthenticate(HttpContext context, out AuthenticatedActor? actor, out string? token, out IResult? error)
    {
        var subject = context.User.FindFirstValue("sub");
        var authorization = context.Request.Headers.Authorization.ToString();
        token = !string.IsNullOrWhiteSpace(authorization) && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization["Bearer ".Length..].Trim()
            : null;
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(token))
        {
            actor = null;
            error = Problem(context, StatusCodes.Status401Unauthorized, "authentication_required", "A autenticação do usuário é obrigatória.");
            return false;
        }

        actor = AuthenticatedActor.FromSubject(subject) with { DisplayName = context.User.FindFirstValue("preferred_username") };
        error = null;
        return true;
    }

    private static IResult InvalidEventQuery(HttpContext context) =>
        Problem(context, StatusCodes.Status400BadRequest, "invalid_event_query", "Os parâmetros da consulta são inválidos.");

    private static bool TryDecodeEventCursor(string? cursor, out OperationalEventCursor? after)
    {
        after = null;
        if (string.IsNullOrWhiteSpace(cursor)) return true;
        try
        {
            var parts = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(cursor)).Split('|');
            if (parts.Length != 2 ||
                !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) ||
                !Guid.TryParseExact(parts[1], "D", out var eventId))
            {
                return false;
            }

            after = new OperationalEventCursor(new DateTimeOffset(ticks, TimeSpan.Zero), eventId);
            return true;
        }
        catch (Exception exception) when (exception is FormatException or ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static string? EncodeEventCursor(OperationalEventCursor? cursor) =>
        cursor is null
            ? null
            : WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(
                $"{cursor.OccurredAtUtc.UtcTicks.ToString(CultureInfo.InvariantCulture)}|{cursor.EventId:D}"));

    internal static Guid GetCorrelationId(HttpContext context) =>
        Guid.TryParse(context.Request.Headers["X-Correlation-Id"], out var correlationId) ? correlationId : Guid.NewGuid();

    internal static IResult Problem(HttpContext context, int status, string code, string detail) =>
        Results.Problem(
            statusCode: status,
            title: status switch
            {
                StatusCodes.Status401Unauthorized => "Autenticação necessária",
                StatusCodes.Status403Forbidden => "Acesso negado",
                StatusCodes.Status404NotFound => "Ambiente não encontrado",
                StatusCodes.Status503ServiceUnavailable => "Dependência indisponível",
                _ => "Requisição inválida",
            },
            detail: detail,
            instance: context.Request.Path,
            extensions: new Dictionary<string, object?> { ["code"] = code, ["traceId"] = context.TraceIdentifier });
}
