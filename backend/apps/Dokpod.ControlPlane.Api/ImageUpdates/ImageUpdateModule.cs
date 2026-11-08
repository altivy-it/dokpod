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
        services.AddScoped<IImageUpdateTargetReader, ImageUpdateTargetReader>();
        services.AddScoped<IImageUpdateEntitlement, LicenseImageUpdateEntitlement>();
        services.AddSingleton<IImageUpdateAgentChannel, DispatcherImageUpdateAgentChannel>();
        services.AddSingleton<ImageUpdateMetrics>();
        services.AddScoped<ImageUpdateScheduler>();
        services.AddScoped<ImageUpdateResultIngestion>();
        services.AddScoped<Dokpod.ControlPlane.Application.Images.IImageUpdateResultSink>(provider => provider.GetRequiredService<ImageUpdateResultIngestion>());
        services.AddScoped<ImageUpdateReadService>();
        services.AddScoped<Dokpod.ControlPlane.Application.Images.IImageUpdateAnnotations>(provider => provider.GetRequiredService<ImageUpdateReadService>());
        services.AddScoped<ImageUpdateCheckService>();
        services.AddScoped<Dokpod.ControlPlane.Application.Images.IImagePullCompletionObserver, ImageUpdateAfterPull>();
        services.AddScoped<Dokpod.ControlPlane.Application.Alerts.IAlertConditionAvailability, ImageOutdatedConditionAvailability>();
        services.AddScoped<Dokpod.ControlPlane.Application.Alerts.IImageOutdatedSource, ImageOutdatedSource>();
        services.AddHostedService<ImageUpdateWorker>();
        return services;
    }

    public static IEndpointRouteBuilder MapImageUpdateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ImageUpdateEndpoints.Map(endpoints);
        return endpoints;
    }
}
