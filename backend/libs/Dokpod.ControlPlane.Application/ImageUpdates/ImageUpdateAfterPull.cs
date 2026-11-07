using Dokpod.ControlPlane.Application.Images;

namespace Dokpod.ControlPlane.Application.ImageUpdates;

/// <summary>
/// Depois de um pull concluído (spec 025 T019): adianta a verificação do host para agora, sem o limite do "Verificar
/// agora", para o estado refletir a imagem nova. Como o agendamento, depende só da capability na licença importada, não
/// do gate (FR-009). Não toca em comandos nem em containers: o container segue com a imagem anterior (FR-004).
/// </summary>
public sealed class ImageUpdateAfterPull(IImageUpdateStore store, IImageUpdateEntitlement entitlement, TimeProvider timeProvider)
    : IImagePullCompletionObserver
{
    public async Task OnPullSucceededAsync(Guid environmentId, CancellationToken cancellationToken)
    {
        if (environmentId == Guid.Empty) throw new ArgumentException("Environment is required.", nameof(environmentId));
        if (!await entitlement.LicenseListsCapabilityAsync(cancellationToken).ConfigureAwait(false)) return;
        await store.RequestNowAsync(environmentId, timeProvider.GetUtcNow(), manual: false, cancellationToken).ConfigureAwait(false);
    }
}
