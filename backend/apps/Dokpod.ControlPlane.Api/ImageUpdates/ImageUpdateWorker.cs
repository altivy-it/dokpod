using Dokpod.ControlPlane.Application.ImageUpdates;

namespace Dokpod.ControlPlane.Api.ImageUpdates;

/// <summary>
/// Verificação periódica de nova versão (spec 025 T013): a cada 30 s, a réplica pega os hosts vencidos com lease
/// (D-006). O dono do lease é único por processo, para réplicas não pedirem em dobro.
/// </summary>
public sealed class ImageUpdateWorker(IServiceScopeFactory scopes, TimeProvider timeProvider, ILogger<ImageUpdateWorker> logger) : BackgroundService
{
    public static readonly TimeSpan CycleInterval = TimeSpan.FromSeconds(30);

    /// <summary>Até 128 caracteres, como <c>lease_owner</c>.</summary>
    public string Owner { get; } = $"{Environment.MachineName[..Math.Min(Environment.MachineName.Length, 90)]}:{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CycleInterval, timeProvider);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ImageUpdateScheduler>().RunOnceAsync(Owner, stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Só o tipo: a mensagem poderia trazer referência ou registro.
                logger.LogWarning("Image update cycle failed: {ExceptionType}", exception.GetType().Name);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
