using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Environments;
using Dokpod.Domain.Environments;

namespace Dokpod.ControlPlane.Application.Hosts;

public sealed record AuthorizedEnvironments(
    IReadOnlyList<EnvironmentRegistration> Registrations,
    bool Available,
    bool Truncated,
    string? FailureCode);

/// <summary>Todos os ambientes que o usuário pode ler (até 500 decisões de autorização por requisição).</summary>
public sealed class AuthorizedEnvironmentsService(EnvironmentRegistrationService registrationService)
{
    public async Task<AuthorizedEnvironments> ListAllAsync(
        AuthenticatedActor actor,
        string accessToken,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var all = new List<EnvironmentRegistration>();
        Guid? cursor = null;
        for (var page = 0; page < 5; page++)
        {
            var result = await registrationService.ListAsync(cursor, 100, actor, accessToken, correlationId, cancellationToken)
                .ConfigureAwait(false);
            if (!result.Available)
            {
                return new AuthorizedEnvironments([], false, false, result.FailureCode);
            }

            all.AddRange(result.Registrations);
            cursor = result.NextCursor;
            if (cursor is null)
            {
                return new AuthorizedEnvironments(all, true, false, null);
            }
        }

        return new AuthorizedEnvironments(all, true, true, null);
    }
}
