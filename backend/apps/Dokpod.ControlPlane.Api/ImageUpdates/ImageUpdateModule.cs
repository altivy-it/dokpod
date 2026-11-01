using Dokpod.ControlPlane.Application.ImageUpdates;
using Dokpod.ControlPlane.Infrastructure.ImageUpdates;

namespace Dokpod.ControlPlane.Api.ImageUpdates;

/// <summary>
/// Composição Business do aviso de nova versão de imagem (spec 025 T007, capability <c>image-update-notice</c>). Só o
/// <c>ProductEdition</c> da Business a registra; a imagem CE não tem agenda, rotas nem pedidos ao agente.
/// </summary>
public static class ImageUpdateModule
{
    public static IServiceCollection AddDokpodImageUpdates(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IImageUpdateStore, ImageUpdateStore>();
        return services;
    }

    public static IEndpointRouteBuilder MapImageUpdateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return endpoints;
    }
}
