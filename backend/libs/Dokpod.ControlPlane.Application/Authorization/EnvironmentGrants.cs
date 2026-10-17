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
