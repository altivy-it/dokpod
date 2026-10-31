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
        services.AddScoped<LicenseStatusService>();
        return services;
    }

    public static IEndpointRouteBuilder MapLicensingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        LicensingEndpoints.Map(endpoints);
        return endpoints;
    }
}
