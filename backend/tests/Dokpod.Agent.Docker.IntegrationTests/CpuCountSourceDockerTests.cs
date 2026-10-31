using System.Text.Json;
using Dokpod.Agent.Application.Engines;
using Dokpod.Agent.Contracts.V1;
using Dokpod.Agent.Infrastructure.Engines.Docker;
using Xunit;

namespace Dokpod.Agent.Docker.IntegrationTests;

/// <summary>
/// Spec 031 T066 com Docker real: <c>EngineHostInfo.CpuCount</c> vem do <c>/info</c> do engine e resulta em
/// <c>ENGINE</c>. Rodando num container com <c>--cpus</c>, defina <c>DOKPOD_TEST_CPU_LIMITED=1</c> para provar que o
/// limite do container do agente não altera o valor reportado.
/// </summary>
public sealed class CpuCountSourceDockerTests
{
    private const string Socket = "/var/run/docker.sock";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EngineInfo_IsTheReportedCpuCountWithEngineSource()
    {
        if (!File.Exists(Socket)) Assert.Skip("Socket do Docker indisponível.");
        using var http = DockerEngineClient.CreateUnixSocketClient(Socket, TimeSpan.FromSeconds(30));
        var engine = new DockerEngineClient(http);
        var descriptor = await engine.InspectAsync(Token);
        var host = await engine.DescribeHostAsync(Token);
        using var info = JsonDocument.Parse(await http.GetStringAsync($"v{descriptor.ApiVersion}/info", Token));
        var engineCpus = info.RootElement.GetProperty("NCPU").GetInt32();

        var (count, source) = CpuCountResolver.Resolve(host, Environment.ProcessorCount);

        Assert.Equal("docker", host!.Engine);
        Assert.Equal((CpuCountSource.Engine, (uint)engineCpus), (source, count));
        if (Environment.GetEnvironmentVariable("DOKPOD_TEST_CPU_LIMITED") == "1")
        {
            Assert.True(Environment.ProcessorCount < engineCpus, "o teste precisa rodar num container com --cpus menor que o host");
        }
    }
}
