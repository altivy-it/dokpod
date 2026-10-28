using Dokpod.ControlPlane.Application.Auditing;
using Dokpod.ControlPlane.Application.Agents;
using Dokpod.ControlPlane.Application.Commands;
using Dokpod.ControlPlane.Application.Environments;
using Dokpod.ControlPlane.Application.Events;
using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.ControlPlane.Application.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        void ConfigureNpgsql(DbContextOptionsBuilder options) => options
            .UseNpgsql(
                normalizedConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ControlPlaneSchema.Name))
            // O EF não separa transação cancelada de transação com erro (para comandos, separa): agente que cai
            // no meio do BeginTransaction virava `fail` no log. A exceção sempre sobe e é registrada por quem a
            // trata; aqui fica como aviso (spec 005, achado A2).
            .ConfigureWarnings(warnings => warnings.Log((RelationalEventId.TransactionError, LogLevel.Warning)));

        services.AddDbContext<ControlPlaneDbContext>(ConfigureNpgsql);
        services.AddSingleton<AuditEventMetrics>();
        services.AddScoped<IAgentIdentityRegistry, PostgresAgentIdentityRegistry>();
        services.AddScoped<IAgentCommandStore, PostgresAgentCommandStore>();
        services.AddScoped<IAuditEventWriter, PostgresAuditEventWriter>();
        services.AddScoped<IAuditEventReader, PostgresAuditEventReader>();
        services.AddScoped<Dokpod.ControlPlane.Application.Volumes.IVolumeInventoryStore, PostgresVolumeInventoryStore>();
        services.AddScoped<Dokpod.ControlPlane.Application.Networks.INetworkInventoryStore, PostgresNetworkInventoryStore>();
        services.AddScoped<Dokpod.ControlPlane.Application.Registries.IRegistryInventoryStore, PostgresRegistryInventoryStore>();
        services.AddScoped<Dokpod.ControlPlane.Application.Registries.IRegistryPolicyStore, PostgresRegistryPolicyStore>();
        services.AddScoped<Dokpod.ControlPlane.Application.Pods.IPodInventoryStore, PostgresPodInventoryStore>();
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
        services.AddScoped<Dokpod.ControlPlane.Application.Alerts.IAlertStore, PostgresAlertStore>();
        services.AddScoped<Dokpod.ControlPlane.Application.Alerts.IActionJustificationStore, PostgresActionJustificationStore>();

        return services;
    }
}
