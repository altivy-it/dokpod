using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.ControlPlane.Application.Metrics;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Hosts;
using Dokpod.Domain.Inventory;

namespace Dokpod.ControlPlane.Application.Inventory;

public enum ContainerSort
{
    NameAsc,
    NameDesc,
    DisplayStatus,
    Environment,
    CreatedAtAsc,
    CreatedAtDesc,
    CpuAsc,
    CpuDesc,
    MemoryAsc,
    MemoryDesc,
}

public sealed record ContainerSearchCursor(string SortKey, Guid EnvironmentId, string ContainerId);

public sealed record ContainerSearchQuery(
    IReadOnlyCollection<Guid> EnvironmentIds,
    IReadOnlyCollection<string> DisplayStatuses,
    string? Text,
    ContainerSort Sort,
    ContainerSearchCursor? After,
    int Limit);

public sealed record ContainerSearchRow(Guid EnvironmentId, ContainerInventory Container, string SortKey);

public sealed record ContainerSearchResult(
    IReadOnlyList<ContainerSearchRow> Rows,
    IReadOnlyDictionary<string, int> SummaryByDisplayStatus,
    DateTimeOffset? OldestObservedAtUtc,
    ContainerSearchCursor? Next);

public interface IContainerSearchStore
{
    Task<ContainerSearchResult> SearchAsync(ContainerSearchQuery query, CancellationToken cancellationToken);

    Task<ContainerInventory?> GetAsync(Guid environmentId, string containerId, CancellationToken cancellationToken);
}

public sealed record ContainerListFilter(
    EnvironmentStage? Stage,
    IReadOnlyCollection<Guid> EnvironmentIds,
    IReadOnlyCollection<string> Engines,
    IReadOnlyCollection<string> DisplayStatuses,
    string? Text,
    ContainerSort Sort,
    ContainerSearchCursor? After,
    int Limit);

public sealed record ContainerListItem(
    HostView Host,
    ContainerInventory Container,
    IReadOnlyList<string> AllowedActions);

public sealed record ContainerListResult(
    IReadOnlyList<ContainerListItem> Items,
    IReadOnlyDictionary<string, int> Summary,
    DateTimeOffset? ObservedAtUtc,
    ContainerSearchCursor? Next);

/// <summary>Lista unificada de containers dos hosts autorizados (spec 010 FR-001/FR-002).</summary>
public sealed class ContainerSearchService(IContainerSearchStore store, LatestMetricsCache? metrics = null)
{
    /// <summary>Teto do conjunto lido para ordenar por consumo (spec 009: até 2.000 containers por estágio).</summary>
    public const int MaxResourceSortRows = 5_000;

    private const int ResourceSortPageSize = 500;

    public async Task<ContainerListResult> SearchAsync(
        IReadOnlyList<HostView> authorizedHosts,
        ContainerListFilter filter,
        CancellationToken cancellationToken)
    {
        var hosts = authorizedHosts
            .Where(h => filter.Stage is null || h.Registration.Stage == filter.Stage)
            .Where(h => filter.EnvironmentIds.Count == 0 || filter.EnvironmentIds.Contains(h.Registration.EnvironmentId))
            .Where(h => filter.Engines.Count == 0 || (h.Descriptor is not null && filter.Engines.Contains(h.Descriptor.EngineKind)))
            .ToDictionary(h => h.Registration.EnvironmentId);
        if (hosts.Count == 0)
        {
            return new ContainerListResult(
                [],
                Enum.GetValues<ContainerDisplayStatus>().ToDictionary(ContainerDisplayStatuses.ToValue, _ => 0),
                null,
                null);
        }

        var result = ContainerResourceSort.IsResourceSort(filter.Sort)
            ? await SearchByResourceAsync(hosts.Keys.ToArray(), filter, cancellationToken).ConfigureAwait(false)
            : await store.SearchAsync(
                new ContainerSearchQuery(hosts.Keys.ToArray(), filter.DisplayStatuses, filter.Text, filter.Sort, filter.After, filter.Limit),
                cancellationToken).ConfigureAwait(false);
        var items = result.Rows
            .Select(row => new ContainerListItem(hosts[row.EnvironmentId], row.Container, AllowedActions(hosts[row.EnvironmentId], row.Container)))
            .ToArray();
        return new ContainerListResult(items, result.SummaryByDisplayStatus, result.OldestObservedAtUtc, result.Next);
    }

    /// <summary>
    /// Ordenação por consumo (spec 015): lê todo o conjunto filtrado em páginas por nome, anexa a amostra atual e
    /// ordena na aplicação, porque as amostras de containers vivem só em memória.
    /// </summary>
    private async Task<ContainerSearchResult> SearchByResourceAsync(
        IReadOnlyCollection<Guid> environmentIds,
        ContainerListFilter filter,
        CancellationToken cancellationToken)
    {
        var rows = new List<ContainerSearchRow>();
        ContainerSearchCursor? page = null;
        ContainerSearchResult? first = null;
        do
        {
            var result = await store.SearchAsync(
                new ContainerSearchQuery(environmentIds, filter.DisplayStatuses, filter.Text, ContainerSort.NameAsc, page, ResourceSortPageSize),
                cancellationToken).ConfigureAwait(false);
            first ??= result;
            rows.AddRange(result.Rows);
            page = result.Next;
        }
        while (page is not null && rows.Count < MaxResourceSortRows);

        var entries = rows
            .Select(row => (row, ContainerResourceSort.ValueOf(filter.Sort, row.Container.DisplayStatus, metrics?.GetContainer(row.EnvironmentId, row.Container.ContainerId))))
            .ToArray();
        var (pageRows, next) = ContainerResourceSort.Paginate(filter.Sort, entries, filter.After, filter.Limit);
        return new ContainerSearchResult(pageRows, first.SummaryByDisplayStatus, first.OldestObservedAtUtc, next);
    }

    /// <summary>Interseção scope × capability do agente × situação do container × conexão.</summary>
    public static IReadOnlyList<string> AllowedActions(HostView host, ContainerInventory container)
    {
        if (!host.Registration.Enabled || !host.Connected || host.Situation == HostSituation.Disconnected) return [];
        var scopes = host.Registration.Scopes;
        var capabilities = host.Descriptor?.Capabilities ?? [];
        var state = ContainerDisplayStatuses.NormalizeState(container.State);
        var actions = new List<string>();
        void Add(string action, string scope, string? capability, bool stateAllows)
        {
            if (stateAllows && scopes.Contains(scope) && (capability is null || capabilities.Contains(capability))) actions.Add(action);
        }

        Add("start", EnvironmentResourceScopes.StartContainer, "ContainerStart", state is "exited" or "created" or "dead");
        Add("stop", EnvironmentResourceScopes.StopContainer, "ContainerStop", state is "running" or "restarting" or "paused");
        Add("restart", EnvironmentResourceScopes.RestartContainer, "ContainerRestart", state is "running" or "exited" or "created" or "restarting");
        Add("pause", EnvironmentResourceScopes.PauseContainer, "ContainerPause", state == "running");
        Add("unpause", EnvironmentResourceScopes.PauseContainer, "ContainerUnpause", state == "paused");
        Add("delete", EnvironmentResourceScopes.DeleteContainer, "ContainerDelete", state is not ("running" or "restarting" or "paused"));
        // Spec 014: terminal só em container em execução, com scope e capability do agente.
        Add("exec", EnvironmentResourceScopes.ExecContainer, "ContainerExec", state == "running");
        return actions;
    }
}
