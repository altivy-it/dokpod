using System.Globalization;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.ControlPlane.Application.Pods;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Pods;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using static Dokpod.ControlPlane.Api.Endpoints.ConsoleEndpoints;

namespace Dokpod.ControlPlane.Api.Endpoints;

public sealed record PodActionRequest(string? Action, DateTimeOffset DeadlineUtc, string? Justification);

/// <summary>Pods do Podman, detalhe e ações sobre o pod inteiro (spec 021, contracts/pods-api.md).</summary>
public static class PodEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/pods", ListAsync).RequireAuthorization();
        endpoints.MapGet("/api/v1/environments/{environmentId:guid}/pods/{pod}", GetAsync).RequireAuthorization();
        endpoints.MapPost("/api/v1/environments/{environmentId:guid}/pods/{podId}/actions", ActAsync).RequireAuthorization();
    }

    private static async Task<IResult> ListAsync(
        HttpContext context,
        AuthorizedEnvironmentsService authorizedEnvironments,
        HostQueryService hostQuery,
        [FromServices] IPodInventoryStore store,
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

        var statuses = query["status"].Where(v => !string.IsNullOrEmpty(v)).Select(v => v!).Distinct(StringComparer.Ordinal).ToArray();
        var containerId = query["containerId"].ToString();
        var text = query["q"].ToString();
        var limit = 50;
        if (statuses.Any(s => !PodStatuses.All.Contains(s)) ||
            (containerId.Length > 0 && (containerId.Length != 64 || !containerId.All(Uri.IsHexDigit))) ||
            (text.Length > 0 && (text.Length < 2 || text.Length > 128)) ||
            (!string.IsNullOrEmpty(query["limit"]) && (!int.TryParse(query["limit"], out limit) || limit is < 1 or > 200)) ||
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
            .ToDictionary(h => h.Registration.EnvironmentId);

        PodSearchResult result;
        IReadOnlyList<PodProjectionState> projections;
        try
        {
            result = await store.SearchAsync(new PodSearchQuery(hosts.Keys.ToArray(), statuses, containerId.Length == 0 ? null : containerId.ToLowerInvariant(), text.Length == 0 ? null : text, sort, offset, limit), cancellationToken);
            projections = await store.ListProjectionsAsync(hosts.Keys.ToArray(), cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return Problem(context, StatusCodes.Status503ServiceUnavailable, "persistence_unavailable", "O inventário de pods está indisponível.");
        }

        var supported = projections.ToDictionary(p => p.EnvironmentId, p => p.PodsSupported);
        object Hosts(Func<HostView, bool> predicate) => hosts.Values.Where(predicate)
            .OrderBy(h => h.Registration.Name, StringComparer.Ordinal)
            .Select(h => new { environmentId = h.Registration.EnvironmentId, environmentName = h.Registration.Name });
        return Results.Ok(new
        {
            pods = result.Rows.Select(row => ToItem(hosts[row.EnvironmentId], row)),
            summary = new
            {
                total = result.Summary.Total,
                byStatus = result.Summary.ByStatus,
                // FR-003: hosts Docker (sem pods) e hosts sem inventário de pods, à parte.
                withoutPodsHosts = Hosts(h => supported.TryGetValue(h.Registration.EnvironmentId, out var s) && !s),
                unavailableHosts = Hosts(h => !supported.ContainsKey(h.Registration.EnvironmentId)),
            },
            nextCursor = result.HasMore ? EncodeCursor(offset + limit) : null,
        });
    }

    private static async Task<IResult> GetAsync(
        Guid environmentId,
        string pod,
        HttpContext context,
        AuthorizedEnvironmentsService authorizedEnvironments,
        HostQueryService hostQuery,
        [FromServices] IPodInventoryStore store,
        CancellationToken cancellationToken)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return unauthorized!;
        if (!PodNames.IsValidId(pod) && !PodNames.IsValidName(pod)) return Problem(context, StatusCodes.Status400BadRequest, "pod_invalid", "O identificador ou nome do pod é inválido.");
        var authorized = await LoadAuthorizedAsync(context, authorizedEnvironments, actor!, token!, cancellationToken);
        if (authorized.Error is not null) return authorized.Error;
        var registration = authorized.Registrations.FirstOrDefault(r => r.EnvironmentId == environmentId);
        if (registration is null) return NotFound(context);
        var host = (await hostQuery.DescribeAsync([registration], cancellationToken))[0];
        var row = await store.GetAsync(environmentId, pod, cancellationToken);
        return row is null ? NotFound(context) : Results.Ok(ToItem(host, row));
    }

    private static async Task<IResult> ActAsync(
        Guid environmentId,
        string podId,
        PodActionRequest request,
        HttpContext context,
        [FromServices] PodActionService service,
        CancellationToken cancellationToken)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return unauthorized!;
        if (!Guid.TryParse(context.Request.Headers["Idempotency-Key"], out var commandId) || commandId == Guid.Empty ||
            !PodNames.TryParse(request.Action, out var action) ||
            request.DeadlineUtc.Offset != TimeSpan.Zero || request.DeadlineUtc <= DateTimeOffset.UtcNow || request.DeadlineUtc > DateTimeOffset.UtcNow.AddMinutes(10))
        {
            return Problem(context, StatusCodes.Status400BadRequest, "invalid_pod_request", "A solicitação de ação no pod é inválida.");
        }

        PodActionResult result;
        try
        {
            result = await service.ExecuteAsync(environmentId, commandId, podId, action, request.DeadlineUtc, request.Justification, actor!, token!, GetCorrelationId(context), cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return Problem(context, StatusCodes.Status503ServiceUnavailable, "persistence_unavailable", "O comando não pôde ser persistido.");
        }

        return result.Outcome switch
        {
            PodActionOutcome.Accepted or PodActionOutcome.Duplicate => Results.Accepted(value: new { commandId = result.CommandId }),
            PodActionOutcome.InvalidId => Problem(context, StatusCodes.Status400BadRequest, "pod_invalid", "O identificador do pod é inválido."),
            PodActionOutcome.JustificationInvalid => Problem(context, StatusCodes.Status400BadRequest, "justification_invalid", "A justificativa deve ter entre 10 e 500 caracteres."),
            PodActionOutcome.JustificationRequired => Problem(context, StatusCodes.Status422UnprocessableEntity, "justification_required", "Informe a justificativa para esta ação (política do estágio)."),
            PodActionOutcome.AgentPod => Problem(context, StatusCodes.Status409Conflict, PodActionCodes.AgentPod, "O pod do agente não é parado, pausado nem reiniciado pelo console: o host ficaria sem agente."),
            PodActionOutcome.NotFound => Problem(context, StatusCodes.Status404NotFound, PodActionCodes.NotFound, "O pod não foi encontrado."),
            PodActionOutcome.AuthorizationDenied => Problem(context, StatusCodes.Status403Forbidden, result.FailureCode ?? "authorization_denied", "A ação não foi autorizada."),
            PodActionOutcome.AuthorizationUnavailable => Problem(context, StatusCodes.Status503ServiceUnavailable, result.FailureCode ?? "authorization_unavailable", "A autorização está indisponível."),
            PodActionOutcome.EnvironmentNotFound => Problem(context, StatusCodes.Status404NotFound, "environment_not_found", "O ambiente não foi encontrado."),
            PodActionOutcome.AgentOffline or PodActionOutcome.EnvironmentUnavailable =>
                Problem(context, StatusCodes.Status409Conflict, result.FailureCode ?? "command_unavailable", "O ambiente não está disponível para executar o comando."),
            PodActionOutcome.NotSupported => Problem(context, StatusCodes.Status409Conflict, "not_supported", "O agente do host não executa ações de pod."),
            _ => Problem(context, StatusCodes.Status409Conflict, result.FailureCode ?? "command_id_conflict", "A chave idempotente já identifica outro comando."),
        };
    }

    /// <summary>Ações oferecidas: situação compatível, scope no cadastro do host, capability, host conectado e pod do agente protegido.</summary>
    internal static IReadOnlyList<string> AllowedActions(HostView host, PodInventoryItem pod)
    {
        if (!host.Registration.Enabled || !host.Connected || !(host.Descriptor?.Capabilities.Contains("PodActions") ?? false)) return [];
        var status = pod.NormalizedStatus;
        var candidates = new List<PodActionKind>();
        if (status is not "running" and not "paused") candidates.Add(PodActionKind.Start);
        if (status is "running" or "degraded" or "paused") candidates.Add(PodActionKind.Stop);
        if (status is not "paused" and not "created") candidates.Add(PodActionKind.Restart);
        if (status is "running" or "degraded") candidates.Add(PodActionKind.Pause);
        if (status == "paused") candidates.Add(PodActionKind.Unpause);
        return candidates
            .Where(action => !(pod.AgentPod && PodNames.RefusedForAgentPod(action)))
            .Where(action => host.Registration.Scopes.Contains(PodNames.ScopeFor(action)))
            .Select(PodNames.ToValue)
            .ToArray();
    }

    private static object ToItem(HostView host, PodSearchRow row) => new
    {
        environmentId = host.Registration.EnvironmentId,
        environmentName = host.Registration.Name,
        stage = host.Registration.Stage is null ? null : EnvironmentStages.ToValue(host.Registration.Stage.Value),
        hostConnected = host.Connected,
        podId = row.Pod.PodId,
        name = row.Pod.Name,
        status = row.Pod.NormalizedStatus,
        createdAt = row.Pod.CreatedAtUtc,
        labels = row.Pod.Labels,
        networks = row.Pod.Networks,
        infraId = row.Pod.InfraId,
        containers = row.Pod.Containers.Select(c => new { containerId = c.ContainerId, name = c.Name, status = c.Status.ToLowerInvariant(), restarts = c.Restarts, infra = c.Infra }),
        runningContainers = row.Pod.RunningContainers,
        agentPod = row.Pod.AgentPod,
        truncated = row.Pod.Truncated,
        observedAt = row.ObservedAtUtc,
        allowedActions = AllowedActions(host, row.Pod),
    };

    private static bool TryParseSort(string? value, out PodSort sort)
    {
        sort = value switch
        {
            null or "" or "name" => PodSort.NameAsc,
            "-name" => PodSort.NameDesc,
            "createdAt" => PodSort.CreatedAtAsc,
            "-createdAt" => PodSort.CreatedAtDesc,
            "environment" => PodSort.Environment,
            _ => (PodSort)(-1),
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
        Problem(context, StatusCodes.Status404NotFound, "not_found", "Pod não encontrado.");

    private static IResult Invalid(HttpContext context) =>
        Problem(context, StatusCodes.Status400BadRequest, "invalid_pod_query", "Os parâmetros da consulta são inválidos.");
}
