namespace Dokpod.ControlPlane.Api.Licensing;

/// <summary>
/// Edição fixada no build (spec 031 D-009): <c>-p:DokpodEdition=Business</c> define <c>DOKPOD_BUSINESS</c>; sem a
/// propriedade, a API é a CE. O <c>Program</c> e os testes de composição usam os mesmos dois pontos de entrada.
/// </summary>
public static class ProductEdition
{
    public const string Community = "community";
    public const string Business = "business";

#if DOKPOD_BUSINESS
    public const string Current = Business;
#else
    public const string Current = Community;
#endif

    public static void AddServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
#if DOKPOD_BUSINESS
        services.AddDokpodBusinessLicensing();
#endif
    }

    public static void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
#if DOKPOD_BUSINESS
        endpoints.MapLicensingEndpoints();
#endif
    }
}
