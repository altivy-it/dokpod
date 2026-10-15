using System.Security.Claims;
using Dokpod.ControlPlane.Api.Auditing;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests.Auditing;

/// <summary>Spec 017 D-001: nome do ator da requisição, com o último nome visto como reserva.</summary>
public sealed class AuditActorNameProviderTests
{
    private static ClaimsPrincipal User(string subject, string? name) =>
        new(new ClaimsIdentity(name is null ? [new Claim("sub", subject)] : [new Claim("sub", subject), new Claim("preferred_username", name)], "test"));

    [Fact]
    public void UsesTheRequestUserOnlyWhenItIsTheActor()
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = User("sub-ana", "ana") } };
        var provider = new HttpAuditActorNameProvider(accessor, new AuditActorNameCache());

        Assert.Equal("ana", provider.NameFor("sub-ana"));
        Assert.Null(provider.NameFor("sub-bruno"));
    }

    [Fact]
    public void FallsBackToTheLastNameSeenOutsideARequest()
    {
        var cache = new AuditActorNameCache();
        cache.Remember(User("sub-ana", "ana"));
        cache.Remember(User("sub-sem-nome", null));
        var provider = new HttpAuditActorNameProvider(new HttpContextAccessor(), cache);

        Assert.Equal("ana", provider.NameFor("sub-ana")); // fim do terminal, sem requisição
        Assert.Null(provider.NameFor("sub-sem-nome"));
    }
}
