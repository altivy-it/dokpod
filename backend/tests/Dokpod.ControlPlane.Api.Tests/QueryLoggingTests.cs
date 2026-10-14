using Dokpod.ControlPlane.Api;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests;

/// <summary>Spec 011 T011: o termo `contains` dos logs sob demanda não chega aos logs da API.</summary>
public sealed class QueryLoggingTests
{
    [Fact]
    public void HostingRequestLog_IsAboveInformation()
    {
        using var certificate = TestCertificateFactory.CreateServerCertificate();
        var builder = ApiHost.CreateBuilder([], new ApiHostOptions(7443, 8080, certificate, null, true));
        using var provider = builder.Services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ILoggerFactory>();

        Assert.False(factory.CreateLogger("Microsoft.AspNetCore.Hosting.Diagnostics").IsEnabled(LogLevel.Information));
        Assert.False(factory.CreateLogger("System.Net.Http.HttpClient.keycloak.LogicalHandler").IsEnabled(LogLevel.Information));
        Assert.True(factory.CreateLogger("Dokpod.ControlPlane.Api").IsEnabled(LogLevel.Information));
    }
}
