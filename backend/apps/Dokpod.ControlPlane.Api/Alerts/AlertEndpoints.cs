using System.Globalization;
using System.Text;
using Dokpod.ControlPlane.Application.Alerts;
using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.Domain.Alerts;
using Dokpod.Domain.Environments;
using Microsoft.AspNetCore.WebUtilities;
using static Dokpod.ControlPlane.Api.Endpoints.ConsoleEndpoints;

namespace Dokpod.ControlPlane.Api.Endpoints;

public sealed record AlertCommentRequest(string? Comment);

public sealed record AlertSilenceRequest(DateTimeOffset Until, string? Justification);

public sealed record AlertScopeRequest(string? Stage, Guid? EnvironmentId, string? ContainerPattern);

public sealed record AlertRuleRequest(
    string Name,
    string? Description,
    string ConditionKind,
    string? EventType,
    string? Metric,
    double? ThresholdPercent,
    int DurationSeconds,
    int? MinOccurrences,
    string Severity,
    AlertScopeRequest? Scope,
    bool Enabled,
    int? Version);

public sealed record CreateSilenceRequest(AlertScopeRequest? Scope, DateTimeOffset? StartsAt, DateTimeOffset EndsAt, string? Justification);

public sealed record JustificationPolicyRequest(bool Required);

