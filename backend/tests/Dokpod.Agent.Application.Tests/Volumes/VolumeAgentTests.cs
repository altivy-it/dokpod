using System.Net;
using System.Text;
using Dokpod.Agent.Application.Commands;
using Dokpod.Agent.Application.Engines;
using Dokpod.Agent.Application.Volumes;
using Dokpod.Agent.Infrastructure.Engines.Docker;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Volumes;
using Xunit;

namespace Dokpod.Agent.Application.Tests.Volumes;

/// <summary>Spec 018 T003-T005: inventário de volumes, montagens, remoção nunca forçada e uso sob demanda.</summary>
public sealed class VolumeAgentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly string ContainerA = new('a', 64);
    private static readonly string ContainerB = new('b', 64);

    [Fact]
    public void Builder_PublishesMountsOfAllContainersAndAdvancesRevisionOnlyOnChange()
    {
        var builder = new VolumeInventoryBuilder(new FixedTime(Now));
        var volumes = new[]
        {
            new EngineVolume("dados", "local", "local", Now.AddDays(-1), new Dictionary<string, string> { ["com.docker.compose.project"] = "loja" }),
            new EngineVolume("orfao", "local", "local", null, new Dictionary<string, string>()),
        };
        var containers = new[]
        {
            new EngineContainer(ContainerA, "api", "img", "running", "r1") { Mounts = [new EngineMount("volume", "dados", "/srv/dados", false)] },
            new EngineContainer(ContainerB, "backup", "img", "exited", "r2") { Mounts = [new EngineMount("volume", "dados", "/backup", true), new EngineMount("bind", null, "/etc/app", true)] },
        };

        var first = builder.Build(volumes, containers, force: false);
        var revision = builder.Revision;
        var unchanged = builder.Build(volumes, containers, force: false);
        var forced = builder.Build(volumes, containers, force: true);
        var changed = builder.Build(volumes, [], force: false);

        var page = Assert.Single(first);
        Assert.True(page.IsLastPage);
        var dados = page.Volumes.Single(v => v.Name == "dados");
        Assert.Equal(["api", "backup"], dados.Mounts.Select(m => m.ContainerName));
        Assert.True(dados.Mounts.Single(m => m.ContainerName == "backup").ReadOnly); // container parado conta como uso
        Assert.Equal("loja", dados.Labels["com.docker.compose.project"]);
        Assert.Empty(page.Volumes.Single(v => v.Name == "orfao").Mounts);
        Assert.Empty(unchanged);
        Assert.Equal(revision, Assert.Single(forced).VolumeRevision);
        Assert.True(builder.Revision > revision);
        Assert.All(Assert.Single(changed).Volumes, v => Assert.Empty(v.Mounts));
    }

    [Fact]
    public void Builder_SplitsLargeInventoriesIntoPages()
    {
        var builder = new VolumeInventoryBuilder(new FixedTime(Now));
        var volumes = Enumerable.Range(0, 1201).Select(i => new EngineVolume($"vol-{i:D4}", "local", "local", null, new Dictionary<string, string>())).ToArray();

        var pages = builder.Build(volumes, [], force: true);

        Assert.Equal(3, pages.Count);
        Assert.Equal(1201, pages.Sum(p => p.Volumes.Count));
        Assert.Equal([false, false, true], pages.Select(p => p.IsLastPage));
        Assert.Equal([0u, 1u, 2u], pages.Select(p => p.PageNumber));
    }

    [Fact]
    public async Task DockerClient_ReadsVolumesAndMountsButNeverTheHostPath()
    {
        var handler = new Handler(request => request.RequestUri!.AbsolutePath switch
        {
            "/version" => Json("""{"Version":"27.5.1","ApiVersion":"1.47","MinAPIVersion":"1.24"}"""),
            "/v1.47/volumes" => Json("""{"Volumes":[{"Name":"dados","Driver":"local","Scope":"local","CreatedAt":"2026-10-14T10:00:00Z","Mountpoint":"/var/lib/docker/volumes/dados/_data","Labels":{"app":"loja"}},{"Name":"../etc","Driver":"local"}]}"""),
            "/v1.47/containers/json" => Json($$"""[{"Id":"{{ContainerA}}","Names":["/api"],"Image":"img","State":"exited","Created":1,"Mounts":[{"Type":"volume","Name":"dados","Source":"/var/lib/docker/volumes/dados/_data","Destination":"/srv","RW":false},{"Type":"bind","Source":"/etc","Destination":"/host-etc","RW":true}]}]"""),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });
        var client = new DockerEngineClient(new HttpClient(handler) { BaseAddress = new Uri("http://docker/") });
        var ct = TestContext.Current.CancellationToken;

        var volume = Assert.Single(await client.ListVolumesAsync(ct));
        var mounts = Assert.Single(await client.ListContainersAsync(ct)).Mounts;

        Assert.Equal("dados", volume.Name);
        Assert.Equal("loja", volume.Labels["app"]);
        Assert.Equal(new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero), volume.CreatedAtUtc);
        Assert.Equal(new EngineMount("volume", "dados", "/srv", true), mounts[0]);
        Assert.Equal("bind", mounts[1].Type);
        Assert.DoesNotContain(mounts, m => m.Destination.Contains("/var/lib/docker", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(HttpStatusCode.NoContent, null)]
    [InlineData(HttpStatusCode.NotFound, VolumeRemovalCodes.NotFound)]
    [InlineData(HttpStatusCode.Conflict, VolumeRemovalCodes.InUse)]
    [InlineData(HttpStatusCode.InternalServerError, VolumeRemovalCodes.Failed)]
    public async Task DockerClient_RemovesVolumesWithoutForceAndMapsCodes(HttpStatusCode status, string? expected)
    {
        var handler = new Handler(request => request.RequestUri!.AbsolutePath == "/version"
            ? Json("""{"Version":"27.5.1","ApiVersion":"1.47","MinAPIVersion":"1.24"}""")
            : new HttpResponseMessage(status));
        var client = new DockerEngineClient(new HttpClient(handler) { BaseAddress = new Uri("http://docker/") });

        var failure = await client.RemoveVolumeAsync("dados", TestContext.Current.CancellationToken);

        Assert.Equal(expected, failure);
        var request = handler.Requests.Last();
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/v1.47/volumes/dados?force=0", request.RequestUri!.PathAndQuery);
        await Assert.ThrowsAsync<ArgumentException>(() => client.RemoveVolumeAsync("../etc", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DockerClient_ReadsVolumeUsageFromSystemDf()
    {
        var handler = new Handler(request => request.RequestUri!.AbsolutePath switch
        {
            "/version" => Json("""{"Version":"27.5.1","ApiVersion":"1.47","MinAPIVersion":"1.24"}"""),
            "/v1.47/system/df" => Json("""{"Volumes":[{"Name":"outro","UsageData":{"Size":1,"RefCount":0}},{"Name":"dados","UsageData":{"Size":5242880,"RefCount":2}}]}"""),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });
        var client = new DockerEngineClient(new HttpClient(handler) { BaseAddress = new Uri("http://docker/") });

        var usage = await client.GetVolumeUsageAsync("dados", TestContext.Current.CancellationToken);

        Assert.Equal(new EngineVolumeUsage(5242880, 2), usage);
        Assert.Null(await client.GetVolumeUsageAsync("inexistente", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Processor_RefusesToRemoveAVolumeMountedByAStoppedContainer()
    {
        var engine = new VolumeEngine
        {
            Containers = [new EngineContainer(ContainerA, "backup", "img", "exited", "r1") { Mounts = [new EngineMount("volume", "dados", "/backup", false)] }],
        };
        var processor = Processor(engine);

        var inUse = await processor.ProcessAsync(Command("dados"), 8, TestContext.Current.CancellationToken);
        var free = await processor.ProcessAsync(Command("orfao"), 8, TestContext.Current.CancellationToken);

        Assert.Equal(CommandExecutionState.Failed, inUse.State);
        Assert.Equal(VolumeRemovalCodes.InUse, inUse.FailureCode);
        Assert.Equal(CommandExecutionState.Succeeded, free.State);
        Assert.Equal(["orfao"], engine.Removed);
    }

    private static AgentCommandProcessor Processor(IContainerEngine engine)
    {
        var journal = new Dokpod.Agent.Application.Tests.Commands.AgentCommandProcessorTests.MemoryCommandJournal();
        var time = new FixedTime(Now);
        return new AgentCommandProcessor(new AgentCommandGate(journal, time), journal, engine, time);
    }

    private static AgentCommand Command(string volume)
    {
        var parameters = new RemoveVolumeParameters(volume);
        return new AgentCommand(
            Guid.Parse("9dd93625-9d6a-4c04-94da-d04122028901"),
            Guid.NewGuid(),
            AgentCommandKind.RemoveVolume,
            string.Empty,
            string.Empty,
            AgentCommandKinds.PayloadHash(AgentCommandKind.RemoveVolume, string.Empty, string.Empty, parameters),
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

    private sealed class VolumeEngine : IContainerEngine
    {
        public IReadOnlyList<EngineContainer> Containers { get; init; } = [];
        public List<string> Removed { get; } = [];

        public Task<EngineDescriptor> InspectAsync(CancellationToken cancellationToken) => Task.FromResult(new EngineDescriptor("test", "1.47"));
        public Task<IReadOnlyList<EngineContainer>> ListContainersAsync(CancellationToken cancellationToken) => Task.FromResult(Containers);
        public Task<EngineContainer?> InspectContainerAsync(string containerId, CancellationToken cancellationToken) => Task.FromResult<EngineContainer?>(null);
        public Task<ContainerMutationResult> ExecuteAsync(AgentCommandKind commandKind, string containerId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<string?> RemoveVolumeAsync(string name, CancellationToken cancellationToken)
        {
            Removed.Add(name);
            return Task.FromResult<string?>(null);
        }
    }
}
