using Dokpod.ControlPlane.Application.AuditExport;

namespace Dokpod.ControlPlane.Api.AuditExport;

/// <summary>
/// Envio contínuo da trilha aos destinos (spec 027 T021). A cada segundo roda um ciclo; se algum destino encheu um
/// lote de 500, roda de novo sem esperar. O dono do lease é único por processo, para réplicas não enviarem em dobro.
/// </summary>
public sealed class AuditExportWorker(IServiceScopeFactory scopes, TimeProvider timeProvider, ILogger<AuditExportWorker> logger) : BackgroundService
{
    /// <summary>Até 128 caracteres, como <c>lease_owner</c>.</summary>
    public string Owner { get; } = $"{Environment.MachineName[..Math.Min(Environment.MachineName.Length, 90)]}:{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(AuditExportLimits.BatchWait, timeProvider);
        do
        {
            int delivered;
            do
            {
                delivered = 0;
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    delivered = await scope.ServiceProvider.GetRequiredService<AuditExportSender>().RunOnceAsync(Owner, stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // Só o tipo: a mensagem poderia trazer host ou resposta do destino.
                    logger.LogWarning("Audit export cycle failed: {ExceptionType}", exception.GetType().Name);
                }
            }
            while (delivered >= AuditExportLimits.MaxBatch && !stoppingToken.IsCancellationRequested);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
