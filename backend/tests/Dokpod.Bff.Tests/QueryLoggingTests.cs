using Dokpod.Bff;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Dokpod.Bff.Tests;

/// <summary>Spec 011 T011: URLs com `contains` não chegam aos logs do BFF (relay e hosting).</summary>
public sealed class QueryLoggingTests
{
    [Theory]
    [InlineData("Microsoft.AspNetCore.Hosting.Diagnostics")]
    [InlineData("System.Net.Http.HttpClient.downstream-api.LogicalHandler")]
    public void RequestUrlCategories_DoNotLogAtInformation(string category)
    {
        var builder = BffHost.CreateBuilder([]);
        using var provider = builder.Services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ILoggerFactory>();

        Assert.False(factory.CreateLogger(category).IsEnabled(LogLevel.Information));
        Assert.True(factory.CreateLogger(category).IsEnabled(LogLevel.Warning));
        Assert.True(factory.CreateLogger("Dokpod.Bff").IsEnabled(LogLevel.Information));
    }
}
