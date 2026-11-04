using Dokpod.Agent.Contracts.V1;
using Dokpod.Domain.Images;

namespace Dokpod.ControlPlane.Application.ImageUpdates;

public enum ImageUpdatePageOutcome
{
    Applied,

    /// <summary><c>request_id</c> que não é o pendente do host ou chegou depois de 6 min; nada gravado.</summary>
    Discarded,

    /// <summary>Algum resultado fora do contrato; a página inteira é recusada.</summary>
    Rejected,
}

/// <summary>
/// Recebimento da mensagem 27 (spec 025 T014): valida a página inteira (estado, motivo, plataforma, digests e
/// tamanhos) e só então grava pelo <see cref="IImageUpdateStore"/>, que descarta pedidos fora do pendente ou do prazo.
/// </summary>
public sealed class ImageUpdateResultIngestion(IImageUpdateStore store, IImageUpdateTargetReader targets, TimeProvider timeProvider)
    : Dokpod.ControlPlane.Application.Images.IImageUpdateResultSink
{
    public const int MaxReferenceLength = 512;

    public async Task<ImageUpdatePageOutcome> ApplyAsync(Guid environmentId, ImageUpdateCheckResultPage page, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(page.RequestId, out var requestId) || page.Results.Count > ImageUpdateLimits.MaxItems) return ImageUpdatePageOutcome.Rejected;
        var results = new List<(ImageUpdateCheckResult Raw, string State, ImageReference Reference)>(page.Results.Count);
        foreach (var result in page.Results)
        {
            var state = result.State switch
            {
                ImageUpdateState.UpToDate => ImageUpdateStates.UpToDate,
                ImageUpdateState.UpdateAvailable => ImageUpdateStates.UpdateAvailable,
                ImageUpdateState.NotCheckable => ImageUpdateStates.NotCheckable,
                ImageUpdateState.Failed => ImageUpdateStates.Failed,
                _ => null,
            };
            var reason = result.Reason.Length == 0 ? null : result.Reason;
            if (state is null || !ImageUpdateReasons.IsValidFor(state, reason) || !ImagePlatform.IsValid(result.Platform) ||
                result.Reference.Length is 0 or > MaxReferenceLength || !ImageReference.TryParse(result.Reference, out var reference) ||
                !OptionalDigest(result.RemoteDigest) || !OptionalDigest(result.MatchedLocalDigest) ||
                (state is ImageUpdateStates.UpToDate or ImageUpdateStates.UpdateAvailable && !ContentDigest.IsValid(result.RemoteDigest)))
            {
                return ImageUpdatePageOutcome.Rejected;
            }

            results.Add((result, state, reference));
        }

        var localImages = await targets.LocalImageIdsAsync(environmentId, cancellationToken).ConfigureAwait(false);
        var mapped = results.Select(r => new ImageUpdateResult(
            r.Raw.Reference,
            r.Raw.Platform,
            r.State,
            r.Raw.Reason.Length == 0 ? null : r.Raw.Reason,
            r.Raw.RemoteDigest.Length == 0 ? null : r.Raw.RemoteDigest,
            localImages.TryGetValue(r.Reference.Canonical, out var imageId) ? imageId : null)).ToArray();
        var outcome = await store.ApplyPageAsync(environmentId, requestId, mapped, page.IsLastPage, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return outcome == ImageUpdateApplyOutcome.Applied ? ImageUpdatePageOutcome.Applied : ImageUpdatePageOutcome.Discarded;
    }

    async Task<bool> Dokpod.ControlPlane.Application.Images.IImageUpdateResultSink.ApplyAsync(
        Guid environmentId, ImageUpdateCheckResultPage page, CancellationToken cancellationToken) =>
        await ApplyAsync(environmentId, page, cancellationToken).ConfigureAwait(false) == ImageUpdatePageOutcome.Applied;

    private static bool OptionalDigest(string value) => value.Length == 0 || ContentDigest.IsValid(value);
}
