namespace Dokpod.ControlPlane.Application.Realtime;

public static class InvalidationKinds
{
    public const string Inventory = "inventory";
    public const string Host = "host";
    public const string Events = "events";
    public const string Images = "images";
    public const string Alerts = "alerts";
    public const string Volumes = "volumes";
    public const string Networks = "networks";
}

/// <summary>Invalidação em tempo real por ambiente e tipo; o cliente busca o estado pela API.</summary>
public interface IControlPlaneNotifier
{
    Task NotifyAsync(Guid environmentId, string kind, CancellationToken cancellationToken);
}

public sealed class NullControlPlaneNotifier : IControlPlaneNotifier
{
    public Task NotifyAsync(Guid environmentId, string kind, CancellationToken cancellationToken) => Task.CompletedTask;
}
