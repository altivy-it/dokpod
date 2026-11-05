using System.Globalization;
using Dokpod.ControlPlane.Application.ImageUpdates;
using static Dokpod.ControlPlane.Api.Endpoints.ConsoleEndpoints;

namespace Dokpod.ControlPlane.Api.ImageUpdates;

/// <summary>"Verificar agora" por host (spec 025 T016, contracts/api.md); só na composição Business.</summary>
public static class ImageUpdateEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost("/api/v1/environments/{environmentId:guid}/images/update-check", RequestAsync).RequireAuthorization();

    private static async Task<IResult> RequestAsync(Guid environmentId, HttpContext context, ImageUpdateCheckService service, CancellationToken cancellationToken)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return unauthorized!;
        var result = await service.RequestAsync(environmentId, actor!, token!, GetCorrelationId(context), cancellationToken);
        switch (result.Outcome)
        {
            case ImageUpdateCheckOutcome.Accepted:
                return Results.Accepted(value: new { requestedAt = result.RequestedAt, nextDueAt = result.NextDueAt });
            case ImageUpdateCheckOutcome.RateLimited:
                var seconds = result.RetryAt is { } retry ? Math.Max(1, (int)Math.Ceiling((retry - DateTimeOffset.UtcNow).TotalSeconds)) : 60;
                context.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
                return Problem(context, StatusCodes.Status429TooManyRequests, "rate_limited", "Já houve um pedido de verificação neste host há menos de 1 minuto.");
            case ImageUpdateCheckOutcome.LicenseDenied:
                return Problem(context, StatusCodes.Status403Forbidden, result.Code ?? "license_invalid", "A licença não permite o aviso de nova versão de imagem.");
            case ImageUpdateCheckOutcome.AgentUnsupported:
                return Problem(context, StatusCodes.Status409Conflict, "agent_unsupported", "O agente deste host não suporta a verificação de nova versão.");
            case ImageUpdateCheckOutcome.EnvironmentDisconnected:
                return Problem(context, StatusCodes.Status409Conflict, "environment_disconnected", "O host está desconectado.");
            case ImageUpdateCheckOutcome.AuthorizationDenied:
                return Problem(context, StatusCodes.Status403Forbidden, "authorization_denied", "A verificação não foi autorizada.");
            default:
                return Problem(context, StatusCodes.Status404NotFound, "environment_not_found", "O ambiente não foi encontrado.");
        }
    }
}
