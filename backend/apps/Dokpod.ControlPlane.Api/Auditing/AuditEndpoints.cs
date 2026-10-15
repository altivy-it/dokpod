using System.Globalization;
using System.Text;
using Dokpod.ControlPlane.Application.Auditing;
using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.Domain.Auditing;
using Dokpod.Domain.Environments;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using static Dokpod.ControlPlane.Api.Endpoints.ConsoleEndpoints;

namespace Dokpod.ControlPlane.Api.Endpoints;

/// <summary>Consulta e exportação da trilha de auditoria (spec 017, contracts/audit-api.md).</summary>
public static class AuditEndpoints
{
    private const int DefaultLimit = 50;

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/audit/summary", SummaryAsync).RequireAuthorization();
        endpoints.MapGet("/api/v1/audit/events", ListAsync).RequireAuthorization();
        endpoints.MapGet("/api/v1/audit/events/{eventId:guid}", GetAsync).RequireAuthorization();
        endpoints.MapGet("/api/v1/audit/export", ExportAsync).RequireAuthorization();
    }

    private static async Task<(AuditCaller? Caller, IResult? Error)> CallerAsync(HttpContext context, AuthorizedEnvironmentsService authorized, CancellationToken ct)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return (null, unauthorized);
        var loaded = await LoadAuthorizedAsync(context, authorized, actor!, token!, ct);
        return loaded.Error is not null ? (null, loaded.Error) : (new AuditCaller(actor!, token!, GetCorrelationId(context), loaded.Registrations), null);
    }

    private static async Task<IResult> SummaryAsync(HttpContext context, AuthorizedEnvironmentsService authorized, [FromServices] AuditQueryService service, CancellationToken ct)
    {
        var (caller, error) = await CallerAsync(context, authorized, ct);
        if (error is not null) return error;
        var summary = await service.SummarizeAsync(caller!, ct);
        return Results.Ok(new { readableHosts = summary.ReadableHosts, oldestEventAt = summary.OldestEventAtUtc });
    }

    private static async Task<IResult> ListAsync(HttpContext context, AuthorizedEnvironmentsService authorized, [FromServices] AuditQueryService service, CancellationToken ct)
    {
        var (caller, error) = await CallerAsync(context, authorized, ct);
        if (error is not null) return error;
        var query = context.Request.Query;
        if (!TryFilter(query, out var filter) || !TryCursor(query["cursor"], out var cursor)) return Invalid(context);
        var limit = DefaultLimit;
        if (!string.IsNullOrEmpty(query["limit"]) && (!int.TryParse(query["limit"], NumberStyles.None, CultureInfo.InvariantCulture, out limit) || limit is < 1 or > 100)) return Invalid(context);
        var result = await service.ListAsync(caller!, filter!, cursor, limit, ct);
        if (result.Outcome != AuditQueryOutcome.Ok) return Outcome(context, result.Outcome);
        var names = Names(caller!);
        return Results.Ok(new
        {
            events = result.Page.Events.Select(e => ToView(e, names)),
            nextCursor = result.Page.Next is { } next ? WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes($"{next.At.UtcTicks}\n{next.EventId:D}")) : null,
        });
    }

    private static async Task<IResult> GetAsync(Guid eventId, HttpContext context, AuthorizedEnvironmentsService authorized, [FromServices] AuditQueryService service, CancellationToken ct)
    {
        var (caller, error) = await CallerAsync(context, authorized, ct);
        if (error is not null) return error;
        if (!TryDate(context.Request.Query["at"], out var at)) return Invalid(context);
        var detail = await service.GetAsync(caller!, eventId, at, ct);
        if (detail is null) return Problem(context, StatusCodes.Status404NotFound, "not_found", "Evento não encontrado.");
        var names = Names(caller!);
        return Results.Ok(new { @event = ToView(detail.Event, names), related = detail.Related.Select(e => ToView(e, names)) });
    }

    private static async Task<IResult> ExportAsync(HttpContext context, AuthorizedEnvironmentsService authorized, [FromServices] AuditQueryService service, CancellationToken ct)
    {
        var (caller, error) = await CallerAsync(context, authorized, ct);
        if (error is not null) return error;
        if (!TryFilter(context.Request.Query, out var filter)) return Invalid(context);
        var export = await service.ExportAsync(caller!, filter!, ct);
        if (export.Outcome != AuditQueryOutcome.Ok) return Outcome(context, export.Outcome);
        var names = Names(caller!);
        var csv = new StringBuilder();
        csv.AppendLine("Horário (UTC);Ator;Identificador do ator;Tipo de ator;Ação;Categoria;Host;Alvo;Resultado;Código de falha;Correlação;Comando;Detalhes");
        foreach (var e in export.Events)
        {
            var details = e.Details is null ? string.Empty : string.Join(", ", e.Details.OrderBy(d => d.Key, StringComparer.Ordinal).Select(d => $"{d.Key}={d.Value}"));
            csv.AppendLine(string.Join(';', new[]
            {
                e.OccurredAtUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), e.ActorName ?? string.Empty, e.ActorId, ActorKindLabel(e.ActorKind),
                e.Action, AuditCategories.CategoryOf(e.Action) ?? string.Empty, names.GetValueOrDefault(e.EnvironmentId) ?? e.EnvironmentId.ToString("D"),
                e.SubjectId ?? string.Empty, OutcomeLabel(e.Outcome), e.FailureCode ?? string.Empty, e.CorrelationId.ToString("D"),
                e.CommandId?.ToString("D") ?? string.Empty, details,
            }.Select(Cell)));
        }

        var file = $"auditoria-{export.SinceUtc:yyyyMMddHHmm}-{export.UntilUtc:yyyyMMddHHmm}.csv";
        return Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv; charset=utf-8", file);
    }

    /// <summary>Célula CSV com aspas e neutralização de fórmulas (= + - @, tabulação e retorno no início).</summary>
    internal static string Cell(string value)
    {
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0], StringComparison.Ordinal)) value = "'" + value;
        return value.IndexOfAny([';', '"', '\n', '\r']) >= 0 || value.StartsWith('\'') ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : value;
    }

    private static Dictionary<Guid, string> Names(AuditCaller caller) => caller.Readable.ToDictionary(r => r.EnvironmentId, r => r.Name);

    private static object ToView(AuditEventRecord e, IReadOnlyDictionary<Guid, string> names) => new
    {
        id = e.EventId,
        occurredAt = e.OccurredAtUtc,
        correlationId = e.CorrelationId,
        actorKind = e.ActorKind switch { AuditActorKind.User => "user", AuditActorKind.Agent => "agent", _ => "system" },
        actorId = e.ActorId,
        actorName = e.ActorName,
        action = e.Action,
        category = AuditCategories.CategoryOf(e.Action),
        environmentId = e.EnvironmentId,
        environmentName = names.GetValueOrDefault(e.EnvironmentId),
        outcome = e.Outcome switch { AuditOutcome.Succeeded => "succeeded", AuditOutcome.Denied => "denied", AuditOutcome.Failed => "failed", _ => "indeterminate" },
        failureCode = e.FailureCode,
        subjectId = e.SubjectId,
        commandId = e.CommandId,
        details = e.Details,
    };

    private static string ActorKindLabel(AuditActorKind kind) => kind switch { AuditActorKind.User => "Usuário", AuditActorKind.Agent => "Agente", _ => "Sistema" };

    private static string OutcomeLabel(AuditOutcome outcome) => outcome switch
    {
        AuditOutcome.Succeeded => "Sucesso",
        AuditOutcome.Denied => "Negado",
        AuditOutcome.Failed => "Falha",
        _ => "Indeterminado",
    };

    private static IResult Outcome(HttpContext context, AuditQueryOutcome outcome) => outcome switch
    {
        AuditQueryOutcome.PeriodTooLong => Problem(context, StatusCodes.Status400BadRequest, "period_too_long", "O período máximo da consulta é de 90 dias."),
        AuditQueryOutcome.ExportTooLarge => Problem(context, StatusCodes.Status400BadRequest, "export_too_large", "A exportação aceita até 10.000 eventos; reduza o período ou os filtros."),
        AuditQueryOutcome.InvalidPeriod => Problem(context, StatusCodes.Status400BadRequest, "invalid_period", "O fim do período deve ser depois do início."),
        _ => Invalid(context),
    };

    private static bool TryFilter(IQueryCollection query, out AuditFilter? filter)
    {
        filter = null;
        if (!TryDate(query["since"], out var since) || !TryDate(query["until"], out var until)) return false;
        EnvironmentStage? stage = null;
        if (!string.IsNullOrEmpty(query["stage"]))
        {
            if (!EnvironmentStages.TryParse(query["stage"], out var parsed)) return false;
            stage = parsed;
        }

        var environments = new List<Guid>();
        foreach (var value in query["environmentId"])
        {
            if (!Guid.TryParse(value, out var id)) return false;
            environments.Add(id);
        }

        var outcomes = new List<AuditOutcome>();
        foreach (var value in query["outcome"])
        {
            AuditOutcome? parsed = value switch
            {
                "succeeded" => AuditOutcome.Succeeded,
                "denied" => AuditOutcome.Denied,
                "failed" => AuditOutcome.Failed,
                "indeterminate" => AuditOutcome.Indeterminate,
                _ => null,
            };
            if (parsed is null) return false;
            outcomes.Add(parsed.Value);
        }

        var actor = query["actor"].ToString().Trim();
        var text = query["q"].ToString().Trim();
        if (actor.Length > 255 || text.Length > 255) return false;
        filter = new AuditFilter(
            since, until, stage, environments, actor.Length == 0 ? null : actor,
            query["category"].Where(v => !string.IsNullOrEmpty(v)).Select(v => v!).ToArray(),
            query["action"].Where(v => !string.IsNullOrEmpty(v)).Select(v => v!).ToArray(),
            outcomes, text.Length == 0 ? null : text);
        return true;
    }

    private static bool TryDate(string? value, out DateTimeOffset? date)
    {
        date = null;
        if (string.IsNullOrEmpty(value)) return true;
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)) return false;
        date = parsed.ToUniversalTime();
        return true;
    }

    private static bool TryCursor(string? value, out AuditCursor? cursor)
    {
        cursor = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        try
        {
            var parts = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(value)).Split('\n');
            if (parts.Length != 2 || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) || !Guid.TryParseExact(parts[1], "D", out var id)) return false;
            cursor = new AuditCursor(new DateTimeOffset(ticks, TimeSpan.Zero), id);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static IResult Invalid(HttpContext context) => Problem(context, StatusCodes.Status400BadRequest, "invalid_request", "Parâmetros inválidos.");
}
