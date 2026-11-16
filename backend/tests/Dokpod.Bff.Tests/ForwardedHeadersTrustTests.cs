using System.Net;
using Dokpod.Bff.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Dokpod.Bff.Tests;

/// <summary>
/// Spec 033 T026 (L1 do threat model): o BFF só aceita X-Forwarded-* de quem está na rede confiável que o instalador grava
/// (a rede interna da borda). De outro endereço, esquema, host e IP do cliente ficam como chegaram.
/// </summary>
public sealed class ForwardedHeadersTrustTests
{
    [Theory]
    [InlineData("10.253.33.7", "https", "203.0.113.10")]
    [InlineData("203.0.113.99", "http", "203.0.113.99")]
    [InlineData("10.253.34.7", "http", "10.253.34.7")]
    public async Task ForwardedHeaders_AreHonoredOnlyFromTheEdgeNetwork(string remote, string expectedScheme, string expectedClient)
    {
        var options = new ForwardedHeadersOptions();
        new ConfigureForwardedHeadersOptions(Options.Create(new BffSecurityOptions { TrustedNetworks = ["10.253.33.0/24"] })).Configure(options);
        var seen = (Scheme: string.Empty, Client: string.Empty);
        var middleware = new ForwardedHeadersMiddleware(
            context =>
            {
                seen = (context.Request.Scheme, context.Connection.RemoteIpAddress!.ToString());
                return Task.CompletedTask;
            },
            NullLoggerFactory.Instance,
            Options.Create(options));
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(remote);
        context.Request.Scheme = "http";
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.10";

        await middleware.Invoke(context);

        Assert.Equal((expectedScheme, expectedClient), seen);
    }
}
