using System.Globalization;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.ControlPlane.Application.Images;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Images;
using Microsoft.AspNetCore.WebUtilities;
using static Dokpod.ControlPlane.Api.Endpoints.ConsoleEndpoints;

namespace Dokpod.ControlPlane.Api.Endpoints;

public sealed record PullImageRequest(string? Reference, DateTimeOffset DeadlineUtc);

/// <summary>Inventário de imagens, consulta por referência e pull por host (spec 012).</summary>
public static class ImageEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/images", ListImagesAsync).RequireAuthorization();
        endpoints.MapGet("/api/v1/environments/{environmentId:guid}/images/lookup", LookupImageAsync).RequireAuthorization();
        endpoints.MapPost("/api/v1/environments/{environmentId:guid}/images/pull", PullImageAsync).RequireAuthorization();
    }

    private static async Task<IResult> ListImagesAsync(
        HttpContext context,
        AuthorizedEnvironmentsService authorizedEnvironments,
        HostQueryService hostQuery,
        IImageInventoryStore store,
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
        if (engines.Any(e => e is not ("docker" or "podman")) ||
            (text.Length > 0 && (text.Length < 2 || text.Length > 128)) ||
            (!string.IsNullOrEmpty(query["limit"]) && (!int.TryParse(query["limit"], out limit) || limit is < 1 or > 200)) ||
            (!string.IsNullOrEmpty(query["unused"]) && !bool.TryParse(query["unused"], out unused)) ||
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

        ImageSearchResult result;
        try
        {
            result = await store.SearchAsync(
                new ImageSearchQuery(hosts.Keys.ToArray(), unused, text.Length == 0 ? null : text, sort, offset, limit),
                cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return Problem(context, StatusCodes.Status503ServiceUnavailable, "persistence_unavailable", "O inventário de imagens está indisponível.");
        }

        return Results.Ok(new
        {
            images = result.Rows.Select(row => ToItem(hosts[row.EnvironmentId], row)),
            summary = new
            {
                total = result.Summary.Total,
                unused = result.Summary.Unused,
                totalSizeBytes = result.Summary.TotalSizeBytes,
                partial = result.Summary.Partial,
            },
            nextCursor = result.HasMore ? EncodeCursor(offset + limit) : null,
        });
    }

    private static object ToItem(HostView host, ImageSearchRow row)
    {
        var first = row.Image.RepoTags.FirstOrDefault();
        ImageReference? reference = null;
        if (first is not null) ImageReference.TryParse(first, out reference);
        return new
        {
            environmentId = host.Registration.EnvironmentId,
            environmentName = host.Registration.Name,
            stage = host.Registration.Stage is null ? null : EnvironmentStages.ToValue(host.Registration.Stage.Value),
            engine = host.Descriptor?.EngineKind,
            imageId = row.Image.ImageId,
            repository = reference?.Name,
            tag = reference?.Tag,
            repoTags = row.Image.RepoTags,
            repoDigests = row.Image.RepoDigests,
            sizeBytes = row.Image.SizeBytes,
            createdAt = row.Image.CreatedAtUtc,
            architecture = row.Image.Architecture,
            os = row.Image.Os,
            truncated = row.Image.Truncated,
            containers = row.Containers,
            inUse = row.Containers > 0,
            observedAt = row.ObservedAtUtc,
            canPull = CanPull(host),
        };
    }

    internal static bool CanPull(HostView host) =>
        host.Registration.Enabled && host.Connected &&
        host.Registration.Scopes.Contains(EnvironmentResourceScopes.PullImage) &&
        (host.Descriptor?.Capabilities.Contains("ImagePull") ?? false);

    private static async Task<IResult> LookupImageAsync(
        Guid environmentId,
        string? reference,
        HttpContext context,
        IEnvironmentAuthorizationDecider authorizationDecider,
        IImageInventoryStore store,
        CancellationToken cancellationToken)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return unauthorized!;
        if (!ImageReference.TryParse(reference, out var parsed))
        {
            return Problem(context, StatusCodes.Status400BadRequest, "invalid_reference", "A referência da imagem é inválida.");
        }

        EnvironmentAuthorizationDecision decision;
        try
        {
            decision = await authorizationDecider.DecideAsync(
                $"urn:dokpod:environment:{environmentId:D}",
                EnvironmentResourceScopes.Read,
                actor!,
                token!,
                GetCorrelationId(context),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            decision = new EnvironmentAuthorizationDecision(AuthorizationDecisionOutcome.Indeterminate, "authorization_unavailable");
        }

        if (!decision.Allowed)
        {
            return decision.Outcome == AuthorizationDecisionOutcome.Indeterminate
                ? Problem(context, StatusCodes.Status503ServiceUnavailable, decision.FailureCode ?? "authorization_unavailable", "As permissões não puderam ser avaliadas.")
                : Problem(context, StatusCodes.Status403Forbidden, decision.FailureCode ?? "authorization_denied", "A consulta de imagens não foi autorizada.");
        }

        try
        {
            var found = await store.LookupAsync(environmentId, parsed, cancellationToken);
            return Results.Ok(new
            {
                reference = parsed.Canonical,
                mutableTag = parsed.IsMutableTag,
                present = found.Present,
                imageId = found.ImageId,
                sizeBytes = found.SizeBytes,
                createdAt = found.CreatedAtUtc,
                digest = found.Digest,
            });
        }
        catch (InvalidOperationException)
        {
            return Problem(context, StatusCodes.Status503ServiceUnavailable, "persistence_unavailable", "O inventário de imagens está indisponível.");
        }
    }

    private static async Task<IResult> PullImageAsync(
        Guid environmentId,
        PullImageRequest request,
        HttpContext context,
        ImagePullService pullService,
        CancellationToken cancellationToken)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return unauthorized!;
        if (!Guid.TryParse(context.Request.Headers["Idempotency-Key"], out var commandId) || commandId == Guid.Empty ||
            request.DeadlineUtc.Offset != TimeSpan.Zero || request.DeadlineUtc <= DateTimeOffset.UtcNow ||
            request.DeadlineUtc > DateTimeOffset.UtcNow.AddMinutes(30))
        {
            return Problem(context, StatusCodes.Status400BadRequest, "invalid_pull_request", "A solicitação de download é inválida.");
        }

        ImagePullResult result;
        try
        {
            result = await pullService.PullAsync(
                environmentId, commandId, request.Reference ?? string.Empty, request.DeadlineUtc,
                actor!, token!, GetCorrelationId(context), cancellationToken);
        }
        catch (ArgumentException)
        {
            return Problem(context, StatusCodes.Status400BadRequest, "invalid_pull_request", "A solicitação de download é inválida.");
        }
        catch (InvalidOperationException)
        {
            return Problem(context, StatusCodes.Status503ServiceUnavailable, "persistence_unavailable", "O comando não pôde ser persistido.");
        }

        return result.Outcome switch
        {
            ImagePullOutcome.Accepted or ImagePullOutcome.Duplicate => Results.Accepted(value: new { commandId = result.CommandId, reference = result.Reference }),
            ImagePullOutcome.InvalidReference => Problem(context, StatusCodes.Status400BadRequest, "invalid_reference", "A referência da imagem é inválida."),
            ImagePullOutcome.RegistryNotAllowed => Problem(context, StatusCodes.Status403Forbidden, "registry_not_allowed", "O registro não é permitido para o estágio deste host."),
            ImagePullOutcome.AuthorizationDenied => Problem(context, StatusCodes.Status403Forbidden, result.FailureCode ?? "authorization_denied", "O download não foi autorizado."),
            ImagePullOutcome.AuthorizationUnavailable => Problem(context, StatusCodes.Status503ServiceUnavailable, result.FailureCode ?? "authorization_unavailable", "A autorização do download está indisponível."),
            ImagePullOutcome.EnvironmentNotFound => Problem(context, StatusCodes.Status404NotFound, "environment_not_found", "O ambiente não foi encontrado."),
            ImagePullOutcome.EnvironmentUnavailable or ImagePullOutcome.AgentOffline => Problem(context, StatusCodes.Status409Conflict, result.FailureCode ?? "agent_disconnected", "O host não está disponível para baixar a imagem."),
            ImagePullOutcome.ConflictingPayload => Problem(context, StatusCodes.Status409Conflict, "command_id_conflict", "A chave idempotente já identifica outro comando."),
            ImagePullOutcome.NotSupported => Problem(context, StatusCodes.Status422UnprocessableEntity, "not_supported", "O agente deste host não suporta download de imagens."),
            _ => throw new ArgumentOutOfRangeException(nameof(result)),
        };
    }

    private static bool TryParseSort(string? value, out ImageSort sort)
    {
        sort = value switch
        {
            null or "" or "name" => ImageSort.NameAsc,
            "-name" => ImageSort.NameDesc,
            "size" => ImageSort.SizeAsc,
            "-size" => ImageSort.SizeDesc,
            "createdAt" => ImageSort.CreatedAtAsc,
            "-createdAt" => ImageSort.CreatedAtDesc,
            _ => (ImageSort)(-1),
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

    private static IResult Invalid(HttpContext context) =>
        Problem(context, StatusCodes.Status400BadRequest, "invalid_image_query", "Os parâmetros da consulta são inválidos.");
}
