using Dokpod.Agent.Application.Metrics;

namespace Dokpod.LoadSim;

/// <summary>Leitura sintética de CPU e memória do host simulado (contadores crescentes, como `/proc/stat`).</summary>
public sealed class HostProbe(Random random) : IHostResourceProbe
{
    private ulong total;
    private ulong idle;

    public HostResourceReading? Read()
    {
        var step = 4000UL;
        total += step;
        idle += step - (ulong)random.Next(200, 2400);
        return new HostResourceReading(total, idle, 16UL * 1024 * 1024 * 1024, (ulong)random.Next(4, 12) * 1024 * 1024 * 1024);
    }
}
