using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.Domain.Environments;
using Xunit;

namespace Dokpod.ControlPlane.Application.Tests.Authorization;

// Achado A4 da spec 005: leituras com muitos hosts fazem uma só consulta de grants, não uma decisão UMA por host.
public sealed class EnvironmentGrantsTests
{
    private static readonly AuthenticatedActor Actor = AuthenticatedActor.FromSubject("operador");

    [Fact]
    public async Task WithScope_ReadsGrantsOnceAndKeepsOnlyHostsWithTheScope()
    {
        var hosts = Enumerable.Range(0, 101).Select(i => Host(i)).ToArray();
        var reader = new Reader(hosts.Where((_, i) => i % 2 == 0).ToDictionary(
            h => h.EnvironmentId,
            h => (IReadOnlySet<string>)new HashSet<string> { EnvironmentResourceScopes.AuditRead }));

        var allowed = await reader.WithScopeAsync(hosts, EnvironmentResourceScopes.AuditRead, Actor, "token", Guid.NewGuid(), TestContext.Current.CancellationToken);
        var other = await reader.WithScopeAsync(hosts, EnvironmentResourceScopes.Manage, Actor, "token", Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Equal(hosts.Where((_, i) => i % 2 == 0), allowed);
        Assert.Empty(other);
        Assert.Equal(2, reader.Calls);
    }

    [Fact]
    public async Task WithScope_TreatsAnUnavailableProviderAsDenied()
    {
        var hosts = new[] { Host(1), Host(2) };

        Assert.Empty(await new Reader(null).WithScopeAsync(hosts, EnvironmentResourceScopes.Read, Actor, "token", Guid.NewGuid(), TestContext.Current.CancellationToken));
        Assert.Empty(await new Reader(null, fail: true).WithScopeAsync(hosts, EnvironmentResourceScopes.Read, Actor, "token", Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WithScope_DoesNotCallTheProviderWithoutCandidates()
    {
        var reader = new Reader(new Dictionary<Guid, IReadOnlySet<string>>());

        Assert.Empty(await reader.WithScopeAsync([], EnvironmentResourceScopes.Read, Actor, "token", Guid.NewGuid(), TestContext.Current.CancellationToken));
        Assert.Equal(0, reader.Calls);
    }

    private static EnvironmentRegistration Host(int index) =>
        EnvironmentRegistration.Create(Guid.NewGuid(), $"host-{index}", $"host-{index}", true, EnvironmentResourceScopes.Supported, EnvironmentStage.Development);

    private sealed class Reader(IReadOnlyDictionary<Guid, IReadOnlySet<string>>? grants, bool fail = false) : IEnvironmentGrantReader
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyDictionary<Guid, IReadOnlySet<string>>?> ReadAsync(AuthenticatedActor actor, string accessToken, Guid correlationId, CancellationToken cancellationToken)
        {
            Calls++;
            return fail ? throw new HttpRequestException("indisponível") : Task.FromResult(grants);
        }
    }
}
