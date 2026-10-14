using Dokpod.ControlPlane.Api.Realtime;
using Dokpod.ControlPlane.Application.Terminal;
using static Dokpod.ControlPlane.Api.Endpoints.ConsoleEndpoints;

namespace Dokpod.ControlPlane.Api.Endpoints;

/// <summary>Política do terminal e renovação da autorização da sessão (spec 014 T008, D-008).</summary>
public static class TerminalEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/environments/{environmentId:guid}/containers/{containerId}/terminal-policy", GetPolicyAsync).RequireAuthorization();
        endpoints.MapPost("/api/v1/terminal-sessions/{sessionId:guid}/renew", RenewAsync).RequireAuthorization();
    }

    private static async Task<IResult> GetPolicyAsync(
        Guid environmentId,
        string containerId,
        HttpContext context,
        TerminalSessionService service,
        CancellationToken cancellationToken)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return unauthorized!;
        if (containerId.Length != 64 || !containerId.All(Uri.IsHexDigit))
        {
            return Problem(context, StatusCodes.Status400BadRequest, "invalid_request", "O container informado é inválido.");
        }

        var policy = await service.EvaluatePolicyAsync(environmentId, containerId.ToLowerInvariant(), actor!, token!, context.User, GetCorrelationId(context), cancellationToken);
        if (policy.Reason == "authorization_unavailable")
        {
            return Problem(context, StatusCodes.Status503ServiceUnavailable, "authorization_unavailable", "A autorização está indisponível.");
        }

        return Results.Ok(new
        {
            allowed = policy.Allowed,
            reason = policy.Reason,
            requiresJustification = policy.RequiresJustification,
            requiresRecentMfa = policy.RequiresRecentMfa,
            limits = new
            {
                idleTimeoutSeconds = (int)policy.Limits.IdleTimeout.TotalSeconds,
                maxDurationSeconds = (int)policy.Limits.MaxDuration.TotalSeconds,
                maxPerUser = policy.Limits.MaxPerUser,
                maxPerUserPerContainer = policy.Limits.MaxPerUserPerContainer,
                maxPerAgent = policy.Limits.MaxPerAgent,
                justificationMinLength = TerminalSessionService.MinJustification,
                justificationMaxLength = TerminalSessionService.MaxJustification,
            },
        });
    }

    private static async Task<IResult> RenewAsync(
        Guid sessionId,
        HttpContext context,
        TerminalSessionService service,
        CancellationToken cancellationToken)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return unauthorized!;
        var renewed = await service.RenewAsync(sessionId, actor!, token!, ControlPlaneHub.TokenExpiresAt(context.User), GetCorrelationId(context), cancellationToken);
        return renewed
            ? Results.NoContent()
            : Problem(context, StatusCodes.Status403Forbidden, "authorization_denied", "A sessão de terminal não pôde ser renovada.");
    }
}
