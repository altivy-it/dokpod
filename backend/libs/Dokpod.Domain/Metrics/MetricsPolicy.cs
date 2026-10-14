namespace Dokpod.Domain.Metrics;

public sealed record MetricsWindow(TimeSpan Window, TimeSpan Step)
{
    public int Points => (int)(Window.Ticks / Step.Ticks);
}

/// <summary>Política de métricas (spec 009): janelas, passos e agregado do estágio (D-004).</summary>
public static class MetricsPolicy
{
    public const int MaxPoints = 1440;
    public const double DefaultOperationalLimitPercent = 80;
    public static readonly TimeSpan ContainerSeriesWindow = TimeSpan.FromMinutes(30);

    private static readonly IReadOnlyDictionary<string, TimeSpan> Windows = new Dictionary<string, TimeSpan>(StringComparer.Ordinal)
    {
        ["30m"] = TimeSpan.FromMinutes(30),
        ["1h"] = TimeSpan.FromHours(1),
        ["6h"] = TimeSpan.FromHours(6),
        ["12h"] = TimeSpan.FromHours(12),
        ["24h"] = TimeSpan.FromHours(24),
    };

    private static readonly IReadOnlyDictionary<string, TimeSpan> Steps = new Dictionary<string, TimeSpan>(StringComparer.Ordinal)
    {
        ["1m"] = TimeSpan.FromMinutes(1),
        ["5m"] = TimeSpan.FromMinutes(5),
        ["15m"] = TimeSpan.FromMinutes(15),
        ["1h"] = TimeSpan.FromHours(1),
    };

    /// <summary>Valida `window`/`step`: valores conhecidos, passo ≤ janela, janela múltipla do passo e ≤ 1.440 pontos.</summary>
    public static bool TryParse(string? window, string? step, TimeSpan maxWindow, out MetricsWindow parsed)
    {
        parsed = default!;
        if (window is null || step is null || !Windows.TryGetValue(window, out var w) || !Steps.TryGetValue(step, out var s)) return false;
        if (w > maxWindow || s > w || w.Ticks % s.Ticks != 0) return false;
        parsed = new MetricsWindow(w, s);
        return parsed.Points <= MaxPoints;
    }

    /// <summary>
    /// Agregado do estágio: CPU média ponderada pelos núcleos e memória usada sobre total, somente de hosts
    /// com amostra; hosts sem amostra não contam como zero.
    /// </summary>
    public static (double? CpuPercent, double? MemoryPercent, int HostsSampled) Aggregate(
        IEnumerable<(double? CpuPercent, int? Cpus, long? MemoryUsedBytes, long? MemoryTotalBytes)> hosts)
    {
        double cpuWeighted = 0;
        double cpuWeight = 0;
        double memoryUsed = 0;
        double memoryTotal = 0;
        var sampled = 0;
        foreach (var host in hosts)
        {
            var any = false;
            if (host.CpuPercent is { } cpu)
            {
                var weight = host.Cpus is > 0 ? host.Cpus.Value : 1;
                cpuWeighted += cpu * weight;
                cpuWeight += weight;
                any = true;
            }

            if (host.MemoryUsedBytes is { } used && host.MemoryTotalBytes is > 0)
            {
                memoryUsed += used;
                memoryTotal += host.MemoryTotalBytes.Value;
                any = true;
            }

            if (any) sampled++;
        }

        return (
            cpuWeight > 0 ? Math.Round(cpuWeighted / cpuWeight, 2) : null,
            memoryTotal > 0 ? Math.Round(memoryUsed / memoryTotal * 100, 2) : null,
            sampled);
    }
}
