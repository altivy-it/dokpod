using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.ControlPlane.Application.Inventory;
using Dokpod.ControlPlane.Application.Metrics;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Hosts;
using Dokpod.Domain.Inventory;
using Xunit;

namespace Dokpod.ControlPlane.Application.Tests.Inventory;

/// <summary>Spec 015 D-002/D-003: ordenação por consumo sobre todo o conjunto filtrado.</summary>
public sealed class ContainerResourceSortTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Env = Guid.Parse("00000000-0000-4000-8000-000000000015");

    private static ContainerSearchRow Row(string name, string state = "running") =>
        new(Env, new ContainerInventory(new string(name[^1], 64), name, "nginx", state, "r1", Now), name);

    private static ContainerMetricsSample Sample(double cpu, long memory) =>
        new(cpu, null, null, memory, null, null, null, null, null, Now);

    [Theory]
    [InlineData(ContainerSort.CpuDesc, new[] { "c", "a", "b", "sem-amostra", "z-parado" })]
    [InlineData(ContainerSort.CpuAsc, new[] { "b", "a", "c", "sem-amostra", "z-parado" })]
    [InlineData(ContainerSort.MemoryDesc, new[] { "b", "c", "a", "sem-amostra", "z-parado" })]
    public void Order_PutsItemsWithoutSampleLastInBothDirections(ContainerSort sort, string[] expected)
    {
        var samples = new Dictionary<string, ContainerMetricsSample>
        {
            ["a"] = Sample(20, 100),
            ["b"] = Sample(5, 300),
            ["c"] = Sample(80, 200),
            ["z-parado"] = Sample(99, 999),
        };
        var rows = new[] { Row("a"), Row("b"), Row("c"), Row("sem-amostra"), Row("z-parado", "exited") };
        var entries = rows.Select(row => (row, ContainerResourceSort.ValueOf(sort, row.Container.DisplayStatus, samples.GetValueOrDefault(row.Container.Name)))).ToArray();

        var (page, next) = ContainerResourceSort.Paginate(sort, entries, null, 10);

        Assert.Equal(expected, page.Select(r => r.Container.Name));
        Assert.Null(next);
    }

    [Fact]
    public void Paginate_WalksAllPagesWithoutRepeatingOrSkipping()
    {
        var rows = Enumerable.Range(0, 23).Select(i => Row($"svc-{i:D2}")).ToArray();
        // Valores repetidos forçam o desempate por nome entre páginas.
        var entries = rows.Select((row, i) => (row, i % 5 == 0 ? (double?)null : (double)(i % 4))).ToArray();

        var seen = new List<string>();
        ContainerSearchCursor? cursor = null;
        do
        {
            var (page, next) = ContainerResourceSort.Paginate(ContainerSort.CpuDesc, entries, cursor, 4);
            seen.AddRange(page.Select(r => r.Container.Name));
            cursor = next;
        }
        while (cursor is not null);

        Assert.Equal(23, seen.Count);
        Assert.Equal(seen.Count, seen.Distinct().Count());
        var full = ContainerResourceSort.Paginate(ContainerSort.CpuDesc, entries, null, 100).Page.Select(r => r.Container.Name);
        Assert.Equal(full, seen);
    }

    [Fact]
    public void CpuValue_UsesTheSameRuleAsTheColumn()
    {
        var limited = new ContainerMetricsSample(10, 150, 0.5, 1, null, null, null, null, null, Now);
        var unlimited = new ContainerMetricsSample(10, null, null, 1, null, null, null, null, null, Now);

        Assert.Equal(100, ContainerResourceSort.ValueOf(ContainerSort.CpuDesc, ContainerDisplayStatus.Running, limited));
        Assert.Equal(10, ContainerResourceSort.ValueOf(ContainerSort.CpuDesc, ContainerDisplayStatus.Running, unlimited));
        Assert.Null(ContainerResourceSort.ValueOf(ContainerSort.CpuDesc, ContainerDisplayStatus.Paused, unlimited));
    }

    [Fact]
    public async Task Search_SortsTheWholeFilteredSetNotOnlyTheFirstStorePage()
    {
        var clock = new FixedClock(Now);
        var cache = new LatestMetricsCache(clock, new MetricsOptions());
        // 1.200 containers: o maior consumo está no fim da ordem por nome, além da primeira página interna (500).
        var rows = Enumerable.Range(0, 1_200).Select(i => new ContainerSearchRow(Env, new ContainerInventory($"{i:D64}", $"svc-{i:D4}", "nginx", "running", "r1", Now), $"svc-{i:D4}")).ToArray();
        foreach (var (row, i) in rows.Select((row, i) => (row, i))) cache.SetContainer(Env, row.Container.ContainerId, Sample(i / 100.0, i));
        var service = new ContainerSearchService(new PagedStore(rows), cache);

        var result = await service.SearchAsync([Host()], new ContainerListFilter(null, [], [], [], null, ContainerSort.MemoryDesc, null, 3), TestContext.Current.CancellationToken);

        Assert.Equal(["svc-1199", "svc-1198", "svc-1197"], result.Items.Select(i => i.Container.Name));
        Assert.NotNull(result.Next);
        Assert.Equal(1_200, result.Summary["running"]);
    }

    private static HostView Host() =>
        new(EnvironmentRegistration.Create(Env, "lab", "lab", true, EnvironmentResourceScopes.Supported), null, null, true, HostSituation.UpToDate, null);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>Store que pagina por nome como o PostgreSQL, com o resumo do conjunto todo.</summary>
    private sealed class PagedStore(IReadOnlyList<ContainerSearchRow> rows) : IContainerSearchStore
    {
        public Task<ContainerSearchResult> SearchAsync(ContainerSearchQuery query, CancellationToken cancellationToken)
        {
            Assert.Equal(ContainerSort.NameAsc, query.Sort);
            var ordered = rows.OrderBy(r => r.Container.Name, StringComparer.Ordinal).ToList();
            var start = query.After is null ? 0 : ordered.FindIndex(r => r.Container.Name == query.After.SortKey) + 1;
            var page = ordered.Skip(start).Take(query.Limit).ToArray();
            var next = start + query.Limit < ordered.Count ? new ContainerSearchCursor(page[^1].Container.Name, Env, page[^1].Container.ContainerId) : null;
            return Task.FromResult(new ContainerSearchResult(page, new Dictionary<string, int> { ["running"] = rows.Count }, Now, next));
        }

        public Task<ContainerInventory?> GetAsync(Guid environmentId, string containerId, CancellationToken cancellationToken) =>
            Task.FromResult<ContainerInventory?>(null);
    }
}
