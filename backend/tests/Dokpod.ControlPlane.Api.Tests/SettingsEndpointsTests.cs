using System.Text.Json;
using Dokpod.ControlPlane.Api.Commands;
using Dokpod.ControlPlane.Api.Endpoints;
using Dokpod.ControlPlane.Application.Metrics;
using Dokpod.ControlPlane.Application.Terminal;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests;

/// <summary>Spec 022: retenções e limites efetivos, com origem e sem segredos.</summary>
public sealed class SettingsEndpointsTests
{
    private const string Canary = "canario-segredo-022";

    [Fact]
    public void Parameters_ShowEffectiveValuesWithTheirSource()
    {
        var configuration = Configuration(new()
        {
            ["Commands:Retention"] = "30.00:00:00",
            ["Terminal:MaxPerUser"] = "3",
        });

        var parameters = SettingsEndpoints.Parameters(configuration,
            new CommandRetentionOptions { Retention = TimeSpan.FromDays(30) }, new MetricsOptions(), new TerminalOptions { MaxPerUser = 3 });

        var commands = Assert.Single(parameters, p => p.Key == "Commands:Retention");
        Assert.Equal(("30 dias", "installation"), (commands.Value, commands.Source));
        Assert.Equal(("3", "installation"), Single(parameters, "Terminal:MaxPerUser"));
        Assert.Equal(("30 dias", "default"), Single(parameters, "EventRetention:Period"));
        Assert.Equal(("90 dias", "default"), Single(parameters, "Alerts:ResolvedRetention"));
        Assert.Equal(("2 dias", "default"), Single(parameters, "Metrics:HostRetention"));
        Assert.Equal(("10 min", "default"), Single(parameters, "Terminal:IdleTimeout"));
        Assert.Equal(("Produção", "default"), Single(parameters, "Terminal:RequireJustificationStages"));
        Assert.Contains(parameters, p => p.Key is null && p.Label == "Ações e remoções em lote" && p.Source == "default");
        Assert.All(parameters, p => Assert.False(string.IsNullOrWhiteSpace(p.Value)));
    }

    [Fact]
    public void Parameters_NeverExposeSecretsFromConfiguration()
    {
        var configuration = Configuration(new()
        {
            ["ConnectionStrings:ControlPlane"] = $"Host=db;Database={Canary}",
            ["Authentication:Keycloak:ClientSecret"] = Canary,
            ["Commands:Retention"] = "7.00:00:00",
        });

        var json = JsonSerializer.Serialize(SettingsEndpoints.Parameters(configuration,
            new CommandRetentionOptions { Retention = TimeSpan.FromDays(7) }, new MetricsOptions(), new TerminalOptions()));

        Assert.DoesNotContain(Canary, json, StringComparison.Ordinal);
        Assert.DoesNotContain("ConnectionStrings", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Keycloak", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1440, "1 dia")]
    [InlineData(90, "90 min")]
    [InlineData(60, "1 hora")]
    [InlineData(0.75, "45 s")]
    public void Duration_IsReadable(double minutes, string expected) =>
        Assert.Equal(expected, SettingsEndpoints.Duration(TimeSpan.FromMinutes(minutes)));

    private static (string, string) Single(IReadOnlyList<SettingsParameter> parameters, string key)
    {
        var parameter = Assert.Single(parameters, p => p.Key == key);
        return (parameter.Value, parameter.Source);
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
