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

    /// <summary>
    /// Hosts candidatos em que o usuário tem `scope`, numa só consulta ao provedor (achado A4 da spec 005: antes, uma decisão
    /// UMA por host). Só para leituras; ações continuam decididas uma a uma. Sem resposta do provedor, nenhum host: indisponível
    /// conta como negado.
    /// </summary>
    public static async Task<IReadOnlyList<EnvironmentRegistration>> WithScopeAsync(
        this IEnvironmentGrantReader reader,
        IEnumerable<EnvironmentRegistration> candidates,
        string scope,
        AuthenticatedActor actor,
        string accessToken,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var hosts = candidates.ToArray();
        if (hosts.Length == 0) return hosts;
        IReadOnlyDictionary<Guid, IReadOnlySet<string>>? grants;
        try
        {
            grants = await reader.ReadAsync(actor, accessToken, correlationId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            grants = null;
        }

        return grants is null
            ? []
            : hosts.Where(r => grants.TryGetValue(r.EnvironmentId, out var granted) && granted.Contains(scope)).ToArray();
    }
}
