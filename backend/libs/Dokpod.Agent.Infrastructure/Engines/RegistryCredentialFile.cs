using System.Text;
using System.Text.Json;

namespace Dokpod.Agent.Infrastructure.Engines;

/// <summary>
/// Credenciais de registro lidas de um `config.json` do host (formato Docker, campo `auths`), indicadas por
/// `DOKPOD_AGENT_REGISTRY_AUTH_FILE` (spec 012). As credenciais só vão do agente ao engine local no
/// cabeçalho `X-Registry-Auth`; nunca trafegam ao plano de controle nem são registradas.
/// </summary>
public sealed class RegistryCredentialFile(string? path)
{
    private static readonly string[] DockerHubKeys = ["https://index.docker.io/v1/", "index.docker.io", "docker.io", "registry-1.docker.io"];

    /// <summary>
    /// Registros com credencial configurada (spec 020): só as chaves de `auths`, normalizadas; os valores nunca são lidos
    /// para fora deste método nem para fora do host.
    /// </summary>
    public IReadOnlyList<string> ListRegistries()
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return [];
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("auths", out var auths) || auths.ValueKind != JsonValueKind.Object) return [];
            return auths.EnumerateObject()
                .Where(entry => entry.Value.ValueKind == JsonValueKind.Object && entry.Value.TryGetProperty("auth", out _))
                .Select(entry => Dokpod.Domain.Images.RegistryNames.Normalize(entry.Name))
                .OfType<string>()
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>Valor do cabeçalho `X-Registry-Auth` para o registro, ou nulo sem credencial configurada.</summary>
    public string? HeaderFor(string registry)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("auths", out var auths) || auths.ValueKind != JsonValueKind.Object) return null;
            var keys = registry == "docker.io" ? DockerHubKeys : [registry, $"https://{registry}", $"http://{registry}"];
            foreach (var key in keys)
            {
                if (!auths.TryGetProperty(key, out var entry) || !entry.TryGetProperty("auth", out var auth)) continue;
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(auth.GetString() ?? string.Empty));
                var separator = decoded.IndexOf(':');
                if (separator <= 0) return null;
                var json = JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    ["username"] = decoded[..separator],
                    ["password"] = decoded[(separator + 1)..],
                    ["serveraddress"] = registry,
                });
                return Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).Replace('+', '-').Replace('/', '_');
            }
        }
        catch (Exception exception) when (exception is JsonException or FormatException or IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return null;
    }
}
