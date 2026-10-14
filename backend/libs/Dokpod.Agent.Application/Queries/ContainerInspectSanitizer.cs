using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Dokpod.Agent.Application.Queries;

public sealed record SanitizedJson(byte[] Json, bool Truncated, IReadOnlyList<string> RedactedPaths);

/// <summary>
/// Sanitização no agente (spec 011 D-003): o resumo usa allowlist; a inspeção completa usa denylist,
/// oculta chaves sensíveis e passa todo valor de texto pelo <see cref="SecretMasker"/>, inclusive
/// campos desconhecidos de versões novas do engine.
/// </summary>
public static class ContainerInspectSanitizer
{
    public const int MaxInspectBytes = 256 * 1024;
    private const int MaxLabels = 50;
    private const int MaxLabelValue = 256;

    private static readonly HashSet<string> DeniedPaths = new(StringComparer.Ordinal)
    {
        "Config.Env",
        "Config.CreateCommand",
        "Config.Secrets",
        "SecretsPaths",
        "HostConfig.Binds",
        "ExecIDs",
        "GraphDriver.Data",
        "Config.Annotations",
    };

    public static SanitizedJson Inspect(JsonElement root)
    {
        var redacted = new List<string>();
        var node = Sanitize(root, string.Empty, redacted) ?? new JsonObject();
        var truncated = false;
        var bytes = Serialize(node);
        if (bytes.Length > MaxInspectBytes && node is JsonObject obj)
        {
            // Remove as maiores propriedades de topo até caber, registrando o corte.
            foreach (var property in obj.ToArray().OrderByDescending(p => p.Value?.ToJsonString().Length ?? 0))
            {
                if (bytes.Length <= MaxInspectBytes) break;
                obj[property.Key] = "<truncado>";
                redacted.Add($"{property.Key} (truncado)");
                truncated = true;
                bytes = Serialize(node);
            }
        }

        return new SanitizedJson(bytes, truncated, redacted);
    }

    public static SanitizedJson Summary(JsonElement root)
    {
        var config = Get(root, "Config");
        var host = Get(root, "HostConfig");
        var summary = new JsonObject
        {
            ["command"] = SecretMasker.Apply(CommandLine(root, config)),
            ["user"] = Text(config, "User") ?? string.Empty,
            ["workingDir"] = Text(config, "WorkingDir") ?? string.Empty,
            ["healthcheck"] = Healthcheck(config),
            ["labels"] = Labels(config),
            ["logging"] = Logging(host),
            ["networks"] = Networks(root),
            ["volumes"] = Volumes(root),
            ["restartPolicy"] = RestartPolicy(host),
            ["cpuLimitCores"] = host is { } h && h.TryGetProperty("NanoCpus", out var nano) && nano.ValueKind == JsonValueKind.Number && nano.GetInt64() > 0
                ? Math.Round(nano.GetInt64() / 1_000_000_000d, 3)
                : null,
            ["memoryLimitBytes"] = host is { } hm && hm.TryGetProperty("Memory", out var memory) && memory.ValueKind == JsonValueKind.Number && memory.GetInt64() > 0
                ? memory.GetInt64()
                : null,
            ["environmentVariableCount"] = config is { } c && c.TryGetProperty("Env", out var env) && env.ValueKind == JsonValueKind.Array ? env.GetArrayLength() : 0,
            ["secretCount"] = SecretCount(root),
        };
        return new SanitizedJson(Serialize(summary), false, ["Config.Env (somente quantidade)"]);
    }

    private static JsonNode? Sanitize(JsonElement element, string path, List<string> redacted)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var obj = new JsonObject();
                foreach (var property in element.EnumerateObject())
                {
                    var childPath = path.Length == 0 ? property.Name : $"{path}.{property.Name}";
                    var normalizedPath = NormalizeArrayPath(childPath);
                    if (DeniedPaths.Contains(normalizedPath) ||
                        (normalizedPath == "Mounts[].Source" && IsBindMount(element)) ||
                        (SecretMasker.IsSensitiveKey(property.Name) && property.Value.ValueKind is not JsonValueKind.Object and not JsonValueKind.Array && !IsStructuralKey(property.Name)))
                    {
                        obj[property.Name] = SecretMasker.Mask;
                        redacted.Add(childPath);
                        continue;
                    }

                    obj[property.Name] = Sanitize(property.Value, childPath, redacted);
                }

