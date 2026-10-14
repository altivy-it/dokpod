using Dokpod.ControlPlane.Application.Events;
using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Hosts;
using Dokpod.Domain.Inventory;

namespace Dokpod.ControlPlane.Application.Overview;

public sealed record EngineVersionCount(string Version, int Hosts);

public sealed record EngineSummary(string Engine, int Hosts, int Containers, IReadOnlyList<EngineVersionCount> Versions);

public sealed record OverviewSummary(
    EnvironmentStage? Stage,
    DateTimeOffset? ObservedAtUtc,
    int HostsTotal,
    IReadOnlyDictionary<string, int> HostsBySituation,
    int ContainersTotal,
    IReadOnlyDictionary<string, int> ContainersByDisplayStatus,
    IReadOnlyList<EngineSummary> Engines,
    int AttentionEvents24h);

/// <summary>Resumo agregado somente de hosts autorizados e habilitados (FR-001 da spec 008).</summary>
public sealed class OverviewQueryService(IOperationalEventStore eventStore, TimeProvider timeProvider)
{
    public async Task<OverviewSummary> SummarizeAsync(
        EnvironmentStage? stage,
        IReadOnlyList<HostView> hosts,
        CancellationToken cancellationToken)
    {
        var enabled = hosts.Where(h => h.Registration.Enabled && (stage is null || h.Registration.Stage == stage)).ToArray();
        var bySituation = Enum.GetValues<HostSituation>().ToDictionary(HostSituations.ToValue, _ => 0);
        foreach (var host in enabled) bySituation[HostSituations.ToValue(host.Situation)]++;

        var byStatus = Enum.GetValues<ContainerDisplayStatus>().ToDictionary(ContainerDisplayStatuses.ToValue, _ => 0);
        foreach (var host in enabled)
        {
            if (host.Inventory is null) continue;
            foreach (var (status, count) in host.Inventory.ByDisplayStatus)
            {
                if (byStatus.ContainsKey(status)) byStatus[status] += count;
            }
        }

        var engines = enabled
            .Where(h => h.Descriptor is not null)
            .GroupBy(h => h.Descriptor!.EngineKind)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new EngineSummary(
                g.Key,
                g.Count(),
                g.Sum(h => h.Inventory?.Total ?? 0),
                g.GroupBy(h => h.Descriptor!.EngineVersion)
                    .OrderByDescending(v => v.Count())
                    .Select(v => new EngineVersionCount(v.Key, v.Count()))
                    .ToArray()))
            .ToArray();

        var observed = enabled
            .Select(h => h.Inventory?.ObservedAtUtc)
            .Where(v => v is not null)
            .Select(v => v!.Value)
            .DefaultIfEmpty()
            .Min();

        var attention = enabled.Length == 0
            ? 0
            : await eventStore.CountAttentionAsync(
                enabled.Select(h => h.Registration.EnvironmentId).ToArray(),
                timeProvider.GetUtcNow().AddHours(-24),
                cancellationToken).ConfigureAwait(false);

        return new OverviewSummary(
            stage,
            observed == default ? null : observed,
            enabled.Length,
            bySituation,
            byStatus.Values.Sum(),
            byStatus,
            engines,
            attention);
    }
}
