using Dokpod.ControlPlane.Application.Alerts;
using Dokpod.ControlPlane.Application.Licensing;
using Dokpod.Domain.Alerts;

namespace Dokpod.ControlPlane.Application.ImageUpdates;

/// <summary>Container do inventário e a imagem local dele, para a condição de alerta (spec 025 T022).</summary>
public sealed record ImageUpdateContainer(Guid EnvironmentId, string ContainerId, string Name, string Reference, string? ImageId);

/// <summary>
/// Condição "imagem desatualizada há mais de N dias" (spec 025 T021, D-010): oferecida quando a licença importada lista
/// <c>image-update-notice</c>; criar ou editar uma regra ativa passa pelo gate (<c>license_expired</c> ou
/// <c>license_invalid</c>). Regras já criadas continuam avaliadas com a licença restrita (FR-009).
/// </summary>
[BusinessCapability(LicenseImageUpdateEntitlement.Capability)]
public sealed class ImageOutdatedConditionAvailability(IBusinessCapabilityGate gate, IImageUpdateEntitlement entitlement) : IAlertConditionAvailability
{
    public bool Supports(AlertConditionKind kind) => kind == AlertConditionKind.ImageOutdated;

    public async Task<bool> IsOfferedAsync(AlertConditionKind kind, CancellationToken cancellationToken) =>
        Supports(kind) && await entitlement.LicenseListsCapabilityAsync(cancellationToken).ConfigureAwait(false);

    public async Task<string?> EnsureAllowedAsync(AlertConditionKind kind, CancellationToken cancellationToken)
    {
        if (!Supports(kind)) return AlertGovernanceService.ConditionUnavailable;
        var decision = await gate.EnsureAsync(LicenseImageUpdateEntitlement.Capability, cancellationToken).ConfigureAwait(false);
        return decision.Allowed ? null : decision.DenialCode;
    }
}

/// <summary>
/// Containers dos hosts com o estado do aviso, como nas listas: <c>container_behind</c> derivado pela imagem local
/// (D-008) e <c>outdated_since</c> da referência. Sem a capability na licença importada, nulo (nada a avaliar).
/// </summary>
public sealed class ImageOutdatedSource(IImageUpdateStore store, IImageUpdateTargetReader targets, IImageUpdateEntitlement entitlement) : IImageOutdatedSource
{
    public async Task<IReadOnlyList<ImageOutdatedTarget>?> ListAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken)
    {
        if (!await entitlement.LicenseListsCapabilityAsync(cancellationToken).ConfigureAwait(false)) return null;
        if (environmentIds.Count == 0) return [];
        var lookup = new ImageUpdateLookup(await store.ListAsync(environmentIds, cancellationToken).ConfigureAwait(false));
        var containers = await targets.ListContainersAsync(environmentIds, cancellationToken).ConfigureAwait(false);
        return containers
            .Select(c => (Container: c, Check: lookup.ForContainer(c.EnvironmentId, c.Reference, c.ImageId, stale: false)))
            .Select(x => new ImageOutdatedTarget(x.Container.EnvironmentId, x.Container.ContainerId, x.Container.Name, x.Container.Reference, x.Check?.State, x.Check?.OutdatedSince))
            .ToArray();
    }
}
