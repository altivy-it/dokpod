using System.Collections.Frozen;
using Dokpod.Domain.Environments;

namespace Dokpod.ControlPlane.Application.Authorization;

/// <summary>
/// Scopes concedidos ao usuário em cada host, numa só consulta ao provedor de identidade (spec 023). Serve para o console
/// oferecer só as ações permitidas; cada ação continua decidida no provedor no momento do envio.
/// </summary>
public interface IEnvironmentGrantReader
{
    /// <summary>Scopes por host; nulo se o provedor não respondeu (o chamador mantém o comportamento anterior).</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlySet<string>>?> ReadAsync(
        AuthenticatedActor actor,
        string accessToken,
        Guid correlationId,
        CancellationToken cancellationToken);
}

public static class EnvironmentGrants
{
    /// <summary>
    /// `Scopes` do host passa a ser o cadastro ∩ o concedido ao usuário (spec 023). Sem resposta do provedor (`grants` nulo),
    /// mantém o cadastro: cada ação continua decidida no provedor no envio.
    /// </summary>
    public static EnvironmentRegistration Restrict(EnvironmentRegistration registration, IReadOnlyDictionary<Guid, IReadOnlySet<string>>? grants) =>
        grants is null ? registration : registration with
        {
            Scopes = registration.Scopes
                .Where(scope => grants.TryGetValue(registration.EnvironmentId, out var granted) && granted.Contains(scope))
                .ToFrozenSet(StringComparer.Ordinal),
        };
}