                return obj;
            }
            case JsonValueKind.Array:
            {
                var array = new JsonArray();
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    array.Add(Sanitize(item, $"{path}[{index++}]", redacted));
                }

                return array;
            }
            case JsonValueKind.String:
            {
                var value = element.GetString() ?? string.Empty;
                var masked = SecretMasker.Apply(value);
                if (!ReferenceEquals(masked, value) && masked != value) redacted.Add($"{path} (mascarado)");
                return masked;
            }
            case JsonValueKind.Number:
                return JsonNode.Parse(element.GetRawText());
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            default:
                return null;
        }
    }

    /// <summary>Chaves com nome sensível, mas que descrevem estrutura (ex.: `KeyMap`, `AuthConfig` vazio) continuam visíveis.</summary>
    private static bool IsStructuralKey(string name) => name is "SecurityOpt" or "Tokens";

    private static string NormalizeArrayPath(string path)
    {
        var builder = new StringBuilder(path.Length);
        var skipping = false;
        foreach (var c in path)
        {
            if (c == '[') { builder.Append("[]"); skipping = true; continue; }
            if (c == ']') { skipping = false; continue; }
            if (!skipping) builder.Append(c);
        }

        return builder.ToString();
    }

    private static bool IsBindMount(JsonElement mount) =>
        mount.TryGetProperty("Type", out var type) && type.GetString() == "bind";

    private static JsonElement? Get(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object ? value : null;

    private static string? Text(JsonElement? element, string name) =>
        element is { } e && e.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string CommandLine(JsonElement root, JsonElement? config)
    {
        var parts = new List<string>();
        if (root.TryGetProperty("Path", out var path) && path.ValueKind == JsonValueKind.String) parts.Add(path.GetString()!);
        if (root.TryGetProperty("Args", out var args) && args.ValueKind == JsonValueKind.Array)
        {
            parts.AddRange(args.EnumerateArray().Where(a => a.ValueKind == JsonValueKind.String).Select(a => a.GetString()!));
        }

        if (parts.Count == 0 && config is { } c && c.TryGetProperty("Cmd", out var cmd) && cmd.ValueKind == JsonValueKind.Array)
        {
            parts.AddRange(cmd.EnumerateArray().Where(a => a.ValueKind == JsonValueKind.String).Select(a => a.GetString()!));
        }

        var line = string.Join(' ', parts);
        return line.Length > 2048 ? line[..2048] + "…" : line;
    }

    private static JsonNode? Healthcheck(JsonElement? config)
    {
        if (config is not { } c || !c.TryGetProperty("Healthcheck", out var health) || health.ValueKind != JsonValueKind.Object) return null;
        var test = health.TryGetProperty("Test", out var t) && t.ValueKind == JsonValueKind.Array
            ? string.Join(' ', t.EnumerateArray().Select(x => x.GetString()).Where(x => x is not null and not "CMD" and not "CMD-SHELL"))
            : string.Empty;
        static double? Seconds(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.GetInt64() > 0 ? v.GetInt64() / 1_000_000_000d : null;
        return new JsonObject
        {
            ["test"] = SecretMasker.Apply(test),
            ["intervalSeconds"] = Seconds(health, "Interval"),
            ["timeoutSeconds"] = Seconds(health, "Timeout"),
            ["retries"] = health.TryGetProperty("Retries", out var r) && r.ValueKind == JsonValueKind.Number ? r.GetInt32() : null,
        };
    }

    private static JsonObject Labels(JsonElement? config)
    {
        var labels = new JsonObject();
        if (config is not { } c || !c.TryGetProperty("Labels", out var source) || source.ValueKind != JsonValueKind.Object) return labels;
        foreach (var label in source.EnumerateObject().Take(MaxLabels))
        {
            var value = label.Value.ValueKind == JsonValueKind.String ? label.Value.GetString() ?? string.Empty : string.Empty;
            value = SecretMasker.IsSensitiveKey(label.Name) ? SecretMasker.Mask : SecretMasker.Apply(value);
            labels[label.Name.Length > 128 ? label.Name[..128] : label.Name] = value.Length > MaxLabelValue ? value[..MaxLabelValue] + "…" : value;
        }

        return labels;
    }

    private static JsonObject? Logging(JsonElement? host)
    {
        if (host is not { } h || !h.TryGetProperty("LogConfig", out var log) || log.ValueKind != JsonValueKind.Object) return null;
        var options = new JsonObject();
        if (log.TryGetProperty("Config", out var config) && config.ValueKind == JsonValueKind.Object)
        {
            foreach (var option in config.EnumerateObject())
            {
                var value = option.Value.GetString() ?? string.Empty;
                options[option.Name] = SecretMasker.IsSensitiveKey(option.Name) ? SecretMasker.Mask : SecretMasker.Apply(value);
            }
        }

        return new JsonObject { ["driver"] = Text(log, "Type") ?? string.Empty, ["options"] = options };
    }

    private static JsonArray Networks(JsonElement root)
    {
        var result = new JsonArray();
        if (Get(root, "NetworkSettings") is not { } settings || Get(settings, "Networks") is not { } networks) return result;
        foreach (var network in networks.EnumerateObject())
        {
            result.Add(new JsonObject
            {
                ["name"] = network.Name,
                ["ipAddress"] = Text(network.Value, "IPAddress") ?? string.Empty,
            });
        }

        return result;
    }

    private static JsonArray Volumes(JsonElement root)
    {
        var result = new JsonArray();
        if (!root.TryGetProperty("Mounts", out var mounts) || mounts.ValueKind != JsonValueKind.Array) return result;
        foreach (var mount in mounts.EnumerateArray())
        {
            var type = Text(mount, "Type") ?? string.Empty;
            result.Add(new JsonObject
            {
                ["type"] = type,
                // Origem de bind mount não sai do host (D-003); volumes nomeados mostram o nome.
                ["name"] = type == "volume" ? Text(mount, "Name") ?? string.Empty : string.Empty,
                ["destination"] = Text(mount, "Destination") ?? string.Empty,
                ["readOnly"] = mount.TryGetProperty("RW", out var rw) && rw.ValueKind == JsonValueKind.False,
            });
        }

        return result;
    }

    private static JsonObject? RestartPolicy(JsonElement? host)
    {
        if (host is not { } h || !h.TryGetProperty("RestartPolicy", out var policy) || policy.ValueKind != JsonValueKind.Object) return null;
        return new JsonObject
        {
            ["name"] = Text(policy, "Name") ?? string.Empty,
            ["maximumRetryCount"] = policy.TryGetProperty("MaximumRetryCount", out var max) && max.ValueKind == JsonValueKind.Number ? max.GetInt32() : 0,
        };
    }

    private static int SecretCount(JsonElement root)
    {
        if (!root.TryGetProperty("Mounts", out var mounts) || mounts.ValueKind != JsonValueKind.Array) return 0;
        return mounts.EnumerateArray().Count(m => (Text(m, "Destination") ?? string.Empty).StartsWith("/run/secrets", StringComparison.Ordinal));
    }

    private static byte[] Serialize(JsonNode node) =>
        Encoding.UTF8.GetBytes(node.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
}
