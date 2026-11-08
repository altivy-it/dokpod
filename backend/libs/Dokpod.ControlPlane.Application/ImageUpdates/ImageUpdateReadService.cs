using Dokpod.Domain.Images;

namespace Dokpod.ControlPlane.Application.ImageUpdates;

/// <summary>Campo <c>updateCheck</c> (contracts/api.md): nunca leva digest remoto, registro resolvido nem resposta do registro.</summary>
public sealed record ImageUpdateCheckView(
    string State, string? Reason, DateTimeOffset CheckedAt, DateTimeOffset? LastSuccessAt, DateTimeOffset? OutdatedSince, bool Stale);

/// <summary>
/// Leitura do aviso nas listas (spec 025 T015): os últimos resultados dos hosts da página, numa consulta ao banco por
/// lista, sem chamada ao agente. Só existe na composição Business; sem <c>image-update-notice</c> na licença
/// importada, devolve nulo e o campo não aparece.
/// </summary>
public sealed class ImageUpdateReadService(IImageUpdateStore store, IImageUpdateEntitlement entitlement) : Dokpod.ControlPlane.Application.Images.IImageUpdateAnnotations
{
    /// <summary>Todos os resultados dos hosts (alertas e testes); as listas usam a sobrecarga com as referências da página.</summary>
    public async Task<ImageUpdateLookup?> LoadAsync(IEnumerable<Guid> environmentIds, CancellationToken cancellationToken)
    {
        if (!await entitlement.LicenseListsCapabilityAsync(cancellationToken).ConfigureAwait(false)) return null;
        var ids = environmentIds.Distinct().ToArray();
        return new ImageUpdateLookup(ids.Length == 0 ? [] : await store.ListAsync(ids, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Só as linhas das referências da página (SC-004): a referência gravada é a do container, então a consulta leva cada
    /// grafia que tem a mesma forma canônica (<see cref="ImageUpdateLookup.Spellings"/>); a comparação continua canônica.
    /// </summary>
    public async Task<ImageUpdateLookup?> LoadAsync(IEnumerable<Guid> environmentIds, IEnumerable<string> references, CancellationToken cancellationToken)
    {
        if (!await entitlement.LicenseListsCapabilityAsync(cancellationToken).ConfigureAwait(false)) return null;
        var ids = environmentIds.Distinct().ToArray();
        var spellings = ImageUpdateLookup.Spellings(references);
        return new ImageUpdateLookup(ids.Length == 0 || spellings.Count == 0 ? [] : await store.ListAsync(ids, spellings, cancellationToken).ConfigureAwait(false));
    }

    async Task<Dokpod.ControlPlane.Application.Images.IImageUpdateAnnotationSet?> Dokpod.ControlPlane.Application.Images.IImageUpdateAnnotations.LoadAsync(
        IEnumerable<Guid> environmentIds, IEnumerable<string> references, CancellationToken cancellationToken) =>
        await LoadAsync(environmentIds, references, cancellationToken).ConfigureAwait(false);
}

/// <summary>Resultados por host e referência canônica; com várias plataformas, vale o pior estado.</summary>
public sealed class ImageUpdateLookup : Dokpod.ControlPlane.Application.Images.IImageUpdateAnnotationSet
{
    private readonly Dictionary<(Guid, string), ImageUpdateCheck> checks = new();

    public ImageUpdateLookup(IReadOnlyList<ImageUpdateCheck> rows)
    {
        foreach (var row in rows)
        {
            var key = (row.EnvironmentId, Canonical(row.Reference));
            if (!checks.TryGetValue(key, out var current) || Severity(row.State) > Severity(current.State)) checks[key] = row;
        }
    }

    /// <summary>Estado da imagem: o da primeira tag com resultado; nulo sem verificação ainda.</summary>
    public ImageUpdateCheckView? ForImage(Guid environmentId, IEnumerable<string> repoTags, bool stale)
    {
        foreach (var tag in repoTags)
        {
            if (checks.TryGetValue((environmentId, Canonical(tag)), out var check)) return View(check, check.State, stale);
        }

        return null;
    }

    /// <summary>Estado do container, com <c>container_behind</c> derivado pela imagem local (D-008).</summary>
    public ImageUpdateCheckView? ForContainer(Guid environmentId, string reference, string? imageId, bool stale) =>
        checks.TryGetValue((environmentId, Canonical(reference)), out var check) ? View(check, check.StateForContainer(imageId), stale) : null;

    object? Dokpod.ControlPlane.Application.Images.IImageUpdateAnnotationSet.ForImage(Guid environmentId, IEnumerable<string> repoTags, bool stale) =>
        ForImage(environmentId, repoTags, stale);

    object? Dokpod.ControlPlane.Application.Images.IImageUpdateAnnotationSet.ForContainer(Guid environmentId, string reference, string? imageId, bool stale) =>
        ForContainer(environmentId, reference, imageId, stale);

    private static ImageUpdateCheckView View(ImageUpdateCheck check, string state, bool stale) =>
        new(state, check.Reason, check.CheckedAtUtc, check.LastSuccessAtUtc, check.OutdatedSinceUtc, stale);

    /// <summary>
    /// Grafias que o parser leva à mesma forma canônica de cada referência: a própria, a canônica e, no Docker Hub, as
    /// formas com e sem <c>docker.io/</c>, <c>library/</c>, <c>index.docker.io/</c> e <c>registry-1.docker.io/</c>.
    /// Domínio em maiúsculas é a única grafia que fica de fora (o parser o leva a minúsculas).
    /// </summary>
    public static IReadOnlyCollection<string> Spellings(IEnumerable<string> references)
    {
        var spellings = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in references)
        {
            if (string.IsNullOrEmpty(raw) || !spellings.Add(raw) || !ImageReference.TryParse(raw, out var parsed)) continue;
            spellings.Add(parsed.Canonical);
            if (parsed.Registry != ImageReference.DefaultRegistry) continue;
            var suffix = parsed.Canonical[(parsed.Name.Length)..];
            var paths = parsed.Repository.StartsWith("library/", StringComparison.Ordinal) ? new[] { parsed.Repository, parsed.Repository["library/".Length..] } : [parsed.Repository];
            foreach (var path in paths)
            {
                foreach (var prefix in new[] { "", "docker.io/", "index.docker.io/", "registry-1.docker.io/" })
                {
                    spellings.Add(prefix + path + suffix);
                    // Sem tag nem digest, o parser assume `latest`.
                    if (parsed is { Tag: "latest", Digest: null }) spellings.Add(prefix + path);
                }
            }
        }

        return spellings;
    }

    // As mesmas referências voltam a cada página: a forma canônica fica memorizada (até 4.096 grafias por processo).
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> Canonicals = new(StringComparer.Ordinal);

    private static string Canonical(string reference)
    {
        if (Canonicals.TryGetValue(reference, out var canonical)) return canonical;
        canonical = ImageReference.TryParse(reference, out var parsed) ? parsed.Canonical : reference;
        if (Canonicals.Count >= 4096) Canonicals.Clear();
        Canonicals[reference] = canonical;
        return canonical;
    }

    private static int Severity(string state) => state switch
    {
        ImageUpdateStates.UpdateAvailable => 3,
        ImageUpdateStates.Failed => 2,
        ImageUpdateStates.NotCheckable => 1,
        _ => 0,
    };
}
