namespace Dokpod.ControlPlane.Api.Licensing;

/// <summary>
/// Problem details tipados do licenciamento (spec 031 T012, contrato LicensingProblemCode). Recusa por licença é
/// 403, upload inválido 422, conflito 412, tamanho 413 e limite 429; nunca 401, para não encerrar a sessão (FR-024).
/// </summary>
public static class LicensingProblems
{
    public const string AuthorizationDenied = "authorization_denied";
    public const string LicenseInvalid = "license_invalid";
    public const string LicenseObsolete = "license_obsolete";
    public const string LicenseExpired = "license_expired";
    public const string LicenseCapacityExceeded = "license_capacity_exceeded";
    public const string LicenseRevoked = "license_revoked";
    public const string LicenseStateIndeterminate = "license_state_indeterminate";
    public const string LicenseRevisionConflict = "license_revision_conflict";
    public const string LicenseSlotUnknown = "license_slot_unknown";
    public const string LicenseSlotDisabled = "license_slot_disabled";
    public const string PayloadTooLarge = "payload_too_large";
    public const string RateLimited = "rate_limited";
    public const string LicenseNotIndeterminate = "license_not_indeterminate";

    public static IReadOnlyDictionary<string, int> StatusByCode { get; } = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        [AuthorizationDenied] = StatusCodes.Status403Forbidden,
        [LicenseExpired] = StatusCodes.Status403Forbidden,
        [LicenseCapacityExceeded] = StatusCodes.Status403Forbidden,
        [LicenseRevoked] = StatusCodes.Status403Forbidden,
        [LicenseStateIndeterminate] = StatusCodes.Status403Forbidden,
        [LicenseInvalid] = StatusCodes.Status422UnprocessableEntity,
        [LicenseObsolete] = StatusCodes.Status422UnprocessableEntity,
        [LicenseSlotUnknown] = StatusCodes.Status422UnprocessableEntity,
        [LicenseSlotDisabled] = StatusCodes.Status422UnprocessableEntity,
        [LicenseRevisionConflict] = StatusCodes.Status412PreconditionFailed,
        [PayloadTooLarge] = StatusCodes.Status413PayloadTooLarge,
        [RateLimited] = StatusCodes.Status429TooManyRequests,
        [LicenseNotIndeterminate] = StatusCodes.Status409Conflict,
    };

    public static IResult Create(HttpContext context, string code, string detail)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!StatusByCode.TryGetValue(code, out var status))
        {
            throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown licensing problem code.");
        }

        return Results.Problem(
            statusCode: status,
            title: status switch
            {
                StatusCodes.Status403Forbidden when code == AuthorizationDenied => "Acesso negado",
                StatusCodes.Status403Forbidden => "Recurso indisponível pela licença",
                StatusCodes.Status412PreconditionFailed => "Estado da licença mudou",
                StatusCodes.Status413PayloadTooLarge => "Arquivo grande demais",
                StatusCodes.Status429TooManyRequests => "Muitas importações",
                StatusCodes.Status409Conflict => "Nada a recuperar",
                _ => "Arquivo de licença recusado",
            },
            detail: detail,
            instance: context.Request.Path,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["traceId"] = context.TraceIdentifier,
            });
    }
}
