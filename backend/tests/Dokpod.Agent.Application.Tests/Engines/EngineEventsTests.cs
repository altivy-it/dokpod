using System.Net;
using System.Text;
using Dokpod.Agent.Application.Engines;
using Dokpod.Agent.Application.Events;
using Dokpod.Agent.Contracts.V1;
using Dokpod.Agent.Infrastructure.Engines.Docker;
using Xunit;

namespace Dokpod.Agent.Application.Tests.Engines;

public sealed class EngineEventsTests
{
    private const string Id = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void Publisher_FusesDieAfterOomIntoSingleErrorEvent()
    {
        var publisher = new EngineEventPublisher(new ManualTimeProvider(DateTimeOffset.UtcNow));

        Assert.True(publisher.Observe(Record("oom")));
        Assert.True(publisher.Observe(Record("die", exitCode: 137)));

        var batch = publisher.Drain()!;
        var single = Assert.Single(batch.Events);
        Assert.Equal(EngineEventType.ContainerOomKilled, single.Type);
        Assert.Equal(0u, batch.DroppedCount);
    }

    [Theory]
    [InlineData("destroy")] // Docker
    [InlineData("remove")] // Podman
    public void Publisher_MapsContainerRemovalOfBothEngines(string action)
    {
        var publisher = new EngineEventPublisher(new ManualTimeProvider(DateTimeOffset.UtcNow));

        Assert.True(publisher.Observe(Record(action)));

        Assert.Equal(EngineEventType.ContainerRemoved, Assert.Single(publisher.Drain()!.Events).Type);
    }

    [Fact]
    public void Publisher_AggregatesWithinWindowIgnoresCleanExitAndReportsRecovery()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var publisher = new EngineEventPublisher(time);

        publisher.Observe(Record("die", exitCode: 0));
        publisher.Observe(Record("restart"));
        publisher.Observe(Record("restart"));
        publisher.Observe(Record("health_status: unhealthy"));
        publisher.Observe(Record("health_status: healthy"));

        var batch = publisher.Drain()!;
        Assert.Equal(
            [EngineEventType.ContainerAutoRestarted, EngineEventType.ContainerUnhealthy, EngineEventType.ContainerRecovered],
            batch.Events.Select(e => e.Type));
        Assert.Equal(2u, batch.Events[0].AggregatedCount);

        time.Advance(TimeSpan.FromSeconds(11));
        publisher.Observe(Record("restart"));
        Assert.Single(publisher.Drain()!.Events);
    }

    [Fact]
    public void Publisher_LimitsRateAndCountsDropped()
    {
        var publisher = new EngineEventPublisher(new ManualTimeProvider(DateTimeOffset.UtcNow));
        for (var i = 0; i < EngineEventPublisher.MaxEventsPerMinute + 5; i++)
        {
            publisher.Observe(new EngineEventRecord("container", "create", $"{i:D64}", $"c{i}", null, null, DateTimeOffset.UtcNow, i));
        }

        var first = publisher.Drain()!;
        Assert.Equal(EngineEventPublisher.MaxEventsPerBatch, first.Events.Count);
        Assert.Equal(5u, first.DroppedCount);
        Assert.Equal(EngineEventPublisher.MaxEventsPerMinute - EngineEventPublisher.MaxEventsPerBatch, publisher.Drain()!.Events.Count);
        Assert.Null(publisher.Drain());
    }

    [Fact]
    public void ParseEvent_ReadsActorAllowlistOnly()
    {
        var record = DockerEngineClient.ParseEvent("""
            {"Type":"container","Action":"die","Actor":{"ID":"abc","Attributes":{"name":"worker","image":"img:1","exitCode":"1","SECRET":"x"}},"timeNano":1760108096000000000}
            """);

        Assert.NotNull(record);
        Assert.Equal("die", record.Action);
        Assert.Equal("worker", record.Name);
        Assert.Equal(1, record.ExitCode);
        Assert.Null(DockerEngineClient.ParseEvent("not json"));
    }

    [Fact]
    public async Task Client_DetectsPodmanAndEnrichesContainers()
    {
        var handler = new Handler(request => request.RequestUri!.AbsolutePath switch
        {
            "/version" => Json("""{"Version":"5.2.3","ApiVersion":"1.41","MinAPIVersion":"1.24","Components":[{"Name":"Podman Engine"}]}"""),
            "/v1.41/info" => Json("""{"Name":"srv-prod-03","NCPU":8,"MemTotal":17179869184,"SecurityOptions":["name=rootless"]}"""),
            "/v1.41/containers/json" => Json($$"""[{"Id":"{{Id}}","Names":["/api"],"Image":"img","State":"exited","Status":"Exited (137) 1 minute ago","Created":1760100000,"Ports":[{"IP":"0.0.0.0","PrivatePort":80,"PublicPort":8080,"Type":"tcp"},{"IP":"::","PrivatePort":80,"PublicPort":8080,"Type":"tcp"}]}]"""),
            _ => Json("""{"RestartCount":2,"State":{"Status":"exited","ExitCode":137,"OOMKilled":true,"StartedAt":"2026-10-10T18:08:04Z"}}"""),
        });
        var client = new DockerEngineClient(new HttpClient(handler) { BaseAddress = new Uri("http://docker/") });

        var host = await client.DescribeHostAsync(TestContext.Current.CancellationToken);
        var container = Assert.Single(await client.ListContainersAsync(TestContext.Current.CancellationToken));

        Assert.Equal("podman", host!.Engine);
        Assert.True(host.Rootless);
        Assert.Equal(8, host.CpuCount);
        Assert.Equal(137, container.ExitCode);
        Assert.True(container.OomKilled);
        Assert.Equal(2, container.RestartCount);
        Assert.Equal(8080, Assert.Single(container.Ports).HostPort);
    }

    private static EngineEventRecord Record(string action, int? exitCode = null) =>
        new("container", action, Id, "worker", "img", exitCode, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000);

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    private sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset now = start;

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan delta) => now += delta;
    }
}
