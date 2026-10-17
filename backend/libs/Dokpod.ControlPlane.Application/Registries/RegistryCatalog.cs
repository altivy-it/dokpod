using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.ControlPlane.Application.Images;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Images;

namespace Dokpod.ControlPlane.Application.Registries;

public static class RegistryOrigins
{
    public const string Images = "images";
    public const string Mirror = "mirror";
    public const string Insecure = "insecure";
    public const string Search = "search";
    public const string Blocked = "blocked";
    public const string Credential = "credential";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>([Images, Mirror, Insecure, Search, Blocked, Credential], StringComparer.Ordinal);
}

/// <summary>Um registro em um host: configuração (quando informada), credencial e imagens vindas dele.</summary>
public sealed record RegistryHostEntry(
    HostView Host,
    string Registry,
    IReadOnlyList<string> Origins,
    bool Credential,
    bool Insecure,
    bool Blocked,
    bool Search,
    IReadOnlyList<string> Mirrors,
    string? MirrorOf,
    int Images,
    long SizeBytes,
    bool Allowed,
    bool ConfigAvailable);

public sealed record RegistryRow(
    string Registry,
    int Hosts,
    IReadOnlyList<string> Origins,
    int Images,
    long SizeBytes,
    int InsecureHosts,
    int CredentialHosts,
    int BlockedHosts,
    int OutsidePolicyHosts,
    string? MirrorOf);

public enum RegistrySort
{
    RegistryAsc,
    RegistryDesc,
    ImagesDesc,
    ImagesAsc,
    HostsDesc,
    HostsAsc,
}

public sealed record RegistryQuery(IReadOnlyCollection<string> Origins, bool InsecureOnly, bool OutsidePolicyOnly, string? Text, RegistrySort Sort, int Offset, int Limit);

public sealed record RegistrySummary(int Total, int Insecure, int OutsidePolicy);

public sealed record RegistryPage(IReadOnlyList<RegistryRow> Rows, RegistrySummary Summary, bool HasMore);

public sealed record RegistryImpact(int Images, int Hosts);

/// <summary>
/// Consolidação por registro (spec 020 D-003): configuração projetada de cada host somada aos registros de origem
/// das imagens já projetadas (spec 012), com a política efetiva do estágio de cada host.
/// </summary>
public sealed class RegistryCatalog(IRegistryInventoryStore store, ImagePullPolicy policy)
{
    public async Task<IReadOnlyList<RegistryHostEntry>> EntriesAsync(IReadOnlyCollection<HostView> hosts, CancellationToken cancellationToken)
    {
        if (hosts.Count == 0) return [];
        var byId = hosts.ToDictionary(h => h.Registration.EnvironmentId);
        // Spec 023: só a configuração e as imagens que o agente atual publica (projeção de versão mais nova não conta).
        var configIds = hosts.Where(h => h.Publishes("RegistryInventory")).Select(h => h.Registration.EnvironmentId).ToArray();
        var imageIds = hosts.Where(h => h.Publishes("ImageInventory")).Select(h => h.Registration.EnvironmentId).ToArray();
        var configs = await store.ListAsync(configIds, cancellationToken).ConfigureAwait(false);
        var available = await store.ListConfigAvailableAsync(configIds, cancellationToken).ConfigureAwait(false);
        var sources = await store.ListImageSourcesAsync(imageIds, cancellationToken).ConfigureAwait(false);
        var images = sources
            .Select(source => (source.EnvironmentId, Registry: RegistryNames.OfImage(source.RepoTags, source.RepoDigests), source.SizeBytes))
            .Where(item => item.Registry is not null)
            .GroupBy(item => (item.EnvironmentId, item.Registry!))
            .ToDictionary(group => group.Key, group => (Count: group.Count(), Size: group.Sum(item => item.SizeBytes)));
        var keys = configs.Select(c => (c.EnvironmentId, c.Config.Registry)).Concat(images.Keys).Distinct().ToArray();
        var configByKey = configs.ToDictionary(c => (c.EnvironmentId, c.Config.Registry), c => c.Config);
        return keys.Select(key =>
        {
            var host = byId[key.Item1];
            var config = configByKey.GetValueOrDefault(key);
            var (count, size) = images.GetValueOrDefault(key);
            var origins = new List<string>();
            if (count > 0) origins.Add(RegistryOrigins.Images);
            if (config?.MirrorOf is not null) origins.Add(RegistryOrigins.Mirror);
            if (config?.Insecure == true) origins.Add(RegistryOrigins.Insecure);
            if (config?.Search == true) origins.Add(RegistryOrigins.Search);
            if (config?.Blocked == true) origins.Add(RegistryOrigins.Blocked);
            if (config?.Credential == true) origins.Add(RegistryOrigins.Credential);
            return new RegistryHostEntry(
                host,
                key.Item2,
                origins,
                config?.Credential ?? false,
                config?.Insecure ?? false,
                config?.Blocked ?? false,
                config?.Search ?? false,
                config?.Mirrors ?? [],
                config?.MirrorOf,
                count,
                size,
                policy.Allows(host.Registration.Stage, key.Item2),
                available.Contains(key.Item1));
        }).ToArray();
    }

