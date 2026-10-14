using System.Security.Claims;
using Dokpod.Bff.Endpoints;
using Xunit;

namespace Dokpod.Bff.Tests;

public sealed class BffSessionResponseTests
{
    private static ClaimsPrincipal User(params Claim[] extra) =>
        new(new ClaimsIdentity([new Claim("sub", "u-1"), new Claim(ClaimTypes.Name, "Mariana Costa"), .. extra], "test", ClaimTypes.Name, ClaimTypes.Role));

    [Fact]
    public void FromPrincipal_ExposesOnlyTheConfiguredDisplayGroupClaim()
    {
        var principal = User(new Claim("dokpod_display_group", "Plataforma · SRE"), new Claim("groups", "/admins"), new Claim("groups", "/sre"));

        var response = BffSessionResponse.FromPrincipal(principal, "dokpod_display_group");

        Assert.Equal(new BffSessionResponse(true, "u-1", "Mariana Costa", "Plataforma · SRE"), response);
    }

    [Fact]
    public void FromPrincipal_WithoutConfiguredClaim_OmitsDisplayGroup()
    {
        var response = BffSessionResponse.FromPrincipal(User(new Claim("groups", "/sre")), displayGroupClaim: null);

        Assert.Null(response.DisplayGroup);
    }

    [Fact]
    public void FromPrincipal_TruncatesLongDisplayGroup()
    {
        var response = BffSessionResponse.FromPrincipal(User(new Claim("g", new string('x', 200))), "g");

        Assert.Equal(80, response.DisplayGroup!.Length);
    }

    [Fact]
    public void FromPrincipal_Anonymous_RevealsNothing()
    {
        var response = BffSessionResponse.FromPrincipal(new ClaimsPrincipal(new ClaimsIdentity()), "g");

        Assert.Equal(new BffSessionResponse(false, null, null), response);
    }
}
