using System.Threading.Channels;
using Dokpod.Agent.Contracts.V1;
using Dokpod.ControlPlane.Application.Queries;
using Xunit;

namespace Dokpod.ControlPlane.Application.Tests.Queries;

public sealed class AgentQueryDispatcherTests
{
    private const string Container = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task Query_AssemblesPartsInOrder()
    {
        var dispatcher = new AgentQueryDispatcher(TimeProvider.System);
        var outbound = Channel.CreateUnbounded<ControlPlaneMessage>();
        var env = Guid.NewGuid();
        using var _ = dispatcher.Register(env, Guid.NewGuid(), [Capability.ContainerLogs], outbound.Writer);

        var query = dispatcher.QueryAsync(env, ContainerQueryKind.Logs, Container, new LogQuery { Tail = 10 }, TestContext.Current.CancellationToken);
        var request = (await outbound.Reader.ReadAsync(TestContext.Current.CancellationToken)).ContainerQuery;
        dispatcher.OnResponse(env, Part(request.QueryId, 0, false, "a", "b"));
        dispatcher.OnResponse(Guid.NewGuid(), Part(request.QueryId, 1, true, "spoof"));
        dispatcher.OnResponse(env, Part(request.QueryId, 1, true, "c"));

        var outcome = await query;
        Assert.Equal(QueryOutcomeKind.Ok, outcome.Kind);
        Assert.Equal(["a", "b", "c"], outcome.Lines.Select(l => l.Text));
    }

    [Fact]
    public async Task Query_FailsOnOutOfOrderPartsAndWithoutCapability()
    {
        var dispatcher = new AgentQueryDispatcher(TimeProvider.System);
        var outbound = Channel.CreateUnbounded<ControlPlaneMessage>();
        var env = Guid.NewGuid();
        using var _ = dispatcher.Register(env, Guid.NewGuid(), [Capability.ContainerLogs], outbound.Writer);

        Assert.Equal(QueryOutcomeKind.NotSupported, (await dispatcher.QueryAsync(env, ContainerQueryKind.Inspect, Container, null, TestContext.Current.CancellationToken)).Kind);
        Assert.Equal(QueryOutcomeKind.AgentDisconnected, (await dispatcher.QueryAsync(Guid.NewGuid(), ContainerQueryKind.Logs, Container, null, TestContext.Current.CancellationToken)).Kind);

        var query = dispatcher.QueryAsync(env, ContainerQueryKind.Logs, Container, null, TestContext.Current.CancellationToken);
        var request = (await outbound.Reader.ReadAsync(TestContext.Current.CancellationToken)).ContainerQuery;
        dispatcher.OnResponse(env, Part(request.QueryId, 1, true, "x"));
        Assert.Equal("response_out_of_order", (await query).FailureCode);
    }

    [Fact]
    public async Task Query_LimitsConcurrencyAndFailsPendingWhenAgentDisconnects()
    {
        var dispatcher = new AgentQueryDispatcher(TimeProvider.System);
        var outbound = Channel.CreateUnbounded<ControlPlaneMessage>();
        var env = Guid.NewGuid();
        var registration = dispatcher.Register(env, Guid.NewGuid(), [Capability.ContainerInspect], outbound.Writer);

        var running = Enumerable.Range(0, AgentQueryDispatcher.MaxConcurrentPerAgent)
            .Select(_ => dispatcher.QueryAsync(env, ContainerQueryKind.Summary, Container, null, TestContext.Current.CancellationToken))
            .ToArray();
        await Task.Delay(50, TestContext.Current.CancellationToken);
        var extra = await dispatcher.QueryAsync(env, ContainerQueryKind.Summary, Container, null, TestContext.Current.CancellationToken);
        Assert.Equal(QueryOutcomeKind.TooManyRequests, extra.Kind);

        registration.Dispose();
        Assert.All(await Task.WhenAll(running), outcome => Assert.Equal(QueryOutcomeKind.AgentDisconnected, outcome.Kind));
    }

    [Fact]
    public async Task LiveSessions_EnforcePerUserLimitAndEndOnDisconnect()
    {
        var dispatcher = new AgentQueryDispatcher(TimeProvider.System);
        var outbound = Channel.CreateUnbounded<ControlPlaneMessage>();
        var env = Guid.NewGuid();
        var registration = dispatcher.Register(env, Guid.NewGuid(), [Capability.ContainerLogs], outbound.Writer);
        var expires = DateTimeOffset.UtcNow.AddMinutes(15);

        var handles = new List<LogStreamHandle>();
        for (var i = 0; i < AgentQueryDispatcher.MaxLiveSessionsPerUser; i++)
        {
            var (outcome, handle) = await dispatcher.StartLogStreamAsync(env, Container, null, "user-1", expires, TestContext.Current.CancellationToken);
            Assert.Equal(LogStreamStartOutcome.Started, outcome);
            handles.Add(handle!);
        }

        Assert.Equal(LogStreamStartOutcome.TooManySessions, (await dispatcher.StartLogStreamAsync(env, Container, null, "user-1", expires, TestContext.Current.CancellationToken)).Outcome);
        Assert.Equal(LogStreamStartOutcome.Started, (await dispatcher.StartLogStreamAsync(env, Container, null, "user-2", expires, TestContext.Current.CancellationToken)).Outcome);

        dispatcher.OnLogChunk(env, new LogChunk { SessionId = handles[0].SessionId, Lines = { new LogLine { Text = "hello" } } });
        Assert.Equal("hello", (await handles[0].Chunks.ReadAsync(TestContext.Current.CancellationToken)).Lines[0].Text);

        registration.Dispose();
        var ended = await handles[1].Chunks.ReadAsync(TestContext.Current.CancellationToken);
        Assert.True(ended.Ended);
        Assert.Equal("agent_disconnected", ended.EndReason);
    }

    private static ContainerQueryResponse Part(string queryId, uint sequence, bool final, params string[] lines)
    {
        var response = new ContainerQueryResponse { QueryId = queryId, State = QueryResultState.Ok, Sequence = sequence, Final = final };
        response.Lines.AddRange(lines.Select(text => new LogLine { Text = text }));
        return response;
    }
}
