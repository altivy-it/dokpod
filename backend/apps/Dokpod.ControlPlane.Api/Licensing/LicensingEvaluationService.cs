using Dokpod.ControlPlane.Application.Licensing;

namespace Dokpod.ControlPlane.Api.Licensing;

/// <summary>
/// Avaliação periódica do licenciamento (spec 031 T047): a cada 5 minutos reavalia e grava o estado, os avisos de
/// vencimento e o resumo diário de capacidade. Só na composição Business; falha só é registrada e nunca afeta a CE.
/// </summary>
public sealed class LicensingEvaluationService(IServiceScopeFactory scopes, TimeProvider timeProvider, ILogger<LicensingEvaluationService> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, timeProvider);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<LicensingMaintenance>().RunAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Licensing evaluation failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
