using Dokpod.ControlPlane.Application.Licensing;
using Dokpod.ControlPlane.Infrastructure.Licensing;

namespace Dokpod.ControlPlane.Api.Licensing;

/// <summary>
/// Composição Business do licenciamento (spec 031 T011, D-009). Só é registrada pelo <c>Program</c> quando a API
/// é compilada com <c>-p:DokpodEdition=Business</c>; nenhuma variável de ambiente liga a edição.
/// </summary>
public static class LicensingModule
{
    public static IServiceCollection AddDokpodBusinessLicensing(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(_ => EmbeddedTrustStore.Load());
        services.AddSingleton<LicenseStateEvaluator>();
        services.AddSingleton<LicensingUploadRateLimiter>();
        services.AddScoped<ILicensingAuditWriter, LicensingAuditWriter>();
        services.AddScoped<LicenseStore>();
        services.AddScoped<ILicenseStore>(provider => provider.GetRequiredService<LicenseStore>());
        services.AddScoped<ILicenseStateReader>(provider => provider.GetRequiredService<LicenseStore>());
        services.AddScoped<LicenseImportService>();
        services.AddScoped<LicensingHostStore>();
        services.AddScoped<ILicensingHostStore>(provider => provider.GetRequiredService<LicensingHostStore>());
        services.AddScoped<CapacityLedger>();
        services.AddScoped<ICapacityLedger>(provider => provider.GetRequiredService<CapacityLedger>());
        services.AddScoped<IAgentCapacityObserver>(provider => provider.GetRequiredService<CapacityLedger>());
        services.AddScoped<ILicensingEvaluationStore, LicensingEvaluationStore>();
        services.AddScoped<LicenseStatusService>(provider => new LicenseStatusService(
            provider.GetRequiredService<ILicenseStateReader>(),
            provider.GetRequiredService<ILicenseStore>(),
            provider.GetRequiredService<LicenseStateEvaluator>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ICapacityLedger>(),
            provider.GetRequiredService<ILicensingEvaluationStore>()));
        services.AddScoped<LicensingHostService>();
        services.AddScoped<RevocationImportService>();
        services.AddScoped<LicensingMaintenance>();
        services.AddScoped<IBusinessCapabilityGate, BusinessCapabilityGate>();
        services.AddHostedService<CapacityRetentionWorker>();
        services.AddHostedService<LicensingEvaluationService>();
        return services;
    }

    public static IEndpointRouteBuilder MapLicensingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        LicensingEndpoints.Map(endpoints);
        LicensingHostEndpoints.Map(endpoints);
        return endpoints;
    }
}
