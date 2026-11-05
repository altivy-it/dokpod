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
    public async Task<ImageUpdateLookup?> LoadAsync(IEnumerable<Guid> environmentIds, CancellationToken cancellationToken)
    {
        if (!await entitlement.LicenseListsCapabilityAsync(cancellationToken).ConfigureAwait(false)) return null;
        var ids = environmentIds.Distinct().ToArray();
        return new ImageUpdateLookup(ids.Length == 0 ? [] : await store.ListAsync(ids, cancellationToken).ConfigureAwait(false));
    }

    async Task<Dokpod.ControlPlane.Application.Images.IImageUpdateAnnotationSet?> Dokpod.ControlPlane.Application.Images.IImageUpdateAnnotations.LoadAsync(
        IEnumerable<Guid> environmentIds, CancellationToken cancellationToken) =>
        await LoadAsync(environmentIds, cancellationToken).ConfigureAwait(false);
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

    private static string Canonical(string reference) => ImageReference.TryParse(reference, out var parsed) ? parsed.Canonical : reference;

    private static int Severity(string state) => state switch
    {
        ImageUpdateStates.UpdateAvailable => 3,
        ImageUpdateStates.Failed => 2,
        ImageUpdateStates.NotCheckable => 1,
        _ => 0,
    };
}
