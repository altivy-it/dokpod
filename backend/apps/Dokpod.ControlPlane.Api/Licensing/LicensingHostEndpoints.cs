using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Licensing;
using static Dokpod.ControlPlane.Api.Endpoints.ConsoleEndpoints;

namespace Dokpod.ControlPlane.Api.Licensing;

public sealed record LicensingHostRequest(string? Label);

/// <summary>
/// Hosts de licenciamento (spec 031 T039, D-004): leitura com <c>licensing:read</c>; criar, associar, desassociar e
/// remover com <c>licensing:manage</c>. Todas as escritas são idempotentes pelos IDs da rota.
/// </summary>
public static class LicensingHostEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet("/api/v1/licensing/hosts", ListAsync).RequireAuthorization();
        endpoints.MapPut("/api/v1/licensing/hosts/{hostId:guid}", CreateAsync).RequireAuthorization();
        endpoints.MapDelete("/api/v1/licensing/hosts/{hostId:guid}", RemoveAsync).RequireAuthorization();
        endpoints.MapPut("/api/v1/licensing/hosts/{hostId:guid}/environments/{environmentId:guid}", LinkAsync).RequireAuthorization();
        endpoints.MapDelete("/api/v1/licensing/hosts/{hostId:guid}/environments/{environmentId:guid}", UnlinkAsync).RequireAuthorization();
    }

    public static async Task<IResult> ListAsync(HttpContext context, IEnvironmentAuthorizationDecider decider, LicensingHostService service, CancellationToken cancellationToken)
    {
        var (actor, error) = await AuthorizeAsync(context, decider, LicensingAuthorization.ReadScope, cancellationToken);
        if (error is not null) return error;
        _ = actor;
        var hosts = await service.ListAsync(cancellationToken);
        return Results.Ok(new
        {
            hosts = hosts.Select(host => new
            {
                hostId = host.HostId,
                label = host.Label,
                removed = host.Removed,
                environmentIds = host.EnvironmentIds,
                vcpus = host.Vcpus,
                source = host.Source,
                nodeEquivalents = host.NodeEquivalents,
            }),
        });
    }

    public static async Task<IResult> CreateAsync(
        Guid hostId,
        LicensingHostRequest? request,
        HttpContext context,
        IEnvironmentAuthorizationDecider decider,
        LicensingHostService service,
        CancellationToken cancellationToken)
    {
        var (actor, error) = await AuthorizeAsync(context, decider, LicensingAuthorization.ManageScope, cancellationToken);
        if (error is not null) return error;
        if (!LicensingHostService.IsValidLabel(request?.Label))
        {
            return Problem(context, StatusCodes.Status400BadRequest, "invalid_host", "Informe o nome do host (1 a 128 caracteres).");
        }

        return await service.CreateAsync(hostId, request!.Label!, actor!.Subject, GetCorrelationId(context), cancellationToken) switch
        {
            LicensingHostChange.Applied => Results.Created($"/api/v1/licensing/hosts/{hostId:D}", new { hostId }),
            LicensingHostChange.Removed => Problem(context, StatusCodes.Status409Conflict, "host_removed", "O host foi removido e não pode ser recriado com o mesmo ID."),
            _ => Results.Ok(new { hostId }),
        };
    }

    public static Task<IResult> LinkAsync(Guid hostId, Guid environmentId, HttpContext context, IEnvironmentAuthorizationDecider decider, LicensingHostService service, CancellationToken cancellationToken) =>
        ChangeAsync(context, decider, actor => service.LinkAsync(hostId, environmentId, actor, GetCorrelationId(context), cancellationToken), StatusCodes.Status200OK, cancellationToken);

    public static Task<IResult> UnlinkAsync(Guid hostId, Guid environmentId, HttpContext context, IEnvironmentAuthorizationDecider decider, LicensingHostService service, CancellationToken cancellationToken) =>
        ChangeAsync(context, decider, actor => service.UnlinkAsync(hostId, environmentId, actor, GetCorrelationId(context), cancellationToken), StatusCodes.Status204NoContent, cancellationToken);

    public static Task<IResult> RemoveAsync(Guid hostId, HttpContext context, IEnvironmentAuthorizationDecider decider, LicensingHostService service, CancellationToken cancellationToken) =>
        ChangeAsync(context, decider, actor => service.RemoveAsync(hostId, actor, GetCorrelationId(context), cancellationToken), StatusCodes.Status204NoContent, cancellationToken);

    private static async Task<IResult> ChangeAsync(
        HttpContext context,
        IEnvironmentAuthorizationDecider decider,
        Func<string, Task<LicensingHostChange>> change,
        int successStatus,
        CancellationToken cancellationToken)
    {
        var (actor, error) = await AuthorizeAsync(context, decider, LicensingAuthorization.ManageScope, cancellationToken);
        if (error is not null) return error;
        return await change(actor!.Subject) switch
        {
            LicensingHostChange.NotFound => Problem(context, StatusCodes.Status404NotFound, "host_not_found", "O host de licenciamento não existe."),
            LicensingHostChange.Removed => Problem(context, StatusCodes.Status409Conflict, "host_removed", "O host foi removido; associe o ambiente a outro host."),
            _ => Results.StatusCode(successStatus),
        };
    }

    private static async Task<(AuthenticatedActor? Actor, IResult? Error)> AuthorizeAsync(
        HttpContext context,
        IEnvironmentAuthorizationDecider decider,
        string scope,
        CancellationToken cancellationToken)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return (null, unauthorized);
        var denied = await LicensingEndpoints.AuthorizeAsync(context, decider, actor!, token!, scope, cancellationToken);
        return (actor, denied);
    }
}
