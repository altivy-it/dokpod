using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Dokpod.Domain.Images;

namespace Dokpod.Domain.Containers;

public enum HostSecretKind
{
    PodmanSecret,
    File,
}

public enum VolumeKind
{
    Volume,
    Bind,
}

public enum RestartPolicyKind
{
    No,
    OnFailure,
    Always,
    UnlessStopped,
}

public enum HealthCheckOnFailure
{
    None,
    Restart,
}

public sealed record PortSpec(string? HostIp, int HostPort, int ContainerPort, string Protocol);

public sealed record HostSecretReference(HostSecretKind Kind, string Name);

/// <summary>Variável de ambiente: valor literal (efêmero, cifrado na API) ou referência a segredo do host.</summary>
public sealed record EnvironmentSpec(string Name, string? Value, HostSecretReference? Secret, bool Sensitive);

public sealed record VolumeSpec(VolumeKind Kind, string Source, string Target, bool ReadOnly);

public sealed record RestartSpec(RestartPolicyKind Name, int MaximumRetryCount = 0);

public sealed record HealthCheckSpec(
    IReadOnlyList<string> Test,
    int IntervalSeconds,
    int TimeoutSeconds,
    int Retries,
    int StartPeriodSeconds,
    HealthCheckOnFailure OnFailure);

public sealed record FieldError(string Field, string Code);

