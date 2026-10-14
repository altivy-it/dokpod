using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Dokpod.Agent.Contracts.V1;
using Dokpod.ControlPlane.Application.Auditing;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Inventory;
using Dokpod.ControlPlane.Application.Queries;
using Dokpod.ControlPlane.Application.Terminal;
using Dokpod.Domain.Auditing;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Inventory;
using Google.Protobuf;
using Xunit;

namespace Dokpod.ControlPlane.Application.Tests.Terminal;

/// <summary>Spec 014 T008-T010/SC-003: política, retransmissão, auditoria sem conteúdo, limites, revogação e ociosidade.</summary>
public sealed class TerminalSessionServiceTests
{
    private static readonly Guid Env = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private static readonly string ContainerId = new('a', 64);
    private const string Canary = "canario-014-conteudo";

    private sealed class Harness
    {
        public TestDecider Decider { get; } = new();
        public AuditSink Audit { get; } = new();
        public AgentQueryDispatcher Dispatcher { get; } = new(TimeProvider.System);
        public TerminalSessionRegistry Registry { get; }
        public TerminalOptions Options { get; }
        public Channel<ControlPlaneMessage> AgentInbox { get; } = Channel.CreateUnbounded<ControlPlaneMessage>();
        public EnvironmentStage? Stage { get; init; }
        public string State { get; init; } = "running";

        public Harness(TerminalOptions? options = null, bool connected = true, bool exec = true)
        {
            Options = options ?? new TerminalOptions();
            Registry = new TerminalSessionRegistry(Options);
            if (connected) Dispatcher.Register(Env, Guid.NewGuid(), exec ? [Capability.ContainerExec] : [Capability.Inventory], AgentInbox.Writer);
        }

        public TerminalSessionService Service() => new(
            Decider,
            new Registrations(Stage),
            new Containers(State),
            Dispatcher,
            Registry,
            Audit,
            Options,
            TimeProvider.System);

        /// <summary>Simula o agente: lê o ExecOpen, vincula, abre e devolve eco do que receber.</summary>
        public async Task<List<ExecFrame>> AgentAsync(string? refuse = null, bool echo = true)
        {
            var received = new List<ExecFrame>();
            var open = (await AgentInbox.Reader.ReadAsync(TestContext.Current.CancellationToken)).ExecOpen;
            var session = Registry.Attach(Guid.Parse(open.ExecSessionId), Env)!;
            if (refuse is not null)
            {
                await session.FromAgent.Writer.WriteAsync(new ExecFrame { Status = new ExecStatus { State = ExecState.Error, Reason = refuse } });
                session.FromAgent.Writer.TryComplete();
                return received;
            }

            await session.FromAgent.Writer.WriteAsync(new ExecFrame { Status = new ExecStatus { State = ExecState.Opened, Shell = "/bin/bash", ContainerUserIsRoot = true } });
            await foreach (var frame in session.ToAgent.Reader.ReadAllAsync())
            {
                received.Add(frame);
                if (frame.FrameCase == ExecFrame.FrameOneofCase.Stdin && echo)
                {
                    await session.FromAgent.Writer.WriteAsync(new ExecFrame { Output = frame.Stdin });
                }
                else if (frame.FrameCase == ExecFrame.FrameOneofCase.Status)
                {
                    await session.FromAgent.Writer.WriteAsync(new ExecFrame { Status = new ExecStatus { State = ExecState.Closed, Reason = frame.Status.Reason } });
                    break;
                }
            }

            session.FromAgent.Writer.TryComplete();
            return received;
        }

        public async Task<List<TerminalOutputFrame>> RunAsync(IAsyncEnumerable<TerminalInputFrame> input, string? justification = null, bool webSocket = true)
        {
            var output = Channel.CreateUnbounded<TerminalOutputFrame>();
            await Service().RunAsync(
                new TerminalOpenRequest(Env, ContainerId, 100, 30, justification),
                AuthenticatedActor.FromSubject("u-1"),
                "token",
                DateTimeOffset.UtcNow.AddHours(1),
                null,
                webSocket,
                Guid.NewGuid(),
                input,
                output.Writer,
                TestContext.Current.CancellationToken);
            output.Writer.TryComplete();
            return await output.Reader.ReadAllAsync().ToListAsync();
        }
    }

