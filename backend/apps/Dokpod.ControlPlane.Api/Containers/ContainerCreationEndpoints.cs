using System.Text.Json;
using Dokpod.ControlPlane.Application.Containers;
using Dokpod.Domain.Containers;
using static Dokpod.ControlPlane.Api.Endpoints.ConsoleEndpoints;

namespace Dokpod.ControlPlane.Api.Endpoints;

/// <summary>Criação de container e dados do formulário (spec 013).</summary>
public static class ContainerCreationEndpoints
{
    private const int MaxBodyBytes = 4 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/environments/{environmentId:guid}/containers", CreateAsync).RequireAuthorization();
        endpoints.MapGet("/api/v1/environments/{environmentId:guid}/create-options", GetOptionsAsync).RequireAuthorization();
    }

    private static async Task<IResult> CreateAsync(
        Guid environmentId,
        HttpContext context,
        ContainerCreationService service,
        CancellationToken cancellationToken)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return unauthorized!;
        if (!Guid.TryParse(context.Request.Headers["Idempotency-Key"], out var commandId) || commandId == Guid.Empty)
        {
            return Invalid(context, [new FieldError("Idempotency-Key", "required")]);
        }

        if (context.Request.ContentLength > MaxBodyBytes)
        {
            return Invalid(context, [new FieldError("$", "too_large")]);
        }

        ContainerCreationRequest? request;
        try
        {
            // Campo fora da allowlist (privileged, capAdd, devices...) falha aqui: 400 sem efeito algum.
            request = await JsonSerializer.DeserializeAsync<ContainerCreationRequest>(context.Request.Body, Json, cancellationToken);
        }
        catch (JsonException exception)
        {
            return Invalid(context, [new FieldError(exception.Path?.TrimStart('$', '.') is { Length: > 0 } path ? path : "$", "not_allowed")]);
        }

        if (request is null) return Invalid(context, [new FieldError("$", "required")]);

        ContainerCreationResult result;
        try
        {
            result = await service.CreateAsync(environmentId, commandId, request, actor!, token!, GetCorrelationId(context), cancellationToken);
        }
        catch (ArgumentException)
        {
            return Invalid(context, [new FieldError("$", "invalid")]);
        }
        catch (InvalidOperationException)
        {
            return Problem(context, StatusCodes.Status503ServiceUnavailable, "persistence_unavailable", "O comando não pôde ser persistido.");
        }

        return result.Outcome switch
        {
            ContainerCreationOutcome.Accepted or ContainerCreationOutcome.Duplicate => Results.Accepted(value: new { commandId = result.CommandId, equivalentCommand = result.EquivalentCommand }),
            ContainerCreationOutcome.InvalidSpec => Invalid(context, result.Errors),
            ContainerCreationOutcome.RegistryNotAllowed => Problem(context, StatusCodes.Status403Forbidden, "registry_not_allowed", "O registro da imagem não é permitido para o estágio deste host."),
            ContainerCreationOutcome.AuthorizationDenied => Problem(context, StatusCodes.Status403Forbidden, result.FailureCode ?? "authorization_denied", "A criação de containers não foi autorizada neste host."),
            ContainerCreationOutcome.AuthorizationUnavailable => Problem(context, StatusCodes.Status503ServiceUnavailable, result.FailureCode ?? "authorization_unavailable", "A autorização está indisponível."),
            ContainerCreationOutcome.EnvironmentNotFound => Problem(context, StatusCodes.Status404NotFound, "environment_not_found", "O ambiente não foi encontrado."),
            ContainerCreationOutcome.EnvironmentUnavailable or ContainerCreationOutcome.AgentOffline => Problem(context, StatusCodes.Status409Conflict, result.FailureCode ?? "agent_disconnected", "O host não está disponível para criar containers."),
            ContainerCreationOutcome.ConflictingPayload => Problem(context, StatusCodes.Status409Conflict, "command_id_conflict", "A chave idempotente já identifica outro comando."),
            ContainerCreationOutcome.NotSupported => Problem(context, StatusCodes.Status422UnprocessableEntity, "not_supported", "O agente deste host não suporta criação de containers."),
            _ => throw new ArgumentOutOfRangeException(nameof(result)),
        };
    }

    private static async Task<IResult> GetOptionsAsync(
        Guid environmentId,
        HttpContext context,
        ContainerCreationService service,
        CancellationToken cancellationToken)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return unauthorized!;
        ContainerCreateOptionsResult result;
        try
        {
            result = await service.GetOptionsAsync(environmentId, actor!, token!, GetCorrelationId(context), cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return Problem(context, StatusCodes.Status503ServiceUnavailable, "persistence_unavailable", "O cadastro do ambiente está indisponível.");
        }

        if (result.Options is { } options)
        {
            return Results.Ok(new
            {
                engine = options.Engine,
                engineVersion = options.EngineVersion,
                rootless = options.Rootless,
                secrets = options.Secrets.Select(s => new { kind = s.Kind ?? "file", name = s.Name }),
                networks = options.Networks.Select(n => new { name = n.Name, driver = n.Driver }),
                volumes = options.Volumes.Select(v => new { name = v.Name }),
                allowedRegistries = options.AllowedRegistries,
                bindMountsAllowed = options.BindMountsAllowed,
                partial = options.Partial,
                limits = new
                {
                    ports = ContainerCreationSpec.MaxPorts,
                    environment = ContainerCreationSpec.MaxEnvironment,
                    volumes = ContainerCreationSpec.MaxVolumes,
                    valueBytes = ContainerCreationSpec.MaxValueBytes,
                },
            });
        }

        return result.Outcome switch
        {
            ContainerCreationOutcome.AuthorizationDenied => Problem(context, StatusCodes.Status403Forbidden, result.FailureCode ?? "authorization_denied", "A criação de containers não foi autorizada neste host."),
            ContainerCreationOutcome.AuthorizationUnavailable => Problem(context, StatusCodes.Status503ServiceUnavailable, result.FailureCode ?? "authorization_unavailable", "A autorização está indisponível."),
            ContainerCreationOutcome.EnvironmentNotFound => Problem(context, StatusCodes.Status404NotFound, "environment_not_found", "O ambiente não foi encontrado."),
            ContainerCreationOutcome.NotSupported => Problem(context, StatusCodes.Status422UnprocessableEntity, "not_supported", "O agente deste host não suporta criação de containers."),
            _ => Problem(context, StatusCodes.Status409Conflict, result.FailureCode ?? "agent_disconnected", "O agente do host está desconectado."),
        };
    }

    private static IResult Invalid(HttpContext context, IReadOnlyList<FieldError> errors) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Requisição inválida",
            detail: "A especificação do container é inválida.",
            instance: context.Request.Path,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "invalid_container_spec",
                ["traceId"] = context.TraceIdentifier,
                ["errors"] = errors.GroupBy(e => e.Field).ToDictionary(g => g.Key, g => g.Select(e => e.Code).ToArray()),
            });
}