/// <summary>Governança de alertas e justificativas (spec 016, contracts/alerts-api.md).</summary>
public static class AlertEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/alerts/summary", SummaryAsync).RequireAuthorization();
        endpoints.MapGet("/api/v1/alerts", ListAsync).RequireAuthorization();
        endpoints.MapGet("/api/v1/alerts/{alertId:guid}", GetAsync).RequireAuthorization();
        endpoints.MapPost("/api/v1/alerts/{alertId:guid}/acknowledge", (Guid alertId, AlertCommentRequest body, HttpContext context, AuthorizedEnvironmentsService authorized, AlertGovernanceService service, CancellationToken ct) =>
            ActAsync(context, authorized, (caller) => service.AcknowledgeAsync(caller, alertId, body.Comment, ct), ct)).RequireAuthorization();
        endpoints.MapPost("/api/v1/alerts/{alertId:guid}/resolve", (Guid alertId, AlertCommentRequest body, HttpContext context, AuthorizedEnvironmentsService authorized, AlertGovernanceService service, CancellationToken ct) =>
            ActAsync(context, authorized, (caller) => service.ResolveAsync(caller, alertId, body.Comment, ct), ct)).RequireAuthorization();
        endpoints.MapPost("/api/v1/alerts/{alertId:guid}/silence", (Guid alertId, AlertSilenceRequest body, HttpContext context, AuthorizedEnvironmentsService authorized, AlertGovernanceService service, CancellationToken ct) =>
            ActAsync(context, authorized, (caller) => service.SilenceAlertAsync(caller, alertId, body.Until, body.Justification, ct), ct)).RequireAuthorization();

        endpoints.MapGet("/api/v1/alert-rules", ListRulesAsync).RequireAuthorization();
        endpoints.MapPost("/api/v1/alert-rules", (AlertRuleRequest body, HttpContext context, AuthorizedEnvironmentsService authorized, AlertGovernanceService service, CancellationToken ct) =>
            SaveRuleAsync(Guid.NewGuid(), body, null, context, authorized, service, ct)).RequireAuthorization();
        endpoints.MapPut("/api/v1/alert-rules/{ruleId:guid}", (Guid ruleId, AlertRuleRequest body, HttpContext context, AuthorizedEnvironmentsService authorized, AlertGovernanceService service, CancellationToken ct) =>
            body.Version is { } version
                ? SaveRuleAsync(ruleId, body, version, context, authorized, service, ct)
                : Task.FromResult(Problem(context, StatusCodes.Status400BadRequest, "version", "Informe a versão da regra."))).RequireAuthorization();
        endpoints.MapDelete("/api/v1/alert-rules/{ruleId:guid}", (Guid ruleId, HttpContext context, AuthorizedEnvironmentsService authorized, AlertGovernanceService service, CancellationToken ct) =>
            ActAsync(context, authorized, (caller) => service.DeleteRuleAsync(caller, ruleId, ct), ct)).RequireAuthorization();

        endpoints.MapGet("/api/v1/alert-silences", ListSilencesAsync).RequireAuthorization();
        endpoints.MapPost("/api/v1/alert-silences", CreateSilenceAsync).RequireAuthorization();
        endpoints.MapPost("/api/v1/alert-silences/{silenceId:guid}/end", (Guid silenceId, HttpContext context, AuthorizedEnvironmentsService authorized, AlertGovernanceService service, CancellationToken ct) =>
            ActAsync(context, authorized, (caller) => service.EndSilenceAsync(caller, silenceId, ct), ct)).RequireAuthorization();

        endpoints.MapGet("/api/v1/action-justification-policies", PoliciesAsync).RequireAuthorization();
        endpoints.MapPut("/api/v1/action-justification-policies/{stage}", SetPolicyAsync).RequireAuthorization();
        endpoints.MapGet("/api/v1/environments/{environmentId:guid}/containers/{containerId}/justifications", ContainerJustificationsAsync).RequireAuthorization();
        endpoints.MapGet("/api/v1/environments/{environmentId:guid}/justifications", EnvironmentJustificationsAsync).RequireAuthorization();
    }

    private static async Task<(AlertCaller? Caller, IResult? Error)> CallerAsync(HttpContext context, AuthorizedEnvironmentsService authorized, CancellationToken ct)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return (null, unauthorized);
        var loaded = await LoadAuthorizedAsync(context, authorized, actor!, token!, ct);
        return loaded.Error is not null ? (null, loaded.Error) : (new AlertCaller(actor!, token!, GetCorrelationId(context), loaded.Registrations), null);
    }

    private static async Task<IResult> ActAsync(HttpContext context, AuthorizedEnvironmentsService authorized, Func<AlertCaller, Task<GovernanceResult>> act, CancellationToken ct)
    {
        var (caller, error) = await CallerAsync(context, authorized, ct);
        if (error is not null) return error;
        return ToResult(context, await act(caller!));
    }

    private static IResult ToResult(HttpContext context, GovernanceResult result) => result.Outcome switch
    {
        GovernanceOutcome.Ok => result.Id is { } id ? Results.Ok(new { id }) : Results.NoContent(),
        GovernanceOutcome.NotFound => Problem(context, StatusCodes.Status404NotFound, "not_found", "Recurso não encontrado."),
        GovernanceOutcome.Forbidden => Problem(context, StatusCodes.Status403Forbidden, result.Code ?? "authorization_denied", "Você não tem permissão para esta ação."),
        GovernanceOutcome.Conflict => Problem(context, StatusCodes.Status409Conflict, result.Code ?? "conflict", "O recurso mudou; atualize e tente de novo."),
        GovernanceOutcome.Invalid => Problem(context, StatusCodes.Status400BadRequest, result.Code ?? "invalid_request", "Dados inválidos."),
        _ => Problem(context, StatusCodes.Status503ServiceUnavailable, "authorization_unavailable", "A autorização está indisponível."),
    };

    private static async Task<IResult> SummaryAsync(HttpContext context, AuthorizedEnvironmentsService authorized, AlertGovernanceService service, CancellationToken ct)
    {
        var (caller, error) = await CallerAsync(context, authorized, ct);
        if (error is not null) return error;
        if (!TryStage(context.Request.Query["stage"], out var stage)) return Problem(context, StatusCodes.Status400BadRequest, "stage", "Estágio inválido.");
        var summary = await service.SummarizeAsync(caller!, stage, ct);
        return Results.Ok(new { open = summary.Open, bySeverity = summary.BySeverity, recent = summary.Recent.Select(a => ToAlert(a, false)) });
    }

    private static async Task<IResult> ListAsync(HttpContext context, AuthorizedEnvironmentsService authorized, AlertGovernanceService service, CancellationToken ct)
    {
        var (caller, error) = await CallerAsync(context, authorized, ct);
        if (error is not null) return error;
        var query = context.Request.Query;
        var history = query["view"].ToString() == "history";
        var severities = new List<AlertSeverity>();
        var states = new List<AlertState>();
        var environments = new List<Guid>();
        var limit = 50;
        foreach (var value in query["severity"]) { if (!AlertValues.TryParseSeverity(value, out var s)) return Invalid(context); severities.Add(s); }
        foreach (var value in query["state"]) { if (!AlertValues.TryParseState(value, out var s)) return Invalid(context); states.Add(s); }
        foreach (var value in query["environmentId"]) { if (!Guid.TryParse(value, out var id)) return Invalid(context); environments.Add(id); }
        if (!TryStage(query["stage"], out var stage) ||
            (!string.IsNullOrEmpty(query["limit"]) && (!int.TryParse(query["limit"], out limit) || limit is < 1 or > 100)) ||
            !TryDate(query["since"], out var since) || !TryDate(query["until"], out var until) ||
            !TryCursor(query["cursor"], out var cursor) ||
            (!string.IsNullOrEmpty(query["ruleId"]) && !Guid.TryParse(query["ruleId"], out _)))
        {
            return Invalid(context);
        }

        var text = query["q"].ToString();
        if (text.Length > 128) return Invalid(context);
        Guid? ruleId = Guid.TryParse(query["ruleId"], out var parsedRule) ? parsedRule : null;
        if (history && since is null) since = DateTimeOffset.UtcNow.AddDays(-90);
        var (page, manageable) = await service.ListAsync(caller!, stage, new AlertQuery(environments, history, severities, states, text.Length == 0 ? null : text, ruleId, since, until, cursor, limit), ct);
        return Results.Ok(new
        {
            alerts = page.Alerts.Select(a => ToAlert(a, manageable.Contains(a.EnvironmentId))),
            nextCursor = page.Next is { } next ? WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes($"{next.At.UtcTicks}\n{next.AlertId:D}")) : null,
        });
    }

    private static async Task<IResult> GetAsync(Guid alertId, HttpContext context, AuthorizedEnvironmentsService authorized, AlertGovernanceService service, CancellationToken ct)
    {
        var (caller, error) = await CallerAsync(context, authorized, ct);
        if (error is not null) return error;
        var detail = await service.GetAsync(caller!, alertId, ct);
        if (detail is null) return Problem(context, StatusCodes.Status404NotFound, "not_found", "Alerta não encontrado.");
        return Results.Ok(new
        {
            alert = ToAlert(detail.Alert, detail.CanManage),
            rule = detail.Rule is null ? null : ToRule(detail.Rule),
            timeline = detail.Timeline.Select(t => new { at = t.OccurredAtUtc, kind = t.Kind, actor = t.Actor, comment = t.Comment }),
        });
    }

    private static async Task<IResult> ListRulesAsync(HttpContext context, AuthorizedEnvironmentsService authorized, AlertGovernanceService service, CancellationToken ct)
    {
        var (caller, error) = await CallerAsync(context, authorized, ct);
        if (error is not null) return error;
        var (rules, canManage) = await service.ListRulesAsync(caller!, ct);
        return Results.Ok(new { rules = rules.Select(ToRule), canManage });
    }

    private static async Task<IResult> SaveRuleAsync(Guid ruleId, AlertRuleRequest body, int? version, HttpContext context, AuthorizedEnvironmentsService authorized, AlertGovernanceService service, CancellationToken ct)
    {
        var (caller, error) = await CallerAsync(context, authorized, ct);
        if (error is not null) return error;
        if (!TryRule(ruleId, body, out var rule, out var field)) return Problem(context, StatusCodes.Status400BadRequest, field!, "Regra inválida.");
        return ToResult(context, await service.SaveRuleAsync(caller!, rule!, version, ct));
    }

    private static async Task<IResult> ListSilencesAsync(HttpContext context, AuthorizedEnvironmentsService authorized, AlertGovernanceService service, CancellationToken ct)
    {
        var (caller, error) = await CallerAsync(context, authorized, ct);
        if (error is not null) return error;
        var silences = await service.ListSilencesAsync(caller!, context.Request.Query["active"].ToString() != "false", ct);
        return Results.Ok(new
        {
            silences = silences.Select(s => new
            {
                id = s.SilenceId,
                scope = ToScope(s.Scope),
                alertId = s.AlertId,
                startsAt = s.StartsAtUtc,
                endsAt = s.EndsAtUtc,
                endedAt = s.EndedAtUtc,
                justification = s.Justification,
                createdBy = s.CreatedBy,
            }),
        });
    }

    private static async Task<IResult> CreateSilenceAsync(CreateSilenceRequest body, HttpContext context, AuthorizedEnvironmentsService authorized, AlertGovernanceService service, CancellationToken ct)
    {
        var (caller, error) = await CallerAsync(context, authorized, ct);
        if (error is not null) return error;
        if (!TryScope(body.Scope, out var scope)) return Problem(context, StatusCodes.Status400BadRequest, "scope", "Abrangência inválida.");
        return ToResult(context, await service.CreateSilenceAsync(caller!, scope!, body.StartsAt ?? DateTimeOffset.UtcNow, body.EndsAt, body.Justification, ct));
    }

    private static async Task<IResult> PoliciesAsync(HttpContext context, AlertGovernanceService service, CancellationToken ct)
    {
        if (!TryAuthenticate(context, out _, out _, out var unauthorized)) return unauthorized!;
        var policies = await service.GetPoliciesAsync(ct);
        return Results.Ok(new { policies = policies.Select(p => new { stage = EnvironmentStages.ToValue(p.Key), required = p.Value, actions = new[] { "stop", "pause", "delete" } }) });
    }

    private static async Task<IResult> SetPolicyAsync(string stage, JustificationPolicyRequest body, HttpContext context, AuthorizedEnvironmentsService authorized, AlertGovernanceService service, CancellationToken ct)
    {
        if (!EnvironmentStages.TryParse(stage, out var parsed)) return Problem(context, StatusCodes.Status400BadRequest, "stage", "Estágio inválido.");
        return await ActAsync(context, authorized, caller => service.SetPolicyAsync(caller, parsed, body.Required, ct), ct);
    }

    private static async Task<IResult> ContainerJustificationsAsync(Guid environmentId, string containerId, HttpContext context, AuthorizedEnvironmentsService authorized, [Microsoft.AspNetCore.Mvc.FromServices] IActionJustificationStore store, CancellationToken ct)
    {
        var (caller, error) = await CallerAsync(context, authorized, ct);
        if (error is not null) return error;
        if (containerId.Length is 0 or > 128 || caller!.Readable.All(r => r.EnvironmentId != environmentId)) return Problem(context, StatusCodes.Status404NotFound, "not_found", "Container não encontrado.");
        var history = await store.ListAsync(environmentId, containerId, null, null, null, 100, ct);
        var current = (await store.GetCurrentAsync([(environmentId, containerId)], ct)).Values.FirstOrDefault();
        return Results.Ok(new { current = current is null ? null : ToJustification(current), history = history.Select(ToJustification) });
    }

    private static async Task<IResult> EnvironmentJustificationsAsync(Guid environmentId, HttpContext context, AuthorizedEnvironmentsService authorized, [Microsoft.AspNetCore.Mvc.FromServices] IActionJustificationStore store, CancellationToken ct)
    {
        var (caller, error) = await CallerAsync(context, authorized, ct);
        if (error is not null) return error;
        if (caller!.Readable.All(r => r.EnvironmentId != environmentId)) return Problem(context, StatusCodes.Status404NotFound, "not_found", "Ambiente não encontrado.");
        var query = context.Request.Query;
        if (!TryDate(query["since"], out var since) || !TryDate(query["until"], out var until)) return Invalid(context);
        JustificationCursor? after = null;
        if (!string.IsNullOrEmpty(query["cursor"]))
        {
            if (!TryCursor(query["cursor"], out var cursor) || cursor is null) return Invalid(context);
            after = new JustificationCursor(cursor.At, cursor.AlertId);
        }

        var items = await store.ListAsync(environmentId, null, since, until, after, 51, ct);
        var page = items.Take(50).ToArray();
        return Results.Ok(new
        {
            justifications = page.Select(ToJustification),
            nextCursor = items.Count > 50 ? WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes($"{page[^1].CreatedAtUtc.UtcTicks}\n{page[^1].JustificationId:D}")) : null,
        });
    }

    internal static object ToJustification(ContainerActionJustification j) => new
    {
        id = j.JustificationId,
        commandId = j.CommandId,
        environmentId = j.EnvironmentId,
        containerId = j.ContainerId,
        containerName = j.ContainerName,
        action = ActionJustifications.Value(j.Action),
        actor = j.Actor,
        reason = j.Reason,
        at = j.CreatedAtUtc,
        endedAt = j.EndedAtUtc,
    };

    private static object ToAlert(AlertRecord a, bool canManage) => new
    {
        id = a.AlertId,
        ruleId = a.RuleId,
        ruleName = a.RuleName,
        environmentId = a.EnvironmentId,
        containerId = a.ContainerId,
        containerName = a.ContainerName,
        severity = AlertValues.Severity(a.Severity),
        state = AlertValues.State(a.State),
        condition = a.ConditionText,
        openedAt = a.OpenedAtUtc,
        lastOccurredAt = a.LastOccurredAtUtc,
        occurrences = a.Occurrences,
        assignee = a.Assignee,
        silenceId = a.SilenceId,
        resolvedAt = a.ResolvedAtUtc,
        resolution = a.ResolutionReason,
        canManage,
    };

    private static object ToRule(AlertRule r) => new
    {
        id = r.RuleId,
        name = r.Name,
        description = r.Description,
        conditionKind = r.ConditionKind == AlertConditionKind.Event ? "event" : "metric",
        eventType = r.EventType,
        metric = r.Metric is { } m ? AlertValues.Metric(m) : null,
        thresholdPercent = r.ThresholdPercent,
        durationSeconds = r.DurationSeconds,
        minOccurrences = r.MinOccurrences,
        severity = AlertValues.Severity(r.Severity),
        scope = ToScope(r.Scope),
        enabled = r.Enabled,
        builtIn = r.BuiltIn,
        version = r.Version,
    };

    private static object ToScope(AlertScope s) => new
    {
        stage = s.Stage is { } stage ? EnvironmentStages.ToValue(stage) : null,
        environmentId = s.EnvironmentId,
        containerPattern = s.ContainerPattern,
    };

    private static bool TryRule(Guid ruleId, AlertRuleRequest body, out AlertRule? rule, out string? field)
    {
        rule = null;
        field = null;
        var kind = body.ConditionKind switch { "event" => AlertConditionKind.Event, "metric" => (AlertConditionKind?)AlertConditionKind.Metric, _ => null };
        if (kind is null) { field = "conditionKind"; return false; }
        if (!AlertValues.TryParseSeverity(body.Severity, out var severity)) { field = "severity"; return false; }
        AlertMetric? metric = null;
        if (body.Metric is not null)
        {
            if (!AlertValues.TryParseMetric(body.Metric, out var parsed)) { field = "metric"; return false; }
            metric = parsed;
        }

        if (!TryScope(body.Scope, out var scope)) { field = "scope"; return false; }
        rule = new AlertRule(ruleId, body.Name ?? string.Empty, body.Description, kind.Value, body.EventType, metric, body.ThresholdPercent, body.DurationSeconds, severity, scope!, body.Enabled, false, body.Version ?? 0)
        {
            MinOccurrences = body.MinOccurrences ?? 1,
        };
        return true;
    }

    private static bool TryScope(AlertScopeRequest? request, out AlertScope? scope)
    {
        scope = AlertScope.Everything;
        if (request is null) return true;
        EnvironmentStage? stage = null;
        if (!string.IsNullOrEmpty(request.Stage))
        {
            if (!EnvironmentStages.TryParse(request.Stage, out var parsed)) return false;
            stage = parsed;
        }

        var pattern = string.IsNullOrWhiteSpace(request.ContainerPattern) ? null : request.ContainerPattern.Trim();
        if (pattern is { Length: > AlertScope.MaxPatternLength }) return false;
        scope = new AlertScope(stage, request.EnvironmentId, pattern);
        return true;
    }

    private static bool TryStage(string? value, out EnvironmentStage? stage)
    {
        stage = null;
        if (string.IsNullOrEmpty(value)) return true;
        if (!EnvironmentStages.TryParse(value, out var parsed)) return false;
        stage = parsed;
        return true;
    }

    private static bool TryDate(string? value, out DateTimeOffset? date)
    {
        date = null;
        if (string.IsNullOrEmpty(value)) return true;
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)) return false;
        date = parsed;
        return true;
    }

    private static bool TryCursor(string? value, out AlertCursor? cursor)
    {
        cursor = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        try
        {
            var parts = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(value)).Split('\n');
            if (parts.Length != 2 || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) || !Guid.TryParseExact(parts[1], "D", out var id)) return false;
            cursor = new AlertCursor(new DateTimeOffset(ticks, TimeSpan.Zero), id);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static IResult Invalid(HttpContext context) => Problem(context, StatusCodes.Status400BadRequest, "invalid_request", "Parâmetros inválidos.");
}
