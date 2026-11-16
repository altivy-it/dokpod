using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests;

/// <summary>
/// Spec 033 T032: o BFF chega à REST da API em HTTPS sem certificado de cliente (DOKPOD_API_HTTPS_PORT), enquanto a
/// porta do gRPC dos agentes continua exigindo mTLS.
/// </summary>
public sealed class InternalHttpsEndpointTests
{
    [Fact]
    public async Task HttpsPort_ServesRestWithoutClientCertificate_AndGrpcPortStillRequiresIt()
    {
        using var serverCertificate = TestCertificateFactory.CreateServerCertificate();
        var grpcPort = GetFreePort();
        var httpsPort = GetFreePort();
        var builder = ApiHost.CreateBuilder(
            [],
            new ApiHostOptions(
                GrpcPort: grpcPort,
                HealthPort: GetFreePort(),
                ServerCertificate: serverCertificate,
                ClientCertificateValidation: (_, _, _) => true,
                LoopbackOnly: true,
                HttpsPort: httpsPort));
        await using var app = builder.Build();
        ApiHost.MapEndpoints(app);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
                certificate is not null && certificate.Thumbprint == serverCertificate.Thumbprint,
        };
        using var client = new HttpClient(handler);

        using var response = await client.GetAsync(
            new Uri($"https://localhost:{httpsPort}/health/live"),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await Assert.ThrowsAnyAsync<HttpRequestException>(() => client.GetAsync(
            new Uri($"https://localhost:{grpcPort}/health/live"),
            TestContext.Current.CancellationToken));
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
