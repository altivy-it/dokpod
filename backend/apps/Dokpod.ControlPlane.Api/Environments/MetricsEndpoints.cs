using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.ControlPlane.Application.Metrics;
using Dokpod.ControlPlane.Application.Queries;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Metrics;

namespace Dokpod.ControlPlane.Api.Endpoints;

/// <summary>Métricas de recursos (spec 009): agregado do estágio, uso por host e série por container.</summary>
public static class MetricsEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/metrics/overview", GetStageAsync).RequireAuthorization();
        endpoints.MapGet("/api/v1/metrics/hosts", ListHostsAsync).RequireAuthorization();
        endpoints.MapGet("/api/v1/environments/{environmentId:guid}/containers/{containerId}/metrics", GetContainerAsync).RequireAuthorization();
    }

    private static async Task<IResult> GetStageAsync(
        string? stage,
        string? window,
        string? step,
        HttpContext context,
        AuthorizedEnvironmentsService authorizedEnvironments,
        HostQueryService hostQuery,
        MetricsQueryService metrics,
        CancellationToken cancellationToken)
    {
        if (!ConsoleEndpoints.TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return unauthorized!;
        if (!TryParseStage(stage, out var parsedStage) ||
            !MetricsPolicy.TryParse(window ?? "12h", step ?? "1h", TimeSpan.FromHours(24), out var parsedWindow))
        {
            return Invalid(context);
        }

        var hosts = await LoadHostsAsync(context, authorizedEnvironments, hostQuery, actor!, token!, parsedStage, cancellationToken);
        if (hosts.Error is not null) return hosts.Error;
        var result = await metrics.GetStageAsync(hosts.Views, parsedWindow, cancellationToken);
        return Results.Ok(new
        {
            current = new
            {
                cpuPercent = result.Current.CpuPercent,
                memoryPercent = result.Current.MemoryPercent,
                hostsSampled = result.Current.HostsSampled,
                hostsTotal = result.Current.HostsTotal,
                sampledAt = result.Current.SampledAtUtc,
            },
            series = new { x = result.Series.X, cpu = result.Series.Cpu, memory = result.Series.Memory },
            operationalLimitPercent = result.OperationalLimitPercent,
        });
    }

    private static async Task<IResult> ListHostsAsync(
        string? stage,
        HttpContext context,
        AuthorizedEnvironmentsService authorizedEnvironments,
        HostQueryService hostQuery,
        MetricsQueryService metrics,
        CancellationToken cancellationToken)
    {
        if (!ConsoleEndpoints.TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return unauthorized!;
        if (!TryParseStage(stage, out var parsedStage)) return Invalid(context);
        var hosts = await LoadHostsAsync(context, authorizedEnvironments, hostQuery, actor!, token!, parsedStage, cancellationToken);
        if (hosts.Error is not null) return hosts.Error;
        return Results.Ok(new
        {
            hosts = metrics.GetHosts(hosts.Views).Select(h => new
            {
                environmentId = h.EnvironmentId,
                cpuPercent = h.CpuPercent,
                memoryUsedBytes = h.MemoryUsedBytes,
                memoryTotalBytes = h.MemoryTotalBytes,
                sampledAt = h.SampledAtUtc,
            }),
            operationalLimitPercent = metrics.OperationalLimitPercent,
        });
    }

    private static async Task<IResult> GetContainerAsync(
        Guid environmentId,
        string containerId,
        string? window,
        string? step,
        HttpContext context,
        ContainerInsightsService insights,
        MetricsQueryService metrics,
        CancellationToken cancellationToken)
    {
        if (!ConsoleEndpoints.TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return unauthorized!;
        if (!MetricsPolicy.TryParse(window ?? "30m", step ?? "1m", MetricsPolicy.ContainerSeriesWindow, out var parsedWindow)) return Invalid(context);
        var access = await insights.AuthorizeAsync(
            environmentId, containerId, EnvironmentResourceScopes.Read, actor!, token!, ConsoleEndpoints.GetCorrelationId(context), cancellationToken);
        if (access.Access != InsightAccess.Allowed)
        {
            return access.Access switch
            {
                InsightAccess.NotFound => ConsoleEndpoints.Problem(context, StatusCodes.Status404NotFound, "container_not_found", "O container não foi encontrado."),
                InsightAccess.Unavailable => ConsoleEndpoints.Problem(context, StatusCodes.Status503ServiceUnavailable, access.FailureCode ?? "authorization_unavailable", "As permissões não puderam ser avaliadas."),
                _ => ConsoleEndpoints.Problem(context, StatusCodes.Status403Forbidden, access.FailureCode ?? "authorization_denied", "A consulta de métricas não foi autorizada."),
            };
        }

        var result = metrics.GetContainer(environmentId, containerId, parsedWindow);
        var current = result.Current;
        return Results.Ok(new
        {
            current = current is null ? null : new
            {
                cpuPercent = current.CpuPercentOfLimit is { } ofLimit ? Math.Min(100, ofLimit) : current.CpuPercentOfHost,
                cpuPercentOfLimit = current.CpuPercentOfLimit,
                cpuPercentOfHost = current.CpuPercentOfHost,
                cpuLimitCores = current.CpuLimitCores,
                memoryUsedBytes = current.MemoryUsedBytes,
                memoryLimitBytes = current.MemoryLimitBytes,
                netRxBps = current.NetRxBps,
                netTxBps = current.NetTxBps,
                blkReadBps = current.BlockReadBps,
                blkWriteBps = current.BlockWriteBps,
                sampledAt = current.SampledAtUtc,
            },
            series = new { x = result.Series.X, cpu = result.Series.Cpu, memory = result.Series.Memory },
            operationalLimitPercent = result.OperationalLimitPercent,
        });
    }

    private static async Task<(IReadOnlyList<HostView> Views, IResult? Error)> LoadHostsAsync(
        HttpContext context,
        AuthorizedEnvironmentsService authorizedEnvironments,
        HostQueryService hostQuery,
        Dokpod.ControlPlane.Application.Authorization.AuthenticatedActor actor,
        string token,
        EnvironmentStage? stage,
        CancellationToken cancellationToken)
    {
        var authorized = await ConsoleEndpoints.LoadAuthorizedAsync(context, authorizedEnvironments, actor, token, cancellationToken);
        if (authorized.Error is not null) return ([], authorized.Error);
        var registrations = authorized.Registrations.Where(r => stage is null || r.Stage == stage).ToArray();
        return (await hostQuery.DescribeAsync(registrations, cancellationToken), null);
    }

    private static bool TryParseStage(string? value, out EnvironmentStage? stage)
    {
        stage = null;
        if (string.IsNullOrEmpty(value)) return true;
        if (!EnvironmentStages.TryParse(value, out var parsed)) return false;
        stage = parsed;
        return true;
    }

    private static IResult Invalid(HttpContext context) =>
        ConsoleEndpoints.Problem(context, StatusCodes.Status400BadRequest, "invalid_metrics_query", "Os parâmetros da consulta de métricas são inválidos.");
}
