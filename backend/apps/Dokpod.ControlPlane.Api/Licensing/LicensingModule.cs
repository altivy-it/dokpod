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
        services.AddScoped<ILicensingAuditWriter, LicensingAuditWriter>();
        return services;
    }

    /// <summary>As rotas de <c>/api/v1/licensing</c> entram com as histórias US2 a US6.</summary>
    public static IEndpointRouteBuilder MapLicensingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return endpoints;
    }
}
