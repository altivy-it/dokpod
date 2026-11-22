using System.Globalization;
using System.Text.Json;
using Dokpod.ControlPlane.Api.AuditExport;
using Dokpod.ControlPlane.Application.Alerts;
using Dokpod.ControlPlane.Application.ChangeWindows;
using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.Domain.ChangeWindows;
using Dokpod.Domain.Environments;
using static Dokpod.ControlPlane.Api.Endpoints.ConsoleEndpoints;

namespace Dokpod.ControlPlane.Api.ChangeWindows;

/// <summary>
/// <c>/api/v1/change-windows</c> da composição Business (spec 026 T018, contracts/api.md). Ordem: autenticar, hosts
/// legíveis, regra de edição do estágio, gate da licença (só no <c>PUT</c>), validação e versão.
/// </summary>
public static class ChangeWindowEndpoints
{
    public const string Route = "/api/v1/change-windows";
    public const int MaxBodyBytes = 16 * 1024;
    public const string InvalidCode = "change_window_invalid";

    private static readonly HashSet<string> BodyProperties = new(StringComparer.Ordinal) { "timeZone", "ranges" };
    private static readonly HashSet<string> RangeProperties = new(StringComparer.Ordinal) { "days", "start", "end" };

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet(Route, ListAsync).RequireAuthorization();
        endpoints.MapPut(Route + "/{stage}", SaveAsync).RequireAuthorization();
        endpoints.MapDelete(Route + "/{stage}", DeleteAsync).RequireAuthorization();
    }

    public static async Task<IResult> ListAsync(
        HttpContext context, AuthorizedEnvironmentsService authorized, ChangeWindowSettingsService service, CancellationToken cancellationToken)
    {
        var (caller, error) = await CallerAsync(context, authorized, cancellationToken);
        if (error is not null) return error;
        var views = await service.ListAsync(caller!, cancellationToken);
        if (views is null) return Forbidden(context);
        return Results.Ok(new { stages = views.Select(ToResponse) });
    }

    public static async Task<IResult> SaveAsync(
        string stage, HttpContext context, AuthorizedEnvironmentsService authorized, ChangeWindowSettingsService service, CancellationToken cancellationToken)
    {
        if (!EnvironmentStages.TryParse(stage, out var parsed)) return NotFound(context);
        var (caller, error) = await CallerAsync(context, authorized, cancellationToken);
        if (error is not null) return error;
        var create = context.Request.Headers.IfNoneMatch.ToString().Trim() == "*";
        int? expected = null;
        if (!create)
        {
            if (!TryReadIfMatch(context, out var version)) return IfMatchRequired(context);
            expected = version;
        }

        var (body, errors) = await ReadBodyAsync(context.Request, cancellationToken);
        var result = await service.SaveAsync(
            caller!,
            parsed,
            body?.TimeZone,
            // Corpo malformado vira lista vazia: a validação recusa e a resposta aponta o campo.
            errors.Count > 0 ? null : body?.Ranges,
            expected,
            cancellationToken);
        if (result.Outcome == ChangeWindowSettingsOutcome.Invalid)
        {
            return Invalid(context, errors.Count > 0 ? errors : result.Errors ?? []);
        }

        if (result.Outcome != ChangeWindowSettingsOutcome.Ok) return Failure(context, result);
        SetETag(context, result.View!.Window!.Version);
        return Results.Ok(ToResponse(result.View));
    }

    public static async Task<IResult> DeleteAsync(
        string stage, HttpContext context, AuthorizedEnvironmentsService authorized, ChangeWindowSettingsService service, CancellationToken cancellationToken)
    {
        if (!EnvironmentStages.TryParse(stage, out var parsed)) return NotFound(context);
        var (caller, error) = await CallerAsync(context, authorized, cancellationToken);
        if (error is not null) return error;
        if (!TryReadIfMatch(context, out var version)) return IfMatchRequired(context);
        var result = await service.DeleteAsync(caller!, parsed, version, cancellationToken);
        return result.Outcome == ChangeWindowSettingsOutcome.Ok ? Results.NoContent() : Failure(context, result);
    }

    private static async Task<(AlertCaller? Caller, IResult? Error)> CallerAsync(HttpContext context, AuthorizedEnvironmentsService authorized, CancellationToken cancellationToken)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return (null, unauthorized);
        var loaded = await LoadAuthorizedAsync(context, authorized, actor!, token!, cancellationToken);
        return loaded.Error is not null ? (null, loaded.Error) : (new AlertCaller(actor!, token!, GetCorrelationId(context), loaded.Registrations), null);
    }

    private static object ToResponse(ChangeWindowView view) => new
    {
        stage = EnvironmentStages.ToValue(view.Stage),
        timeZone = view.Window?.TimeZone,
        ranges = view.Window?.Ranges.Select(range => new
        {
            days = range.Days.OrderBy(day => ((int)day + 6) % 7).Select(ChangeWindowFormat.DayToValue),
            start = ChangeWindowFormat.TimeToValue(range.Start),
            end = ChangeWindowFormat.TimeToValue(range.End),
        }).ToArray() ?? [],
        updatedBy = view.Window?.UpdatedBy,
        updatedAt = view.Window?.UpdatedAtUtc,
        version = view.Window?.Version,
        editable = view.Editable,
        status = ToStatus(view.Status),
    };

    /// <summary>Estado no formato do contrato (<c>ChangeWindowStatus</c>), também usado nas respostas de host.</summary>
    public static object ToStatus(ChangeWindowStatus status) => new
    {
        state = status.State switch
        {
            ChangeWindowState.Open => "open",
            ChangeWindowState.Closed => "closed",
            _ => "none",
        },
        closesAt = status.ClosesAt,
        nextOpensAt = status.NextOpensAt,
        timeZone = status.TimeZone,
    };

    private sealed record Body(string? TimeZone, IReadOnlyList<ChangeWindowRange>? Ranges);

    /// <summary>Lê o corpo sem aceitar propriedade desconhecida; os erros apontam o campo, como no <c>400</c>.</summary>
    private static async Task<(Body? Body, List<ChangeWindowFieldError> Errors)> ReadBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var errors = new List<ChangeWindowFieldError>();
        if (request.ContentLength > MaxBodyBytes) return (null, [new("body", "too_large")]);
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxBodyBytes) return (null, [new("body", "too_large")]);
            buffer.Write(chunk, 0, read);
        }

        try
        {
            using var document = JsonDocument.Parse(buffer.ToArray());
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return (null, [new("body", "invalid")]);
            foreach (var property in root.EnumerateObject().Where(property => !BodyProperties.Contains(property.Name)))
            {
                errors.Add(new(property.Name, "unknown"));
            }

            var timeZone = root.TryGetProperty("timeZone", out var zone) && zone.ValueKind == JsonValueKind.String ? zone.GetString() : null;
            if (timeZone is null) errors.Add(new("timeZone", "invalid"));
            if (!root.TryGetProperty("ranges", out var rangesElement) || rangesElement.ValueKind != JsonValueKind.Array)
            {
                errors.Add(new("ranges", "count"));
                return (new Body(timeZone, null), errors);
            }

            var ranges = new List<ChangeWindowRange>();
            var index = 0;
            foreach (var element in rangesElement.EnumerateArray())
            {
                if (ReadRange(element, index, errors) is { } range) ranges.Add(range);
                index++;
            }

            return (new Body(timeZone, ranges), errors);
        }
        catch (JsonException)
        {
            return (null, [new("body", "invalid")]);
        }
    }

    private static ChangeWindowRange? ReadRange(JsonElement element, int index, List<ChangeWindowFieldError> errors)
    {
        var prefix = $"ranges[{index.ToString(CultureInfo.InvariantCulture)}]";
        if (element.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new(prefix, "invalid"));
            return null;
        }

        foreach (var property in element.EnumerateObject().Where(property => !RangeProperties.Contains(property.Name)))
        {
            errors.Add(new($"{prefix}.{property.Name}", "unknown"));
        }

        var days = new HashSet<DayOfWeek>();
        var daysValid = element.TryGetProperty("days", out var daysElement) && daysElement.ValueKind == JsonValueKind.Array;
        if (daysValid)
        {
            foreach (var day in daysElement.EnumerateArray())
            {
                // Dia desconhecido ou repetido recusa a lista inteira.
                if (day.ValueKind != JsonValueKind.String || !ChangeWindowFormat.TryParseDay(day.GetString(), out var parsed) || !days.Add(parsed)) daysValid = false;
            }
        }

        if (!daysValid || days.Count == 0) errors.Add(new($"{prefix}.days", "invalid"));
        var start = ReadTime(element, "start", $"{prefix}.start", errors);
        var end = ReadTime(element, "end", $"{prefix}.end", errors);
        return daysValid && start is { } startTime && end is { } endTime ? new ChangeWindowRange(days, startTime, endTime) : null;
    }

    private static TimeOnly? ReadTime(JsonElement element, string name, string field, List<ChangeWindowFieldError> errors)
    {
        if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && ChangeWindowFormat.TryParseTime(value.GetString(), out var time)) return time;
        errors.Add(new(field, "invalid"));
        return null;
    }

    private static IResult Failure(HttpContext context, ChangeWindowSettingsResult result) => result.Outcome switch
    {
        ChangeWindowSettingsOutcome.Forbidden => Forbidden(context),
        ChangeWindowSettingsOutcome.LicenseDenied => AuditExportProblems.License(context, result.DenialCode ?? "license_invalid"),
        ChangeWindowSettingsOutcome.NotFound or ChangeWindowSettingsOutcome.PreconditionFailed => Results.Problem(
            statusCode: StatusCodes.Status412PreconditionFailed,
            title: "Janela mudou",
            detail: "A janela mudou desde a leitura. Atualize e tente de novo.",
            instance: context.Request.Path,
            extensions: new Dictionary<string, object?> { ["code"] = "precondition_failed", ["traceId"] = context.TraceIdentifier }),
        _ => Problem(context, StatusCodes.Status503ServiceUnavailable, "authorization_unavailable", "A autorização está indisponível."),
    };

    private static IResult Invalid(HttpContext context, IReadOnlyList<ChangeWindowFieldError> errors) => Results.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Janela inválida",
        detail: "Revise o fuso e as faixas da janela.",
        instance: context.Request.Path,
        extensions: new Dictionary<string, object?>
        {
            ["code"] = InvalidCode,
            ["traceId"] = context.TraceIdentifier,
            ["errors"] = errors.Select(error => new { field = error.Field, code = error.Code }).ToArray(),
        });

    private static IResult Forbidden(HttpContext context) =>
        Problem(context, StatusCodes.Status403Forbidden, "authorization_denied", "Editar a janela exige environment:manage em todos os hosts do estágio.");

    private static IResult NotFound(HttpContext context) =>
        Problem(context, StatusCodes.Status404NotFound, "not_found", "Estágio desconhecido.");

    private static IResult IfMatchRequired(HttpContext context) =>
        Problem(context, StatusCodes.Status400BadRequest, "if_match_required", "Informe o If-Match com a versão lida da janela, ou If-None-Match: * para criar.");

    private static void SetETag(HttpContext context, int version) =>
        context.Response.Headers.ETag = $"\"{version.ToString(CultureInfo.InvariantCulture)}\"";

    private static bool TryReadIfMatch(HttpContext context, out int version)
    {
        version = 0;
        var value = context.Request.Headers.IfMatch.ToString().Trim();
        return value.Length >= 3 && value[0] == '"' && value[^1] == '"'
            && int.TryParse(value.AsSpan(1, value.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out version) && version >= 1;
    }
}
