using System.Net;
using Dokpod.ControlPlane.Application.AuditExport;
using Dokpod.ControlPlane.Infrastructure.AuditExport;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests.AuditExport;

/// <summary>Spec 027 T010 (L3): a política vale sobre o IP resolvido, inclusive para nomes que apontam para loopback.</summary>
public sealed class DnsDestinationAddressResolverTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task NameResolvingToLoopback_IsBlocked_WithRealDns()
    {
        var resolver = new DnsDestinationAddressResolver(new DestinationAddressPolicy());

        var result = await resolver.ResolveAsync("localhost", Ct);

        Assert.Equal(AuditExportErrorCodes.AddressBlocked, result.ErrorCode);
    }

    [Fact]
    public async Task ResolvedAddresses_AreTheOnesReturned_AndRebindingIsCaughtOnEachResolution()
    {
        var answers = new Queue<IPAddress[]>([[IPAddress.Parse("10.0.0.5")], [IPAddress.Parse("169.254.169.254")]]);
        var resolver = new DnsDestinationAddressResolver(new DestinationAddressPolicy(), (_, _) => Task.FromResult(answers.Dequeue()));

        var first = await resolver.ResolveAsync("siem.exemplo.interno", Ct);
        var second = await resolver.ResolveAsync("siem.exemplo.interno", Ct);

        Assert.Equal([IPAddress.Parse("10.0.0.5")], first.Allowed);
        Assert.Equal(AuditExportErrorCodes.AddressBlocked, second.ErrorCode);
    }

    [Fact]
    public async Task LiteralAddress_SkipsDns_AndUnknownName_FailsAsConnection()
    {
        var resolver = new DnsDestinationAddressResolver(new DestinationAddressPolicy(), (_, _) => throw new System.Net.Sockets.SocketException(11001));

        Assert.True((await resolver.ResolveAsync("10.0.0.5", Ct)).IsAllowed);
        Assert.Equal(AuditExportErrorCodes.ConnectionRefused, (await resolver.ResolveAsync("nao-existe.invalid", Ct)).ErrorCode);
    }
}
