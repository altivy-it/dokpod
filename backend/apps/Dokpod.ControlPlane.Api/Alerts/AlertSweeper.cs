using Dokpod.ControlPlane.Application.Alerts;

namespace Dokpod.ControlPlane.Api.Alerts;

/// <summary>Varredura de alertas a cada 30 s (spec 016 D-003) e retenção diária de resolvidos (90 dias).</summary>
public sealed class AlertSweeper(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<AlertSweeper> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = configuration.GetValue("Alerts:SweepInterval", TimeSpan.FromSeconds(30));
        var retention = configuration.GetValue("Alerts:ResolvedRetention", TimeSpan.FromDays(90));
        if (interval < TimeSpan.FromSeconds(1) || retention < TimeSpan.FromDays(1)) throw new InvalidOperationException("Alerts options are invalid.");
        var nextRetention = timeProvider.GetUtcNow();
        using var timer = new PeriodicTimer(interval, timeProvider);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                // Sem banco não há store de alertas: nada a varrer.
                if (scope.ServiceProvider.GetService<IAlertStore>() is not { } store) continue;
                var result = await scope.ServiceProvider.GetRequiredService<AlertSweepService>().RunOnceAsync(stoppingToken);
                if (result.Opened + result.Resolved + result.Silenced + result.Unsilenced + result.JustificationsEnded > 0)
                {
                    logger.LogInformation("Alert sweep {@Result}", result);
                }

                if (timeProvider.GetUtcNow() >= nextRetention)
                {
                    var removed = await store.DeleteResolvedBeforeAsync(timeProvider.GetUtcNow() - retention, stoppingToken);
                    if (removed > 0) logger.LogInformation("Alert retention removed {Count} alerts", removed);
                    nextRetention = timeProvider.GetUtcNow().AddDays(1);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Alert sweep failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
