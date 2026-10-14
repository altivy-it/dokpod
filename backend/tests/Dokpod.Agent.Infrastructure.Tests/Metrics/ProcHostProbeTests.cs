using Dokpod.Agent.Infrastructure.Metrics;
using Xunit;

namespace Dokpod.Agent.Infrastructure.Tests.Metrics;

public sealed class ProcHostProbeTests
{
    [Fact]
    public void ParseStat_SumsUserThroughStealAndCountsIowaitAsIdle()
    {
        // user nice system idle iowait irq softirq steal guest guest_nice
        var parsed = ProcHostProbe.ParseStat("cpu  100 5 50 800 20 3 2 10 7 0");

        Assert.Equal((990UL, 820UL), parsed);
    }

    [Theory]
    [InlineData("cpu0 1 2 3 4")]
    [InlineData("intr 1 2 3")]
    [InlineData("cpu  a b c d")]
    [InlineData(null)]
    public void ParseStat_RejectsUnexpectedLines(string? line)
    {
        Assert.Null(ProcHostProbe.ParseStat(line));
    }

    [Fact]
    public void ParseMeminfo_ReadsTotalAndAvailableInBytes()
    {
        var parsed = ProcHostProbe.ParseMeminfo(["MemTotal:       16384000 kB", "MemFree:  100 kB", "MemAvailable:    4096000 kB"]);

        Assert.Equal((16384000UL * 1024, 4096000UL * 1024), parsed);
        Assert.Null(ProcHostProbe.ParseMeminfo(["MemFree: 1 kB"]));
    }

    [Fact]
    public void Read_UsesTheConfiguredProcRootOnly()
    {
        var root = Directory.CreateTempSubdirectory("dokpod-proc-");
        try
        {
            File.WriteAllText(Path.Combine(root.FullName, "stat"), "cpu  10 0 10 80 0 0 0 0 0 0\ncpu0 1 1 1 1\n");
            File.WriteAllText(Path.Combine(root.FullName, "meminfo"), "MemTotal: 2048 kB\nMemAvailable: 1024 kB\n");

            var reading = new ProcHostProbe(root.FullName).Read();

            Assert.NotNull(reading);
            Assert.Equal(100UL, reading.CpuTotal);
            Assert.Equal(2048UL * 1024, reading.MemoryTotalBytes);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