/// <summary>
/// Especificação de criação de container (spec 013): allowlist fechada. Qualquer opção fora destes campos
/// (privileged, capabilities, devices, namespaces do host, security-opt, sysctls, userns) não tem
/// representação e por isso não pode ser pedida.
/// </summary>
public sealed partial record ContainerCreationSpec(
    ImageReference Image,
    string Name,
    IReadOnlyList<PortSpec> Ports,
    IReadOnlyList<EnvironmentSpec> Environment,
    IReadOnlyList<VolumeSpec> Volumes,
    string? Network,
    double? CpuLimitCores,
    long? MemoryLimitBytes,
    RestartSpec Restart,
    HealthCheckSpec? HealthCheck,
    bool AutoRemove)
{
    public const int MaxPorts = 32;
    public const int MaxEnvironment = 100;
    public const int MaxVolumes = 10;
    public const int MaxValueBytes = 32 * 1024;
    public const long MinMemoryBytes = 6L * 1024 * 1024;
    public const long MaxMemoryBytes = 1L << 40;

    /// <summary>Label de reconciliação gravado pelo agente (D-004).</summary>
    public const string CommandLabel = "dokpod.command-id";
    public const string CreatedByLabel = "dokpod.created-by";

    [GeneratedRegex("^[a-zA-Z0-9][a-zA-Z0-9_.-]{0,62}$")]
    private static partial Regex NamePattern();

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{0,127}$")]
    private static partial Regex VariablePattern();

    [GeneratedRegex("^[a-zA-Z0-9][a-zA-Z0-9_.-]{0,127}$")]
    private static partial Regex ObjectNamePattern();

    private static readonly string[] ReservedNetworks = ["host", "none"];

    /// <summary>Valores literais presentes (falso quando os valores foram purgados ou não chegaram).</summary>
    public bool ValuesAvailable => Environment.All(e => e.Secret is not null || e.Value is not null) && !HealthCheckStripped;

    /// <summary>Comando de saúde substituído por `***` ao persistir e ainda não recolocado (spec 023).</summary>
    private bool HealthCheckStripped { get; init; }

    /// <summary>Valida a allowlist, os limites e as regras cruzadas; `engine` (docker|podman) habilita regras por engine.</summary>
    public IReadOnlyList<FieldError> Validate(string? engine = null)
    {
        var errors = new List<FieldError>();
        void Add(string field, string code) => errors.Add(new FieldError(field, code));

        if (!NamePattern().IsMatch(Name ?? string.Empty)) Add("name", "invalid_name");

        if (Ports.Count > MaxPorts) Add("ports", "too_many");
        var published = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < Ports.Count; i++)
        {
            var port = Ports[i];
            if (port.HostPort is < 0 or > 65535) Add($"ports[{i}].hostPort", "out_of_range");
            if (port.ContainerPort is < 1 or > 65535) Add($"ports[{i}].containerPort", "out_of_range");
            if (port.Protocol is not ("tcp" or "udp")) Add($"ports[{i}].protocol", "invalid_protocol");
            if (port.HostIp is not null && !IPAddress.TryParse(port.HostIp, out _)) Add($"ports[{i}].hostIp", "invalid_ip");
            if (port.HostPort > 0 && !published.Add($"{port.HostIp}|{port.HostPort}/{port.Protocol}")) Add($"ports[{i}].hostPort", "duplicate");
        }

        if (Environment.Count > MaxEnvironment) Add("environment", "too_many");
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < Environment.Count; i++)
        {
            var variable = Environment[i];
            if (!VariablePattern().IsMatch(variable.Name ?? string.Empty)) Add($"environment[{i}].name", "invalid_name");
            else if (!names.Add(variable.Name!)) Add($"environment[{i}].name", "duplicate");
            var hasValue = variable.Value is not null;
            var hasSecret = variable.Secret is not null;
            if ((hasValue && hasSecret) || (!hasValue && !hasSecret && ValuesExpected))
            {
                Add($"environment[{i}]", "value_or_secret_required");
            }

            if (variable.Value is not null && Encoding.UTF8.GetByteCount(variable.Value) > MaxValueBytes) Add($"environment[{i}].value", "too_long");
            if (variable.Value?.Contains('\0') == true) Add($"environment[{i}].value", "invalid_value");
            if (variable.Secret is not null && !ObjectNamePattern().IsMatch(variable.Secret.Name ?? string.Empty)) Add($"environment[{i}].secret.name", "invalid_name");
        }

        if (Volumes.Count > MaxVolumes) Add("volumes", "too_many");
        var targets = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < Volumes.Count; i++)
        {
            var volume = Volumes[i];
            if (!IsAbsoluteContainerPath(volume.Target)) Add($"volumes[{i}].target", "invalid_path");
            else if (!targets.Add(volume.Target.TrimEnd('/'))) Add($"volumes[{i}].target", "duplicate");
            if (volume.Kind == VolumeKind.Volume && !ObjectNamePattern().IsMatch(volume.Source ?? string.Empty)) Add($"volumes[{i}].source", "invalid_name");
            if (volume.Kind == VolumeKind.Bind && !IsAbsoluteHostPath(volume.Source)) Add($"volumes[{i}].source", "invalid_path");
        }

        if (Network is not null && (!ObjectNamePattern().IsMatch(Network) || ReservedNetworks.Contains(Network, StringComparer.OrdinalIgnoreCase)))
        {
            Add("network", "invalid_network");
        }

        if (CpuLimitCores is { } cpu && (double.IsNaN(cpu) || cpu < 0.01 || cpu > 64)) Add("cpuLimitCores", "out_of_range");
        if (MemoryLimitBytes is { } memory && (memory < MinMemoryBytes || memory > MaxMemoryBytes)) Add("memoryLimitBytes", "out_of_range");
        if (Restart.Name == RestartPolicyKind.OnFailure ? Restart.MaximumRetryCount is < 0 or > 100 : Restart.MaximumRetryCount != 0)
        {
            Add("restartPolicy.maximumRetryCount", "out_of_range");
        }

        if (HealthCheck is { } health)
        {
            if (health.Test.Count is 0 or > 16 || health.Test.Any(t => string.IsNullOrEmpty(t) || t.Length > 4096 || t.Contains('\0')))
            {
                Add("healthcheck.test", "invalid_test");
            }
            else if (health.Test[0] is not ("CMD" or "CMD-SHELL") || health.Test.Count < 2 || (health.Test[0] == "CMD-SHELL" && health.Test.Count != 2))
            {
                Add("healthcheck.test", "invalid_test");
            }

            if (health.IntervalSeconds is < 1 or > 3600) Add("healthcheck.intervalSeconds", "out_of_range");
            if (health.TimeoutSeconds is < 1 or > 600) Add("healthcheck.timeoutSeconds", "out_of_range");
            if (health.Retries is < 1 or > 100) Add("healthcheck.retries", "out_of_range");
            if (health.StartPeriodSeconds is < 0 or > 3600) Add("healthcheck.startPeriodSeconds", "out_of_range");
            if (health.OnFailure == HealthCheckOnFailure.Restart && engine == "docker") Add("healthcheck.onFailure", "unsupported_option");
        }

        if (AutoRemove && Restart.Name != RestartPolicyKind.No) Add("autoRemove", "incompatible_with_restart");
        return errors;
    }

    /// <summary>Quando falso (parâmetros vindos do armazenamento sem valores), variáveis sem valor são aceitas na validação.</summary>
    private bool ValuesExpected { get; init; } = true;

    /// <summary>
    /// Chave do comando de saúde entre os valores cifrados (spec 023): pode conter segredo e, como os valores das
    /// variáveis, só fica em claro na memória. O caractere nulo não é aceito em nomes de variável, então não colide.
    /// </summary>
    public const string HealthCheckValueKey = "\0healthcheck";

    private const string MaskedHealthCheck = "***";

    /// <summary>Cópia sem os valores literais (para persistir em claro e para a auditoria).</summary>
    public ContainerCreationSpec WithoutValues() =>
        this with
        {
            Environment = Environment.Select(e => e with { Value = null }).ToArray(),
            HealthCheck = HealthCheck is { } health ? health with { Test = [health.Test[0], MaskedHealthCheck] } : null,
            HealthCheckStripped = HealthCheck is not null,
            ValuesExpected = false,
        };

    /// <summary>Recoloca os valores literais por nome (reentrega após decifrar).</summary>
    public ContainerCreationSpec WithValues(IReadOnlyDictionary<string, string> values) =>
        this with
        {
            Environment = Environment.Select(e => e.Secret is null && values.TryGetValue(e.Name, out var value) ? e with { Value = value } : e).ToArray(),
            HealthCheck = HealthCheck is { } health && values.TryGetValue(HealthCheckValueKey, out var test)
                ? health with { Test = test.Split('\u001f') }
                : HealthCheck,
            HealthCheckStripped = HealthCheckStripped && !values.ContainsKey(HealthCheckValueKey),
            ValuesExpected = true,
        };

    /// <summary>Valores literais por nome e o comando de saúde, para cifrar.</summary>
    public IReadOnlyDictionary<string, string> LiteralValues()
    {
        var values = Environment.Where(e => e.Secret is null && e.Value is not null).ToDictionary(e => e.Name, e => e.Value!, StringComparer.Ordinal);
        if (HealthCheck is { } health) values[HealthCheckValueKey] = string.Join('\u001f', health.Test);
        return values;
    }

    /// <summary>
    /// Forma canônica (hash `v2`, D-001): campos em ordem fixa; valores literais entram como SHA-256, nunca em claro.
    /// Exige valores presentes (calculada na API antes da cifragem).
    /// </summary>
    public string Canonical()
    {
        var text = new StringBuilder();
        text.Append("image=").Append(Image.Canonical).Append('\n');
        text.Append("name=").Append(Name).Append('\n');
        foreach (var port in Ports)
        {
            text.Append("port=").Append(port.HostIp).Append('|').Append(port.HostPort.ToString(CultureInfo.InvariantCulture))
                .Append('|').Append(port.ContainerPort.ToString(CultureInfo.InvariantCulture)).Append('|').Append(port.Protocol).Append('\n');
        }

        foreach (var variable in Environment)
        {
            text.Append("env=").Append(variable.Name).Append('|').Append(variable.Sensitive ? '1' : '0').Append('|');
            text.Append(variable.Secret is { } secret
                ? $"secret:{(secret.Kind == HostSecretKind.PodmanSecret ? "podman_secret" : "file")}:{secret.Name}"
                : "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(variable.Value ?? string.Empty))));
            text.Append('\n');
        }

        foreach (var volume in Volumes)
        {
            text.Append("volume=").Append(volume.Kind == VolumeKind.Bind ? "bind" : "volume").Append('|').Append(volume.Source)
                .Append('|').Append(volume.Target).Append('|').Append(volume.ReadOnly ? "ro" : "rw").Append('\n');
        }

        text.Append("network=").Append(Network).Append('\n');
        text.Append("cpu=").Append(CpuLimitCores?.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
        text.Append("memory=").Append(MemoryLimitBytes?.ToString(CultureInfo.InvariantCulture)).Append('\n');
        text.Append("restart=").Append(Restart.Name).Append('|').Append(Restart.MaximumRetryCount.ToString(CultureInfo.InvariantCulture)).Append('\n');
        if (HealthCheck is { } health)
        {
            text.Append("health=").Append(string.Join('\u001f', health.Test)).Append('|')
                .Append(string.Join('|', new[] { health.IntervalSeconds, health.TimeoutSeconds, health.Retries, health.StartPeriodSeconds }.Select(v => v.ToString(CultureInfo.InvariantCulture))))
                .Append('|').Append(health.OnFailure).Append('\n');
        }

        text.Append("autoRemove=").Append(AutoRemove ? '1' : '0');
        return text.ToString();
    }

    private static bool IsAbsoluteContainerPath(string? path) =>
        !string.IsNullOrEmpty(path) && path.Length <= 4096 && path[0] == '/' && !path.Contains('\0') &&
        !path.Split('/').Any(segment => segment == "..") && path != "/";

    private static bool IsAbsoluteHostPath(string? path) =>
        !string.IsNullOrEmpty(path) && path.Length <= 4096 && path[0] == '/' && !path.Contains('\0') &&
        !path.Split('/').Any(segment => segment == "..");
}
