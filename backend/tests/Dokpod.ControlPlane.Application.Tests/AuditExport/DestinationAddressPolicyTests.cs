using System.Net;
using Dokpod.ControlPlane.Application.AuditExport;
using Xunit;

namespace Dokpod.ControlPlane.Application.Tests.AuditExport;

/// <summary>Spec 027 T010 (D-007, threat model B2): endereços proibidos e permitidos para destinos de exportação.</summary>
public sealed class DestinationAddressPolicyTests
{
    private static readonly DestinationAddressPolicy Policy = DestinationAddressPolicy.FromCidrs(["10.89.0.0/24", "fd00:d0c::/64"]);

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.9.9.9")]
    [InlineData("::1")]
    [InlineData("169.254.169.254")]
    [InlineData("169.254.1.1")]
    [InlineData("fe80::1")]
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("ff02::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("10.89.0.15")]
    [InlineData("fd00:d0c::5")]
    public void Blocked(string address) => Assert.True(Policy.IsBlocked(IPAddress.Parse(address)));

    [Theory]
    [InlineData("10.0.0.5")]
    [InlineData("10.89.1.15")]
    [InlineData("172.16.4.2")]
    [InlineData("192.168.1.20")]
    [InlineData("203.0.113.10")]
    [InlineData("fd12::5")]
    [InlineData("2001:db8::1")]
    public void Allowed(string address) => Assert.False(Policy.IsBlocked(IPAddress.Parse(address)));

    [Fact]
    public void OneBlockedAddress_RefusesTheWholeHost()
    {
        var mixed = Policy.Evaluate([IPAddress.Parse("10.0.0.5"), IPAddress.Parse("127.0.0.1")]);
        var allowed = Policy.Evaluate([IPAddress.Parse("10.0.0.5")]);

        Assert.Equal(AuditExportErrorCodes.AddressBlocked, mixed.ErrorCode);
        Assert.Empty(mixed.Allowed);
        Assert.True(allowed.IsAllowed);
        Assert.False(Policy.Evaluate([]).IsAllowed);
    }

    [Theory]
    [InlineData("10.0.0.0")]
    [InlineData("10.0.0.0/33")]
    [InlineData("rede/8")]
    public void InvalidControlPlaneNetwork_IsRefused(string cidr) =>
        Assert.Throws<FormatException>(() => DestinationAddressPolicy.FromCidrs([cidr]));
}
