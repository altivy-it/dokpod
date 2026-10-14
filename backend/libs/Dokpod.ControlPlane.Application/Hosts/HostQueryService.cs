using Dokpod.ControlPlane.Application.Agents;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Hosts;

namespace Dokpod.ControlPlane.Application.Hosts;

public sealed record HostFilter(
    EnvironmentStage? Stage = null,
    string? Engine = null,
    HostSituation? Situation = null,
    string? Query = null)
{
    public bool IsEmpty => Stage is null && Engine is null && Situation is null && string.IsNullOrWhiteSpace(Query);
}

/// <summary>Monta a visão de host (descritor, conexão, situação FR-004 e contagens) para ambientes já autorizados.</summary>
public sealed class HostQueryService(
    IAgentHostStore hostStore,
    IInventoryStatisticsStore statisticsStore,
    IAgentSessionStore sessionStore,
    AgentLivenessTracker liveness,
    EngineSupportOptions engineSupport)
{
    public async Task<IReadOnlyList<HostView>> DescribeAsync(
        IReadOnlyList<EnvironmentRegistration> registrations,
        CancellationToken cancellationToken)
    {
        if (registrations.Count == 0) return [];
        var ids = registrations.Select(r => r.EnvironmentId).ToArray();
        var descriptors = await hostStore.GetDescriptorsAsync(ids, cancellationToken).ConfigureAwait(false);
        var connections = await hostStore.GetConnectionsAsync(ids, cancellationToken).ConfigureAwait(false);
        var statistics = await statisticsStore.GetStatisticsAsync(ids, cancellationToken).ConfigureAwait(false);
        var views = new List<HostView>(registrations.Count);
        foreach (var registration in registrations)
        {
            descriptors.TryGetValue(registration.EnvironmentId, out var descriptor);
            connections.TryGetValue(registration.EnvironmentId, out var connection);
            statistics.TryGetValue(registration.EnvironmentId, out var inventory);
            var connected = await sessionStore.FindActiveAsync(registration.EnvironmentId, cancellationToken).ConfigureAwait(false) is not null;
            var situation = HostSituations.Evaluate(
                registration.Enabled,
                connected,
                connected && liveness.IsDiverging(registration.EnvironmentId),
                descriptor?.EngineVersion,
                engineSupport.MinimumFor(descriptor?.EngineKind));
            views.Add(new HostView(registration, descriptor, connection, connected, situation, inventory));
        }

        return views;
    }

    public static IReadOnlyList<HostView> Filter(IEnumerable<HostView> hosts, HostFilter filter)
    {
        var query = filter.Query?.Trim();
        return hosts.Where(host =>
                (filter.Stage is null || host.Registration.Stage == filter.Stage) &&
                (filter.Engine is null || host.Descriptor?.EngineKind == filter.Engine) &&
                (filter.Situation is null || host.Situation == filter.Situation) &&
                (string.IsNullOrEmpty(query) ||
                    host.Registration.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    host.Registration.Host.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (host.Descriptor?.HostName.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)))
            .ToArray();
    }
}
