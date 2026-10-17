using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.Domain.Environments;
using Microsoft.Extensions.Options;

namespace Dokpod.ControlPlane.Api.Authorization;

/// <summary>
/// Scopes concedidos ao usuário por host (spec 023): UMA com `response_mode=permissions`, que devolve de uma vez os pares
/// recurso × scope permitidos. Só recursos `urn:dokpod:environment:{guid}` e scopes suportados são aceitos.
/// </summary>
public sealed class KeycloakEnvironmentGrantReader(
    HttpClient httpClient,
    IOptions<KeycloakAuthorizationOptions> options,
    ILogger<KeycloakEnvironmentGrantReader> logger) : IEnvironmentGrantReader
{
    private const string ResourcePrefix = "urn:dokpod:environment:";

    public async Task<IReadOnlyDictionary<Guid, IReadOnlySet<string>>?> ReadAsync(
        AuthenticatedActor actor,
        string accessToken,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (string.IsNullOrWhiteSpace(accessToken)) return null;
        var stopwatch = Stopwatch.StartNew();
        var authorizationOptions = options.Value;
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{authorizationOptions.EffectiveBackchannelAuthority.TrimEnd('/')}/protocol/openid-connect/token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:uma-ticket",
            ["audience"] = authorizationOptions.Audience,
            ["response_mode"] = "permissions",
        });

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            // 403: o usuário não tem nenhuma permissão no servidor de recursos.
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden) return Log(new Dictionary<Guid, IReadOnlySet<string>>(), correlationId, stopwatch);
            if (!response.IsSuccessStatusCode) return Log(null, correlationId, stopwatch);
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return Log(Parse(document.RootElement), correlationId, stopwatch);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return Log(null, correlationId, stopwatch);
        }
    }

    /// <summary>`[{ "rsname": "urn:dokpod:environment:{guid}", "scopes": [...] }]`; entradas fora do formato são ignoradas.</summary>
    public static IReadOnlyDictionary<Guid, IReadOnlySet<string>>? Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array) return null;
        var grants = new Dictionary<Guid, HashSet<string>>();
        foreach (var item in root.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("rsname", out var name) || name.ValueKind != JsonValueKind.String ||
                name.GetString() is not { } resource || !resource.StartsWith(ResourcePrefix, StringComparison.Ordinal) ||
                !Guid.TryParse(resource[ResourcePrefix.Length..], out var environmentId))
            {
                continue;
            }

            var scopes = grants.TryGetValue(environmentId, out var existing) ? existing : grants[environmentId] = new HashSet<string>(StringComparer.Ordinal);
            if (!item.TryGetProperty("scopes", out var list) || list.ValueKind != JsonValueKind.Array) continue;
            foreach (var scope in list.EnumerateArray())
            {
                if (scope.ValueKind == JsonValueKind.String && scope.GetString() is { } value && EnvironmentResourceScopes.Supported.Contains(value)) scopes.Add(value);
            }
        }

        return grants.ToDictionary(g => g.Key, g => (IReadOnlySet<string>)g.Value);
    }

    private IReadOnlyDictionary<Guid, IReadOnlySet<string>>? Log(IReadOnlyDictionary<Guid, IReadOnlySet<string>>? grants, Guid correlationId, Stopwatch stopwatch)
    {
        stopwatch.Stop();
        logger.LogInformation(
            "Keycloak environment grants available={Available} environments={Environments} correlation_id={CorrelationId} duration_ms={DurationMs}",
            grants is not null, grants?.Count ?? 0, correlationId, stopwatch.Elapsed.TotalMilliseconds);
        return grants;
    }
}
