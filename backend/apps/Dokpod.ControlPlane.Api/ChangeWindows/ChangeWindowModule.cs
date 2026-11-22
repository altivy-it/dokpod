using Dokpod.ControlPlane.Application.ChangeWindows;
using Dokpod.ControlPlane.Infrastructure.ChangeWindows;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dokpod.ControlPlane.Api.ChangeWindows;

/// <summary>
/// Composição Business das janelas de mudança (spec 026 T014, D-002). Só o <c>ProductEdition</c> da Business a registra:
/// troca o calendário vazio da CE pelo store sobre o PostgreSQL. O guard continua o da CE e não depende da licença.
/// </summary>
public static class ChangeWindowModule
{
    public static IServiceCollection AddDokpodChangeWindows(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.RemoveAll<IChangeWindowSchedule>();
        services.AddScoped<ChangeWindowStore>();
        services.AddScoped<IChangeWindowSchedule>(provider => provider.GetRequiredService<ChangeWindowStore>());
        services.AddScoped<IChangeWindowStore>(provider => provider.GetRequiredService<ChangeWindowStore>());
        return services;
    }
}
