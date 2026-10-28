namespace Dokpod.Domain.Metrics;

/// <summary>Contadores brutos de CPU de um container em um instante (`/containers/{id}/stats`).</summary>
public readonly record struct ContainerCpuCounters(ulong ContainerTotalNanos, int OnlineCpus);

/// <summary>Uso de CPU de um container: % do host (0-100) e % do limite (pode exceder 100 por instantes).</summary>
public readonly record struct ContainerCpuUsage(double PercentOfHost, double PercentOfLimit);

/// <summary>
/// Fórmulas de CPU e taxas (spec 009 D-001). Contador do container que volta ou intervalo nulo indica
/// contadores reiniciados ou amostra repetida: o resultado é ausência (lacuna), nunca zero nem pico.
/// </summary>
public static class CpuUsageCalculator
{
    /// <summary>
    /// Núcleos usados no intervalo pelo tempo de parede entre as leituras. Não usa `system_cpu_usage`: no Podman
    /// a API compatível usa outra base nesse campo, e a fórmula do Docker inflava o uso várias vezes.
    /// </summary>
    public static ContainerCpuUsage? Container(ContainerCpuCounters previous, ContainerCpuCounters current, TimeSpan elapsed, double? limitCores)
    {
        if (current.ContainerTotalNanos < previous.ContainerTotalNanos || elapsed <= TimeSpan.Zero)
        {
            return null;
        }

        var cpus = current.OnlineCpus > 0 ? current.OnlineCpus : previous.OnlineCpus;
        if (cpus <= 0) return null;
        var cores = (current.ContainerTotalNanos - previous.ContainerTotalNanos) / (elapsed.TotalSeconds * 1_000_000_000d);
        var percentOfHost = Math.Clamp(cores / cpus * 100, 0, 100);
        var reference = limitCores is > 0 ? limitCores.Value : cpus;
        var percentOfLimit = Math.Max(0, cores / reference * 100);
        return new ContainerCpuUsage(Round(percentOfHost), Round(percentOfLimit));
    }

    /// <summary>% de CPU do host a partir de dois instantes de `/proc/stat` (total e ocioso em jiffies).</summary>
    public static double? Host(ulong previousTotal, ulong previousIdle, ulong currentTotal, ulong currentIdle)
    {
        if (currentTotal <= previousTotal || currentIdle < previousIdle) return null;
        var total = currentTotal - previousTotal;
        var idle = currentIdle - previousIdle;
        if (idle > total) return null;
        return Round((double)(total - idle) / total * 100);
    }

    /// <summary>Bytes por segundo entre dois contadores monotônicos; reinício do contador é lacuna.</summary>
    public static ulong? Rate(ulong previous, ulong current, TimeSpan elapsed)
    {
        if (current < previous || elapsed <= TimeSpan.Zero) return null;
        return (ulong)Math.Round((current - previous) / elapsed.TotalSeconds);
    }

    private static double Round(double value) => Math.Round(value, 2);
}
