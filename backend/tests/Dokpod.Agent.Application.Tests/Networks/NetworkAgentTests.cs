using System.Net;
using System.Text;
using Dokpod.Agent.Application.Commands;
using Dokpod.Agent.Application.Engines;
using Dokpod.Agent.Application.Networks;
using Dokpod.Agent.Infrastructure.Engines.Docker;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Networks;
using Xunit;

namespace Dokpod.Agent.Application.Tests.Networks;

/// <summary>Spec 019 T003-T005: inventário de redes, conexões de containers parados e remoção que nunca desconecta.</summary>
public sealed class NetworkAgentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly string ContainerA = new('a', 64);
    private static readonly string ContainerB = new('b', 64);
    private static readonly string NetPedidos = new('1', 64);
    private static readonly string NetOrfa = new('2', 64);
    private static readonly string NetBridge = new('3', 64);

    private static EngineNetwork Network(string id, string name, string scope = "local", bool ingress = false) =>
        new(id, name, "bridge", scope, false, false, false, ingress, null, [new EngineNetworkSubnet("172.20.0.0/16", "172.20.0.1")], new Dictionary<string, string>());

    [Fact]
    public void Builder_PublishesConnectionsOfAllContainersMarksSystemAndAdvancesRevisionOnlyOnChange()
    {
        var builder = new NetworkInventoryBuilder(new FixedTime(Now));
        var networks = new[] { Network(NetPedidos, "rede-pedidos"), Network(NetOrfa, "orfa"), Network(NetBridge, "bridge") };
        var containers = new[]
        {
            new EngineContainer(ContainerA, "api", "img", "running", "r1") { Networks = [new EngineNetworkAttachment("rede-pedidos", NetPedidos, "172.20.0.14", null)] },
            // Parado: sem endereço e, no Podman, às vezes sem o identificador da rede (ligação pelo nome).
            new EngineContainer(ContainerB, "worker", "img", "exited", "r2") { Networks = [new EngineNetworkAttachment("rede-pedidos", null, null, null)] },
        };

        var page = Assert.Single(builder.Build(networks, containers, force: false));
        var revision = builder.Revision;

        var pedidos = page.Networks.Single(n => n.Name == "rede-pedidos");
        Assert.Equal(["api", "worker"], pedidos.Containers.Select(c => c.ContainerName));
        Assert.Equal("172.20.0.14", pedidos.Containers[0].Ipv4Address);
        Assert.Equal(string.Empty, pedidos.Containers[1].Ipv4Address);
        Assert.Equal("172.20.0.0/16", pedidos.Subnets[0].Subnet);
        Assert.Empty(page.Networks.Single(n => n.Name == "orfa").Containers);
        Assert.True(page.Networks.Single(n => n.Name == "bridge").System);
        Assert.False(pedidos.System);
        Assert.Empty(builder.Build(networks, containers, force: false));
        Assert.Single(builder.Build(networks, [], force: false));
        Assert.True(builder.Revision > revision);
    }

    [Fact]
    public async Task DockerClient_ReadsNetworksAndConnectionsButNeverDriverOptions()
    {
        var handler = new Handler(request => request.RequestUri!.AbsolutePath switch
        {
            "/version" => Json("""{"Version":"27.5.1","ApiVersion":"1.47","MinAPIVersion":"1.24"}"""),
            "/v1.47/networks" => Json($$$"""
                [{"Name":"rede-pedidos","Id":"{{{NetPedidos}}}","Created":"2026-10-14T10:00:00Z","Scope":"local","Driver":"macvlan","EnableIPv6":true,"Internal":true,"Attachable":true,"Ingress":false,
                  "IPAM":{"Config":[{"Subnet":"10.89.77.0/24","Gateway":"10.89.77.1"}]},"Options":{"parent":"eth0"},"Labels":{"app":"loja"},"Containers":{}},
                 {"Name":"ingress","Id":"{{{NetBridge}}}","Scope":"swarm","Driver":"overlay","Ingress":true},
                 {"Name":"../etc","Id":"{{{NetOrfa}}}"}]
                """),
            "/v1.47/containers/json" => Json($$$"""[{"Id":"{{{ContainerA}}}","Names":["/api"],"Image":"img","State":"exited","Created":1,"NetworkSettings":{"Networks":{"rede-pedidos":{"NetworkID":"{{{NetPedidos}}}","IPAddress":"","GlobalIPv6Address":"" } } } }]"""),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });
        var client = new DockerEngineClient(new HttpClient(handler) { BaseAddress = new Uri("http://docker/") });
        var ct = TestContext.Current.CancellationToken;

        var networks = await client.ListNetworksAsync(ct);
        var attachment = Assert.Single(Assert.Single(await client.ListContainersAsync(ct)).Networks);

        Assert.Equal(2, networks.Count);
        var pedidos = networks.Single(n => n.Name == "rede-pedidos");
        Assert.Equal(("macvlan", true, true, true), (pedidos.Driver, pedidos.Internal, pedidos.IPv6, pedidos.Attachable));
        Assert.Equal(new EngineNetworkSubnet("10.89.77.0/24", "10.89.77.1"), Assert.Single(pedidos.Subnets));
        Assert.Equal("loja", pedidos.Labels["app"]);
        Assert.True(networks.Single(n => n.Name == "ingress").Ingress);
        Assert.Equal(new EngineNetworkAttachment("rede-pedidos", NetPedidos, null, null), attachment);
        Assert.DoesNotContain("eth0", NetworkInventoryBuilder.ToState(pedidos, []).ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.NoContent, null)]
    [InlineData(HttpStatusCode.NotFound, NetworkRemovalCodes.NotFound)]
    [InlineData(HttpStatusCode.Forbidden, NetworkRemovalCodes.InUse)]
    [InlineData(HttpStatusCode.Conflict, NetworkRemovalCodes.InUse)]
    [InlineData(HttpStatusCode.InternalServerError, NetworkRemovalCodes.Failed)]
    public async Task DockerClient_RemovesNetworksByIdAndMapsCodes(HttpStatusCode status, string? expected)
    {
        var handler = new Handler(request => request.RequestUri!.AbsolutePath == "/version"
            ? Json("""{"Version":"27.5.1","ApiVersion":"1.47","MinAPIVersion":"1.24"}""")
            : new HttpResponseMessage(status));
        var client = new DockerEngineClient(new HttpClient(handler) { BaseAddress = new Uri("http://docker/") });

        Assert.Equal(expected, await client.RemoveNetworkAsync(NetOrfa, TestContext.Current.CancellationToken));
        var request = handler.Requests.Last();
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal($"/v1.47/networks/{NetOrfa}", request.RequestUri!.PathAndQuery);
        await Assert.ThrowsAsync<ArgumentException>(() => client.RemoveNetworkAsync("rede", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Processor_RefusesInUseSystemSwarmAndMissingNetworksAndNeverDisconnects()
    {
        var swarm = new string('4', 64);
        var engine = new NetworkEngine
        {
            Networks = [Network(NetPedidos, "rede-pedidos"), Network(NetOrfa, "orfa"), Network(NetBridge, "bridge"), Network(swarm, "cluster", scope: "swarm")],
            Containers = [new EngineContainer(ContainerA, "worker", "img", "exited", "r1") { Networks = [new EngineNetworkAttachment("rede-pedidos", NetPedidos, null, null)] }],
        };
        var processor = Processor(engine);
        var ct = TestContext.Current.CancellationToken;

        var inUse = await processor.ProcessAsync(Command(NetPedidos, "rede-pedidos"), 8, ct);
        var system = await processor.ProcessAsync(Command(NetBridge, "bridge"), 8, ct);
        var cluster = await processor.ProcessAsync(Command(swarm, "cluster"), 8, ct);
        var missing = await processor.ProcessAsync(Command(new string('5', 64), "sumiu"), 8, ct);
        var free = await processor.ProcessAsync(Command(NetOrfa, "orfa"), 8, ct);

        Assert.Equal(NetworkRemovalCodes.InUse, inUse.FailureCode);
        Assert.Equal(NetworkRemovalCodes.System, system.FailureCode);
        Assert.Equal(NetworkRemovalCodes.NotLocal, cluster.FailureCode);
        Assert.Equal(NetworkRemovalCodes.NotFound, missing.FailureCode);
        Assert.Equal(CommandExecutionState.Succeeded, free.State);
        Assert.Equal([NetOrfa], engine.Removed);
    }

    private static AgentCommandProcessor Processor(IContainerEngine engine)
    {
        var journal = new Dokpod.Agent.Application.Tests.Commands.AgentCommandProcessorTests.MemoryCommandJournal();
        var time = new FixedTime(Now);
        return new AgentCommandProcessor(new AgentCommandGate(journal, time), journal, engine, time);
    }

    private static AgentCommand Command(string networkId, string name)
    {
        var parameters = new RemoveNetworkParameters(networkId, name);
        return new AgentCommand(
            Guid.Parse("9dd93625-9d6a-4c04-94da-d04122028901"),
            Guid.NewGuid(),
            AgentCommandKind.RemoveNetwork,
            string.Empty,
            string.Empty,
            AgentCommandKinds.PayloadHash(AgentCommandKind.RemoveNetwork, string.Empty, string.Empty, parameters),
            Now.AddMinutes(1),
            8)
        {
            Parameters = parameters,
        };
    }

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private sealed class NetworkEngine : IContainerEngine
    {
        public IReadOnlyList<EngineNetwork> Networks { get; init; } = [];
        public IReadOnlyList<EngineContainer> Containers { get; init; } = [];
        public List<string> Removed { get; } = [];

        public Task<EngineDescriptor> InspectAsync(CancellationToken cancellationToken) => Task.FromResult(new EngineDescriptor("test", "1.47"));
        public Task<IReadOnlyList<EngineContainer>> ListContainersAsync(CancellationToken cancellationToken) => Task.FromResult(Containers);
        public Task<EngineContainer?> InspectContainerAsync(string containerId, CancellationToken cancellationToken) => Task.FromResult<EngineContainer?>(null);
        public Task<ContainerMutationResult> ExecuteAsync(AgentCommandKind commandKind, string containerId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<EngineNetwork>> ListNetworksAsync(CancellationToken cancellationToken) => Task.FromResult(Networks);

        public Task<string?> RemoveNetworkAsync(string networkId, CancellationToken cancellationToken)
        {
            Removed.Add(networkId);
            return Task.FromResult<string?>(null);
        }
    }
}
