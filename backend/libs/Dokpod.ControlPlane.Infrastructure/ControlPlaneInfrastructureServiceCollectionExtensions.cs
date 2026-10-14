using Dokpod.ControlPlane.Application.Auditing;
using Dokpod.ControlPlane.Application.Agents;
using Dokpod.ControlPlane.Application.Commands;
using Dokpod.ControlPlane.Application.Environments;
using Dokpod.ControlPlane.Application.Events;
using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.ControlPlane.Application.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dokpod.ControlPlane.Infrastructure;

public static class ControlPlaneInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddControlPlaneInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Control Plane database connection string is required.", nameof(connectionString));
        }

        var normalizedConnectionString = ControlPlaneConnectionString.Normalize(connectionString);
        void ConfigureNpgsql(DbContextOptionsBuilder options) => options.UseNpgsql(
            normalizedConnectionString,
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ControlPlaneSchema.Name));

        services.AddDbContext<ControlPlaneDbContext>(ConfigureNpgsql);
        services.AddSingleton<AuditEventMetrics>();
        services.AddScoped<IAgentIdentityRegistry, PostgresAgentIdentityRegistry>();
        services.AddScoped<IAgentCommandStore, PostgresAgentCommandStore>();
        services.AddScoped<IAuditEventWriter, PostgresAuditEventWriter>();
        services.AddScoped<IEnvironmentRegistrationStore, PostgresEnvironmentRegistrationStore>();
        services.AddScoped<IInventoryProjectionStore, PostgresInventoryProjectionStore>();
        services.AddScoped<PostgresAgentHostStore>();
        services.AddScoped<IAgentHostStore>(provider => provider.GetRequiredService<PostgresAgentHostStore>());
        services.AddScoped<IInventoryStatisticsStore>(provider => provider.GetRequiredService<PostgresAgentHostStore>());
        services.AddScoped<IEnvironmentStageStore>(provider => provider.GetRequiredService<PostgresAgentHostStore>());
        services.AddScoped<IOperationalEventStore, PostgresOperationalEventStore>();
        services.AddScoped<Dokpod.ControlPlane.Application.Metrics.IHostMetricsStore, PostgresHostMetricsStore>();
        services.AddScoped<IContainerSearchStore, PostgresContainerSearchStore>();
        services.AddScoped<Dokpod.ControlPlane.Application.Images.IImageInventoryStore, PostgresImageInventoryStore>();

        return services;
    }
}