    public static RegistryPage Page(IReadOnlyList<RegistryHostEntry> entries, RegistryQuery query)
    {
        var rows = entries
            .GroupBy(e => e.Registry, StringComparer.Ordinal)
            .Select(group => new RegistryRow(
                group.Key,
                group.Select(e => e.Host.Registration.EnvironmentId).Distinct().Count(),
                group.SelectMany(e => e.Origins).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                group.Sum(e => e.Images),
                group.Sum(e => e.SizeBytes),
                group.Count(e => e.Insecure),
                group.Count(e => e.Credential),
                group.Count(e => e.Blocked),
                group.Count(e => !e.Allowed),
                group.Select(e => e.MirrorOf).FirstOrDefault(m => m is not null)))
            .Where(row => query.Origins.Count == 0 || row.Origins.Any(query.Origins.Contains))
            .Where(row => string.IsNullOrWhiteSpace(query.Text) || row.Registry.Contains(query.Text.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var summary = new RegistrySummary(rows.Length, rows.Count(r => r.InsecureHosts > 0), rows.Count(r => r.OutsidePolicyHosts > 0));
        IEnumerable<RegistryRow> filtered = rows;
        if (query.InsecureOnly) filtered = filtered.Where(r => r.InsecureHosts > 0);
        if (query.OutsidePolicyOnly) filtered = filtered.Where(r => r.OutsidePolicyHosts > 0);
        filtered = query.Sort switch
        {
            RegistrySort.RegistryDesc => filtered.OrderByDescending(r => r.Registry, StringComparer.Ordinal),
            RegistrySort.ImagesDesc => filtered.OrderByDescending(r => r.Images).ThenBy(r => r.Registry, StringComparer.Ordinal),
            RegistrySort.ImagesAsc => filtered.OrderBy(r => r.Images).ThenBy(r => r.Registry, StringComparer.Ordinal),
            RegistrySort.HostsDesc => filtered.OrderByDescending(r => r.Hosts).ThenBy(r => r.Registry, StringComparer.Ordinal),
            RegistrySort.HostsAsc => filtered.OrderBy(r => r.Hosts).ThenBy(r => r.Registry, StringComparer.Ordinal),
            _ => filtered.OrderBy(r => r.Registry, StringComparer.Ordinal),
        };
        var page = filtered.Skip(query.Offset).Take(query.Limit + 1).ToArray();
        return new RegistryPage(page.Take(query.Limit).ToArray(), summary, page.Length > query.Limit);
    }

    /// <summary>Imagens e hosts do estágio que ficariam fora da lista proposta (vazia = nenhum).</summary>
    public static RegistryImpact Impact(IReadOnlyList<RegistryHostEntry> entries, EnvironmentStage? stage, IReadOnlyCollection<string> proposed)
    {
        if (proposed.Count == 0) return new RegistryImpact(0, 0);
        var outside = entries
            .Where(e => e.Host.Registration.Stage == stage && e.Images > 0 && !proposed.Contains(e.Registry, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        return new RegistryImpact(outside.Sum(e => e.Images), outside.Select(e => e.Host.Registration.EnvironmentId).Distinct().Count());
    }
}
