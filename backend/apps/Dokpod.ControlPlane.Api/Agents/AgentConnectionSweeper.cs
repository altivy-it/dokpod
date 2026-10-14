using Dokpod.ControlPlane.Application.Agents;
using Dokpod.ControlPlane.Application.Events;
using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.ControlPlane.Application.Realtime;
using Dokpod.Domain.Events;

namespace Dokpod.ControlPlane.Api.Agents;

/// <summary>
/// Na inicialização e a cada 60 s, marca como desconectadas as conexões persistidas sem sessão ativa
/// nesta instância, gerando `agent.disconnected` uma única vez (spec 008 T028).
/// </summary>
public sealed class AgentConnectionSweeper(
    IServiceScopeFactory scopeFactory,
    IAgentSessionStore sessionStore,
    TimeProvider timeProvider,
    ILogger<AgentConnectionSweeper> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await SweepSafelyAsync(stoppingToken);
        using var timer = new PeriodicTimer(Interval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await SweepSafelyAsync(stoppingToken);
        }
    }

    internal async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var hostStore = scope.ServiceProvider.GetService<IAgentHostStore>();
        if (hostStore is null) return 0;
        var events = scope.ServiceProvider.GetRequiredService<OperationalEventService>();
        var notifier = scope.ServiceProvider.GetRequiredService<IControlPlaneNotifier>();
        var swept = 0;
        foreach (var environmentId in await hostStore.ListConnectedAsync(cancellationToken))
        {
            if (await sessionStore.FindActiveAsync(environmentId, cancellationToken) is not null) continue;
            var previous = await hostStore.SetConnectionAsync(environmentId, false, timeProvider.GetUtcNow(), cancellationToken);
            if (previous == true)
            {
                await events.RecordAsync(environmentId, OperationalEventTypes.AgentDisconnected, "host", null, null, null, cancellationToken);
                await notifier.NotifyAsync(environmentId, InvalidationKinds.Host, cancellationToken);
                swept++;
            }
        }

        return swept;
    }

    private async Task SweepSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SweepAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Agent connection sweep failed");
        }
    }
}

/// <summary>Aplica a retenção de `environment_events` uma vez por hora (spec 008 FR-012).</summary>
public sealed class EventRetentionWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<EventRetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retention = configuration.GetValue("EventRetention:Period", TimeSpan.FromDays(30));
        if (retention < TimeSpan.FromHours(1)) throw new InvalidOperationException("EventRetention:Period must be at least one hour.");
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), timeProvider);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var store = scope.ServiceProvider.GetService<IOperationalEventStore>();
                if (store is not null)
                {
                    var removed = await store.ApplyRetentionAsync(retention, stoppingToken);
                    if (removed > 0) logger.LogInformation("Event retention removed {Count} rows", removed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Event retention failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
