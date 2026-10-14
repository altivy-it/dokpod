using Dokpod.ControlPlane.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests;

public sealed class InfrastructureCompositionTests
{
    [Fact]
    public void AddControlPlaneInfrastructure_RejectsInvalidConnectionString()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.AddControlPlaneInfrastructure("not-a-postgresql-connection-string"));
    }

    [Theory]
    [InlineData("Host=postgres;Database=keycloak;Username=keycloak;Search Path=dokpod")]
    [InlineData("Host=postgres;Database=keycloak;Username=keycloak;Search Path=dokpod;Timezone=America/Sao_Paulo")]
    public void ConnectionString_FixesSessionTimezoneToUtcAndKeepsOtherSettings(string connectionString)
    {
        var normalized = new NpgsqlConnectionStringBuilder(ControlPlaneConnectionString.Normalize(connectionString));

        Assert.Equal("UTC", normalized.Timezone);
        Assert.Equal("postgres", normalized.Host);
        Assert.Equal("dokpod", normalized.SearchPath);
    }
}
