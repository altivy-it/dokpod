using Dokpod.Agent.Application.Engines;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Containers;

namespace Dokpod.Agent.Application.Containers;

/// <summary>Segredos em arquivo do host (`&lt;data&gt;/secrets/&lt;nome&gt;`), lidos só pelo agente (spec 013 FR-005).</summary>
public interface IHostSecretFiles
{
    /// <summary>Valor do segredo, ou nulo se não existir ou não for um arquivo protegido válido.</summary>
    Task<string?> ReadAsync(string name, CancellationToken cancellationToken);

    IReadOnlyList<string> List();
}

/// <summary>Política local do agente: bind mounts permitidos e caminhos sempre proibidos (T010).</summary>
public sealed class ContainerCreationPolicy(IReadOnlyList<string> bindAllowlist, string? engineSocketPath)
{
    private static readonly string[] AlwaysDenied = ["/", "/etc", "/proc", "/sys", "/dev", "/run", "/var/run", "/boot", "/root", "/var/lib/docker", "/var/lib/containers"];

    public bool BindMountsAllowed => bindAllowlist.Count > 0;

    public bool AllowsBind(string source)
    {
        var path = Normalize(source);
        if (path is null) return false;
        if (engineSocketPath is not null && (path == Normalize(engineSocketPath) || path.EndsWith("/docker.sock", StringComparison.Ordinal) || path.EndsWith("/podman.sock", StringComparison.Ordinal)))
        {
            return false;
        }

        if (AlwaysDenied.Any(denied => path == denied)) return false;
        return bindAllowlist.Select(Normalize).OfType<string>().Any(root => path == root || path.StartsWith(root.TrimEnd('/') + "/", StringComparison.Ordinal));
    }

    private static string? Normalize(string? path)
    {
        if (string.IsNullOrEmpty(path) || path[0] != '/' || path.Contains('\0')) return null;
        var segments = new List<string>();
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".") continue;
            if (segment == "..") return null;
            segments.Add(segment);
        }

        return "/" + string.Join('/', segments);
    }
}

/// <summary>
/// Executa `CREATE_CONTAINER` (spec 013 T008): reconcilia por label, revalida, resolve segredos, baixa a imagem se
/// faltar, cria e inicia. Em conflito de porta (ou porta privilegiada no rootless) remove o container recém-criado.
/// Nada sensível é registrado: o journal guarda só o resultado.
/// </summary>
public sealed class ContainerCreator(IContainerEngine engine, IHostSecretFiles secretFiles, ContainerCreationPolicy policy)
{
    public async Task<(CommandExecutionState State, string? FailureCode, string? ContainerId)> CreateAsync(
        AgentCommand command,
        ContainerCreationSpec spec,
        CancellationToken cancellationToken)
    {
        var label = command.CommandId.ToString("D");
        var existing = await engine.FindByLabelAsync(ContainerCreationSpec.CommandLabel, label, cancellationToken);
        if (existing is not null)
        {
            // Reentrega após resposta perdida ou reinício: conclui sem recriar (D-004, SC-004).
            if (existing.State is "running" or "restarting") return (CommandExecutionState.Succeeded, null, existing.ContainerId);
            var restarted = await engine.StartCreatedContainerAsync(existing.ContainerId, cancellationToken);
            return restarted is null
                ? (CommandExecutionState.Succeeded, null, existing.ContainerId)
                : (CommandExecutionState.Failed, restarted, existing.ContainerId);
        }

        if (!spec.ValuesAvailable) return (CommandExecutionState.Failed, "parameters_unavailable", null);
        var engineKind = await engine.GetEngineKindAsync(cancellationToken);
        if (spec.Validate(engineKind).FirstOrDefault() is { } error)
        {
            return (CommandExecutionState.Failed, error.Code == "unsupported_option" ? "unsupported_option" : "invalid_spec", null);
        }

        if (spec.Volumes.Any(v => v.Kind == VolumeKind.Bind && !policy.AllowsBind(v.Source)))
        {
            return (CommandExecutionState.Failed, "mount_not_allowed", null);
        }

        var environment = new List<KeyValuePair<string, string>>();
        var podmanSecrets = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var variable in spec.Environment)
        {
            switch (variable.Secret)
            {
                case { Kind: HostSecretKind.PodmanSecret } podman:
                    if (engineKind != "podman") return (CommandExecutionState.Failed, "unsupported_option", null);
                    podmanSecrets[variable.Name] = podman.Name;
                    break;
                case { } file:
                    var value = await secretFiles.ReadAsync(file.Name, cancellationToken);
                    if (value is null) return (CommandExecutionState.Failed, "secret_not_found", null);
                    environment.Add(new(variable.Name, value));
                    break;
                default:
                    environment.Add(new(variable.Name, variable.Value!));
                    break;
            }
        }

        if (podmanSecrets.Count > 0)
        {
            var known = await engine.ListHostObjectsAsync(EngineHostObjectKind.PodmanSecrets, cancellationToken);
            if (podmanSecrets.Values.Any(name => known.All(s => s.Name != name))) return (CommandExecutionState.Failed, "secret_not_found", null);
        }

        var image = await engine.InspectImageConfigAsync(spec.Image, cancellationToken);
        if (!image.Present)
        {
            var pulled = await engine.PullImageAsync(spec.Image, cancellationToken);
            if (!pulled.Succeeded) return (CommandExecutionState.Failed, pulled.FailureCode == "registry_unauthorized" ? "registry_unauthorized" : "image_pull_failed", null);
            image = await engine.InspectImageConfigAsync(spec.Image, cancellationToken);
        }

        if (spec.AutoRemove)
        {
            // Princípio II: --rm apagaria volumes anônimos dos VOLUME da imagem sem volume nomeado no destino.
            var named = spec.Volumes.Where(v => v.Kind == VolumeKind.Volume).Select(v => v.Target.TrimEnd('/')).ToHashSet(StringComparer.Ordinal);
            if (image.Volumes.Any(target => !named.Contains(target.TrimEnd('/'))))
            {
                return (CommandExecutionState.Failed, "anonymous_volume_with_auto_remove", null);
            }
        }

        var created = await engine.CreateContainerAsync(new EngineCreateRequest(spec, environment, podmanSecrets, command.CommandId), cancellationToken);
        if (created.ContainerId is null) return (CommandExecutionState.Failed, created.FailureCode ?? "create_failed", null);

        var startFailure = await engine.StartCreatedContainerAsync(created.ContainerId, cancellationToken);
        if (startFailure is null) return (CommandExecutionState.Succeeded, null, created.ContainerId);
        if (startFailure is "port_conflict" or "privileged_port")
        {
            await engine.RemoveCreatedContainerAsync(created.ContainerId, cancellationToken);
            return (CommandExecutionState.Failed, startFailure, null);
        }

        // Falha ao iniciar: o container permanece criado e parado; o operador decide removê-lo (US1-5).
        return (CommandExecutionState.Failed, startFailure, created.ContainerId);
    }
}
