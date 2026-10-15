using System.Globalization;
using Dokpod.ControlPlane.Application.Metrics;
using Dokpod.Domain.Inventory;

namespace Dokpod.ControlPlane.Application.Inventory;

/// <summary>
/// Ordenação da lista por consumo atual (spec 015 D-002/D-003). As amostras ficam em memória, então a ordem é
/// calculada na aplicação sobre todo o conjunto filtrado. Chave: (grupo, valor, nome, ambiente, id); grupo 0 tem
/// amostra recente e grupo 1 não tem, e fica no fim nas duas direções. A direção vale só para o valor.
/// </summary>
public static class ContainerResourceSort
{
    public static bool IsResourceSort(ContainerSort sort) =>
        sort is ContainerSort.CpuAsc or ContainerSort.CpuDesc or ContainerSort.MemoryAsc or ContainerSort.MemoryDesc;

    /// <summary>Valor ordenado, com a mesma regra da coluna exibida; containers fora de execução não têm valor.</summary>
    public static double? ValueOf(ContainerSort sort, ContainerDisplayStatus status, ContainerMetricsSample? sample)
    {
        if (sample is null || status is ContainerDisplayStatus.Stopped or ContainerDisplayStatus.Failed or ContainerDisplayStatus.Paused)
        {
            return null;
        }

        return sort switch
        {
            ContainerSort.CpuAsc or ContainerSort.CpuDesc => sample.CpuPercentOfLimit is { } ofLimit ? Math.Min(100, ofLimit) : sample.CpuPercentOfHost,
            ContainerSort.MemoryAsc or ContainerSort.MemoryDesc => sample.MemoryUsedBytes,
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, "Ordenação não é por consumo."),
        };
    }

    /// <summary>Ordena, aplica o cursor e devolve a página com o cursor da próxima.</summary>
    public static (IReadOnlyList<ContainerSearchRow> Page, ContainerSearchCursor? Next) Paginate(
        ContainerSort sort,
        IReadOnlyList<(ContainerSearchRow Row, double? Value)> entries,
        ContainerSearchCursor? after,
        int limit)
    {
        var descending = sort is ContainerSort.CpuDesc or ContainerSort.MemoryDesc;
        var keyed = entries
            .Select(entry => (entry.Row, Key: new Key(entry.Value is null ? 1 : 0, entry.Value ?? 0, entry.Row.Container.Name, entry.Row.EnvironmentId, entry.Row.Container.ContainerId)))
            .ToList();
        var comparer = new KeyComparer(descending);
        keyed.Sort((left, right) => comparer.Compare(left.Key, right.Key));

        IEnumerable<(ContainerSearchRow Row, Key Key)> remaining = keyed;
        if (after is not null && TryParse(after, out var cursorKey))
        {
            remaining = keyed.Where(item => comparer.Compare(item.Key, cursorKey) > 0);
        }

        var window = remaining.Take(limit + 1).ToArray();
        var page = window.Take(limit).Select(item => item.Row with { SortKey = Format(item.Key) }).ToArray();
        var next = window.Length > limit && page.Length > 0
            ? new ContainerSearchCursor(page[^1].SortKey, page[^1].EnvironmentId, page[^1].Container.ContainerId)
            : null;
        return (page, next);
    }

    private static string Format(Key key) =>
        string.Join('|', key.Group.ToString(CultureInfo.InvariantCulture), key.Group == 0 ? key.Value.ToString("F6", CultureInfo.InvariantCulture) : string.Empty, key.Name);

    private static bool TryParse(ContainerSearchCursor cursor, out Key key)
    {
        key = default;
        var parts = cursor.SortKey.Split('|', 3);
        if (parts.Length != 3 || parts[0] is not ("0" or "1")) return false;
        var group = parts[0] == "0" ? 0 : 1;
        var value = 0d;
        if (group == 0 && !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return false;
        key = new Key(group, value, parts[2], cursor.EnvironmentId, cursor.ContainerId);
        return true;
    }

    private readonly record struct Key(int Group, double Value, string Name, Guid EnvironmentId, string ContainerId);

    private sealed class KeyComparer(bool descending) : IComparer<Key>
    {
        public int Compare(Key left, Key right)
        {
            var byGroup = left.Group.CompareTo(right.Group);
            if (byGroup != 0) return byGroup;
            var byValue = left.Value.CompareTo(right.Value);
            if (byValue != 0) return descending ? -byValue : byValue;
            var byName = string.CompareOrdinal(left.Name, right.Name);
            if (byName != 0) return byName;
            var byEnvironment = left.EnvironmentId.CompareTo(right.EnvironmentId);
            return byEnvironment != 0 ? byEnvironment : string.CompareOrdinal(left.ContainerId, right.ContainerId);
        }
    }
}
