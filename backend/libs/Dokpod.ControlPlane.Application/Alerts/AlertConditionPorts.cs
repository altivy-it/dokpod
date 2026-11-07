using Dokpod.Domain.Alerts;

namespace Dokpod.ControlPlane.Application.Alerts;

/// <summary>
/// Porta neutra das condições que dependem de um módulo Business (spec 025 T021, D-010). Só a composição Business a
/// registra; sem ela, criar ou editar uma regra com a condição é recusado com <c>422 alert_condition_unavailable</c>.
/// </summary>
public interface IAlertConditionAvailability
{
    bool Supports(AlertConditionKind kind);

    /// <summary>A tela pode oferecer a condição (a licença importada lista a capability).</summary>
    Task<bool> IsOfferedAsync(AlertConditionKind kind, CancellationToken cancellationToken);

    /// <summary>Gate do direito comercial ao criar ou editar; nulo quando permitido, ou o código da recusa.</summary>
    Task<string?> EnsureAllowedAsync(AlertConditionKind kind, CancellationToken cancellationToken);
}

/// <summary>
/// Container com o estado do aviso de nova versão da sua imagem (spec 025 T022). <c>State</c> nulo: ainda sem
/// verificação. <c>OutdatedSinceUtc</c> é o da referência, preenchido só enquanto ela está desatualizada.
/// </summary>
public sealed record ImageOutdatedTarget(
    Guid EnvironmentId, string ContainerId, string ContainerName, string Reference, string? State, DateTimeOffset? OutdatedSinceUtc);

/// <summary>Porta neutra da varredura para a condição <see cref="AlertConditionKind.ImageOutdated"/>; só na composição Business.</summary>
public interface IImageOutdatedSource
{
    /// <summary>Containers dos hosts; nulo quando o aviso não está disponível (a varredura não abre nem resolve nada).</summary>
    Task<IReadOnlyList<ImageOutdatedTarget>?> ListAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken);
}
