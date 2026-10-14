namespace Dokpod.Agent;

public sealed record AgentOptions(
    string DataDirectory,
    string DockerSocketPath,
    TimeSpan EngineTimeout,
    TimeSpan InventoryInterval,
    bool RunOnce,
    Uri? ControlPlaneEndpoint,
    Guid? EnvironmentId,
    string ClientCertificatePath,
    string? ClientCertificatePassword,
    string? ServerCaCertificatePath,
    TimeSpan MetricsInterval = default,
    string? RegistryAuthFile = null,
    IReadOnlyList<string>? BindMountAllowlist = null)
{
    /// <summary>Segredos em arquivo para criação de container (spec 013): `&lt;data&gt;/secrets`.</summary>
    public string SecretsDirectory => Path.Combine(DataDirectory, "secrets");

    /// <summary>Intervalo de amostragem de recursos (spec 009): padrão 15 s, máximo 30 s.</summary>
    public TimeSpan EffectiveMetricsInterval => MetricsInterval > TimeSpan.Zero ? MetricsInterval : TimeSpan.FromSeconds(15);

    public static AgentOptions FromEnvironment()
    {
        var defaultDataDirectory = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Dokpod", "Agent")
            : "/var/lib/dokpod-agent";
        var dataDirectory = GetAbsolutePath("DOKPOD_AGENT_DATA_DIRECTORY", defaultDataDirectory);

        var controlPlaneEndpoint = GetOptionalHttpsUri("DOKPOD_AGENT_CONTROL_PLANE_ENDPOINT");
        var environmentId = GetOptionalGuid("DOKPOD_AGENT_ENVIRONMENT_ID");
        if (controlPlaneEndpoint is not null && environmentId is null)
        {
            throw new InvalidOperationException(
                "DOKPOD_AGENT_ENVIRONMENT_ID is required when the control plane endpoint is configured.");
        }

        return new AgentOptions(
            dataDirectory,
            GetDockerSocketPath(),
            TimeSpan.FromSeconds(GetBoundedInteger("DOKPOD_AGENT_ENGINE_TIMEOUT_SECONDS", 30, 1, 300)),
            TimeSpan.FromSeconds(GetBoundedInteger("DOKPOD_AGENT_INVENTORY_INTERVAL_SECONDS", 30, 5, 900)),
            string.Equals(Environment.GetEnvironmentVariable("DOKPOD_AGENT_RUN_ONCE"), "true", StringComparison.OrdinalIgnoreCase),
            controlPlaneEndpoint,
            environmentId,
            GetAbsolutePath(
                "DOKPOD_AGENT_CLIENT_CERTIFICATE_PATH",
                Path.Combine(dataDirectory, "identity", "agent.pfx")),
            Environment.GetEnvironmentVariable("DOKPOD_AGENT_CLIENT_CERTIFICATE_PASSWORD"),
            GetOptionalAbsolutePath("DOKPOD_AGENT_SERVER_CA_CERTIFICATE_PATH"),
            TimeSpan.FromSeconds(GetBoundedInteger("DOKPOD_AGENT_METRICS_INTERVAL_SECONDS", 15, 5, 30)),
            // Spec 012: config.json (formato Docker, `auths`) com credenciais de registro que ficam no host.
            GetOptionalAbsolutePath("DOKPOD_AGENT_REGISTRY_AUTH_FILE"),
            // Spec 013: bind mounts só dentro destes diretórios absolutos (separados por ':'); vazio = somente volumes nomeados.
            GetBindAllowlist());
    }

    private static string GetAbsolutePath(string variableName, string defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(variableName) ?? defaultValue;
        if (!Path.IsPathFullyQualified(value))
        {
            throw new InvalidOperationException($"{variableName} must be an absolute path.");
        }

        return Path.GetFullPath(value);
    }

    private static string GetDockerSocketPath() =>
        Dokpod.Agent.Infrastructure.Engines.EngineSocketResolver.Resolve(
            Environment.GetEnvironmentVariable("DOKPOD_AGENT_DOCKER_SOCKET"),
            OperatingSystem.IsWindows(),
            Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR"),
            File.Exists);

    private static string? GetOptionalAbsolutePath(string variableName)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return string.IsNullOrWhiteSpace(value) ? null : GetAbsolutePath(variableName, value);
    }

    private static IReadOnlyList<string> GetBindAllowlist()
    {
        var value = Environment.GetEnvironmentVariable("DOKPOD_AGENT_BIND_MOUNT_ALLOWLIST");
        if (string.IsNullOrWhiteSpace(value)) return [];
        var paths = value.Split(OperatingSystem.IsWindows() ? ';' : ':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (paths.Any(path => !Path.IsPathFullyQualified(path)))
        {
            throw new InvalidOperationException("DOKPOD_AGENT_BIND_MOUNT_ALLOWLIST must contain absolute paths.");
        }

        return paths.Select(path => Path.GetFullPath(path)).ToArray();
    }

    private static int GetBoundedInteger(string variableName, int defaultValue, int minimum, int maximum)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        if (value is null)
        {
            return defaultValue;
        }

        if (!int.TryParse(value, out var parsed) || parsed < minimum || parsed > maximum)
        {
            throw new InvalidOperationException($"{variableName} must be between {minimum} and {maximum}.");
        }

        return parsed;
    }

    private static Uri? GetOptionalHttpsUri(string variableName)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException($"{variableName} must be an absolute HTTPS URI.");
        }

        return uri;
    }

    private static Guid? GetOptionalGuid(string variableName)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Guid.TryParse(value, out var parsed) || parsed == Guid.Empty)
        {
            throw new InvalidOperationException($"{variableName} must be a non-empty UUID.");
        }

        return parsed;
    }
}