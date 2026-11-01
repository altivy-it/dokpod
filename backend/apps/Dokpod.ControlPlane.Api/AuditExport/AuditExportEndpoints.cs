using System.Globalization;
using System.Text.Json;
using Dokpod.ControlPlane.Application.AuditExport;
using Dokpod.ControlPlane.Application.Authorization;
using static Dokpod.ControlPlane.Api.Endpoints.ConsoleEndpoints;

namespace Dokpod.ControlPlane.Api.AuditExport;

/// <summary>Problem details da exportação (contracts/api.md); a recusa pela licença é sempre 403, nunca 401.</summary>
public static class AuditExportProblems
{
    public const string InvalidDestination = "audit_export_invalid_destination";
    public const string InvalidSecretRef = "audit_export_invalid_secret_ref";
    public const string AddressBlocked = "audit_export_address_blocked";
    public const string NotFound = "audit_export_not_found";
    public const string LimitReached = "audit_export_limit_reached";
    public const string NameTaken = "audit_export_name_taken";
    public const string VersionConflict = "audit_export_version_conflict";
    public const string RateLimited = "rate_limited";
    public const string AuthorizationDenied = "authorization_denied";

    private static readonly Dictionary<string, (int Status, string Title, string Detail)> Problems = new(StringComparer.Ordinal)
    {
        [InvalidDestination] = (StatusCodes.Status400BadRequest, "Destino inválido", "Revise nome, protocolo, host, porta e categorias do destino."),
        [InvalidSecretRef] = (StatusCodes.Status422UnprocessableEntity, "Secret inválido", "O secretRef aceita só letras minúsculas, números e hífen (até 64)."),
        [AddressBlocked] = (StatusCodes.Status422UnprocessableEntity, "Endereço não permitido", "O host resolve para um endereço proibido (loopback, link-local, metadados ou rede do plano de controle)."),
        [NotFound] = (StatusCodes.Status404NotFound, "Destino não encontrado", "O destino não existe."),
        [LimitReached] = (StatusCodes.Status409Conflict, "Limite de destinos", "Já existem 5 destinos de exportação."),
        [NameTaken] = (StatusCodes.Status409Conflict, "Nome em uso", "Já existe um destino com esse nome."),
        [VersionConflict] = (StatusCodes.Status412PreconditionFailed, "Destino mudou", "O destino mudou desde a leitura. Atualize e tente de novo."),
        [RateLimited] = (StatusCodes.Status429TooManyRequests, "Muitos testes", "Limite de 5 envios de teste por minuto atingido. Tente de novo em instantes."),
        [AuthorizationDenied] = (StatusCodes.Status403Forbidden, "Acesso negado", "A exportação da auditoria exige a permissão correspondente no Keycloak."),
    };

    public static IResult Create(HttpContext context, string code)
    {
        var (status, title, detail) = Problems[code];
        return Result(context, status, title, code, detail);
    }

    /// <summary>Códigos do gate (spec 031): <c>license_*</c>, todos 403.</summary>
    public static IResult License(HttpContext context, string denialCode) =>
        Result(context, StatusCodes.Status403Forbidden, "Recurso indisponível pela licença", denialCode,
            "A licença não permite criar, editar, reativar ou testar destinos agora. Desativar e excluir continuam disponíveis.");

    private static IResult Result(HttpContext context, int status, string title, string code, string detail) => Results.Problem(
        statusCode: status,
        title: title,
        detail: detail,
        instance: context.Request.Path,
        extensions: new Dictionary<string, object?> { ["code"] = code, ["traceId"] = context.TraceIdentifier });
}

/// <summary>
/// <c>/api/v1/settings/audit-export/destinations</c> da composição Business (spec 027 T020). Ordem fixa: autenticar,
/// autorizar no Keycloak (<c>urn:dokpod:installation</c>, falha nega), validar e só então o gate da licença no
/// serviço. O conteúdo dos secrets nunca sai: <c>secretFiles</c> diz só se cada arquivo existe.
/// </summary>
public static class AuditExportEndpoints
{
    public const string Route = "/api/v1/settings/audit-export/destinations";
    public const int MaxBodyBytes = 16 * 1024;

