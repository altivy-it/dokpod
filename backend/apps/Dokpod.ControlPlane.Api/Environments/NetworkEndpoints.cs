using System.Globalization;
using System.Text.RegularExpressions;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.ControlPlane.Application.Networks;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Networks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using static Dokpod.ControlPlane.Api.Endpoints.ConsoleEndpoints;

namespace Dokpod.ControlPlane.Api.Endpoints;

public sealed record RemoveNetworkRequest(DateTimeOffset DeadlineUtc, string? Justification);

/// <summary>Inventário de redes, detalhe e remoção de rede sem uso (spec 019, contracts/networks-api.md).</summary>
public static partial class NetworkEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/networks", ListAsync).RequireAuthorization();
        endpoints.MapGet("/api/v1/environments/{environmentId:guid}/networks/{network}", GetAsync).RequireAuthorization();
        endpoints.MapPost("/api/v1/environments/{environmentId:guid}/networks/{networkId}/remove", RemoveAsync).RequireAuthorization();
    }

    private static async Task<IResult> ListAsync(
        HttpContext context,
        AuthorizedEnvironmentsService authorizedEnvironments,
        HostQueryService hostQuery,
        [FromServices] INetworkInventoryStore store,
        CancellationToken cancellationToken)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return unauthorized!;
        var query = context.Request.Query;
        EnvironmentStage? stage = null;
        if (!string.IsNullOrEmpty(query["stage"]))
        {
            if (!EnvironmentStages.TryParse(query["stage"], out var parsedStage)) return Invalid(context);
            stage = parsedStage;
        }

        var environmentIds = new List<Guid>();
        foreach (var value in query["environmentId"])
        {
            if (!Guid.TryParse(value, out var id)) return Invalid(context);
            environmentIds.Add(id);
        }

        var engines = query["engine"].Where(v => !string.IsNullOrEmpty(v)).Select(v => v!).ToArray();
        var drivers = query["driver"].Where(v => !string.IsNullOrEmpty(v)).Select(v => v!).Distinct(StringComparer.Ordinal).ToArray();
        var text = query["q"].ToString();
        var limit = 50;
        var unused = false;
        bool? system = null;
        if (engines.Any(e => e is not ("docker" or "podman")) ||
            drivers.Length > 20 || drivers.Any(d => !DriverPattern().IsMatch(d)) ||
            (text.Length > 0 && (text.Length < 2 || text.Length > 128)) ||
            (!string.IsNullOrEmpty(query["limit"]) && (!int.TryParse(query["limit"], out limit) || limit is < 1 or > 200)) ||
            (!string.IsNullOrEmpty(query["unused"]) && !bool.TryParse(query["unused"], out unused)) ||
            !TryParseOptionalBool(query["system"], out system) ||
            !TryParseSort(query["sort"], out var sort) ||
            !TryDecodeCursor(query["cursor"], out var offset))
        {
            return Invalid(context);
        }

        var authorized = await LoadAuthorizedAsync(context, authorizedEnvironments, actor!, token!, cancellationToken);
        if (authorized.Error is not null) return authorized.Error;
        var hosts = (await hostQuery.DescribeAsync(authorized.Registrations, cancellationToken))
            .Where(h => h.Registration.Enabled)
            .Where(h => stage is null || h.Registration.Stage == stage)
            .Where(h => environmentIds.Count == 0 || environmentIds.Contains(h.Registration.EnvironmentId))
            .Where(h => engines.Length == 0 || (h.Descriptor is not null && engines.Contains(h.Descriptor.EngineKind)))
            .ToDictionary(h => h.Registration.EnvironmentId);

        NetworkSearchResult result;
        IReadOnlySet<Guid> projected;
        var inventoryHosts = hosts.Values.Where(h => h.Publishes("NetworkInventory")).Select(h => h.Registration.EnvironmentId).ToArray();
        try
        {
            result = await store.SearchAsync(new NetworkSearchQuery(inventoryHosts, unused, system, drivers, text.Length == 0 ? null : text, sort, offset, limit), cancellationToken);
            projected = await store.ListProjectedAsync(inventoryHosts, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return Problem(context, StatusCodes.Status503ServiceUnavailable, "persistence_unavailable", "O inventário de redes está indisponível.");
        }

        return Results.Ok(new
        {
            networks = result.Rows.Select(row => ToItem(hosts[row.EnvironmentId], row)),
            summary = new
            {
                total = result.Summary.Total,
                unused = result.Summary.Unused,
                // FR-010: hosts sem inventário de redes (agente sem a capability ou ainda sem snapshot).
                unavailableHosts = hosts.Values
                    .Where(h => !projected.Contains(h.Registration.EnvironmentId))
                    .OrderBy(h => h.Registration.Name, StringComparer.Ordinal)
                    .Select(h => new { environmentId = h.Registration.EnvironmentId, environmentName = h.Registration.Name }),
            },
            nextCursor = result.HasMore ? EncodeCursor(offset + limit) : null,
        });
    }

    private static async Task<IResult> GetAsync(
        Guid environmentId,
        string network,
        HttpContext context,
        AuthorizedEnvironmentsService authorizedEnvironments,
        HostQueryService hostQuery,
        [FromServices] INetworkInventoryStore store,
        CancellationToken cancellationToken)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return unauthorized!;
        if (!NetworkNames.IsValidId(network) && !NetworkNames.IsValidName(network))
        {
            return Problem(context, StatusCodes.Status400BadRequest, "network_invalid", "O identificador ou nome da rede é inválido.");
        }

        var authorized = await LoadAuthorizedAsync(context, authorizedEnvironments, actor!, token!, cancellationToken);
        if (authorized.Error is not null) return authorized.Error;
        // Host sem leitura é indistinguível de inexistente.
        var registration = authorized.Registrations.FirstOrDefault(r => r.EnvironmentId == environmentId);
        if (registration is null) return NotFound(context);
        var host = (await hostQuery.DescribeAsync([registration], cancellationToken))[0];
        var row = await store.GetAsync(environmentId, network, cancellationToken);
        return row is null ? NotFound(context) : Results.Ok(ToItem(host, row));
    }

    private static async Task<IResult> RemoveAsync(
        Guid environmentId,
        string networkId,
        RemoveNetworkRequest request,
        HttpContext context,
        [FromServices] NetworkRemovalService service,
        CancellationToken cancellationToken)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return unauthorized!;
        if (!Guid.TryParse(context.Request.Headers["Idempotency-Key"], out var commandId) || commandId == Guid.Empty ||
            request.DeadlineUtc.Offset != TimeSpan.Zero || request.DeadlineUtc <= DateTimeOffset.UtcNow || request.DeadlineUtc > DateTimeOffset.UtcNow.AddMinutes(10))
        {
            return Problem(context, StatusCodes.Status400BadRequest, "invalid_network_request", "A solicitação de remoção é inválida.");
        }

        NetworkRemovalResult result;
        try
        {
            result = await service.RemoveAsync(environmentId, commandId, networkId, request.DeadlineUtc, request.Justification, actor!, token!, GetCorrelationId(context), cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return Problem(context, StatusCodes.Status503ServiceUnavailable, "persistence_unavailable", "O comando não pôde ser persistido.");
        }

        return result.Outcome switch
        {
            NetworkRemovalOutcome.Accepted or NetworkRemovalOutcome.Duplicate => Results.Accepted(value: new { commandId = result.CommandId }),
            NetworkRemovalOutcome.InvalidId => Problem(context, StatusCodes.Status400BadRequest, "network_invalid", "O identificador da rede é inválido."),
            NetworkRemovalOutcome.JustificationInvalid => Problem(context, StatusCodes.Status400BadRequest, "justification_invalid", "A justificativa deve ter entre 10 e 500 caracteres."),
            NetworkRemovalOutcome.JustificationRequired => Problem(context, StatusCodes.Status422UnprocessableEntity, "justification_required", "Informe a justificativa para remover a rede (política do estágio)."),
            NetworkRemovalOutcome.InUse => Problem(context, StatusCodes.Status409Conflict, NetworkRemovalCodes.InUse, "A rede tem containers conectados."),
            NetworkRemovalOutcome.System => Problem(context, StatusCodes.Status409Conflict, NetworkRemovalCodes.System, "Redes do sistema não são removidas pelo console."),
            NetworkRemovalOutcome.NotLocal => Problem(context, StatusCodes.Status409Conflict, NetworkRemovalCodes.NotLocal, "Redes de cluster não são removidas pelo console."),
            NetworkRemovalOutcome.NotFound => Problem(context, StatusCodes.Status404NotFound, NetworkRemovalCodes.NotFound, "A rede não foi encontrada."),
            NetworkRemovalOutcome.AuthorizationDenied => Problem(context, StatusCodes.Status403Forbidden, result.FailureCode ?? "authorization_denied", "A remoção não foi autorizada."),
            NetworkRemovalOutcome.AuthorizationUnavailable => Problem(context, StatusCodes.Status503ServiceUnavailable, result.FailureCode ?? "authorization_unavailable", "A autorização está indisponível."),
            NetworkRemovalOutcome.EnvironmentNotFound => Problem(context, StatusCodes.Status404NotFound, "environment_not_found", "O ambiente não foi encontrado."),
            NetworkRemovalOutcome.AgentOffline or NetworkRemovalOutcome.EnvironmentUnavailable =>
                Problem(context, StatusCodes.Status409Conflict, result.FailureCode ?? "command_unavailable", "O ambiente não está disponível para executar o comando."),
            NetworkRemovalOutcome.NotSupported => Problem(context, StatusCodes.Status409Conflict, "not_supported", "O agente do host não remove redes."),
            _ => Problem(context, StatusCodes.Status409Conflict, result.FailureCode ?? "command_id_conflict", "A chave idempotente já identifica outro comando."),
        };
    }

    private static object ToItem(HostView host, NetworkSearchRow row) => new
    {
        environmentId = host.Registration.EnvironmentId,
        environmentName = host.Registration.Name,
        stage = host.Registration.Stage is null ? null : EnvironmentStages.ToValue(host.Registration.Stage.Value),
        engine = host.Descriptor?.EngineKind,
        hostConnected = host.Connected,
        networkId = row.Network.NetworkId,
        name = row.Network.Name,
        driver = row.Network.Driver,
        scope = row.Network.Scope,
        @internal = row.Network.Internal,
        ipv6 = row.Network.IPv6,
        attachable = row.Network.Attachable,
        system = row.Network.System,
        createdAt = row.Network.CreatedAtUtc,
        subnets = row.Network.Subnets.Select(s => new { subnet = s.Subnet, gateway = s.Gateway }),
        labels = row.Network.Labels,
        containers = row.Network.Containers.Select(c => new { containerId = c.ContainerId, containerName = c.ContainerName, ipv4 = c.IPv4Address, ipv6 = c.IPv6Address }),
        inUse = row.Network.InUse,
        truncated = row.Network.Truncated,
        observedAt = row.ObservedAtUtc,
        canRemove = CanRemove(host) && !row.Network.InUse && !row.Network.System && row.Network.Local,
    };

    internal static bool CanRemove(HostView host) =>
        host.Registration.Enabled && host.Connected &&
        host.Registration.Scopes.Contains(EnvironmentResourceScopes.DeleteNetwork) &&
        (host.Descriptor?.Capabilities.Contains("NetworkRemove") ?? false);

    [GeneratedRegex("^[a-z0-9][a-z0-9_.:/-]{0,63}$")]
    private static partial Regex DriverPattern();

    private static bool TryParseOptionalBool(string? value, out bool? result)
    {
        result = null;
        if (string.IsNullOrEmpty(value)) return true;
        if (!bool.TryParse(value, out var parsed)) return false;
        result = parsed;
        return true;
    }

    private static bool TryParseSort(string? value, out NetworkSort sort)
    {
        sort = value switch
        {
            null or "" or "name" => NetworkSort.NameAsc,
            "-name" => NetworkSort.NameDesc,
            "createdAt" => NetworkSort.CreatedAtAsc,
            "-createdAt" => NetworkSort.CreatedAtDesc,
            "environment" => NetworkSort.Environment,
            _ => (NetworkSort)(-1),
        };
        return Enum.IsDefined(sort);
    }

    private static bool TryDecodeCursor(string? cursor, out int offset)
    {
        offset = 0;
        if (string.IsNullOrWhiteSpace(cursor)) return true;
        try
        {
            var text = System.Text.Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(cursor));
            return text.StartsWith("o:", StringComparison.Ordinal) &&
                int.TryParse(text[2..], NumberStyles.None, CultureInfo.InvariantCulture, out offset) && offset <= 100_000;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string EncodeCursor(int offset) =>
        WebEncoders.Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes($"o:{offset.ToString(CultureInfo.InvariantCulture)}"));

    private static IResult NotFound(HttpContext context) =>
        Problem(context, StatusCodes.Status404NotFound, "not_found", "Rede não encontrada.");

    private static IResult Invalid(HttpContext context) =>
        Problem(context, StatusCodes.Status400BadRequest, "invalid_network_query", "Os parâmetros da consulta são inválidos.");
}
