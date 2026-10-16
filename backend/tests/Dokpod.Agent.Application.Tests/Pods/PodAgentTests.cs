using System.Net;
using System.Text;
using Dokpod.Agent.Application.Commands;
using Dokpod.Agent.Application.Engines;
using Dokpod.Agent.Application.Pods;
using Dokpod.Agent.Infrastructure.Engines;
using Dokpod.Agent.Infrastructure.Engines.Docker;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Pods;
using Xunit;

namespace Dokpod.Agent.Application.Tests.Pods;

/// <summary>Spec 021 T003-T005: pods pela API libpod, Docker sem pods, pod do agente e ações com falha parcial.</summary>
public sealed class PodAgentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly string PodA = new('1', 64);
    private static readonly string PodAgent = new('2', 64);
    private static readonly string Web = new('a', 64);
    private static readonly string Worker = new('b', 64);
    private static readonly string Agent = "8762fe176328" + new string('c', 52);

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task DockerClient_ReadsLibpodPodsAndReportsNoPodsOnDocker()
    {
        var podman = new Handler(request => request.RequestUri!.AbsolutePath switch
        {
            "/version" => Json("""{"Version":"5.7.0","ApiVersion":"1.41","MinAPIVersion":"1.24"}"""),
            "/v1.41/libpod/pods/json" => Json($$$"""
                [{"Id":"{{{PodA}}}","Name":"pod-loja","Status":"Degraded","Created":"2026-10-15T10:00:00Z","InfraId":"","Labels":{"io.podman.compose.project":"loja"},"Networks":["loja_default"],
                  "Containers":[{"Id":"{{{Web}}}","Names":"web","Status":"running","RestartCount":2},{"Id":"{{{Worker}}}","Names":"worker","Status":"exited","RestartCount":0}]},
                 {"Id":"invalido","Name":"x"}]
                """),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });
        var docker = new Handler(request => request.RequestUri!.AbsolutePath == "/version"
            ? Json("""{"Version":"27.5.1","ApiVersion":"1.47","MinAPIVersion":"1.24"}""")
            : new HttpResponseMessage(HttpStatusCode.NotFound));
        var ct = TestContext.Current.CancellationToken;

        var pod = Assert.Single((await new DockerEngineClient(new HttpClient(podman) { BaseAddress = new Uri("http://docker/") }).ListPodsAsync(ct))!);
        var none = await new DockerEngineClient(new HttpClient(docker) { BaseAddress = new Uri("http://docker/") }).ListPodsAsync(ct);

        Assert.Equal(("pod-loja", "Degraded", (string?)null), (pod.Name, pod.Status, pod.InfraId));
        Assert.Equal(["web", "worker"], pod.Members.Select(m => m.Name));
        Assert.Equal(2, pod.Members[0].Restarts);
        Assert.Equal("loja", pod.Labels["io.podman.compose.project"]);
        Assert.Null(none);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, null)]
    [InlineData(HttpStatusCode.NotModified, null)]
    [InlineData(HttpStatusCode.NotFound, PodActionCodes.NotFound)]
    [InlineData(HttpStatusCode.Conflict, PodActionCodes.PartialFailure)]
    [InlineData(HttpStatusCode.InternalServerError, PodActionCodes.Failed)]
    public async Task DockerClient_PostsPodActionsAndMapsCodes(HttpStatusCode status, string? expected)
    {
        var handler = new Handler(request => request.RequestUri!.AbsolutePath == "/version"
            ? Json("""{"Version":"5.7.0","ApiVersion":"1.41","MinAPIVersion":"1.24"}""")
            : new HttpResponseMessage(status));
        var client = new DockerEngineClient(new HttpClient(handler) { BaseAddress = new Uri("http://docker/") });

        Assert.Equal(expected, await client.PodActionAsync(PodA, PodActionKind.Restart, TestContext.Current.CancellationToken));
        Assert.Equal($"/v1.41/libpod/pods/{PodA}/restart", handler.Requests.Last().RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Post, handler.Requests.Last().Method);
    }

    [Fact]
    public async Task DockerClient_StopsRunningMembersWhenPodmanAnswers304ForADegradedPod()
    {
        var handler = new Handler(request => request.RequestUri!.AbsolutePath switch
        {
            "/version" => Json("""{"Version":"5.7.0","ApiVersion":"1.41","MinAPIVersion":"1.24"}"""),
            var path when path == $"/v1.41/libpod/pods/{PodA}/stop" => new HttpResponseMessage(HttpStatusCode.NotModified),
            "/v1.41/libpod/pods/json" => Json($$$"""
                [{"Id":"{{{PodA}}}","Name":"pod-teste","Status":"Degraded","Containers":[
                  {"Id":"{{{Web}}}","Names":"web","Status":"running","RestartCount":0},
                  {"Id":"{{{Worker}}}","Names":"worker","Status":"exited","RestartCount":0}]}]
                """),
            var path when path.EndsWith("/stop", StringComparison.Ordinal) => new HttpResponseMessage(HttpStatusCode.NoContent),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });
        var client = new DockerEngineClient(new HttpClient(handler) { BaseAddress = new Uri("http://docker/") });

        Assert.Null(await client.PodActionAsync(PodA, PodActionKind.Stop, TestContext.Current.CancellationToken));
        var stops = handler.Requests.Where(r => r.RequestUri!.AbsolutePath.StartsWith("/v1.41/containers/", StringComparison.Ordinal)).Select(r => r.RequestUri!.AbsolutePath).ToArray();
        Assert.Equal([$"/v1.41/containers/{Web}/stop"], stops); // só o membro em execução
    }

    [Fact]
    public void SelfIdentity_RecognizesTheContainerByMountInfoOrHostnamePrefix()
    {
        var mounts = Path.GetTempFileName();
        var hostname = Path.GetTempFileName();
        try
        {
            File.WriteAllText(mounts, $"1 2 0:1 / /etc/hosts rw - overlay overlay rw\n3 4 0:2 /containers/{Worker}/userdata/hosts /etc/hosts rw\n");
            File.WriteAllText(hostname, "8762fe176328\n");
            var self = new SelfContainerIdentity(mounts, hostname);

            Assert.True(self.IsSelf(Worker));
            Assert.True(self.IsSelf(Agent));
            Assert.False(self.IsSelf(Web));
            Assert.False(new SelfContainerIdentity("/nao/existe", "/nao/existe").IsSelf(Agent));
        }
        finally
        {
            File.Delete(mounts);
            File.Delete(hostname);
        }
    }

    [Fact]
    public void Builder_MarksTheAgentPodAndReportsEnginesWithoutPods()
    {
        var self = new Self(Agent);
        var builder = new PodInventoryBuilder(new FixedTime(Now), self);
        var pods = new[] { Pod(PodA, "pod-loja", "Running", ("web", Web, "running")), Pod(PodAgent, "pod_dokpod", "Running", ("agent", Agent, "running")) };

        var page = Assert.Single(builder.Build(pods, force: false));
        var docker = Assert.Single(new PodInventoryBuilder(new FixedTime(Now), self).Build(null, force: false));

        Assert.True(page.Pods.Single(p => p.Name == "pod_dokpod").AgentPod);
        Assert.False(page.Pods.Single(p => p.Name == "pod-loja").AgentPod);
        Assert.False(page.EngineWithoutPods);
        Assert.Empty(builder.Build(pods, force: false));
        Assert.True(docker.EngineWithoutPods);
        Assert.Empty(docker.Pods);
    }

    [Fact]
    public async Task Processor_RefusesToStopTheAgentPodAndReportsMembersOutsideTheExpectedState()
    {
        var engine = new PodEngine(new Self(Agent))
        {
            Pods = [Pod(PodA, "pod-loja", "Exited", ("web", Web, "exited"), ("worker", Worker, "exited")), Pod(PodAgent, "pod_dokpod", "Running", ("agent", Agent, "running"))],
            Stuck = Worker,
        };
        var processor = Processor(engine, new Self(Agent));
        var ct = TestContext.Current.CancellationToken;

        var agentStop = await processor.ProcessAsync(Command(PodAgent, "pod_dokpod", PodActionKind.Stop), 8, ct);
        var agentStart = await processor.ProcessAsync(Command(PodAgent, "pod_dokpod", PodActionKind.Start), 8, ct);
        var partial = await processor.ProcessAsync(Command(PodA, "pod-loja", PodActionKind.Start), 8, ct);
        var missing = await processor.ProcessAsync(Command(new string('9', 64), "sumiu", PodActionKind.Start), 8, ct);

        Assert.Equal(PodActionCodes.AgentPod, agentStop.FailureCode);
        Assert.Equal(CommandExecutionState.Succeeded, agentStart.State);
        Assert.Equal(PodActionCodes.PartialFailure, partial.FailureCode);
        Assert.Equal(["worker"], partial.Output!.FailedContainers);
        Assert.Equal(PodActionCodes.NotFound, missing.FailureCode);
        Assert.Equal([(PodAgent, PodActionKind.Start), (PodA, PodActionKind.Start)], engine.Actions);
    }

    private static EnginePod Pod(string id, string name, string status, params (string Name, string Id, string Status)[] members) =>
        new(id, name, status, Now, new Dictionary<string, string>(), [], null, members.Select(m => new EnginePodMember(m.Id, m.Name, m.Status, 0)).ToArray());

    private static AgentCommandProcessor Processor(IContainerEngine engine, ISelfContainer self)
    {
        var journal = new Dokpod.Agent.Application.Tests.Commands.AgentCommandProcessorTests.MemoryCommandJournal();
        var time = new FixedTime(Now);
        return new AgentCommandProcessor(new AgentCommandGate(journal, time), journal, engine, time, null, self);
    }

    private static AgentCommand Command(string podId, string name, PodActionKind action)
    {
        var parameters = new PodActionParameters(podId, name, action);
        return new AgentCommand(Guid.Parse("9dd93625-9d6a-4c04-94da-d04122028901"), Guid.NewGuid(), AgentCommandKind.PodAction, string.Empty, string.Empty,
            AgentCommandKinds.PayloadHash(AgentCommandKind.PodAction, string.Empty, string.Empty, parameters), Now.AddMinutes(1), 8)
        {
            Parameters = parameters,
        };
    }

    private sealed class Self(string id) : ISelfContainer
    {
        public bool IsSelf(string containerId) => containerId == id;
    }

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

    /// <summary>Engine em memória: iniciar coloca os membros em execução, exceto o "preso" (falha parcial).</summary>
    private sealed class PodEngine(ISelfContainer self) : IContainerEngine
    {
        public IReadOnlyList<EnginePod> Pods { get; set; } = [];
        public string? Stuck { get; init; }
        public List<(string, PodActionKind)> Actions { get; } = [];

        public Task<EngineDescriptor> InspectAsync(CancellationToken cancellationToken) => Task.FromResult(new EngineDescriptor("5.7.0", "1.41"));
        public Task<IReadOnlyList<EngineContainer>> ListContainersAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<EngineContainer>>([]);
        public Task<EngineContainer?> InspectContainerAsync(string containerId, CancellationToken cancellationToken) => Task.FromResult<EngineContainer?>(null);
        public Task<ContainerMutationResult> ExecuteAsync(AgentCommandKind commandKind, string containerId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<EnginePod>?> ListPodsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<EnginePod>?>(Pods);

        public Task<string?> PodActionAsync(string podId, PodActionKind action, CancellationToken cancellationToken)
        {
            Assert.False(PodNames.RefusedForAgentPod(action) && PodStates.IsAgentPod(Pods.Single(p => p.PodId == podId), self), "a ação não pode chegar ao engine no pod do agente");
            Actions.Add((podId, action));
            Pods = Pods.Select(p => p.PodId != podId ? p : p with
            {
                Members = p.Members.Select(m => m with { Status = m.ContainerId == Stuck ? m.Status : "running" }).ToArray(),
            }).ToArray();
            return Task.FromResult<string?>(Stuck is null ? null : PodActionCodes.PartialFailure);
        }
    }
}
