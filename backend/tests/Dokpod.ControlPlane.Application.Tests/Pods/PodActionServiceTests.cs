using Dokpod.ControlPlane.Application.Agents;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Commands;
using Dokpod.ControlPlane.Application.Pods;
using Dokpod.ControlPlane.Application.Tests.Alerts;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Pods;
using Xunit;

namespace Dokpod.ControlPlane.Application.Tests.Pods;

/// <summary>Spec 021 T008: ações de pod pela fila de comandos, com o scope de container da ação e o pod do agente protegido.</summary>
public sealed class PodActionServiceTests
{
    private static readonly Guid EnvironmentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly string Loja = new('1', 64);
    private static readonly string AgentPod = new('2', 64);

    [Theory]
    [InlineData(PodActionKind.Start, "container:start")]
    [InlineData(PodActionKind.Stop, "container:stop")]
    [InlineData(PodActionKind.Restart, "container:restart")]
    [InlineData(PodActionKind.Unpause, "container:pause")]
    public async Task Action_UsesTheContainerScopeAndIsAuditedPerAction(PodActionKind action, string scope)
    {
        var (service, store, decider) = Create(EnvironmentStage.Development);

        var result = await Act(service, Loja, action, "Janela de manutenção do pod");

        Assert.Equal(PodActionOutcome.Accepted, result.Outcome);
        Assert.Equal(scope, decider.Scope);
        var parameters = Assert.IsType<PodActionParameters>(store.Command!.Command.Parameters);
        Assert.Equal((Loja, "pod-loja", action), (parameters.PodId, parameters.Name, parameters.Action));
        Assert.Equal(PodNames.AuditAction(action), store.AuditEvent!.Action);
        Assert.Equal("pod-loja", store.AuditEvent.SubjectId);
        Assert.Equal(PodNames.RequiresJustification(action), store.AuditEvent.Details!.ContainsKey("justification"));
    }

    [Fact]
    public async Task AgentPod_RefusesStopPauseAndRestartButAcceptsStart()
    {
        var (service, store, _) = Create(EnvironmentStage.Development);

        foreach (var action in new[] { PodActionKind.Stop, PodActionKind.Pause, PodActionKind.Restart })
        {
            Assert.Equal(PodActionOutcome.AgentPod, (await Act(service, AgentPod, action, "Justificativa longa o bastante")).Outcome);
        }

        Assert.Null(store.Command);
        Assert.Equal(PodActionOutcome.Accepted, (await Act(service, AgentPod, PodActionKind.Start, null)).Outcome);
    }

    [Fact]
    public async Task Production_RequiresJustificationOnlyForStopAndPause()
    {
        var (service, _, _) = Create(EnvironmentStage.Production);

        Assert.Equal(PodActionOutcome.JustificationRequired, (await Act(service, Loja, PodActionKind.Stop, null)).Outcome);
        Assert.Equal(PodActionOutcome.JustificationRequired, (await Act(service, Loja, PodActionKind.Pause, null)).Outcome);
        Assert.Equal(PodActionOutcome.Accepted, (await Act(service, Loja, PodActionKind.Restart, null)).Outcome);
    }

    [Fact]
    public async Task UnknownDeniedOrUnsupported_AreRefused()
    {
        var (service, _, _) = Create(EnvironmentStage.Development);
        var (denied, _, _) = Create(EnvironmentStage.Development, AuthorizationDecisionOutcome.Denied);
        var (old, _, _) = Create(EnvironmentStage.Development, capabilities: ["PodInventory"]);

        Assert.Equal(PodActionOutcome.NotFound, (await Act(service, new string('9', 64), PodActionKind.Start, null)).Outcome);
        Assert.Equal(PodActionOutcome.InvalidId, (await Act(service, "pod-loja", PodActionKind.Start, null)).Outcome);
        Assert.Equal(PodActionOutcome.AuthorizationDenied, (await Act(denied, Loja, PodActionKind.Start, null)).Outcome);
        Assert.Equal(PodActionOutcome.NotSupported, (await Act(old, Loja, PodActionKind.Start, null)).Outcome);
    }

    private static Task<PodActionResult> Act(PodActionService service, string podId, PodActionKind action, string? justification) =>
        service.ExecuteAsync(EnvironmentId, Guid.NewGuid(), podId, action, DateTimeOffset.UtcNow.AddMinutes(2), justification,
            AuthenticatedActor.FromSubject("user-1"), "token", Guid.NewGuid(), TestContext.Current.CancellationToken);

    private static (PodActionService Service, Store Store, Decider Decider) Create(
        EnvironmentStage stage,
        AuthorizationDecisionOutcome outcome = AuthorizationDecisionOutcome.Allowed,
        string[]? capabilities = null)
    {
        var store = new Store(AgentCommandEnqueueResult.Created);
        var decider = new Decider(outcome);
        var service = new PodActionService(
            decider,
            new Registrations(stage),
            new Sessions(new AgentSession(EnvironmentId, Guid.NewGuid(), 17)),
            new AgentCommandQueueService(store, new Delivery(), TimeProvider.System),
            new FakeHostStore(capabilities ?? ["PodInventory", "PodActions"], EnvironmentId, "podman"),
            new Pods(),
            new JustifiedCommandTests.Justifications(),
            TimeProvider.System);
        return (service, store, decider);
    }

    private sealed class Pods : IPodInventoryStore
    {
        private static PodInventoryItem Item(string id, string name, bool agent) =>
            new(id, name, "Running", null, new Dictionary<string, string>(), [], null, [new PodMemberItem(new string('a', 64), "web", "running", 0, false)], agent, false);

        public Task<PodSearchRow?> GetAsync(Guid environmentId, string podIdOrName, CancellationToken cancellationToken)
        {
            PodInventoryItem? item = podIdOrName == Loja ? Item(Loja, "pod-loja", false) : podIdOrName == AgentPod ? Item(AgentPod, "pod_dokpod", true) : null;
            return Task.FromResult(item is null ? null : new PodSearchRow(environmentId, item, DateTimeOffset.UtcNow));
        }

        public Task<bool> ReplaceSnapshotAsync(PodSnapshot snapshot, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<PodProjectionState>> ListProjectionsAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PodSearchResult> SearchAsync(PodSearchQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
