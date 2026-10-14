using System.Collections.Concurrent;
using Dokpod.ControlPlane.Application.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace Dokpod.ControlPlane.Api.Realtime;

public sealed record InvalidationNotification(Guid EnvironmentId, string Kind);

/// <summary>Emite `invalidated` no máximo uma vez por segundo por ambiente e tipo (spec 008 FR-013).</summary>
public sealed class SignalRControlPlaneNotifier(
    IHubContext<ControlPlaneHub> hubContext,
    TimeProvider timeProvider,
    ILogger<SignalRControlPlaneNotifier> logger) : IControlPlaneNotifier
{
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(1);
    private readonly ConcurrentDictionary<(Guid, string), DateTimeOffset> lastSent = new();
    private readonly ConcurrentDictionary<(Guid, string), byte> pending = new();

    public Task NotifyAsync(Guid environmentId, string kind, CancellationToken cancellationToken)
    {
        var key = (environmentId, kind);
        var now = timeProvider.GetUtcNow();
        var last = lastSent.GetValueOrDefault(key, DateTimeOffset.MinValue);
        if (now - last >= MinimumInterval)
        {
            lastSent[key] = now;
            return SendAsync(environmentId, kind);
        }

        if (pending.TryAdd(key, 0))
        {
            var delay = MinimumInterval - (now - last);
            _ = Task.Run(async () =>
            {
                await Task.Delay(delay < TimeSpan.Zero ? TimeSpan.Zero : delay, timeProvider);
                pending.TryRemove(key, out _);
                lastSent[key] = timeProvider.GetUtcNow();
                await SendAsync(environmentId, kind);
            }, CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    private async Task SendAsync(Guid environmentId, string kind)
    {
        try
        {
            await hubContext.Clients
                .Group(ControlPlaneHub.GroupFor(environmentId.ToString("D")))
                .SendAsync("invalidated", new InvalidationNotification(environmentId, kind), CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Realtime invalidation could not be sent");
        }
    }
}
