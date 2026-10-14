using Dokpod.ControlPlane.Application.Commands;

namespace Dokpod.ControlPlane.Api.Commands;

/// <summary>Opções de `Commands` (spec 012 D-008): retenção dos comandos terminais, padrão 90 dias.</summary>
public sealed class CommandRetentionOptions
{
    public const string SectionName = "Commands";

    public TimeSpan Retention { get; init; } = TimeSpan.FromDays(90);

    public bool IsValid() => Retention >= TimeSpan.FromDays(1) && Retention <= TimeSpan.FromDays(3650);
}

public sealed class AgentCommandExpirationWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<AgentCommandExpirationWorker> logger,
    CommandRetentionOptions? retentionOptions = null) : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(30);
    private DateTimeOffset lastRetention = DateTimeOffset.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ExpireAsync(stoppingToken);
        using var timer = new PeriodicTimer(SweepInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ExpireAsync(stoppingToken);
        }
    }

    private async Task ExpireAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IAgentCommandStore>();
            var expired = await store.ExpireNonTerminalAsync(
                timeProvider.GetUtcNow(),
                cancellationToken);
            if (expired > 0)
            {
                logger.LogInformation("Terminally expired {ExpiredCommandCount} agent commands", expired);
            }

            var now = timeProvider.GetUtcNow();
            if (now - lastRetention >= TimeSpan.FromHours(1))
            {
                lastRetention = now;
                var retention = (retentionOptions ?? new CommandRetentionOptions()).Retention;
                var purged = await store.PurgeTerminalAsync(now - retention, 5_000, cancellationToken);
                if (purged > 0)
                {
                    logger.LogInformation("Purged {PurgedCommandCount} terminal agent commands", purged);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to terminally expire agent commands");
        }
    }
}