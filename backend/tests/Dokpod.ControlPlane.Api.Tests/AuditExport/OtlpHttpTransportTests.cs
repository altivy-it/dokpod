using System.Diagnostics;
using System.Net;
using Dokpod.ControlPlane.Application.AuditExport;
using Dokpod.ControlPlane.Infrastructure.AuditExport;
using OpenTelemetry.Proto.Logs.V1;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests.AuditExport;

/// <summary>
/// Spec 027 T029: <see cref="OtlpHttpTransport"/> contra um receptor HTTPS em processo com certificado gerado na
/// execução. O nome <c>otel.test</c> não existe no DNS: a conexão só funciona porque vai ao IP validado (L3).
/// </summary>
public sealed class OtlpHttpTransportTests : IAsyncLifetime
{
    private const string Host = "otel.test";
    private static readonly Guid Installation = Guid.Parse("3f0c5a1e-2b4d-4c6e-8f90-a1b2c3d4e5f6");
    private readonly TestPki pki = new();
    private readonly string root = Directory.CreateTempSubdirectory("dokpod-027-otlp-").FullName;
    private System.Security.Cryptography.X509Certificates.X509Certificate2? certificate;
    private OtlpTestReceiver? receiver;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private OtlpTestReceiver Receiver => receiver!;

    public async ValueTask InitializeAsync()
    {
        certificate = pki.Issue(Host, server: true);
        receiver = await OtlpTestReceiver.StartAsync(certificate, Ct);
        TestPki.WriteSecret(root, "otel", pki.CaPem);
    }

    [Fact]
    public async Task Batch_IsOneResourceAndScope_WithTheContractFields()
    {
        var success = Record("container.restart", "succeeded") with { Details = new Dictionary<string, string> { ["reason"] = "x", ["kind"] = "y" } };
        var denied = Record("container.exec.denied", "denied") with { FailureCode = "authorization_denied", CommandId = null };

        var result = await Transport().SendAsync(Destination(), [success, denied], Ct);

        Assert.Equal(AuditExportSendResult.Ok, result);
        var (request, authorization, contentType, path) = Assert.Single(Receiver.Received);
        Assert.Equal(("/v1/logs", "application/x-protobuf", (string?)null), (path, contentType, authorization));
        var resourceLogs = Assert.Single(request.ResourceLogs);
        Assert.Equal(
            [("service.name", "dokpod"), ("dokpod.installation.id", Installation.ToString("D"))],
            resourceLogs.Resource.Attributes.Select(a => (a.Key, a.Value.StringValue)));
        var scope = Assert.Single(resourceLogs.ScopeLogs);
        Assert.Equal("dokpod.audit", scope.Scope.Name);
        Assert.Equal(2, scope.LogRecords.Count);

        var info = scope.LogRecords[0];
        Assert.Equal((ulong)(success.OccurredAtUtc - DateTimeOffset.UnixEpoch).Ticks * 100, info.TimeUnixNano);
        Assert.Equal((SeverityNumber.Info, "INFO", "container.restart"), (info.SeverityNumber, info.SeverityText, info.Body.StringValue));
        var attributes = info.Attributes.ToDictionary(a => a.Key, a => a.Value);
        Assert.Equal(success.EventId.ToString("D"), attributes["dokpod.event.id"].StringValue);
        Assert.Equal(("user", "user-1", "Maria"), (attributes["dokpod.actor.kind"].StringValue, attributes["dokpod.actor.id"].StringValue, attributes["dokpod.actor.name"].StringValue));
        Assert.Equal(("containers", "succeeded"), (attributes["dokpod.action.category"].StringValue, attributes["dokpod.outcome"].StringValue));
        Assert.Equal("{\"kind\":\"y\",\"reason\":\"x\"}", attributes["dokpod.details"].StringValue);
        Assert.False(attributes["dokpod.test"].BoolValue);
        Assert.Equal(success.CommandId!.Value.ToString("D"), attributes["dokpod.command.id"].StringValue);
        Assert.DoesNotContain("dokpod.failure.code", attributes.Keys);

        var warn = scope.LogRecords[1];
        Assert.Equal((SeverityNumber.Warn, "WARN"), (warn.SeverityNumber, warn.SeverityText));
        Assert.Contains(warn.Attributes, a => a.Key == "dokpod.failure.code" && a.Value.StringValue == "authorization_denied");
        Assert.DoesNotContain(warn.Attributes, a => a.Key == "dokpod.command.id");
    }

    [Fact]
    public async Task Token_GoesAsBearer_AndIsReadAgainOnEveryConnection()
    {
        var directory = TestPki.WriteSecret(root, "otel-token", pki.CaPem);
        await File.WriteAllTextAsync(Path.Combine(directory, "token"), "token-um\n", Ct);
        var transport = Transport();

        Assert.True((await transport.SendAsync(Destination("otel-token"), [Record()], Ct)).Delivered);
        await File.WriteAllTextAsync(Path.Combine(directory, "token"), "token-dois", Ct);
        Assert.True((await transport.SendAsync(Destination("otel-token"), [Record()], Ct)).Delivered);

        Assert.Equal(["Bearer token-um", "Bearer token-dois"], Receiver.Received.Select(item => item.Authorization));
    }

