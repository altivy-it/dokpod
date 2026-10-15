using Dokpod.ControlPlane.Application.Alerts;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.ControlPlane.Application.Registries;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Images;
using Microsoft.AspNetCore.Mvc;
using static Dokpod.ControlPlane.Api.Endpoints.ConsoleEndpoints;

namespace Dokpod.ControlPlane.Api.Endpoints;

public sealed record RegistryPolicyRequest(IReadOnlyList<string>? Registries);

/// <summary>Registros dos hosts e política de registros permitidos por estágio (spec 020, contracts/registries-api.md).</summary>
public static class RegistryEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/registries", ListAsync).RequireAuthorization();
        endpoints.MapGet("/api/v1/registries/{registry}", GetAsync).RequireAuthorization();
        endpoints.MapGet("/api/v1/registry-policies", PoliciesAsync).RequireAuthorization();
        endpoints.MapGet("/api/v1/registry-policies/{stage}/impact", ImpactAsync).RequireAuthorization();
        endpoints.MapPut("/api/v1/registry-policies/{stage}", SaveAsync).RequireAuthorization();
    }

    private static async Task<IResult> ListAsync(
        HttpContext context,
        AuthorizedEnvironmentsService authorizedEnvironments,
        HostQueryService hostQuery,
        [FromServices] RegistryCatalog catalog,
        [FromServices] IRegistryInventoryStore store,
        CancellationToken cancellationToken)
    {
        var query = context.Request.Query;
        var origins = query["origin"].Where(v => !string.IsNullOrEmpty(v)).Select(v => v!).ToArray();
        var text = query["q"].ToString();
        var limit = 50;
        var insecure = false;
        var outside = false;
        var offset = 0;
        if (origins.Any(o => !RegistryOrigins.All.Contains(o)) ||
            (text.Length > 0 && (text.Length < 2 || text.Length > 128)) ||
            (!string.IsNullOrEmpty(query["limit"]) && (!int.TryParse(query["limit"], out limit) || limit is < 1 or > 200)) ||
            (!string.IsNullOrEmpty(query["insecure"]) && !bool.TryParse(query["insecure"], out insecure)) ||
            (!string.IsNullOrEmpty(query["outsidePolicy"]) && !bool.TryParse(query["outsidePolicy"], out outside)) ||
            (!string.IsNullOrEmpty(query["cursor"]) && (!int.TryParse(query["cursor"], out offset) || offset is < 0 or > 100_000)) ||
            !TryParseSort(query["sort"], out var sort))
        {
            return Invalid(context);
        }

        var (hosts, error) = await HostsAsync(context, authorizedEnvironments, hostQuery, cancellationToken);
        if (error is not null) return error;
        IReadOnlyList<RegistryHostEntry> entries;
        IReadOnlySet<Guid> available;
        try
        {
            entries = await catalog.EntriesAsync(hosts!, cancellationToken);
            available = await store.ListConfigAvailableAsync(hosts!.Select(h => h.Registration.EnvironmentId).ToArray(), cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return Problem(context, StatusCodes.Status503ServiceUnavailable, "persistence_unavailable", "O inventário de registros está indisponível.");
        }

        var page = RegistryCatalog.Page(entries, new RegistryQuery(origins, insecure, outside, text.Length == 0 ? null : text, sort, offset, limit));
        return Results.Ok(new
        {
            registries = page.Rows.Select(r => new
            {
                registry = r.Registry,
                hosts = r.Hosts,
                origins = r.Origins,
                images = r.Images,
                sizeBytes = r.SizeBytes,
                insecureHosts = r.InsecureHosts,
                credentialHosts = r.CredentialHosts,
                blockedHosts = r.BlockedHosts,
                outsidePolicyHosts = r.OutsidePolicyHosts,
                mirrorOf = r.MirrorOf,
            }),
            summary = new
            {
                total = page.Summary.Total,
                insecure = page.Summary.Insecure,
                outsidePolicy = page.Summary.OutsidePolicy,
                // FR-009: hosts cujo engine não informou a configuração (registros só pelas imagens).
                unavailableHosts = hosts!
                    .Where(h => !available.Contains(h.Registration.EnvironmentId))
                    .OrderBy(h => h.Registration.Name, StringComparer.Ordinal)
                    .Select(h => new { environmentId = h.Registration.EnvironmentId, environmentName = h.Registration.Name }),
            },
            nextCursor = page.HasMore ? (offset + limit).ToString(System.Globalization.CultureInfo.InvariantCulture) : null,
        });
    }

    private static async Task<IResult> GetAsync(
        string registry,
        HttpContext context,
        AuthorizedEnvironmentsService authorizedEnvironments,
        HostQueryService hostQuery,
        [FromServices] RegistryCatalog catalog,
        CancellationToken cancellationToken)
    {
        if (!RegistryNames.IsValid(registry)) return Problem(context, StatusCodes.Status400BadRequest, "registry_invalid", "O registro é inválido.");
        var (hosts, error) = await HostsAsync(context, authorizedEnvironments, hostQuery, cancellationToken);
        if (error is not null) return error;
        var entries = (await catalog.EntriesAsync(hosts!, cancellationToken)).Where(e => e.Registry == registry).ToArray();
        if (entries.Length == 0) return Problem(context, StatusCodes.Status404NotFound, "not_found", "Registro não encontrado.");
        return Results.Ok(new
        {
            registry,
            hosts = entries.OrderBy(e => e.Host.Registration.Name, StringComparer.Ordinal).Select(e => new
            {
                environmentId = e.Host.Registration.EnvironmentId,
                environmentName = e.Host.Registration.Name,
                stage = e.Host.Registration.Stage is null ? null : EnvironmentStages.ToValue(e.Host.Registration.Stage.Value),
                hostConnected = e.Host.Connected,
                configAvailable = e.ConfigAvailable,
                origins = e.Origins,
                credential = e.Credential,
                insecure = e.Insecure,
                blocked = e.Blocked,
                search = e.Search,
                mirrors = e.Mirrors,
                mirrorOf = e.MirrorOf,
                images = e.Images,
                sizeBytes = e.SizeBytes,
                allowed = e.Allowed,
            }),
        });
    }

    private static async Task<IResult> PoliciesAsync(
        HttpContext context,
        AuthorizedEnvironmentsService authorizedEnvironments,
        [FromServices] RegistryPolicyService service,
        CancellationToken cancellationToken)
    {
        var (caller, error) = await CallerAsync(context, authorizedEnvironments, cancellationToken);
        if (error is not null) return error;
        var policies = await service.ListAsync(caller!, cancellationToken);
        return Results.Ok(new
        {
            policies = policies.Select(p => new
            {
                stage = RegistryPolicyStages.Key(p.Stage),
                registries = p.Registries,
                source = p.FromConsole ? "console" : "configuration",
                updatedBy = p.UpdatedBy,
                updatedAt = p.UpdatedAt,
                canEdit = p.CanEdit,
            }),
        });
    }

    private static async Task<IResult> ImpactAsync(
        string stage,
        HttpContext context,
        AuthorizedEnvironmentsService authorizedEnvironments,
        HostQueryService hostQuery,
        [FromServices] RegistryCatalog catalog,
        CancellationToken cancellationToken)
    {
        if (!RegistryPolicyStages.TryParse(stage, out var parsed)) return Problem(context, StatusCodes.Status400BadRequest, "stage", "Estágio inválido.");
        if (RegistryPolicyService.Normalize(context.Request.Query["registries"].Select(v => v ?? string.Empty).ToArray()) is not { } proposed)
        {
            return Problem(context, StatusCodes.Status400BadRequest, "registry_invalid", "Há registro inválido na lista.");
        }

        var (hosts, error) = await HostsAsync(context, authorizedEnvironments, hostQuery, cancellationToken);
        if (error is not null) return error;
        var impact = RegistryCatalog.Impact(await catalog.EntriesAsync(hosts!, cancellationToken), parsed, proposed);
        return Results.Ok(new { images = impact.Images, hosts = impact.Hosts });
    }

    private static async Task<IResult> SaveAsync(
        string stage,
        RegistryPolicyRequest request,
        HttpContext context,
        AuthorizedEnvironmentsService authorizedEnvironments,
        [FromServices] RegistryPolicyService service,
        CancellationToken cancellationToken)
    {
        if (!RegistryPolicyStages.TryParse(stage, out var parsed)) return Problem(context, StatusCodes.Status400BadRequest, "stage", "Estágio inválido.");
        var (caller, error) = await CallerAsync(context, authorizedEnvironments, cancellationToken);
        if (error is not null) return error;
        RegistryPolicyOutcome outcome;
        try
        {
            outcome = await service.SaveAsync(caller!, parsed, request.Registries, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return Problem(context, StatusCodes.Status503ServiceUnavailable, "persistence_unavailable", "A política não pôde ser gravada.");
        }

        return outcome switch
        {
            RegistryPolicyOutcome.Saved => Results.NoContent(),
            RegistryPolicyOutcome.Invalid => Problem(context, StatusCodes.Status400BadRequest, "registry_invalid", "Informe registros como host ou host:porta, sem esquema, caminho ou credencial (até 100)."),
            _ => Problem(context, StatusCodes.Status403Forbidden, "authorization_denied", "Alterar a política exige administração de todos os hosts do estágio."),
        };
    }

    private static async Task<(IReadOnlyList<HostView>? Hosts, IResult? Error)> HostsAsync(
        HttpContext context,
        AuthorizedEnvironmentsService authorizedEnvironments,
        HostQueryService hostQuery,
        CancellationToken cancellationToken)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return (null, unauthorized);
        var query = context.Request.Query;
        EnvironmentStage? stage = null;
        if (!string.IsNullOrEmpty(query["stage"]))
        {
            if (!EnvironmentStages.TryParse(query["stage"], out var parsedStage)) return (null, Invalid(context));
            stage = parsedStage;
        }

        var environmentIds = new List<Guid>();
        foreach (var value in query["environmentId"])
        {
            if (!Guid.TryParse(value, out var id)) return (null, Invalid(context));
            environmentIds.Add(id);
        }

        var authorized = await LoadAuthorizedAsync(context, authorizedEnvironments, actor!, token!, cancellationToken);
        if (authorized.Error is not null) return (null, authorized.Error);
        var hosts = (await hostQuery.DescribeAsync(authorized.Registrations, cancellationToken))
            .Where(h => h.Registration.Enabled)
            .Where(h => stage is null || h.Registration.Stage == stage)
            .Where(h => environmentIds.Count == 0 || environmentIds.Contains(h.Registration.EnvironmentId))
            .ToArray();
        return (hosts, null);
    }

    private static async Task<(AlertCaller? Caller, IResult? Error)> CallerAsync(HttpContext context, AuthorizedEnvironmentsService authorized, CancellationToken ct)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return (null, unauthorized);
        var loaded = await LoadAuthorizedAsync(context, authorized, actor!, token!, ct);
        return loaded.Error is not null ? (null, loaded.Error) : (new AlertCaller(actor!, token!, GetCorrelationId(context), loaded.Registrations), null);
    }

    private static bool TryParseSort(string? value, out RegistrySort sort)
    {
        sort = value switch
        {
            null or "" or "registry" => RegistrySort.RegistryAsc,
            "-registry" => RegistrySort.RegistryDesc,
            "images" => RegistrySort.ImagesAsc,
            "-images" => RegistrySort.ImagesDesc,
            "hosts" => RegistrySort.HostsAsc,
            "-hosts" => RegistrySort.HostsDesc,
            _ => (RegistrySort)(-1),
        };
        return Enum.IsDefined(sort);
    }

    private static IResult Invalid(HttpContext context) =>
        Problem(context, StatusCodes.Status400BadRequest, "invalid_registry_query", "Os parâmetros da consulta são inválidos.");
}
