using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.Domain.Environments;
using Xunit;

namespace Dokpod.ControlPlane.Application.Tests.ChangeWindows;

/// <summary>
/// Spec 026 T032: `change:override` chega ao console pela leitura de grants, para a tela saber quando oferecer a
/// exceção; sem a concessão no Keycloak, ele some do host, e a API decide de novo em toda tentativa.
/// </summary>
public sealed class ChangeWindowGrantsTests
{
    private static readonly Guid Host = Guid.Parse("0c3c1d5e-7a8b-4c2d-9e1f-2a3b4c5d6e7f");

    [Fact]
    public void Restrict_KeepsChangeOverride_OnlyWhenGranted()
    {
        var registration = EnvironmentRegistration.Create(Host, "prod-1", "srv-1", true, EnvironmentResourceScopes.Supported, EnvironmentStage.Production);

        var granted = EnvironmentGrants.Restrict(registration, new Dictionary<Guid, IReadOnlySet<string>>
        {
            [Host] = new HashSet<string> { EnvironmentResourceScopes.Read, EnvironmentResourceScopes.ChangeOverride },
        });
        var notGranted = EnvironmentGrants.Restrict(registration, new Dictionary<Guid, IReadOnlySet<string>>
        {
            [Host] = new HashSet<string> { EnvironmentResourceScopes.Read, EnvironmentResourceScopes.StopContainer },
        });

        Assert.Contains(EnvironmentResourceScopes.ChangeOverride, granted.Scopes);
        Assert.DoesNotContain(EnvironmentResourceScopes.ChangeOverride, notGranted.Scopes);
        Assert.Contains(EnvironmentResourceScopes.ChangeOverride, EnvironmentResourceScopes.Supported);
    }
}
