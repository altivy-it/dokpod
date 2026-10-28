using System.Text.Json;
using Dokpod.Agent.Infrastructure.Engines.Docker;
using Xunit;

namespace Dokpod.Agent.Infrastructure.Tests.Metrics;

public sealed class ContainerStatsParsingTests
{
    [Fact]
    public void ParseStats_PodmanCompatResponse_ReadsCountersAndReadInstant()
    {
        // Podman 6.1.3 (API compatível, VM da spec 005): `preread` vem vazio e `system_cpu_usage` não segue a base do Docker.
        using var document = JsonDocument.Parse("""
            {"read":"2026-10-28T23:03:26.254772229Z","preread":"0001-01-01T00:00:00Z",
             "cpu_stats":{"cpu_usage":{"total_usage":234476807000},"system_cpu_usage":35516433114000,"online_cpus":8},
             "memory_stats":{"usage":209715200,"limit":536870912,"stats":{"inactive_file":1048576}}}
            """);

        var stats = DockerEngineClient.ParseStats(document.RootElement)!;

        Assert.Equal(234_476_807_000UL, stats.CpuTotalNanos);
        Assert.Equal(8, stats.OnlineCpus);
        Assert.Equal(208_666_624UL, stats.MemoryUsedBytes);
        Assert.Equal(new DateTimeOffset(2026, 10, 28, 23, 3, 26, TimeSpan.Zero).AddTicks(2_547_722), stats.ReadAt);
    }

    [Theory]
    [InlineData("\"0001-01-01T00:00:00Z\"")]
    [InlineData("\"não é data\"")]
    [InlineData("null")]
    public void ParseStats_WithoutUsableReadInstant_LeavesItEmpty(string read)
    {
        using var document = JsonDocument.Parse("""{"read":""" + read + ""","cpu_stats":{"cpu_usage":{"total_usage":1},"online_cpus":4}}""");

        Assert.Null(DockerEngineClient.ParseStats(document.RootElement)!.ReadAt);
    }
}
