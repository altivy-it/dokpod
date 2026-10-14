using System.Net;
using System.Net.Sockets;
using System.IO.Pipes;
using System.Text.Json;
using Dokpod.Agent.Application.Engines;
using Dokpod.Domain.Commands;

namespace Dokpod.Agent.Infrastructure.Engines.Docker;

public sealed class DockerEngineClient(HttpClient httpClient, RegistryCredentialFile? registryCredentials = null, EngineRawConnector? rawConnector = null) : IContainerEngine
{
    private const int MaximumResponseBytes = 8 * 1024 * 1024;
    private static readonly Version MaximumApiVersion = new(1, 47);
    private readonly SemaphoreSlim negotiationLock = new(1, 1);
    private const int InspectConcurrency = 8;
    private string? apiVersion;
    private string? engineVersion;
    private string engineKind = "docker";

    public static HttpClient CreateUnixSocketClient(string socketPath, TimeSpan timeout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(socketPath);
        if (!Path.IsPathFullyQualified(socketPath))
        {
            throw new ArgumentException("The Docker socket path must be absolute.", nameof(socketPath));
        }

        var normalizedSocketPath = Path.GetFullPath(socketPath);
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectCallback = async (_, cancellationToken) =>
            {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                try
                {
                    await socket.ConnectAsync(new UnixDomainSocketEndPoint(normalizedSocketPath), cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            },
        };

        return new HttpClient(handler)
        {
            BaseAddress = new Uri("http://docker/", UriKind.Absolute),
            Timeout = timeout,
        };
    }

    public static HttpClient CreateNamedPipeClient(string pipeName, TimeSpan timeout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectCallback = async (_, cancellationToken) =>
            {
                var pipe = new NamedPipeClientStream(
                    ".",
                    pipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);
                try
                {
                    await pipe.ConnectAsync(cancellationToken);
                    return pipe;
                }
                catch
                {
                    pipe.Dispose();
                    throw;
                }
            },
        };

        return new HttpClient(handler)
        {
            BaseAddress = new Uri("http://docker/", UriKind.Absolute),
            Timeout = timeout,
        };
    }

    public async Task<EngineDescriptor> InspectAsync(CancellationToken cancellationToken)
    {
        await EnsureNegotiatedAsync(cancellationToken);
        return new EngineDescriptor(engineVersion!, apiVersion!);
    }

