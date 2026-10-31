using System.Text.Json;
using Dokpod.Agent.Application.Engines;
using Dokpod.Agent.Contracts.V1;
using Dokpod.Agent.Infrastructure.Engines.Docker;
using Xunit;

namespace Dokpod.Agent.Podman.IntegrationTests;

/// <summary>
/// Spec 031 T067 com Podman real: rootful e rootless reportam <c>ENGINE</c> com o <c>NCPU</c> do host. Requer
/// <c>DOKPOD_TEST_PODMAN_SOCKET</c>; <c>DOKPOD_TEST_PODMAN_ROOTLESS=1|0</c> confere o modo. Rodando dentro de um
/// container com <c>--cpus</c>, defina <c>DOKPOD_TEST_CPU_LIMITED=1</c> para provar que o limite não altera o valor.
/// </summary>
public sealed class CpuCountSourcePodmanTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EngineInfo_IsTheReportedCpuCountWithEngineSource()
    {
        var socket = Environment.GetEnvironmentVariable("DOKPOD_TEST_PODMAN_SOCKET");
        if (string.IsNullOrWhiteSpace(socket)) Assert.Skip("DOKPOD_TEST_PODMAN_SOCKET não configurado.");
        using var http = DockerEngineClient.CreateUnixSocketClient(socket, TimeSpan.FromSeconds(30));
        var engine = new DockerEngineClient(http);
        var descriptor = await engine.InspectAsync(Token);
        var host = await engine.DescribeHostAsync(Token);
        using var info = JsonDocument.Parse(await http.GetStringAsync($"v{descriptor.ApiVersion}/info", Token));
        var engineCpus = info.RootElement.GetProperty("NCPU").GetInt32();

        var (count, source) = CpuCountResolver.Resolve(host, Environment.ProcessorCount);

        Assert.Equal("podman", host!.Engine);
        Assert.Equal(CpuCountSource.Engine, source);
        Assert.Equal((uint)engineCpus, count);
        if (Environment.GetEnvironmentVariable("DOKPOD_TEST_PODMAN_ROOTLESS") is { } rootless)
        {
            Assert.Equal(rootless == "1", host.Rootless);
        }

        if (Environment.GetEnvironmentVariable("DOKPOD_TEST_CPU_LIMITED") == "1")
        {
            Assert.True(Environment.ProcessorCount < engineCpus, "o teste precisa rodar num container com --cpus menor que o host");
        }
    }

    [Fact]
    public void EngineWithoutCpuCount_FallsBackToProcess()
    {
        var (count, source) = CpuCountResolver.Resolve(new EngineHostInfo("podman", null, null, null, true), Environment.ProcessorCount);

        Assert.Equal(((uint)Environment.ProcessorCount, CpuCountSource.ProcessFallback), (count, source));
    }
}
