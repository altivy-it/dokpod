using Dokpod.ControlPlane.Application.Metrics;

namespace Dokpod.ControlPlane.Api.Metrics;

/// <summary>
/// A cada minuto grava minutos de host pendentes e varre séries de container expiradas; a cada hora
/// aplica a retenção de `host_resource_samples` (spec 009 FR-004, SC-005).
/// </summary>
public sealed class MetricsMaintenanceWorker(
    IServiceScopeFactory scopeFactory,
    MetricsOptions options,
    TimeProvider timeProvider,
    ILogger<MetricsMaintenanceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lastRetention = DateTimeOffset.MinValue;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<MetricsIngestionService>().FlushAsync(stoppingToken);
                if (timeProvider.GetUtcNow() - lastRetention >= TimeSpan.FromHours(1))
                {
                    await scope.ServiceProvider.GetRequiredService<IHostMetricsStore>().ApplyRetentionAsync(options.HostRetention, stoppingToken);
                    lastRetention = timeProvider.GetUtcNow();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Metrics maintenance failed");
            }
        }
    }
}
