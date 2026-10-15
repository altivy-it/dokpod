using Dokpod.ControlPlane.Application.Images;
using Dokpod.ControlPlane.Application.Registries;

namespace Dokpod.ControlPlane.Api.Endpoints;

/// <summary>
/// Recarrega a política de registros salva no console a cada 3 s (spec 020 D-004): uma alteração feita em outra
/// instância da API vale para os próximos downloads e criações em até 5 s (SC-003).
/// </summary>
public sealed class RegistryPolicyRefresher(IServiceScopeFactory scopes, ImagePullPolicy policy, ILogger<RegistryPolicyRefresher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var stored = await scope.ServiceProvider.GetRequiredService<IRegistryPolicyStore>().ListAsync(stoppingToken);
                policy.ApplyOverrides(stored.ToDictionary(p => RegistryPolicyStages.Key(p.Stage), p => p.Registries.ToArray()));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Sem banco, mantém a última política carregada (ou a configuração).
                logger.LogWarning(exception, "Registry policy refresh failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