    private static async IAsyncEnumerable<TerminalInputFrame> Input(params TerminalInputFrame[] frames)
    {
        foreach (var frame in frames)
        {
            await Task.Delay(50);
            yield return frame;
        }

        await Task.Delay(300);
    }

    private static async IAsyncEnumerable<TerminalInputFrame> Forever([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        yield break;
    }

    [Fact]
    public async Task Open_RelaysInputAndOutputAndAuditsOnlyMetadata()
    {
        var harness = new Harness { Stage = EnvironmentStage.Production };
        var agent = harness.AgentAsync();

        var output = await harness.RunAsync(
            Input(new TerminalInputFrame(Encoding.UTF8.GetBytes("echo " + Canary + "\n"), null, null), new TerminalInputFrame(null, 120, 40)),
            justification: "Investigar incidente INC-42 " + "tok" + "en=abc123segredo");

        var frames = await agent;
        Assert.Contains(frames, f => f.FrameCase == ExecFrame.FrameOneofCase.Resize && f.Resize.Cols == 120);
        Assert.Contains(frames, f => f.FrameCase == ExecFrame.FrameOneofCase.Status && f.Status.Reason == "user_closed");
        Assert.Equal("connecting", output[0].Status!.State);
        Assert.Contains(output, o => o.Status is { State: "connected", Shell: "/bin/bash", ContainerUserIsRoot: true });
        Assert.Contains(Canary, string.Concat(output.Where(o => o.Data is not null).Select(o => Encoding.UTF8.GetString(o.Data!))));
        Assert.Equal("closed", output[^1].Status!.State);

        Assert.Equal(["container.exec.requested", "container.exec.started", "container.exec.ended"], harness.Audit.Events.Select(e => e.Action));
        var audit = JsonSerializer.Serialize(harness.Audit.Events.Select(e => e.Details));
        Assert.DoesNotContain(Canary, audit);
        Assert.DoesNotContain("abc123segredo", audit);
        Assert.Contains("token=***", harness.Audit.Events[0].Details!["justification"]);
        var ended = harness.Audit.Events[2].Details!;
        Assert.Equal("user_closed", ended["reason"]);
        Assert.Equal("false", ended["orphanSuspected"]);
        Assert.True(long.Parse(ended["bytesIn"]) > 0);
        Assert.Equal(0, harness.Registry.Count);
    }

    [Theory]
    [InlineData("websocket")]
    [InlineData("scope")]
    [InlineData("justification")]
    [InlineData("not_running")]
    [InlineData("not_supported")]
    [InlineData("agent_disconnected")]
    public async Task Open_IsDeniedWithReason(string scenario)
    {
        var harness = scenario switch
        {
            "justification" => new Harness { Stage = EnvironmentStage.Production },
            "not_running" => new Harness { State = "exited" },
            "not_supported" => new Harness(exec: false),
            "agent_disconnected" => new Harness(connected: false),
            _ => new Harness(),
        };
        if (scenario == "scope") harness.Decider.Outcome = AuthorizationDecisionOutcome.Denied;

        var output = await harness.RunAsync(Input(), justification: scenario == "justification" ? "curta" : null, webSocket: scenario != "websocket");

        var expected = scenario switch
        {
            "websocket" => "websocket_required",
            "justification" => "justification_missing",
            _ => scenario,
        };
        Assert.Equal(new TerminalStatus("error", expected), Assert.Single(output).Status);
        var denied = Assert.Single(harness.Audit.Events);
        Assert.Equal("container.exec.denied", denied.Action);
        Assert.Equal(expected, denied.Details!["reason"]);
        Assert.Equal(0, harness.Registry.Count);
    }

    [Fact]
    public async Task Open_RespectsPerUserPerContainerLimit()
    {
        var harness = new Harness();
        harness.Registry.TryReserve(new TerminalSession(Guid.NewGuid(), AuthenticatedActor.FromSubject("u-1"), Env, ContainerId, DateTimeOffset.UtcNow));

        var output = await harness.RunAsync(Input());

        Assert.Equal("limit", Assert.Single(output).Status!.Reason);
    }

    [Fact]
    public async Task AgentRefusal_IsReportedAndAudited()
    {
        var harness = new Harness();
        var agent = harness.AgentAsync(refuse: "label_disabled");

        var output = await harness.RunAsync(Input());
        await agent;

        Assert.Contains(output, o => o.Status?.Reason == "label_disabled");
        Assert.Equal("container.exec.denied", harness.Audit.Events[^1].Action);
    }

    [Fact]
    public async Task Revocation_EndsTheSessionAtTheNextRecheck()
    {
        var harness = new Harness(new TerminalOptions { AuthorizationRecheckInterval = TimeSpan.FromMilliseconds(300) });
        var agent = harness.AgentAsync();
        _ = Task.Run(async () =>
        {
            await Task.Delay(500, TestContext.Current.CancellationToken);
            harness.Decider.Outcome = AuthorizationDecisionOutcome.Denied;
        }, TestContext.Current.CancellationToken);

        var output = await harness.RunAsync(Forever(TestContext.Current.CancellationToken));
        await agent;

        Assert.Equal("permission_revoked", output[^1].Status!.Reason);
    }

    [Fact]
    public async Task Idle_WarnsThenEnds()
    {
        var harness = new Harness(new TerminalOptions { IdleTimeout = TimeSpan.FromMilliseconds(2500), WarningBefore = TimeSpan.FromMilliseconds(2000) });
        var agent = harness.AgentAsync();

        var output = await harness.RunAsync(Forever(TestContext.Current.CancellationToken));
        await agent;

        Assert.Contains(output, o => o.Status?.Warning == "idle_timeout");
        Assert.Equal("idle_timeout", output[^1].Status!.Reason);
    }

    [Fact]
    public async Task Renew_OnlyForTheSameActorWithAValidDecision()
    {
        var harness = new Harness();
        var session = new TerminalSession(Guid.NewGuid(), AuthenticatedActor.FromSubject("u-1"), Env, ContainerId, DateTimeOffset.UtcNow);
        harness.Registry.TryReserve(session);
        var service = harness.Service();

        Assert.False(await service.RenewAsync(session.SessionId, AuthenticatedActor.FromSubject("u-2"), "novo", null, Guid.NewGuid(), TestContext.Current.CancellationToken));
        Assert.True(await service.RenewAsync(session.SessionId, AuthenticatedActor.FromSubject("u-1"), "novo", DateTimeOffset.UtcNow.AddMinutes(5), Guid.NewGuid(), TestContext.Current.CancellationToken));
        Assert.Equal("novo", session.Authorization.Token);
        harness.Decider.Outcome = AuthorizationDecisionOutcome.Denied;
        Assert.False(await service.RenewAsync(session.SessionId, AuthenticatedActor.FromSubject("u-1"), "outro", null, Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Masker_HidesCommonSecretShapes()
    {
        // Canários montados por concatenação para não parecerem credenciais literais na varredura de segredos.
        var jwt = string.Join('.', "eyJhbGciOi", "eyJzdWIiOi", "c2lnbmF0dXJl");
        var masked = JustificationMasker.Mask("acesso postgres://app:" + "senha@db " + "Bea" + "rer abcdefghijkl " + "pass" + "word=x1 " + jwt);
        Assert.DoesNotContain("senha", masked);
        Assert.DoesNotContain("abcdefghijkl", masked);
        Assert.DoesNotContain("x1", masked);
        Assert.DoesNotContain("eyJhbGciOi", masked);
    }

    internal sealed class TestDecider : IEnvironmentAuthorizationDecider
    {
        public AuthorizationDecisionOutcome Outcome { get; set; } = AuthorizationDecisionOutcome.Allowed;

        public Task<EnvironmentAuthorizationDecision> DecideAsync(string resource, string scope, AuthenticatedActor actor, string accessToken, Guid correlationId, CancellationToken cancellationToken) =>
            Task.FromResult(new EnvironmentAuthorizationDecision(Outcome, Outcome == AuthorizationDecisionOutcome.Allowed ? null : "access_denied"));
    }

    internal sealed class AuditSink : IAuditEventWriter
    {
        public List<AuditEvent> Events { get; } = [];

        public Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
        {
            lock (Events) Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class Containers(string state) : IContainerSearchStore
    {
        public Task<ContainerSearchResult> SearchAsync(ContainerSearchQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ContainerInventory?> GetAsync(Guid environmentId, string containerId, CancellationToken cancellationToken) =>
            Task.FromResult<ContainerInventory?>(new ContainerInventory(containerId, "web", "nginx", state, "r", DateTimeOffset.UtcNow));
    }
}