    [Theory]
    [InlineData(HttpStatusCode.Accepted, null)]
    [InlineData(HttpStatusCode.BadRequest, AuditExportErrorCodes.Rejected)]
    [InlineData(HttpStatusCode.Unauthorized, AuditExportErrorCodes.Rejected)]
    [InlineData(HttpStatusCode.Redirect, AuditExportErrorCodes.Rejected)]
    [InlineData(HttpStatusCode.RequestTimeout, AuditExportErrorCodes.Timeout)]
    [InlineData(HttpStatusCode.TooManyRequests, AuditExportErrorCodes.ConnectionRefused)]
    [InlineData(HttpStatusCode.ServiceUnavailable, AuditExportErrorCodes.ConnectionRefused)]
    public async Task Status_IsClassified_AndRedirectsAreNotFollowed(HttpStatusCode status, string? errorCode)
    {
        Receiver.Status = status;

        var result = await Transport().SendAsync(Destination(), [Record()], Ct);

        Assert.Equal(errorCode, result.ErrorCode);
        Assert.Equal(errorCode is null, result.Delivered);
        // Um envio só: o 302 aponta para outra porta e nunca é seguido (L1).
        Assert.Single(Receiver.Received);
    }

    [Fact]
    public async Task UntrustedCertificate_IsTlsError()
    {
        TestPki.WriteSecret(root, "sem-ca", null);

        var result = await Transport().SendAsync(Destination("sem-ca"), [Record()], Ct);

        Assert.Equal(AuditExportErrorCodes.TlsError, result.ErrorCode);
        Assert.Empty(Receiver.Received);
    }

    [Fact]
    public async Task WrongName_IsTlsError()
    {
        var result = await Transport().SendAsync(Destination() with { Host = "outro.test" }, [Record()], Ct);

        Assert.Equal(AuditExportErrorCodes.TlsError, result.ErrorCode);
    }

    [Fact]
    public async Task SlowReceiver_TimesOut_AndLargeResponsesAreReadOnlyUpTo64KiB()
    {
        Receiver.Delay = TimeSpan.FromSeconds(5);
        var quick = new OtlpHttpTransport(new FixedResolver(IPAddress.Loopback), new MountedSecretReader(root), new OtlpHttpTransportOptions { SendTimeout = TimeSpan.FromSeconds(1) });
        var stopwatch = Stopwatch.StartNew();
        Assert.Equal(AuditExportErrorCodes.Timeout, (await quick.SendAsync(Destination(), [Record()], Ct)).ErrorCode);
        Assert.InRange(stopwatch.Elapsed, TimeSpan.FromMilliseconds(800), TimeSpan.FromSeconds(4));

        Receiver.Delay = TimeSpan.Zero;
        Receiver.ResponseBytes = 64 * 1024 * 1024;
        stopwatch.Restart();
        Assert.True((await Transport().SendAsync(Destination(), [Record()], Ct)).Delivered);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), "a resposta grande não pode ser lida inteira");
        var defaults = new OtlpHttpTransportOptions();
        Assert.Equal((TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), 65536), (defaults.ConnectTimeout, defaults.SendTimeout, defaults.MaxResponseBytes));
    }

    [Fact]
    public async Task BlockedAddressOrMissingSecret_NeverConnects()
    {
        var blocked = new OtlpHttpTransport(
            new DnsDestinationAddressResolver(new DestinationAddressPolicy(), (_, _) => Task.FromResult(new[] { IPAddress.Loopback })),
            new MountedSecretReader(root));

        Assert.Equal(AuditExportErrorCodes.AddressBlocked, (await blocked.SendAsync(Destination(), [Record()], Ct)).ErrorCode);
        Assert.Equal(AuditExportErrorCodes.SecretMissing, (await Transport().SendAsync(Destination("nao-existe"), [Record()], Ct)).ErrorCode);
        Assert.Empty(Receiver.Received);
    }

    [Fact]
    public async Task CustomPath_IsUsed()
    {
        Assert.True((await Transport().SendAsync(Destination() with { Path = "/coletor/v1/logs" }, [Record()], Ct)).Delivered);

        Assert.Equal("/coletor/v1/logs", Assert.Single(Receiver.Received).Path);
    }

    private OtlpHttpTransport Transport() => new(new FixedResolver(IPAddress.Loopback), new MountedSecretReader(root));

    private AuditExportDestination Destination(string secretRef = "otel") => new(
        Guid.NewGuid(), "otel", AuditExportProtocols.Otlp, Host, Receiver.Port, "/v1/logs", secretRef, ["audit_export", "containers"], true, 1,
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private static AuditExportRecord Record(string action = "container.restart", string outcome = "succeeded") => new(
        Guid.NewGuid(), new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero).AddTicks(1_234_567), Installation, Guid.NewGuid(), Guid.NewGuid(),
        "user", "user-1", "Maria", action, "containers", Guid.NewGuid(), "web-1", outcome, null, new Dictionary<string, string>(), Test: false);

    public async ValueTask DisposeAsync()
    {
        if (receiver is not null) await receiver.DisposeAsync();
        certificate?.Dispose();
        pki.Dispose();
        Directory.Delete(root, recursive: true);
    }
}
