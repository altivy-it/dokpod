using Dokpod.Agent.Application.Engines;
using Dokpod.Agent.Contracts.V1;
using Google.Protobuf;
using Xunit;

namespace Dokpod.Agent.Application.Tests;

/// <summary>Spec 031 T030: a CPU do engine é confiável; o processo do agente é só fallback (FR-013).</summary>
public sealed class CpuCountSourceTests
{
    [Fact]
    public void EngineCount_IsReportedAsEngine()
    {
        var (count, source) = CpuCountResolver.Resolve(new EngineHostInfo("podman", "host-a", 64, 1L << 34, true), processorCount: 2);

        Assert.Equal((64u, CpuCountSource.Engine), (count, source));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void MissingEngineCount_FallsBackToProcessAndSaysSo(int? engineCount)
    {
        var (count, source) = CpuCountResolver.Resolve(new EngineHostInfo("docker", "host-a", engineCount, null, false), processorCount: 4);

        Assert.Equal((4u, CpuCountSource.ProcessFallback), (count, source));
    }

    [Fact]
    public void NoHostInfo_FallsBackToProcess()
    {
        Assert.Equal((8u, CpuCountSource.ProcessFallback), CpuCountResolver.Resolve(null, processorCount: 8));
    }

    [Fact]
    public void HelloField_IsAdditiveAndAbsentMeansUnspecified()
    {
        var withoutSource = AgentHello.Parser.ParseFrom(new AgentHello { CpuCount = 16 }.ToByteArray());

        Assert.Equal(CpuCountSource.Unspecified, withoutSource.CpuCountSource);
        Assert.Equal(14, AgentHello.CpuCountSourceFieldNumber);
        Assert.Equal(10, AgentHello.CpuCountFieldNumber);
    }
}