    public async Task<IReadOnlyList<EngineContainer>> ListContainersAsync(CancellationToken cancellationToken)
    {
        await EnsureNegotiatedAsync(cancellationToken);
        using var response = await httpClient.GetAsync(
            $"v{apiVersion}/containers/json?all=1",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var content = await ReadLimitedAsync(response, cancellationToken);
        using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
        var containers = new List<EngineContainer>();

        foreach (var item in document.RootElement.EnumerateArray())
        {
            var id = item.GetProperty("Id").GetString() ?? throw new InvalidDataException("Docker returned an empty container ID.");
            var name = item.TryGetProperty("Names", out var names) && names.GetArrayLength() > 0
                ? names[0].GetString()?.TrimStart('/') ?? string.Empty
                : string.Empty;
            var image = item.GetProperty("Image").GetString() ?? string.Empty;
            var state = item.GetProperty("State").GetString() ?? "unknown";
            var created = item.GetProperty("Created").GetInt64();
            containers.Add(new EngineContainer(id, name, image, state, $"{id}:{created}")
            {
                CreatedAtUtc = DateTimeOffset.FromUnixTimeSeconds(created),
                Health = ParseHealthFromStatus(item.TryGetProperty("Status", out var status) ? status.GetString() : null),
                Ports = ParseListPorts(item),
                ImageId = item.TryGetProperty("ImageID", out var imageId) ? imageId.GetString() : null,
            });
        }

        return await EnrichAsync(containers, cancellationToken);
    }

    /// <summary>Completa início, saúde, reinícios, código de saída e OOM por inspeção (concorrência limitada).</summary>
    private async Task<IReadOnlyList<EngineContainer>> EnrichAsync(
        List<EngineContainer> containers,
        CancellationToken cancellationToken)
    {
        var enriched = new EngineContainer[containers.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, containers.Count),
            new ParallelOptions { MaxDegreeOfParallelism = InspectConcurrency, CancellationToken = cancellationToken },
            async (index, token) =>
            {
                var container = containers[index];
                enriched[index] = container;
                if (container.ContainerId.Length != 64 || container.ContainerId.Any(c => !Uri.IsHexDigit(c))) return;
                try
                {
                    using var response = await httpClient.GetAsync(
                        $"v{apiVersion}/containers/{container.ContainerId}/json",
                        HttpCompletionOption.ResponseHeadersRead,
                        token);
                    if (!response.IsSuccessStatusCode) return;
                    await using var content = await ReadLimitedAsync(response, token);
                    using var document = await JsonDocument.ParseAsync(content, cancellationToken: token);
                    var root = document.RootElement;
                    if (!root.TryGetProperty("State", out var state) || state.ValueKind != JsonValueKind.Object) return;
                    enriched[index] = container with
                    {
                        StartedAtUtc = ParseEngineTime(state, "StartedAt"),
                        Health = state.TryGetProperty("Health", out var health) && health.ValueKind == JsonValueKind.Object &&
                            health.TryGetProperty("Status", out var healthStatus)
                            ? NormalizeHealth(healthStatus.GetString())
                            : container.Health ?? "none",
                        RestartCount = root.TryGetProperty("RestartCount", out var restarts) && restarts.ValueKind == JsonValueKind.Number
                            ? restarts.GetInt32()
                            : null,
                        ExitCode = state.TryGetProperty("ExitCode", out var exit) && exit.ValueKind == JsonValueKind.Number &&
                            !string.Equals(container.State, "running", StringComparison.OrdinalIgnoreCase)
                            ? exit.GetInt32()
                            : null,
                        OomKilled = state.TryGetProperty("OOMKilled", out var oom) && oom.ValueKind is JsonValueKind.True or JsonValueKind.False
                            ? oom.GetBoolean()
                            : null,
                        CpuLimitCores = CpuLimit(root),
                    };
                }
                catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidDataException or KeyNotFoundException or InvalidOperationException or FormatException)
                {
                    // Inspeção é complementar: a listagem básica continua válida.
                }
            });
        return enriched;
    }

    public async Task<EngineHostInfo?> DescribeHostAsync(CancellationToken cancellationToken)
    {
        await EnsureNegotiatedAsync(cancellationToken);
        using var response = await httpClient.GetAsync($"v{apiVersion}/info", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new EngineHostInfo(engineKind, null, null, null, null);
        }

        await using var content = await ReadLimitedAsync(response, cancellationToken);
        using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var rootless = false;
        if (root.TryGetProperty("SecurityOptions", out var options) && options.ValueKind == JsonValueKind.Array)
        {
            rootless = options.EnumerateArray().Any(option => option.GetString()?.Contains("rootless", StringComparison.OrdinalIgnoreCase) == true);
        }

        return new EngineHostInfo(
            engineKind,
            root.TryGetProperty("Name", out var name) ? name.GetString() : null,
            root.TryGetProperty("NCPU", out var cpus) && cpus.ValueKind == JsonValueKind.Number ? cpus.GetInt32() : null,
            root.TryGetProperty("MemTotal", out var memory) && memory.ValueKind == JsonValueKind.Number ? memory.GetInt64() : null,
            rootless);
    }

    public async Task<IReadOnlyList<EngineImage>> ListImagesAsync(CancellationToken cancellationToken)
    {
        await EnsureNegotiatedAsync(cancellationToken);
        using var response = await httpClient.GetAsync($"v{apiVersion}/images/json", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var content = await ReadLimitedAsync(response, cancellationToken);
        using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
        var images = new List<EngineImage>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var id = item.TryGetProperty("Id", out var idElement) ? idElement.GetString() : null;
            if (string.IsNullOrEmpty(id)) continue;
            images.Add(new EngineImage(
                id,
                Strings(item, "RepoTags").Where(tag => tag != "<none>:<none>").ToArray(),
                Strings(item, "RepoDigests").Where(digest => digest != "<none>@<none>").ToArray(),
                item.TryGetProperty("Size", out var size) && size.ValueKind == JsonValueKind.Number ? size.GetInt64() : 0,
                item.TryGetProperty("Created", out var created) && created.ValueKind == JsonValueKind.Number ? DateTimeOffset.FromUnixTimeSeconds(created.GetInt64()) : null));
        }

        return images;

        static IEnumerable<string> Strings(JsonElement item, string property) =>
            item.TryGetProperty(property, out var array) && array.ValueKind == JsonValueKind.Array
                ? array.EnumerateArray().Select(value => value.GetString()).Where(value => !string.IsNullOrEmpty(value)).Select(value => value!)
                : [];
    }

    /// <summary>
    /// Pull por `/images/create` com nome e tag/digest codificados como parâmetros (nunca concatenados ao caminho);
    /// lê o stream de progresso até o fim e mapeia erros para os códigos do contrato da 012.
    /// </summary>
    public async Task<ImagePullResult> PullImageAsync(Dokpod.Domain.Images.ImageReference reference, CancellationToken cancellationToken)
    {
        await EnsureNegotiatedAsync(cancellationToken);
        var tag = reference.Digest ?? reference.Tag ?? "latest";
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"v{apiVersion}/images/create?fromImage={Uri.EscapeDataString(reference.Name)}&tag={Uri.EscapeDataString(tag)}");
        var auth = registryCredentials?.HeaderFor(reference.Registry);
        if (auth is not null) request.Headers.TryAddWithoutValidation("X-Registry-Auth", auth);

        string? error = null;
        using (var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            if (!response.IsSuccessStatusCode)
            {
                await using var failure = await ReadLimitedAsync(response, cancellationToken);
                using var reader = new StreamReader(failure);
                return new ImagePullResult(false, MapPullFailure((int)response.StatusCode, await reader.ReadToEndAsync(cancellationToken)));
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var lines = new StreamReader(stream);
            while (await lines.ReadLineAsync(cancellationToken) is { } line)
            {
                if (line.Length == 0 || line.Length > 64 * 1024) continue;
                try
                {
                    using var progress = JsonDocument.Parse(line);
                    if (progress.RootElement.TryGetProperty("error", out var message)) error = message.GetString() ?? "erro";
                }
                catch (JsonException)
                {
                }
            }
        }

        if (error is not null) return new ImagePullResult(false, MapPullFailure(200, error));

        using var inspect = await httpClient.GetAsync($"v{apiVersion}/images/{Uri.EscapeDataString(reference.Canonical)}/json", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!inspect.IsSuccessStatusCode) return new ImagePullResult(true, null, reference.Digest);
        await using var content = await ReadLimitedAsync(inspect, cancellationToken);
        using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var digest = reference.Digest;
        if (digest is null && root.TryGetProperty("RepoDigests", out var digests) && digests.ValueKind == JsonValueKind.Array)
        {
            foreach (var candidate in digests.EnumerateArray().Select(d => d.GetString()))
            {
                if (Dokpod.Domain.Images.ImageReference.TryParse(candidate, out var parsed) && parsed.Name == reference.Name)
                {
                    digest = parsed.Digest;
                    break;
                }
            }
        }

        return new ImagePullResult(true, null, digest, root.TryGetProperty("Size", out var size) && size.ValueKind == JsonValueKind.Number ? size.GetInt64() : null);
    }

    public async Task<string> GetEngineKindAsync(CancellationToken cancellationToken)
    {
        await EnsureNegotiatedAsync(cancellationToken);
        return engineKind;
    }

    public async Task<EngineImageConfig> InspectImageConfigAsync(Dokpod.Domain.Images.ImageReference reference, CancellationToken cancellationToken)
    {
        await EnsureNegotiatedAsync(cancellationToken);
        using var response = await httpClient.GetAsync($"v{apiVersion}/images/{Uri.EscapeDataString(reference.Canonical)}/json", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return new EngineImageConfig(false, []);
        response.EnsureSuccessStatusCode();
        await using var content = await ReadLimitedAsync(response, cancellationToken);
        using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
        var volumes = document.RootElement.TryGetProperty("Config", out var config) && config.ValueKind == JsonValueKind.Object &&
            config.TryGetProperty("Volumes", out var declared) && declared.ValueKind == JsonValueKind.Object
                ? declared.EnumerateObject().Select(v => v.Name).ToArray()
                : [];
        return new EngineImageConfig(true, volumes);
    }

    public async Task<EngineContainer?> FindByLabelAsync(string label, string value, CancellationToken cancellationToken)
    {
        await EnsureNegotiatedAsync(cancellationToken);
        var filters = JsonSerializer.Serialize(new Dictionary<string, string[]> { ["label"] = [$"{label}={value}"] });
        using var response = await httpClient.GetAsync($"v{apiVersion}/containers/json?all=true&filters={Uri.EscapeDataString(filters)}", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var content = await ReadLimitedAsync(response, cancellationToken);
        using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var id = item.GetProperty("Id").GetString();
            if (id is { Length: 64 }) return await InspectContainerAsync(id, cancellationToken);
        }

        return null;
    }

    /// <summary>
    /// API compatível (`/containers/create`) para Docker e Podman; no Podman, segredos do Podman e health check com
    /// reinício exigem `/libpod/containers/create` (spec 013 D-003). O corpo é montado só a partir da allowlist.
    /// </summary>
    public async Task<EngineCreateResult> CreateContainerAsync(EngineCreateRequest request, CancellationToken cancellationToken)
    {
        await EnsureNegotiatedAsync(cancellationToken);
        var spec = request.Spec;
        var libpod = engineKind == "podman" &&
            (request.PodmanSecrets.Count > 0 || spec.HealthCheck?.OnFailure == Dokpod.Domain.Containers.HealthCheckOnFailure.Restart);
        if (!libpod && request.PodmanSecrets.Count > 0) return new EngineCreateResult(null, "unsupported_option");
        if (engineKind != "podman" && spec.HealthCheck?.OnFailure == Dokpod.Domain.Containers.HealthCheckOnFailure.Restart)
        {
            return new EngineCreateResult(null, "unsupported_option");
        }

        var body = libpod ? LibpodCreateBody(request) : CompatCreateBody(request);
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            libpod ? "v4.0.0/libpod/containers/create" : $"v{apiVersion}/containers/create?name={Uri.EscapeDataString(spec.Name)}")
        {
            Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json"),
        };
        using var response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await using var content = await ReadLimitedAsync(response, cancellationToken);
        using var reader = new StreamReader(content);
        var text = await reader.ReadToEndAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new EngineCreateResult(null, MapCreateFailure((int)response.StatusCode, text));
        }

        using var document = JsonDocument.Parse(text);
        var id = document.RootElement.TryGetProperty("Id", out var idElement) ? idElement.GetString() : null;
        return id is { Length: 64 } ? new EngineCreateResult(id, null) : new EngineCreateResult(null, "engine_result_unknown");
    }

    public async Task<string?> StartCreatedContainerAsync(string containerId, CancellationToken cancellationToken)
    {
        ValidateContainerId(containerId);
        await EnsureNegotiatedAsync(cancellationToken);
        using var response = await httpClient.PostAsync($"v{apiVersion}/containers/{containerId}/start", null, cancellationToken);
        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotModified) return null;
        await using var content = await ReadLimitedAsync(response, cancellationToken);
        using var reader = new StreamReader(content);
        return MapStartFailure(await reader.ReadToEndAsync(cancellationToken));
    }

    public async Task RemoveCreatedContainerAsync(string containerId, CancellationToken cancellationToken)
    {
        ValidateContainerId(containerId);
        await EnsureNegotiatedAsync(cancellationToken);
        using var response = await httpClient.DeleteAsync($"v{apiVersion}/containers/{containerId}?v=0&force=1", cancellationToken);
    }

    public async Task<IReadOnlyList<EngineHostObject>> ListHostObjectsAsync(EngineHostObjectKind kind, CancellationToken cancellationToken)
    {
        await EnsureNegotiatedAsync(cancellationToken);
        if (kind == EngineHostObjectKind.PodmanSecrets && engineKind != "podman") return [];
        var path = kind switch
        {
            EngineHostObjectKind.PodmanSecrets => "v4.0.0/libpod/secrets/json",
            EngineHostObjectKind.Networks => $"v{apiVersion}/networks",
            _ => $"v{apiVersion}/volumes",
        };
        using var response = await httpClient.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var content = await ReadLimitedAsync(response, cancellationToken);
        using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var items = kind == EngineHostObjectKind.Volumes && root.ValueKind == JsonValueKind.Object && root.TryGetProperty("Volumes", out var volumes) && volumes.ValueKind == JsonValueKind.Array
            ? volumes.EnumerateArray()
            : root.ValueKind == JsonValueKind.Array ? root.EnumerateArray() : default;
        var result = new List<EngineHostObject>();
        foreach (var item in items)
        {
            var name = kind == EngineHostObjectKind.PodmanSecrets
                ? item.TryGetProperty("Spec", out var specElement) && specElement.TryGetProperty("Name", out var secretName) ? secretName.GetString() : null
                : item.TryGetProperty("Name", out var objectName) ? objectName.GetString() : null;
            var driver = item.TryGetProperty("Driver", out var driverElement) ? driverElement.GetString() : null;
            if (!string.IsNullOrEmpty(name) && name.Length <= 128) result.Add(new EngineHostObject(name, driver));
            if (result.Count >= 500) break;
        }

        return result;
    }

    private static System.Text.Json.Nodes.JsonObject Labels(EngineCreateRequest request) => new()
    {
        [Dokpod.Domain.Containers.ContainerCreationSpec.CommandLabel] = request.CommandId.ToString("D"),
        [Dokpod.Domain.Containers.ContainerCreationSpec.CreatedByLabel] = "dokpod",
    };

    private static long Nanoseconds(int seconds) => seconds * 1_000_000_000L;

    private static System.Text.Json.Nodes.JsonObject CompatCreateBody(EngineCreateRequest request)
    {
        var spec = request.Spec;
        var exposed = new System.Text.Json.Nodes.JsonObject();
        var bindings = new System.Text.Json.Nodes.JsonObject();
        foreach (var port in spec.Ports)
        {
            var key = $"{port.ContainerPort}/{port.Protocol}";
            exposed[key] = new System.Text.Json.Nodes.JsonObject();
            if (bindings[key] is not System.Text.Json.Nodes.JsonArray list)
            {
                list = [];
                bindings[key] = list;
            }

            list.Add(new System.Text.Json.Nodes.JsonObject
            {
                ["HostIp"] = port.HostIp ?? string.Empty,
                ["HostPort"] = port.HostPort == 0 ? string.Empty : port.HostPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            });
        }

        var mounts = new System.Text.Json.Nodes.JsonArray();
        foreach (var volume in spec.Volumes)
        {
            mounts.Add(new System.Text.Json.Nodes.JsonObject
            {
                ["Type"] = volume.Kind == Dokpod.Domain.Containers.VolumeKind.Bind ? "bind" : "volume",
                ["Source"] = volume.Source,
                ["Target"] = volume.Target,
                ["ReadOnly"] = volume.ReadOnly,
            });
        }

        var hostConfig = new System.Text.Json.Nodes.JsonObject
        {
            ["PortBindings"] = bindings,
            ["Mounts"] = mounts,
            ["AutoRemove"] = spec.AutoRemove,
            ["RestartPolicy"] = new System.Text.Json.Nodes.JsonObject
            {
                ["Name"] = RestartName(spec.Restart.Name),
                ["MaximumRetryCount"] = spec.Restart.Name == Dokpod.Domain.Containers.RestartPolicyKind.OnFailure ? spec.Restart.MaximumRetryCount : 0,
            },
        };
        if (spec.CpuLimitCores is { } cpu) hostConfig["NanoCpus"] = (long)Math.Round(cpu * 1_000_000_000);
        if (spec.MemoryLimitBytes is { } memory) hostConfig["Memory"] = memory;
        if (spec.Network is { } network) hostConfig["NetworkMode"] = network;

        var body = new System.Text.Json.Nodes.JsonObject
        {
            ["Image"] = spec.Image.Canonical,
            ["Env"] = new System.Text.Json.Nodes.JsonArray(request.Environment.Select(e => (System.Text.Json.Nodes.JsonNode?)System.Text.Json.Nodes.JsonValue.Create($"{e.Key}={e.Value}")).ToArray()),
            ["Labels"] = Labels(request),
            ["ExposedPorts"] = exposed,
            ["HostConfig"] = hostConfig,
        };
        if (spec.HealthCheck is { } health)
        {
            body["Healthcheck"] = new System.Text.Json.Nodes.JsonObject
            {
                ["Test"] = new System.Text.Json.Nodes.JsonArray(health.Test.Select(t => (System.Text.Json.Nodes.JsonNode?)System.Text.Json.Nodes.JsonValue.Create(t)).ToArray()),
                ["Interval"] = Nanoseconds(health.IntervalSeconds),
                ["Timeout"] = Nanoseconds(health.TimeoutSeconds),
                ["Retries"] = health.Retries,
                ["StartPeriod"] = Nanoseconds(health.StartPeriodSeconds),
            };
        }

        return body;
    }

    /// <summary>SpecGenerator mínimo do Podman (Libpod) para `secret_env` e `health_check_on_failure_action`.</summary>
    private static System.Text.Json.Nodes.JsonObject LibpodCreateBody(EngineCreateRequest request)
    {
        var spec = request.Spec;
        var env = new System.Text.Json.Nodes.JsonObject();
        foreach (var (name, value) in request.Environment) env[name] = value;
        var secrets = new System.Text.Json.Nodes.JsonObject();
        foreach (var (name, secret) in request.PodmanSecrets) secrets[name] = secret;
        var ports = new System.Text.Json.Nodes.JsonArray();
        foreach (var port in spec.Ports)
        {
            ports.Add(new System.Text.Json.Nodes.JsonObject
            {
                ["host_ip"] = port.HostIp ?? string.Empty,
                ["host_port"] = port.HostPort,
                ["container_port"] = port.ContainerPort,
                ["protocol"] = port.Protocol,
            });
        }

        var namedVolumes = new System.Text.Json.Nodes.JsonArray();
        var binds = new System.Text.Json.Nodes.JsonArray();
        foreach (var volume in spec.Volumes)
        {
            if (volume.Kind == Dokpod.Domain.Containers.VolumeKind.Volume)
            {
                namedVolumes.Add(new System.Text.Json.Nodes.JsonObject
                {
                    ["Name"] = volume.Source,
                    ["Dest"] = volume.Target,
                    ["Options"] = new System.Text.Json.Nodes.JsonArray(volume.ReadOnly ? "ro" : "rw"),
                });
            }
            else
            {
                binds.Add(new System.Text.Json.Nodes.JsonObject
                {
                    ["Destination"] = volume.Target,
                    ["Source"] = volume.Source,
                    ["Type"] = "bind",
                    ["Options"] = new System.Text.Json.Nodes.JsonArray("rbind", volume.ReadOnly ? "ro" : "rw"),
                });
            }
        }

        var body = new System.Text.Json.Nodes.JsonObject
        {
            ["name"] = spec.Name,
            ["image"] = spec.Image.Canonical,
            ["env"] = env,
            ["secret_env"] = secrets,
            ["labels"] = Labels(request),
            ["portmappings"] = ports,
            ["volumes"] = namedVolumes,
            ["mounts"] = binds,
            ["remove"] = spec.AutoRemove,
            ["restart_policy"] = RestartName(spec.Restart.Name),
        };
        if (spec.Restart.Name == Dokpod.Domain.Containers.RestartPolicyKind.OnFailure && spec.Restart.MaximumRetryCount > 0)
        {
            body["restart_tries"] = spec.Restart.MaximumRetryCount;
        }

        var limits = new System.Text.Json.Nodes.JsonObject();
        if (spec.CpuLimitCores is { } cpu)
        {
            limits["cpu"] = new System.Text.Json.Nodes.JsonObject { ["quota"] = (long)Math.Round(cpu * 100_000), ["period"] = 100_000 };
        }

        if (spec.MemoryLimitBytes is { } memory) limits["memory"] = new System.Text.Json.Nodes.JsonObject { ["limit"] = memory };
        if (limits.Count > 0) body["resource_limits"] = limits;
        if (spec.Network is { } network) body["networks"] = new System.Text.Json.Nodes.JsonObject { [network] = new System.Text.Json.Nodes.JsonObject() };
        if (spec.HealthCheck is { } health)
        {
            body["healthconfig"] = new System.Text.Json.Nodes.JsonObject
            {
                ["Test"] = new System.Text.Json.Nodes.JsonArray(health.Test.Select(t => (System.Text.Json.Nodes.JsonNode?)System.Text.Json.Nodes.JsonValue.Create(t)).ToArray()),
                ["Interval"] = Nanoseconds(health.IntervalSeconds),
                ["Timeout"] = Nanoseconds(health.TimeoutSeconds),
                ["Retries"] = health.Retries,
                ["StartPeriod"] = Nanoseconds(health.StartPeriodSeconds),
            };
            // define.HealthCheckOnFailureAction: 0 inválido, 1 none, 2 kill, 3 restart, 4 stop.
            body["health_check_on_failure_action"] = health.OnFailure == Dokpod.Domain.Containers.HealthCheckOnFailure.Restart ? 3 : 1;
        }

        return body;
    }

    private static string RestartName(Dokpod.Domain.Containers.RestartPolicyKind kind) => kind switch
    {
        Dokpod.Domain.Containers.RestartPolicyKind.OnFailure => "on-failure",
        Dokpod.Domain.Containers.RestartPolicyKind.Always => "always",
        Dokpod.Domain.Containers.RestartPolicyKind.UnlessStopped => "unless-stopped",
        _ => "no",
    };

    public async Task<EngineExecSession> StartExecAsync(string containerId, IReadOnlyList<string> command, int cols, int rows, CancellationToken cancellationToken)
    {
        ValidateContainerId(containerId);
        if (rawConnector is null) throw new NotSupportedException("Raw engine connection is not configured.");
        await EnsureNegotiatedAsync(cancellationToken);
        var create = new System.Text.Json.Nodes.JsonObject
        {
            ["AttachStdin"] = true,
            ["AttachStdout"] = true,
            ["AttachStderr"] = true,
            ["Tty"] = true,
            ["Cmd"] = new System.Text.Json.Nodes.JsonArray(command.Select(c => (System.Text.Json.Nodes.JsonNode?)System.Text.Json.Nodes.JsonValue.Create(c)).ToArray()),
            ["ConsoleSize"] = new System.Text.Json.Nodes.JsonArray(Math.Clamp(rows, 1, 500), Math.Clamp(cols, 1, 1000)),
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, $"v{apiVersion}/containers/{containerId}/exec")
        {
            Content = new StringContent(create.ToJsonString(), System.Text.Encoding.UTF8, "application/json"),
        };
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new EngineExecException(response.StatusCode == HttpStatusCode.Conflict ? "not_running" : "exec_create_failed", IsExecutableNotFound(text));
        }

        var execId = JsonDocument.Parse(text).RootElement.GetProperty("Id").GetString()!;
        var (status, head, stream) = await rawConnector.SendUpgradeAsync($"/v{apiVersion}/exec/{execId}/start", "{\"Detach\":false,\"Tty\":true}", cancellationToken);
        if (status is not (101 or 200))
        {
            await using (stream)
            {
                var error = new byte[4096];
                var read = 0;
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(TimeSpan.FromSeconds(2));
                    read = await stream.ReadAsync(error, timeout.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                }

                var message = head + System.Text.Encoding.UTF8.GetString(error, 0, read);
                throw new EngineExecException("exec_start_failed", IsExecutableNotFound(message));
            }
        }

        return new EngineExecSession(execId, stream);
    }

    public async Task<EngineExecState?> InspectExecAsync(string execId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(execId) || !execId.All(Uri.IsHexDigit)) return null;
        await EnsureNegotiatedAsync(cancellationToken);
        using var response = await httpClient.GetAsync($"v{apiVersion}/exec/{execId}/json", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        return new EngineExecState(
            root.TryGetProperty("Running", out var running) && running.ValueKind == JsonValueKind.True,
            root.TryGetProperty("ExitCode", out var exit) && exit.ValueKind == JsonValueKind.Number ? exit.GetInt32() : null);
    }

    public async Task ResizeExecAsync(string execId, int cols, int rows, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(execId) || !execId.All(Uri.IsHexDigit)) return;
        await EnsureNegotiatedAsync(cancellationToken);
        using var response = await httpClient.PostAsync($"v{apiVersion}/exec/{execId}/resize?h={Math.Clamp(rows, 1, 500)}&w={Math.Clamp(cols, 1, 1000)}", null, cancellationToken);
    }

    public async Task SignalContainerProcessAsync(string containerId, int pid, string signal, CancellationToken cancellationToken)
    {
        ValidateContainerId(containerId);
        if (pid <= 1 || signal is not ("HUP" or "KILL")) throw new ArgumentOutOfRangeException(nameof(signal));
        await EnsureNegotiatedAsync(cancellationToken);
        var create = new System.Text.Json.Nodes.JsonObject
        {
            ["AttachStdin"] = false,
            ["AttachStdout"] = false,
            ["AttachStderr"] = false,
            ["Tty"] = false,
            // `kill` é builtin do sh: não depende de binário extra na imagem.
            ["Cmd"] = new System.Text.Json.Nodes.JsonArray("/bin/sh", "-c", $"kill -{signal} {pid.ToString(System.Globalization.CultureInfo.InvariantCulture)} 2>/dev/null; kill -{signal} -{pid.ToString(System.Globalization.CultureInfo.InvariantCulture)} 2>/dev/null; true"),
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, $"v{apiVersion}/containers/{containerId}/exec")
        {
            Content = new StringContent(create.ToJsonString(), System.Text.Encoding.UTF8, "application/json"),
        };
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return;
        var execId = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken)).RootElement.GetProperty("Id").GetString();
        using var start = new HttpRequestMessage(HttpMethod.Post, $"v{apiVersion}/exec/{execId}/start")
        {
            Content = new StringContent("{\"Detach\":true,\"Tty\":false}", System.Text.Encoding.UTF8, "application/json"),
        };
        using var started = await httpClient.SendAsync(start, cancellationToken);
    }

    /// <summary>Mensagens do runtime para "binário inexistente" (D-003: cai de `/bin/bash` para `/bin/sh`).</summary>
    public static bool IsExecutableNotFound(string message)
    {
        var text = message.ToLowerInvariant();
        return text.Contains("executable file not found") || text.Contains("no such file or directory") || text.Contains("not found in $path") || text.Contains("exit code 127");
    }

    /// <summary>Falhas de `create` para o contrato da 013 (sem repassar o texto do engine).</summary>
    public static string MapCreateFailure(int status, string message)
    {
        var text = message.ToLowerInvariant();
        if (status == 409 || text.Contains("already in use") && text.Contains("name")) return "name_conflict";
        if (text.Contains("network") && (text.Contains("not found") || text.Contains("no such network") || text.Contains("unable to find network"))) return "network_not_found";
        if (text.Contains("secret") && (text.Contains("not found") || text.Contains("no such secret"))) return "secret_not_found";
        if (status == 404 || text.Contains("no such image") || text.Contains("image not known")) return "image_not_found";
        if (status == 400) return "invalid_spec";
        return "create_failed";
    }

    /// <summary>Falhas de `start` para o contrato da 013.</summary>
    public static string MapStartFailure(string message)
    {
        var text = message.ToLowerInvariant();
        if (text.Contains("privileged port") || text.Contains("permission denied") && text.Contains("bind")) return "privileged_port";
        if (text.Contains("port is already allocated") || text.Contains("address already in use")) return "port_conflict";
        return "start_failed";
    }

    /// <summary>Mensagens do Docker e do Podman para os códigos de falha do contrato (sem repassar o texto do engine).</summary>
    public static string MapPullFailure(int status, string message)
    {
        var text = message.ToLowerInvariant();
        // "pull access denied ..., repository does not exist" (Docker Hub) é inexistência, não falta de credencial.
        if (status == 404 || text.Contains("manifest unknown") || text.Contains("does not exist") || text.Contains("not found")) return "image_not_found";
        if (status is 401 or 403 || text.Contains("unauthorized") || text.Contains("denied") || text.Contains("authentication required")) return "registry_unauthorized";
        if (text.Contains("no space left")) return "insufficient_storage";
        if (text.Contains("no matching manifest") || text.Contains("does not match the specified platform") || text.Contains("choosing an image from manifest list")) return "platform_mismatch";
        if (text.Contains("no such host") || text.Contains("connection refused") || text.Contains("timeout") || text.Contains("i/o timeout") || text.Contains("dial tcp") || text.Contains("tls")) return "registry_unreachable";
        return "image_pull_failed";
    }

    /// <summary>Stream `/events` filtrado por tipo (container, image, volume); termina quando a conexão cai.</summary>
    public async IAsyncEnumerable<EngineEventRecord> WatchEventsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await EnsureNegotiatedAsync(cancellationToken);
        var filters = Uri.EscapeDataString("""{"type":["container","image","volume"]}""");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"v{apiVersion}/events?filters={filters}");
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null) yield break;
            if (line.Length == 0 || line.Length > 64 * 1024) continue;
            var record = ParseEvent(line);
            if (record is not null) yield return record;
        }
    }

    public static EngineEventRecord? ParseEvent(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            var type = root.TryGetProperty("Type", out var typeElement) ? typeElement.GetString() : null;
            var action = root.TryGetProperty("Action", out var actionElement) ? actionElement.GetString() : root.TryGetProperty("status", out var status) ? status.GetString() : null;
            if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(action)) return null;
            string? id = null;
            Dictionary<string, string> attributes = new(StringComparer.Ordinal);
            if (root.TryGetProperty("Actor", out var actor) && actor.ValueKind == JsonValueKind.Object)
            {
                id = actor.TryGetProperty("ID", out var actorId) ? actorId.GetString() : null;
                if (actor.TryGetProperty("Attributes", out var attrs) && attrs.ValueKind == JsonValueKind.Object)
                {
                    foreach (var key in new[] { "name", "image", "exitCode" })
                    {
                        if (attrs.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
                        {
                            attributes[key] = value.GetString()!;
                        }
                    }
                }
            }

            id ??= root.TryGetProperty("id", out var legacyId) ? legacyId.GetString() : null;
            if (string.IsNullOrEmpty(id)) return null;
            var timeNano = root.TryGetProperty("timeNano", out var nano) && nano.ValueKind == JsonValueKind.Number
                ? nano.GetInt64()
                : root.TryGetProperty("time", out var seconds) && seconds.ValueKind == JsonValueKind.Number ? seconds.GetInt64() * 1_000_000_000 : 0;
            var occurredAt = timeNano > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(timeNano / 1_000_000)
                : DateTimeOffset.UtcNow;
            return new EngineEventRecord(
                type,
                action,
                id,
                attributes.GetValueOrDefault("name"),
                attributes.GetValueOrDefault("image"),
                int.TryParse(attributes.GetValueOrDefault("exitCode"), out var exitCode) ? exitCode : null,
                occurredAt,
                timeNano);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task<string?> GetContainerJsonAsync(string containerId, CancellationToken cancellationToken)
    {
        ValidateContainerId(containerId);
        await EnsureNegotiatedAsync(cancellationToken);
        using var response = await httpClient.GetAsync($"v{apiVersion}/containers/{containerId}/json", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        await using var content = await ReadLimitedAsync(response, cancellationToken);
        using var reader = new StreamReader(content);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    public async IAsyncEnumerable<EngineLogLine> ReadLogsAsync(
        string containerId,
        EngineLogRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ValidateContainerId(containerId);
        var json = await GetContainerJsonAsync(containerId, cancellationToken)
            ?? throw new KeyNotFoundException("container_not_found");
        bool tty;
        using (var document = JsonDocument.Parse(json))
        {
            tty = document.RootElement.TryGetProperty("Config", out var config) &&
                config.TryGetProperty("Tty", out var ttyElement) && ttyElement.ValueKind == JsonValueKind.True;
        }

        var query = new List<string> { "stdout=1", "stderr=1", "timestamps=1" };
        if (request.Follow) query.Add("follow=1");
        query.Add($"tail={(request.Tail is > 0 ? request.Tail.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : request.Follow ? "0" : "all")}");
        if (request.Since is not null) query.Add($"since={request.Since.Value.ToUnixTimeSeconds()}");
        if (request.Until is not null) query.Add($"until={request.Until.Value.ToUnixTimeSeconds()}");
        using var message = new HttpRequestMessage(HttpMethod.Get, $"v{apiVersion}/containers/{containerId}/logs?{string.Join('&', query)}");
        using var response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) throw new KeyNotFoundException("container_not_found");
        if (response.StatusCode == HttpStatusCode.NotImplemented || response.StatusCode == HttpStatusCode.BadRequest)
        {
            // Driver de log sem leitura (ex.: none, syslog remoto).
            throw new NotSupportedException("log_driver_unreadable");
        }

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await foreach (var line in (tty ? ReadPlainAsync(stream, cancellationToken) : ReadMultiplexedAsync(stream, cancellationToken)).WithCancellation(cancellationToken))
        {
            yield return line;
        }
    }

    private static async IAsyncEnumerable<EngineLogLine> ReadPlainAsync(
        Stream stream,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            yield return new EngineLogLine(1, line.Length > 64 * 1024 ? line[..(64 * 1024)] : line);
        }
    }

    /// <summary>Formato multiplexado sem TTY: cabeçalho de 8 bytes [stream, 0, 0, 0, tamanho big-endian] + carga.</summary>
    private static async IAsyncEnumerable<EngineLogLine> ReadMultiplexedAsync(
        Stream stream,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var header = new byte[8];
        var pending = new Dictionary<int, System.Text.StringBuilder> { [1] = new(), [2] = new() };
        while (true)
        {
            if (!await ReadExactAsync(stream, header, cancellationToken)) break;
            var streamId = header[0] is 2 ? 2 : 1;
            var size = (header[4] << 24) | (header[5] << 16) | (header[6] << 8) | header[7];
            if (size < 0 || size > 4 * 1024 * 1024) break;
            var payload = new byte[size];
            if (!await ReadExactAsync(stream, payload, cancellationToken)) break;
            var buffer = pending[streamId];
            buffer.Append(System.Text.Encoding.UTF8.GetString(payload));
            var text = buffer.ToString();
            var start = 0;
            for (var newline = text.IndexOf('\n'); newline >= 0; newline = text.IndexOf('\n', start))
            {
                yield return new EngineLogLine(streamId, text[start..newline].TrimEnd('\r'));
                start = newline + 1;
            }

            buffer.Clear();
            if (start < text.Length)
            {
                var rest = text[start..];
                buffer.Append(rest.Length > 64 * 1024 ? rest[..(64 * 1024)] : rest);
            }
        }

        foreach (var (streamId, rest) in pending)
        {
            if (rest.Length > 0) yield return new EngineLogLine(streamId, rest.ToString());
        }
    }

    private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(read), cancellationToken);
            if (count == 0) return false;
            read += count;
        }

        return true;
    }

    private static double? CpuLimit(JsonElement root)
    {
        if (!root.TryGetProperty("HostConfig", out var host) || host.ValueKind != JsonValueKind.Object) return null;
        if (host.TryGetProperty("NanoCpus", out var nano) && nano.ValueKind == JsonValueKind.Number && nano.GetInt64() > 0)
        {
            return nano.GetInt64() / 1_000_000_000d;
        }

        if (host.TryGetProperty("CpuQuota", out var quota) && quota.ValueKind == JsonValueKind.Number && quota.GetInt64() > 0 &&
            host.TryGetProperty("CpuPeriod", out var period) && period.ValueKind == JsonValueKind.Number && period.GetInt64() > 0)
        {
            return (double)quota.GetInt64() / period.GetInt64();
        }

        return null;
    }

    public async Task<EngineContainerStats?> GetContainerStatsAsync(string containerId, CancellationToken cancellationToken)
    {
        ValidateContainerId(containerId);
        await EnsureNegotiatedAsync(cancellationToken);
        using var response = await httpClient.GetAsync(
            $"v{apiVersion}/containers/{containerId}/stats?stream=false&one-shot=true",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        await using var content = await ReadLimitedAsync(response, cancellationToken);
        using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
        return ParseStats(document.RootElement);
    }

    /// <summary>Lê somente contadores numéricos; nenhum nome de processo, argumento ou label é lido.</summary>
    public static EngineContainerStats? ParseStats(JsonElement root)
    {
        static ulong U(JsonElement element, params string[] path)
        {
            var current = element;
            foreach (var name in path)
            {
                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(name, out current)) return 0;
            }

            return current.ValueKind == JsonValueKind.Number && current.TryGetUInt64(out var value) ? value : 0;
        }

        if (!root.TryGetProperty("cpu_stats", out var cpu) || cpu.ValueKind != JsonValueKind.Object) return null;
        var memory = root.TryGetProperty("memory_stats", out var m) ? m : default;
        var usage = memory.ValueKind == JsonValueKind.Object ? U(memory, "usage") : 0;
        // cgroup v2: inactive_file; v1: total_inactive_file. Mesmo cálculo do `docker stats`.
        var inactive = memory.ValueKind == JsonValueKind.Object ? Math.Max(U(memory, "stats", "inactive_file"), U(memory, "stats", "total_inactive_file")) : 0;
        var limit = memory.ValueKind == JsonValueKind.Object ? U(memory, "limit") : 0;
        ulong rx = 0, tx = 0;
        if (root.TryGetProperty("networks", out var networks) && networks.ValueKind == JsonValueKind.Object)
        {
            foreach (var network in networks.EnumerateObject())
            {
                rx += U(network.Value, "rx_bytes");
                tx += U(network.Value, "tx_bytes");
            }
        }

        ulong read = 0, write = 0;
        if (root.TryGetProperty("blkio_stats", out var blkio) && blkio.ValueKind == JsonValueKind.Object &&
            blkio.TryGetProperty("io_service_bytes_recursive", out var io) && io.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in io.EnumerateArray())
            {
                var op = entry.TryGetProperty("op", out var opElement) ? opElement.GetString()?.ToLowerInvariant() : null;
                var value = U(entry, "value");
                if (op == "read") read += value;
                else if (op == "write") write += value;
            }
        }

        var online = cpu.TryGetProperty("online_cpus", out var onlineElement) && onlineElement.ValueKind == JsonValueKind.Number ? onlineElement.GetInt32() : 0;
        if (online <= 0 && cpu.TryGetProperty("cpu_usage", out var cpuUsage) && cpuUsage.TryGetProperty("percpu_usage", out var percpu) && percpu.ValueKind == JsonValueKind.Array)
        {
            online = percpu.GetArrayLength();
        }

        return new EngineContainerStats(
            U(cpu, "cpu_usage", "total_usage"),
            U(cpu, "system_cpu_usage"),
            Math.Max(1, online),
            usage > inactive ? usage - inactive : usage,
            limit is > 0 and < 1UL << 60 ? limit : null,
            rx,
            tx,
            read,
            write);
    }

    private static string? ParseHealthFromStatus(string? status)
    {
        if (string.IsNullOrEmpty(status)) return null;
        if (status.Contains("(healthy)", StringComparison.Ordinal)) return "healthy";
        if (status.Contains("(unhealthy)", StringComparison.Ordinal)) return "unhealthy";
        if (status.Contains("(health: starting)", StringComparison.Ordinal)) return "starting";
        return null;
    }

    private static string NormalizeHealth(string? value) => value?.ToLowerInvariant() switch
    {
        "healthy" => "healthy",
        "unhealthy" => "unhealthy",
        "starting" => "starting",
        _ => "none",
    };

    private static DateTimeOffset? ParseEngineTime(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String) return null;
        return DateTimeOffset.TryParse(value.GetString(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed) &&
            parsed.Year > 1
            ? parsed.ToUniversalTime()
            : null;
    }

    private static IReadOnlyList<EnginePortBinding> ParseListPorts(JsonElement item)
    {
        if (!item.TryGetProperty("Ports", out var ports) || ports.ValueKind != JsonValueKind.Array) return [];
        var result = new List<EnginePortBinding>();
        foreach (var port in ports.EnumerateArray().Take(64))
        {
            if (!port.TryGetProperty("PrivatePort", out var privatePort) || privatePort.ValueKind != JsonValueKind.Number) continue;
            var publicPort = port.TryGetProperty("PublicPort", out var pub) && pub.ValueKind == JsonValueKind.Number ? pub.GetInt32() : 0;
            var ip = port.TryGetProperty("IP", out var ipElement) ? ipElement.GetString() : null;
            var protocol = port.TryGetProperty("Type", out var typeElement) ? typeElement.GetString() ?? "tcp" : "tcp";
            var binding = new EnginePortBinding(ip, publicPort, privatePort.GetInt32(), protocol);
            // Docker lista a mesma porta para IPv4 e IPv6; mantém uma por (host, container, protocolo).
            if (!result.Any(existing => existing.HostPort == binding.HostPort && existing.ContainerPort == binding.ContainerPort && existing.Protocol == binding.Protocol))
            {
                result.Add(binding);
            }
        }

        return result;
    }

    public async Task<EngineContainer?> InspectContainerAsync(
        string containerId,
        CancellationToken cancellationToken)
    {
        ValidateContainerId(containerId);
        await EnsureNegotiatedAsync(cancellationToken);
        using var response = await httpClient.GetAsync(
            $"v{apiVersion}/containers/{containerId}/json",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        await using var content = await ReadLimitedAsync(response, cancellationToken);
        using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var id = root.GetProperty("Id").GetString() ?? throw new InvalidDataException("Docker returned an empty container ID.");
        var name = root.GetProperty("Name").GetString()?.TrimStart('/') ?? string.Empty;
        var image = root.GetProperty("Config").GetProperty("Image").GetString() ?? string.Empty;
        var state = root.GetProperty("State").GetProperty("Status").GetString() ?? "unknown";
        var created = DateTimeOffset.Parse(root.GetProperty("Created").GetString()!).ToUnixTimeSeconds();
        return new EngineContainer(id, name, image, state, $"{id}:{created}");
    }

    public async Task<ContainerMutationResult> ExecuteAsync(
        AgentCommandKind commandKind,
        string containerId,
        CancellationToken cancellationToken)
    {
        ValidateContainerId(containerId);
        await EnsureNegotiatedAsync(cancellationToken);

        // Podman recusa `stop` em container pausado ("refusing to clean up"); o Docker aceita.
        // Para manter o mesmo comportamento nas duas engines, retoma antes de parar (spec 008 T011).
        if (commandKind == AgentCommandKind.StopContainer && engineKind == "podman" &&
            (await InspectContainerAsync(containerId, cancellationToken))?.State == "paused")
        {
            using var unpause = new HttpRequestMessage(HttpMethod.Post, $"v{apiVersion}/containers/{containerId}/unpause");
            using var unpaused = await httpClient.SendAsync(unpause, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }

        var request = commandKind switch
        {
            AgentCommandKind.StartContainer => new HttpRequestMessage(HttpMethod.Post, $"v{apiVersion}/containers/{containerId}/start"),
            AgentCommandKind.StopContainer => new HttpRequestMessage(HttpMethod.Post, $"v{apiVersion}/containers/{containerId}/stop"),
            AgentCommandKind.RestartContainer => new HttpRequestMessage(HttpMethod.Post, $"v{apiVersion}/containers/{containerId}/restart"),
            AgentCommandKind.DeleteContainer => new HttpRequestMessage(HttpMethod.Delete, $"v{apiVersion}/containers/{containerId}?v=0&force=0"),
            AgentCommandKind.PauseContainer => new HttpRequestMessage(HttpMethod.Post, $"v{apiVersion}/containers/{containerId}/pause"),
            AgentCommandKind.UnpauseContainer => new HttpRequestMessage(HttpMethod.Post, $"v{apiVersion}/containers/{containerId}/unpause"),
            _ => throw new ArgumentOutOfRangeException(nameof(commandKind), commandKind, "Unsupported Docker command."),
        };

        using (request)
        using (var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotModified)
            {
                return new ContainerMutationResult(true, null);
            }

            // Docker sinaliza estado incompatível com 409; Podman com 500 e `cause` "container state improper".
            var stateConflict = response.StatusCode == HttpStatusCode.Conflict ||
                (response.StatusCode == HttpStatusCode.InternalServerError && engineKind == "podman" &&
                 await HasCauseAsync(response, "container state improper", cancellationToken));
            return new ContainerMutationResult(
                false,
                response.StatusCode switch
                {
                    HttpStatusCode.NotFound => "target_not_found",
                    // Pausa sem container em execução / retomada sem container pausado (spec 010).
                    _ when stateConflict && commandKind == AgentCommandKind.PauseContainer => "not_running",
                    _ when stateConflict && commandKind == AgentCommandKind.UnpauseContainer => "not_paused",
                    _ => "operation_failed",
                });
        }
    }

    private static async Task<bool> HasCauseAsync(HttpResponseMessage response, string cause, CancellationToken cancellationToken)
    {
        try
        {
            await using var content = await ReadLimitedAsync(response, cancellationToken);
            using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("cause", out var value) &&
                string.Equals(value.GetString(), cause, StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            return false;
        }
    }

    private async Task EnsureNegotiatedAsync(CancellationToken cancellationToken)
    {
        if (apiVersion is not null)
        {
            return;
        }

        await negotiationLock.WaitAsync(cancellationToken);
        try
        {
            if (apiVersion is not null)
            {
                return;
            }

            using var response = await httpClient.GetAsync("version", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var content = await ReadLimitedAsync(response, cancellationToken);
            using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);

            var reportedApiVersion = Version.Parse(document.RootElement.GetProperty("ApiVersion").GetString()!);
            var minimumApiVersion = Version.Parse(document.RootElement.GetProperty("MinAPIVersion").GetString()!);
            if (minimumApiVersion > MaximumApiVersion)
            {
                throw new NotSupportedException($"Docker requires API {minimumApiVersion}, above supported {MaximumApiVersion}.");
            }

            var selectedVersion = reportedApiVersion < MaximumApiVersion ? reportedApiVersion : MaximumApiVersion;
            engineVersion = document.RootElement.GetProperty("Version").GetString() ?? "unknown";
            // Podman expõe a API compatível com Docker e se identifica em Components (008 D-007).
            if (document.RootElement.TryGetProperty("Components", out var components) &&
                components.ValueKind == JsonValueKind.Array &&
                components.EnumerateArray().Any(component =>
                    component.TryGetProperty("Name", out var componentName) &&
                    componentName.GetString()?.Contains("Podman", StringComparison.OrdinalIgnoreCase) == true))
            {
                engineKind = "podman";
            }
            apiVersion = $"{selectedVersion.Major}.{selectedVersion.Minor}";
        }
        finally
        {
            negotiationLock.Release();
        }
    }

    private static void ValidateContainerId(string containerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerId);
        if (containerId.Length != 64 || containerId.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("Docker container IDs must contain exactly 64 hexadecimal characters.", nameof(containerId));
        }
    }

    private static async Task<MemoryStream> ReadLimitedAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength > MaximumResponseBytes)
        {
            throw new InvalidDataException("Docker response exceeded the configured limit.");
        }

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        var destination = new MemoryStream();
        var buffer = new byte[81920];
        var total = 0;

        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                destination.Position = 0;
                return destination;
            }

            total += read;
            if (total > MaximumResponseBytes)
            {
                await destination.DisposeAsync();
                throw new InvalidDataException("Docker response exceeded the configured limit.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }
}