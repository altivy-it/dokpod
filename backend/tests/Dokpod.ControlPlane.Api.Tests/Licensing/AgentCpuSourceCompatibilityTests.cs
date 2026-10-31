using Dokpod.Agent.Contracts.V1;
using Google.Protobuf;
using Dokpod.ControlPlane.Api.Agents;
using Dokpod.ControlPlane.Api.Tests.Agents;
using Dokpod.ControlPlane.Application.Agents;
using Dokpod.ControlPlane.Application.Licensing;
using Dokpod.Domain.Events;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests.Licensing;

/// <summary>
/// Spec 031 T031 (FR-013, FR-017): agente N-1 sem <c>cpu_count_source</c> vira medição desconhecida, e a sessão
/// do agente nunca cai por falha ao gravar a observação de capacidade.
/// </summary>
public sealed class AgentCpuSourceCompatibilityTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(CpuCountSource.Engine, CapacitySource.Engine)]
    [InlineData(CpuCountSource.ProcessFallback, CapacitySource.ProcessFallback)]
    [InlineData(CpuCountSource.Unspecified, CapacitySource.Unknown)]
    public async Task HelloSource_IsMappedForTheObserver(CpuCountSource sent, string expected)
    {
        var observer = new RecordingObserver();
        var activity = AgentHostActivityTests.Create(new AgentHostActivityTests.FlakyHostStore(0), new AgentHostActivityTests.RecordingEventStore(), capacityObserver: observer);
        var hello = AgentHostActivityTests.Hello();
        hello.CpuCount = 32;
        hello.CpuCountSource = sent;

        await activity.OnSessionStartedAsync(new AgentSession(Guid.NewGuid(), Guid.NewGuid(), 1), hello, Ct);

        var call = Assert.Single(observer.Calls);
        Assert.Equal((32, expected, "carga-001"), (call.CpuCount, call.Source, call.HostName));
    }

    [Fact]
    public async Task NMinus1Hello_WithoutTheFieldIsUnknown()
    {
        var observer = new RecordingObserver();
        var activity = AgentHostActivityTests.Create(new AgentHostActivityTests.FlakyHostStore(0), new AgentHostActivityTests.RecordingEventStore(), capacityObserver: observer);
        var legacy = AgentHello.Parser.ParseFrom(new AgentHello { Engine = EngineKind.Docker, EngineVersion = "28.0.0", HostName = "n-1", CpuCount = 16 }.ToByteArray());

        await activity.OnSessionStartedAsync(new AgentSession(Guid.NewGuid(), Guid.NewGuid(), 1), legacy, Ct);

        Assert.Equal(CapacitySource.Unknown, Assert.Single(observer.Calls).Source);
    }

    [Fact]
    public async Task ObserverFailure_NeverBreaksTheSessionStart()
    {
        var store = new AgentHostActivityTests.FlakyHostStore(0);
        var events = new AgentHostActivityTests.RecordingEventStore();
        var activity = AgentHostActivityTests.Create(store, events, capacityObserver: new FailingObserver());
        var environment = Guid.NewGuid();

        await activity.OnSessionStartedAsync(new AgentSession(environment, Guid.NewGuid(), 1), AgentHostActivityTests.Hello(), Ct);

        Assert.True(store.Connected);
        Assert.Single(events.Events, item => item.Type == OperationalEventTypes.AgentConnected);
    }

    [Fact]
    public async Task CommunityComposition_HasNoObserverAndStillStarts()
    {
        var store = new AgentHostActivityTests.FlakyHostStore(0);
        var activity = AgentHostActivityTests.Create(store, new AgentHostActivityTests.RecordingEventStore());

        await activity.OnSessionStartedAsync(new AgentSession(Guid.NewGuid(), Guid.NewGuid(), 1), AgentHostActivityTests.Hello(), Ct);

        Assert.True(store.Connected);
    }

    private sealed class RecordingObserver : IAgentCapacityObserver
    {
        public List<(Guid EnvironmentId, string? HostName, int? CpuCount, string Source)> Calls { get; } = [];

        public Task OnAgentHelloAsync(Guid environmentId, string? hostName, int? cpuCount, string source, DateTimeOffset observedAtUtc, CancellationToken cancellationToken)
        {
            Calls.Add((environmentId, hostName, cpuCount, source));
            return Task.CompletedTask;
        }
    }

    private sealed class FailingObserver : IAgentCapacityObserver
    {
        public Task OnAgentHelloAsync(Guid environmentId, string? hostName, int? cpuCount, string source, DateTimeOffset observedAtUtc, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("licensing database unavailable");
    }
}
