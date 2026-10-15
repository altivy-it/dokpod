using Dokpod.Domain.Environments;

namespace Dokpod.ControlPlane.Application.Registries;

/// <summary>Configuração de um registro em um host (spec 020); credencial só como indicação.</summary>
public sealed record RegistryHostConfig(string Registry, IReadOnlyList<string> Mirrors, string? MirrorOf, bool Insecure, bool Blocked, bool Search, bool Credential);

public sealed record RegistryInventorySnapshot(Guid EnvironmentId, ulong Revision, bool ConfigAvailable, DateTimeOffset ObservedAtUtc, IReadOnlyList<RegistryHostConfig> Registries);

public sealed record RegistryHostConfigRow(Guid EnvironmentId, RegistryHostConfig Config);

/// <summary>Imagem projetada reduzida ao necessário para saber o registro de origem.</summary>
public sealed record RegistryImageSource(Guid EnvironmentId, IReadOnlyList<string> RepoTags, IReadOnlyList<string> RepoDigests, long SizeBytes);

public interface IRegistryInventoryStore
{
    /// <summary>Substitui a configuração do host; falso quando a revisão é mais antiga que a gravada.</summary>
    Task<bool> ReplaceAsync(RegistryInventorySnapshot snapshot, CancellationToken cancellationToken);

    /// <summary>Hosts com configuração informada pelo engine (os demais aparecem como "configuração indisponível").</summary>
    Task<IReadOnlySet<Guid>> ListConfigAvailableAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken);

    Task<IReadOnlyList<RegistryHostConfigRow>> ListAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken);

    Task<IReadOnlyList<RegistryImageSource>> ListImageSourcesAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken);
}

public sealed record StoredRegistryPolicy(EnvironmentStage? Stage, IReadOnlyList<string> Registries, string UpdatedBy, DateTimeOffset UpdatedAt);

public interface IRegistryPolicyStore
{
    Task<IReadOnlyList<StoredRegistryPolicy>> ListAsync(CancellationToken cancellationToken);

    Task SaveAsync(StoredRegistryPolicy policy, CancellationToken cancellationToken);
}

public static class RegistryPolicyStages
{
    /// <summary>Chave da política: valor do estágio ou `none` para host sem estágio (chave da configuração da spec 012).</summary>
    public static string Key(EnvironmentStage? stage) => stage is null ? "none" : EnvironmentStages.ToValue(stage.Value);

    public static bool TryParse(string? value, out EnvironmentStage? stage)
    {
        stage = null;
        if (value == "none") return true;
        if (!EnvironmentStages.TryParse(value, out var parsed)) return false;
        stage = parsed;
        return true;
    }

    public static IReadOnlyList<EnvironmentStage?> All { get; } = [EnvironmentStage.Production, EnvironmentStage.Staging, EnvironmentStage.Development, null];
}