    private static readonly HashSet<string> InputProperties = new(StringComparer.Ordinal)
    {
        "name", "protocol", "host", "port", "path", "secretRef", "categories", "active",
    };

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet(Route, ListAsync).RequireAuthorization();
        endpoints.MapPost(Route, CreateAsync).RequireAuthorization();
        endpoints.MapPut(Route + "/{destinationId:guid}", UpdateAsync).RequireAuthorization();
        endpoints.MapDelete(Route + "/{destinationId:guid}", DeleteAsync).RequireAuthorization();
        endpoints.MapPost(Route + "/{destinationId:guid}/test", TestAsync).RequireAuthorization();
    }

    public static async Task<IResult> ListAsync(
        HttpContext context, IEnvironmentAuthorizationDecider authorizationDecider, AuditExportDestinationService service, CancellationToken cancellationToken)
    {
        if (await AuthorizeAsync(context, authorizationDecider, AuditExportAuthorization.ReadScope, cancellationToken) is { Error: { } error }) return error;
        var views = await service.ListAsync(cancellationToken);
        return Results.Ok(views.Select(ToResponse));
    }

    public static async Task<IResult> CreateAsync(
        HttpContext context, IEnvironmentAuthorizationDecider authorizationDecider, AuditExportDestinationService service, CancellationToken cancellationToken)
    {
        var (caller, denied) = await AuthorizeAsync(context, authorizationDecider, AuditExportAuthorization.ManageScope, cancellationToken);
        if (denied is not null) return denied;
        if (!Guid.TryParse(context.Request.Headers["Idempotency-Key"].ToString(), out var idempotencyKey))
        {
            return Problem(context, StatusCodes.Status400BadRequest, "idempotency_key_required", "Informe o Idempotency-Key (UUID) da criação.");
        }

        if (await ReadInputAsync(context.Request, cancellationToken) is not { } input) return AuditExportProblems.Create(context, AuditExportProblems.InvalidDestination);
        var result = await service.CreateAsync(input, idempotencyKey.ToString("D"), caller!, cancellationToken);
        if (result.Outcome is not (AuditExportServiceOutcome.Created or AuditExportServiceOutcome.Replayed)) return Failure(context, result);

        SetETag(context, result.View!.Destination.Version);
        return Results.Created($"{Route}/{result.View.Destination.DestinationId:D}", ToResponse(result.View));
    }

    public static async Task<IResult> UpdateAsync(
        Guid destinationId, HttpContext context, IEnvironmentAuthorizationDecider authorizationDecider, AuditExportDestinationService service, CancellationToken cancellationToken)
    {
        var (caller, denied) = await AuthorizeAsync(context, authorizationDecider, AuditExportAuthorization.ManageScope, cancellationToken);
        if (denied is not null) return denied;
        if (!TryReadIfMatch(context, out var version)) return IfMatchRequired(context);
        if (await ReadInputAsync(context.Request, cancellationToken) is not { } input) return AuditExportProblems.Create(context, AuditExportProblems.InvalidDestination);

        var result = await service.UpdateAsync(destinationId, input, version, caller!, cancellationToken);
        if (result.Outcome != AuditExportServiceOutcome.Ok) return Failure(context, result);
        SetETag(context, result.View!.Destination.Version);
        return Results.Ok(ToResponse(result.View));
    }

    public static async Task<IResult> DeleteAsync(
        Guid destinationId, HttpContext context, IEnvironmentAuthorizationDecider authorizationDecider, AuditExportDestinationService service, CancellationToken cancellationToken)
    {
        var (caller, denied) = await AuthorizeAsync(context, authorizationDecider, AuditExportAuthorization.ManageScope, cancellationToken);
        if (denied is not null) return denied;
        if (!TryReadIfMatch(context, out var version)) return IfMatchRequired(context);

        var result = await service.DeleteAsync(destinationId, version, caller!, cancellationToken);
        return result.Outcome == AuditExportServiceOutcome.Deleted ? Results.NoContent() : Failure(context, result);
    }

    public static async Task<IResult> TestAsync(
        Guid destinationId, HttpContext context, IEnvironmentAuthorizationDecider authorizationDecider, AuditExportDestinationService service, CancellationToken cancellationToken)
    {
        var (caller, denied) = await AuthorizeAsync(context, authorizationDecider, AuditExportAuthorization.ManageScope, cancellationToken);
        if (denied is not null) return denied;

        var result = await service.TestAsync(destinationId, caller!, cancellationToken);
        return result.Outcome == AuditExportServiceOutcome.Ok
            ? Results.Ok(new { delivered = result.TestResult!.Delivered, errorCode = result.TestResult.ErrorCode })
            : Failure(context, result);
    }

    internal static object ToResponse(AuditExportDestinationView view) => new
    {
        destinationId = view.Destination.DestinationId,
        name = view.Destination.Name,
        protocol = view.Destination.Protocol,
        host = view.Destination.Host,
        port = view.Destination.Port,
        path = view.Destination.Path,
        secretRef = view.Destination.SecretRef,
        secretFiles = new { ca = view.SecretFiles.Ca, clientCertificate = view.SecretFiles.ClientCertificate, token = view.SecretFiles.Token },
        categories = view.Destination.Categories,
        active = view.Destination.Active,
        state = view.State,
        lastSentAt = view.Cursor.LastSentAtUtc,
        lastError = view.Cursor.LastErrorCode is null ? null : new { code = view.Cursor.LastErrorCode, at = view.Cursor.LastErrorAtUtc },
        pending = new { count = view.Pending.Count, capped = view.Pending.Capped, oldestAt = view.Pending.OldestAtUtc },
        lastGapAt = view.Cursor.LastGapAtUtc,
        version = view.Destination.Version,
    };

    private static IResult Failure(HttpContext context, AuditExportServiceResult result) => result.Outcome switch
    {
        AuditExportServiceOutcome.LicenseDenied => AuditExportProblems.License(context, result.DenialCode ?? "license_invalid"),
        AuditExportServiceOutcome.InvalidDestination => AuditExportProblems.Create(context, AuditExportProblems.InvalidDestination),
        AuditExportServiceOutcome.InvalidSecretRef => AuditExportProblems.Create(context, AuditExportProblems.InvalidSecretRef),
        AuditExportServiceOutcome.AddressBlocked => AuditExportProblems.Create(context, AuditExportProblems.AddressBlocked),
        AuditExportServiceOutcome.NotFound => AuditExportProblems.Create(context, AuditExportProblems.NotFound),
        AuditExportServiceOutcome.VersionConflict => AuditExportProblems.Create(context, AuditExportProblems.VersionConflict),
        AuditExportServiceOutcome.NameTaken => AuditExportProblems.Create(context, AuditExportProblems.NameTaken),
        AuditExportServiceOutcome.LimitReached => AuditExportProblems.Create(context, AuditExportProblems.LimitReached),
        AuditExportServiceOutcome.RateLimited => AuditExportProblems.Create(context, AuditExportProblems.RateLimited),
        _ => throw new InvalidOperationException($"Unexpected outcome {result.Outcome}."),
    };

    private static async Task<(AuditExportCaller? Caller, IResult? Error)> AuthorizeAsync(
        HttpContext context, IEnvironmentAuthorizationDecider authorizationDecider, string scope, CancellationToken cancellationToken)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return (null, unauthorized);
        EnvironmentAuthorizationDecision decision;
        try
        {
            decision = await authorizationDecider.DecideAsync(AuditExportAuthorization.Resource, scope, actor!, token!, GetCorrelationId(context), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            decision = new EnvironmentAuthorizationDecision(AuthorizationDecisionOutcome.Indeterminate, "authorization_unavailable");
        }

        return decision.Outcome switch
        {
            AuthorizationDecisionOutcome.Allowed => (new AuditExportCaller(actor!.Subject, actor.DisplayName, GetCorrelationId(context)), null),
            AuthorizationDecisionOutcome.Denied => (null, AuditExportProblems.Create(context, AuditExportProblems.AuthorizationDenied)),
            _ => (null, Problem(context, StatusCodes.Status503ServiceUnavailable, "authorization_unavailable", "A decisão de autorização está indisponível.")),
        };
    }

    /// <summary>Corpo de contracts/api.md com tipos estritos; propriedade desconhecida ou tipo errado devolve null (400).</summary>
    internal static async Task<AuditExportDestinationInput?> ReadInputAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaxBodyBytes) return null;
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxBodyBytes) return null;
            buffer.Write(chunk, 0, read);
        }

        try
        {
            using var document = JsonDocument.Parse(buffer.ToArray());
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Any(property => !InputProperties.Contains(property.Name))) return null;
            if (!root.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String
                || !root.TryGetProperty("protocol", out var protocol) || protocol.ValueKind != JsonValueKind.String
                || !root.TryGetProperty("host", out var host) || host.ValueKind != JsonValueKind.String
                || !root.TryGetProperty("port", out var port) || !port.TryGetInt32(out var portValue)
                || !root.TryGetProperty("categories", out var categories) || categories.ValueKind != JsonValueKind.Array
                || categories.EnumerateArray().Any(category => category.ValueKind != JsonValueKind.String)
                || !root.TryGetProperty("active", out var active) || active.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                || !OptionalString(root, "path", out var path) || !OptionalString(root, "secretRef", out var secretRef))
            {
                return null;
            }

            var list = categories.EnumerateArray().Select(category => category.GetString()!).ToArray();
            if (list.Length > 32 || list.Distinct(StringComparer.Ordinal).Count() != list.Length) return null;
            return new AuditExportDestinationInput(name.GetString()!, protocol.GetString()!, host.GetString()!, portValue, path, secretRef, list, active.GetBoolean());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool OptionalString(JsonElement root, string property, out string? value)
    {
        value = null;
        if (!root.TryGetProperty(property, out var element) || element.ValueKind == JsonValueKind.Null) return true;
        if (element.ValueKind != JsonValueKind.String) return false;
        value = element.GetString();
        return true;
    }

    private static void SetETag(HttpContext context, long version) =>
        context.Response.Headers.ETag = $"\"{version.ToString(CultureInfo.InvariantCulture)}\"";

    private static IResult IfMatchRequired(HttpContext context) =>
        Problem(context, StatusCodes.Status400BadRequest, "if_match_required", "Informe o If-Match com a versão lida do destino.");

    private static bool TryReadIfMatch(HttpContext context, out long version)
    {
        version = 0;
        var value = context.Request.Headers.IfMatch.ToString().Trim();
        return value.Length >= 3 && value[0] == '"' && value[^1] == '"'
            && long.TryParse(value.AsSpan(1, value.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out version);
    }
}
