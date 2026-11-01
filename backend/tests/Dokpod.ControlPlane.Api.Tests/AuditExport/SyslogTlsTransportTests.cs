using System.Diagnostics;
using System.Net;
using Dokpod.ControlPlane.Application.AuditExport;
using Dokpod.ControlPlane.Infrastructure.AuditExport;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests.AuditExport;

/// <summary>
/// Spec 027 T015: <see cref="SyslogTlsTransport"/> contra um servidor TLS real em processo, com CA e certificados
/// gerados na execução. O nome <c>siem.test</c> não existe no DNS: a conexão só funciona porque vai ao IP validado.
/// </summary>
public sealed class SyslogTlsTransportTests : IDisposable
{
    private const string Host = "siem.test";
    private readonly TestPki pki = new();
    private readonly string root = Directory.CreateTempSubdirectory("dokpod-027-transport-").FullName;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ValidCertificate_DeliversTheWholeBatch()
    {
        using var certificate = pki.Issue(Host, server: true);
        await using var server = SyslogTlsTestServer.Tls(certificate);
        TestPki.WriteSecret(root, "soc", pki.CaPem);
        var resolver = new FixedResolver(IPAddress.Loopback);

        var result = await Transport(resolver).SendAsync(Destination(server.Port, "soc"), [Record("container.start"), Record("container.stop")], Ct);

        Assert.Equal(AuditExportSendResult.Ok, result);
        var records = await server.WaitForAsync(items => items.Count == 2, TimeSpan.FromSeconds(5));
        Assert.Equal(["container.start", "container.stop"], records.Select(r => r!["action"]!.GetValue<string>()));
        Assert.Equal(1, resolver.Calls);
        Assert.Equal(1, server.Connections);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("unknown-ca")]
    [InlineData("wrong-name")]
    public async Task InvalidServerCertificate_IsTlsError(string scenario)
    {
        using var certificate = scenario switch
        {
            "expired" => pki.Issue(Host, server: true, DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(-1)),
            "wrong-name" => pki.Issue("outro.test", server: true),
            _ => pki.Issue(Host, server: true),
        };
        await using var server = SyslogTlsTestServer.Tls(certificate);
        // Sem ca.pem, vale o trust store do sistema, que não conhece a CA de teste.
        TestPki.WriteSecret(root, "soc", scenario == "unknown-ca" ? null : pki.CaPem);

        var result = await Transport(new FixedResolver(IPAddress.Loopback)).SendAsync(Destination(server.Port, "soc"), [Record("container.start")], Ct);

        Assert.Equal(AuditExportSendResult.Failed(AuditExportErrorCodes.TlsError), result);
        Assert.Empty(server.Messages);
    }

    [Fact]
    public async Task MutualTls_SendsTheClientCertificate()
    {
        using var certificate = pki.Issue(Host, server: true);
        using var client = pki.Issue("dokpod-client", server: false);
        await using var server = SyslogTlsTestServer.Tls(certificate, clientCa: pki.Ca);
        TestPki.WriteSecret(root, "mtls", pki.CaPem, client);
        TestPki.WriteSecret(root, "sem-cliente", pki.CaPem);
        var transport = Transport(new FixedResolver(IPAddress.Loopback));

        Assert.Equal(AuditExportSendResult.Ok, await transport.SendAsync(Destination(server.Port, "mtls"), [Record("container.start")], Ct));
        Assert.Single(await server.WaitForAsync(items => items.Count == 1, TimeSpan.FromSeconds(5)));

        Assert.False((await transport.SendAsync(Destination(server.Port, "sem-cliente"), [Record("container.stop")], Ct)).Delivered);
        await Task.Delay(300, Ct);
        Assert.Single(server.Messages);
    }

    [Fact]
    public async Task MissingSecret_IsSecretMissingWithoutConnecting()
    {
        using var certificate = pki.Issue(Host, server: true);
        await using var server = SyslogTlsTestServer.Tls(certificate);
        var resolver = new FixedResolver(IPAddress.Loopback);

        var result = await Transport(resolver).SendAsync(Destination(server.Port, "nao-existe"), [Record("container.start")], Ct);

        Assert.Equal(AuditExportSendResult.Failed(AuditExportErrorCodes.SecretMissing), result);
        Assert.Equal((0, 0), (resolver.Calls, server.Connections));
    }

    [Fact]
    public async Task PlainServer_NeverReceivesTheRecordInClear()
    {
        await using var server = SyslogTlsTestServer.Plain();
        TestPki.WriteSecret(root, "soc", pki.CaPem);

        var result = await Transport(new FixedResolver(IPAddress.Loopback)).SendAsync(Destination(server.Port, "soc"), [Record("container.start")], Ct);

        Assert.Equal(AuditExportErrorCodes.TlsError, result.ErrorCode);
        var first = Assert.Single(server.RawFirstBytes);
        Assert.Equal(0x16, first[0]); // registro TLS de handshake (ClientHello)
        Assert.DoesNotContain("container.start", System.Text.Encoding.ASCII.GetString(first), StringComparison.Ordinal);
    }

    [Fact]
    public async Task StalledHandshake_TimesOut_AndDefaultsFollowL2()
    {
        await using var server = SyslogTlsTestServer.Stalled();
        TestPki.WriteSecret(root, "soc", pki.CaPem);
        var transport = new SyslogTlsTransport(
            new FixedResolver(IPAddress.Loopback),
            new MountedSecretReader(root),
            new SyslogTlsTransportOptions { ConnectTimeout = TimeSpan.FromSeconds(1), SendTimeout = TimeSpan.FromSeconds(1) });
        var stopwatch = Stopwatch.StartNew();

        var result = await transport.SendAsync(Destination(server.Port, "soc"), [Record("container.start")], Ct);

        Assert.Equal(AuditExportErrorCodes.Timeout, result.ErrorCode);
        Assert.InRange(stopwatch.Elapsed, TimeSpan.FromMilliseconds(800), TimeSpan.FromSeconds(5));
        var defaults = new SyslogTlsTransportOptions();
        Assert.Equal((TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)), (defaults.ConnectTimeout, defaults.SendTimeout));
    }

    [Fact]
    public async Task NameResolvingToABlockedAddress_NeverConnects()
    {
        using var certificate = pki.Issue(Host, server: true);
        await using var server = SyslogTlsTestServer.Tls(certificate);
        TestPki.WriteSecret(root, "soc", pki.CaPem);
        var resolver = new DnsDestinationAddressResolver(new DestinationAddressPolicy(), (_, _) => Task.FromResult(new[] { IPAddress.Loopback }));

        var result = await Transport(resolver).SendAsync(Destination(server.Port, "soc"), [Record("container.start")], Ct);

        Assert.Equal(AuditExportSendResult.Failed(AuditExportErrorCodes.AddressBlocked), result);
        await Task.Delay(200, Ct);
        Assert.Equal(0, server.Connections);
    }

    private SyslogTlsTransport Transport(IDestinationAddressResolver resolver) => new(resolver, new MountedSecretReader(root));

    private static AuditExportDestination Destination(int port, string? secretRef) => new(
        Guid.NewGuid(), "siem", AuditExportProtocols.Syslog, Host, port, null, secretRef, ["audit_export", "containers"], true, 1,
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private static AuditExportRecord Record(string action) => new(
        Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), Guid.NewGuid(), null, "user", "user-1", null, action, "containers",
        Guid.NewGuid(), "web-1", "succeeded", null, new Dictionary<string, string>(), Test: false);

    public void Dispose()
    {
        pki.Dispose();
        Directory.Delete(root, recursive: true);
    }
}
