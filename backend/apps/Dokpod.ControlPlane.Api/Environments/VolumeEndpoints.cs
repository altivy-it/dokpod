using System.Globalization;
using System.Text.Json;
using Dokpod.Agent.Contracts.V1;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.ControlPlane.Application.Queries;
using Dokpod.ControlPlane.Application.Volumes;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Volumes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using static Dokpod.ControlPlane.Api.Endpoints.ConsoleEndpoints;

namespace Dokpod.ControlPlane.Api.Endpoints;

public sealed record RemoveVolumeRequest(DateTimeOffset DeadlineUtc, string? Justification);

/// <summary>Inventário de volumes, detalhe, uso sob demanda e remoção de volume sem uso (spec 018, contracts/volumes-api.md).</summary>
public static class VolumeEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/volumes", ListAsync).RequireAuthorization();
        endpoints.MapGet("/api/v1/environments/{environmentId:guid}/volumes/{name}", GetAsync).RequireAuthorization();
        endpoints.MapGet("/api/v1/environments/{environmentId:guid}/volumes/{name}/usage", UsageAsync).RequireAuthorization();
        endpoints.MapPost("/api/v1/environments/{environmentId:guid}/volumes/{name}/remove", RemoveAsync).RequireAuthorization();
    }

    private static async Task<IResult> ListAsync(
        HttpContext context,
        AuthorizedEnvironmentsService authorizedEnvironments,
        HostQueryService hostQuery,
        [FromServices] IVolumeInventoryStore store,
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
        var text = query["q"].ToString();
        var limit = 50;
        var unused = false;
        bool? anonymous = null;
        if (engines.Any(e => e is not ("docker" or "podman")) ||
            (text.Length > 0 && (text.Length < 2 || text.Length > 128)) ||
            (!string.IsNullOrEmpty(query["limit"]) && (!int.TryParse(query["limit"], out limit) || limit is < 1 or > 200)) ||
            (!string.IsNullOrEmpty(query["unused"]) && !bool.TryParse(query["unused"], out unused)) ||
            !TryParseAnonymous(query["anonymous"], out anonymous) ||
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

        VolumeSearchResult result;
        IReadOnlySet<Guid> projected;
        try
        {
            result = await store.SearchAsync(new VolumeSearchQuery(hosts.Keys.ToArray(), unused, anonymous, text.Length == 0 ? null : text, sort, offset, limit), cancellationToken);
            projected = await store.ListProjectedAsync(hosts.Keys.ToArray(), cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return Problem(context, StatusCodes.Status503ServiceUnavailable, "persistence_unavailable", "O inventário de volumes está indisponível.");
        }

        return Results.Ok(new
        {
            volumes = result.Rows.Select(row => ToItem(hosts[row.EnvironmentId], row)),
            summary = new
            {
                total = result.Summary.Total,
                unused = result.Summary.Unused,
                // FR-010: hosts sem inventário de volumes (agente sem a capability ou ainda sem snapshot).
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
        string name,
        HttpContext context,
        AuthorizedEnvironmentsService authorizedEnvironments,
        HostQueryService hostQuery,
        [FromServices] IVolumeInventoryStore store,
        CancellationToken cancellationToken)
    {
        var (host, error) = await HostAsync(environmentId, name, context, authorizedEnvironments, hostQuery, cancellationToken);
        if (error is not null) return error;
        var row = await store.GetAsync(environmentId, name, cancellationToken);
        return row is null ? NotFound(context) : Results.Ok(ToItem(host!, row));
    }

    private static async Task<IResult> UsageAsync(
        Guid environmentId,
        string name,
        HttpContext context,
        AuthorizedEnvironmentsService authorizedEnvironments,
        HostQueryService hostQuery,
        AgentQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var (_, error) = await HostAsync(environmentId, name, context, authorizedEnvironments, hostQuery, cancellationToken);
        if (error is not null) return error;
        var outcome = await dispatcher.QueryAsync(environmentId, ContainerQueryKind.VolumeUsage, name, null, cancellationToken, TimeSpan.FromSeconds(30));
        if (outcome.Kind != QueryOutcomeKind.Ok)
        {
            return outcome.Kind switch
            {
                QueryOutcomeKind.AgentDisconnected => Problem(context, StatusCodes.Status409Conflict, "agent_disconnected", "O agente do host está desconectado."),
                QueryOutcomeKind.NotSupported => Problem(context, StatusCodes.Status409Conflict, "not_supported", "O agente do host não informa o uso de volumes."),
                QueryOutcomeKind.TooManyRequests => Problem(context, StatusCodes.Status429TooManyRequests, "too_many_requests", "Muitas consultas ao agente; tente de novo."),
                _ => Problem(context, StatusCodes.Status504GatewayTimeout, outcome.FailureCode ?? "agent_timeout", "O agente não respondeu a tempo."),
            };
        }

        using var document = JsonDocument.Parse(outcome.Json);
        long? Number(string property) =>
            document.RootElement.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt64() : null;
        return Results.Ok(new { sizeBytes = Number("sizeBytes"), refCount = Number("refCount") });
    }

    private static async Task<IResult> RemoveAsync(
        Guid environmentId,
        string name,
        RemoveVolumeRequest request,
        HttpContext context,
        [FromServices] VolumeRemovalService service,
        CancellationToken cancellationToken)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return unauthorized!;
        if (!Guid.TryParse(context.Request.Headers["Idempotency-Key"], out var commandId) || commandId == Guid.Empty ||
            request.DeadlineUtc.Offset != TimeSpan.Zero || request.DeadlineUtc <= DateTimeOffset.UtcNow || request.DeadlineUtc > DateTimeOffset.UtcNow.AddMinutes(10))
        {
            return Problem(context, StatusCodes.Status400BadRequest, "invalid_volume_request", "A solicitação de remoção é inválida.");
        }

        VolumeRemovalResult result;
        try
        {
            result = await service.RemoveAsync(environmentId, commandId, name, request.DeadlineUtc, request.Justification, actor!, token!, GetCorrelationId(context), cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return Problem(context, StatusCodes.Status503ServiceUnavailable, "persistence_unavailable", "O comando não pôde ser persistido.");
        }

        return result.Outcome switch
        {
            VolumeRemovalOutcome.Accepted or VolumeRemovalOutcome.Duplicate => Results.Accepted(value: new { commandId = result.CommandId }),
            VolumeRemovalOutcome.InvalidName => Problem(context, StatusCodes.Status400BadRequest, "volume_name_invalid", "O nome do volume é inválido."),
            VolumeRemovalOutcome.JustificationInvalid => Problem(context, StatusCodes.Status400BadRequest, "justification_invalid", "A justificativa deve ter entre 10 e 500 caracteres."),
            VolumeRemovalOutcome.JustificationRequired => Problem(context, StatusCodes.Status422UnprocessableEntity, "justification_required", "Informe a justificativa para remover o volume (política do estágio)."),
            VolumeRemovalOutcome.InUse => Problem(context, StatusCodes.Status409Conflict, VolumeRemovalCodes.InUse, "O volume está em uso por um container."),
            VolumeRemovalOutcome.NotFound => Problem(context, StatusCodes.Status404NotFound, VolumeRemovalCodes.NotFound, "O volume não foi encontrado."),
            VolumeRemovalOutcome.AuthorizationDenied => Problem(context, StatusCodes.Status403Forbidden, result.FailureCode ?? "authorization_denied", "A remoção não foi autorizada."),
            VolumeRemovalOutcome.AuthorizationUnavailable => Problem(context, StatusCodes.Status503ServiceUnavailable, result.FailureCode ?? "authorization_unavailable", "A autorização está indisponível."),
            VolumeRemovalOutcome.EnvironmentNotFound => Problem(context, StatusCodes.Status404NotFound, "environment_not_found", "O ambiente não foi encontrado."),
            VolumeRemovalOutcome.AgentOffline or VolumeRemovalOutcome.EnvironmentUnavailable =>
                Problem(context, StatusCodes.Status409Conflict, result.FailureCode ?? "command_unavailable", "O ambiente não está disponível para executar o comando."),
            VolumeRemovalOutcome.NotSupported => Problem(context, StatusCodes.Status409Conflict, "not_supported", "O agente do host não remove volumes."),
            _ => Problem(context, StatusCodes.Status409Conflict, result.FailureCode ?? "command_id_conflict", "A chave idempotente já identifica outro comando."),
        };
    }

    /// <summary>Host legível (`environment:read`) e nome válido; host sem leitura é indistinguível de inexistente.</summary>
    private static async Task<(HostView? Host, IResult? Error)> HostAsync(
        Guid environmentId,
        string name,
        HttpContext context,
        AuthorizedEnvironmentsService authorizedEnvironments,
        HostQueryService hostQuery,
        CancellationToken cancellationToken)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return (null, unauthorized);
        if (!VolumeNames.IsValid(name)) return (null, Problem(context, StatusCodes.Status400BadRequest, "volume_name_invalid", "O nome do volume é inválido."));
        var authorized = await LoadAuthorizedAsync(context, authorizedEnvironments, actor!, token!, cancellationToken);
        if (authorized.Error is not null) return (null, authorized.Error);
        var registration = authorized.Registrations.FirstOrDefault(r => r.EnvironmentId == environmentId);
        if (registration is null) return (null, NotFound(context));
        return ((await hostQuery.DescribeAsync([registration], cancellationToken))[0], null);
    }

    private static object ToItem(HostView host, VolumeSearchRow row) => new
    {
        environmentId = host.Registration.EnvironmentId,
        environmentName = host.Registration.Name,
        stage = host.Registration.Stage is null ? null : EnvironmentStages.ToValue(host.Registration.Stage.Value),
        engine = host.Descriptor?.EngineKind,
        hostConnected = host.Connected,
        name = row.Volume.Name,
        anonymous = row.Volume.Anonymous,
        driver = row.Volume.Driver,
        scope = row.Volume.Scope,
        createdAt = row.Volume.CreatedAtUtc,
        labels = row.Volume.Labels,
        mounts = row.Volume.Mounts.Select(m => new { containerId = m.ContainerId, containerName = m.ContainerName, destination = m.Destination, readOnly = m.ReadOnly }),
        inUse = row.Volume.Mounts.Count > 0,
        truncated = row.Volume.Truncated,
        observedAt = row.ObservedAtUtc,
        canRemove = CanRemove(host) && row.Volume.Mounts.Count == 0,
    };

    internal static bool CanRemove(HostView host) =>
        host.Registration.Enabled && host.Connected &&
        host.Registration.Scopes.Contains(EnvironmentResourceScopes.DeleteVolume) &&
        (host.Descriptor?.Capabilities.Contains("VolumeRemove") ?? false);

    private static bool TryParseAnonymous(string? value, out bool? anonymous)
    {
        anonymous = null;
        if (string.IsNullOrEmpty(value)) return true;
        if (!bool.TryParse(value, out var parsed)) return false;
        anonymous = parsed;
        return true;
    }

    private static bool TryParseSort(string? value, out VolumeSort sort)
    {
        sort = value switch
        {
            null or "" or "name" => VolumeSort.NameAsc,
            "-name" => VolumeSort.NameDesc,
            "createdAt" => VolumeSort.CreatedAtAsc,
            "-createdAt" => VolumeSort.CreatedAtDesc,
            "environment" => VolumeSort.Environment,
            _ => (VolumeSort)(-1),
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
        Problem(context, StatusCodes.Status404NotFound, "not_found", "Volume não encontrado.");

    private static IResult Invalid(HttpContext context) =>
        Problem(context, StatusCodes.Status400BadRequest, "invalid_volume_query", "Os parâmetros da consulta são inválidos.");
}
