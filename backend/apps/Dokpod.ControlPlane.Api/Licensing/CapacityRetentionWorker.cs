using Dokpod.ControlPlane.Application.Licensing;

namespace Dokpod.ControlPlane.Api.Licensing;

/// <summary>
/// Retenção diária das observações de capacidade (D-005): 35 dias, com a âncora de cada host gravada antes de remover
/// (função <c>dokpod_retain_licensing_capacity</c>). Só na composição Business; falha só é registrada.
/// </summary>
public sealed class CapacityRetentionWorker(IServiceScopeFactory scopes, TimeProvider timeProvider, ILogger<CapacityRetentionWorker> logger) : BackgroundService
{
    public static readonly TimeSpan Retention = TimeSpan.FromDays(35);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24), timeProvider);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var removed = await scope.ServiceProvider.GetRequiredService<ICapacityLedger>().ApplyRetentionAsync(Retention, stoppingToken);
                logger.LogInformation("Licensing capacity retention removed={Removed}", removed);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Licensing capacity retention failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
