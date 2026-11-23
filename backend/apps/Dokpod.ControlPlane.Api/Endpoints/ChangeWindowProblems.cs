using Dokpod.ControlPlane.Application.ChangeWindows;

namespace Dokpod.ControlPlane.Api.Endpoints;

/// <summary>
/// Recusas da janela de mudança nas rotas de ação (spec 026 contracts/api.md, CE e Business): fora da janela,
/// <c>409 change_window_closed</c> com a próxima abertura; exceção sem justificativa, <c>422</c>; exceção negada,
/// <c>403 change_override_denied</c>; Keycloak indisponível, <c>503</c>.
/// </summary>
public static class ChangeWindowProblems
{
    public const string ClosedDetail = "Fora da janela de mudança.";

    /// <summary>Pedido de exceção do corpo da ação (<c>changeWindowOverride</c> com a <c>justification</c> do mesmo corpo).</summary>
    public static ChangeWindowOverrideRequest? Override(bool? changeWindowOverride, string? justification) =>
        changeWindowOverride == true ? new ChangeWindowOverrideRequest(justification) : null;

    public static IResult Denied(HttpContext context, ChangeWindowDecision? decision)
    {
        ArgumentNullException.ThrowIfNull(context);
        var code = decision?.FailureCode ?? ChangeWindowGuard.WindowClosed;
        var (status, title, detail) = code switch
        {
            ChangeWindowGuard.JustificationRequired => (StatusCodes.Status422UnprocessableEntity, "Justificativa obrigatória",
                "A exceção de emergência exige uma justificativa de 10 a 500 caracteres."),
            ChangeWindowGuard.OverrideDenied => (StatusCodes.Status403Forbidden, "Exceção não autorizada",
                "A exceção fora da janela de mudança exige change:override neste host."),
            ChangeWindowGuard.AuthorizationUnavailable => (StatusCodes.Status503ServiceUnavailable, "Dependência indisponível",
                "A autorização da exceção está indisponível."),
            _ => (StatusCodes.Status409Conflict, "Fora da janela de mudança", Detail(decision)),
        };
        var extensions = new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = code, ["traceId"] = context.TraceIdentifier };
        if (code == ChangeWindowGuard.WindowClosed)
        {
            extensions["nextWindowStartsAt"] = decision?.NextWindowStartsAt;
            extensions["timeZone"] = decision?.TimeZone;
        }

        return Results.Problem(statusCode: status, title: title, detail: detail, instance: context.Request.Path, extensions: extensions);
    }

    private static string Detail(ChangeWindowDecision? decision) =>
        decision?.NextWindowStartsAt is { } next
            ? $"{ClosedDetail} A próxima janela abre em {next.ToString("O", System.Globalization.CultureInfo.InvariantCulture)} ({decision.TimeZone})."
            : ClosedDetail;
}
