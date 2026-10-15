using Dokpod.ControlPlane.Application.Agents;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Commands;
using Dokpod.ControlPlane.Application.Networks;
using Dokpod.ControlPlane.Application.Tests.Alerts;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Networks;
using Xunit;

namespace Dokpod.ControlPlane.Application.Tests.Networks;

/// <summary>Spec 019 T008: remoção de rede sem uso pela fila de comandos, com scope, sistema, escopo, capability e justificativa.</summary>
public sealed class NetworkRemovalServiceTests
{
    private static readonly Guid EnvironmentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly string Orfa = new('1', 64);
    private static readonly string Usada = new('2', 64);
    private static readonly string Bridge = new('3', 64);
    private static readonly string Cluster = new('4', 64);

    [Fact]
    public async Task UnusedNetwork_IsQueuedAsAuditedHostCommandWithNameAndMaskedJustification()
    {
        var (service, store, decider) = Create(EnvironmentStage.Production);

        var result = await Remove(service, Orfa, "Limpeza de redes antigas " + "pass" + "word=segredo123");

        Assert.Equal(NetworkRemovalOutcome.Accepted, result.Outcome);
        Assert.Equal(EnvironmentResourceScopes.DeleteNetwork, decider.Scope);
        var command = store.Command!.Command;
        Assert.Equal(AgentCommandKind.RemoveNetwork, command.Kind);
        var parameters = Assert.IsType<RemoveNetworkParameters>(command.Parameters);
        Assert.Equal((Orfa, "orfa"), (parameters.NetworkId, parameters.Name));
        Assert.Equal("network.remove", store.AuditEvent!.Action);
        Assert.Equal("orfa", store.AuditEvent.SubjectId);
        Assert.Equal(Orfa, store.AuditEvent.Details!["networkId"]);
        Assert.DoesNotContain("segredo123", store.AuditEvent.Details!["justification"]);
    }

    [Fact]
    public async Task InUseSystemClusterUnknownOrInvalid_AreRefusedBeforeQueueing()
    {
        var (service, store, _) = Create(EnvironmentStage.Development);

        Assert.Equal(NetworkRemovalOutcome.InUse, (await Remove(service, Usada, null)).Outcome);
        Assert.Equal(NetworkRemovalOutcome.System, (await Remove(service, Bridge, null)).Outcome);
        Assert.Equal(NetworkRemovalOutcome.NotLocal, (await Remove(service, Cluster, null)).Outcome);
        Assert.Equal(NetworkRemovalOutcome.NotFound, (await Remove(service, new string('9', 64), null)).Outcome);
        Assert.Equal(NetworkRemovalOutcome.InvalidId, (await Remove(service, "orfa", null)).Outcome);
        Assert.Null(store.Command);
    }

    [Fact]
    public async Task Production_RequiresJustificationAndDevelopmentAcceptsWithout()
    {
        var (production, productionStore, _) = Create(EnvironmentStage.Production);
        var (development, _, _) = Create(EnvironmentStage.Development);

        Assert.Equal(NetworkRemovalOutcome.JustificationRequired, (await Remove(production, Orfa, null)).Outcome);
        Assert.Equal(NetworkRemovalOutcome.JustificationInvalid, (await Remove(production, Orfa, "curta")).Outcome);
        Assert.Null(productionStore.Command);
        Assert.Equal(NetworkRemovalOutcome.Accepted, (await Remove(development, Orfa, null)).Outcome);
    }

    [Fact]
    public async Task DeniedScopeOrAgentWithoutCapability_AreRefused()
    {
        var (denied, _, _) = Create(EnvironmentStage.Development, AuthorizationDecisionOutcome.Denied);
        var (old, _, _) = Create(EnvironmentStage.Development, capabilities: ["VolumeRemove"]);

        Assert.Equal(NetworkRemovalOutcome.AuthorizationDenied, (await Remove(denied, Orfa, null)).Outcome);
        Assert.Equal(NetworkRemovalOutcome.NotSupported, (await Remove(old, Orfa, null)).Outcome);
    }

    private static Task<NetworkRemovalResult> Remove(NetworkRemovalService service, string networkId, string? justification) =>
        service.RemoveAsync(EnvironmentId, Guid.NewGuid(), networkId, DateTimeOffset.UtcNow.AddMinutes(2), justification,
            AuthenticatedActor.FromSubject("user-1"), "token", Guid.NewGuid(), TestContext.Current.CancellationToken);

    private static (NetworkRemovalService Service, Store Store, Decider Decider) Create(
        EnvironmentStage stage,
        AuthorizationDecisionOutcome outcome = AuthorizationDecisionOutcome.Allowed,
        string[]? capabilities = null)
    {
        var store = new Store(AgentCommandEnqueueResult.Created);
        var decider = new Decider(outcome);
        var service = new NetworkRemovalService(
            decider,
            new Registrations(stage),
            new Sessions(new AgentSession(EnvironmentId, Guid.NewGuid(), 17)),
            new AgentCommandQueueService(store, new Delivery(), TimeProvider.System),
            new FakeHostStore(capabilities ?? ["NetworkInventory", "NetworkRemove"], EnvironmentId),
            new Networks(),
            new JustifiedCommandTests.Justifications(),
            TimeProvider.System);
        return (service, store, decider);
    }

    private sealed class Networks : INetworkInventoryStore
    {
        private static readonly IReadOnlyDictionary<string, string> NoLabels = new Dictionary<string, string>();

        private static NetworkInventoryItem Item(string id, string name, string scope = "local", bool system = false, params NetworkConnection[] containers) =>
            new(id, name, "bridge", scope, false, false, false, system, null, [], NoLabels, containers, false);

        public Task<NetworkSearchRow?> GetAsync(Guid environmentId, string networkIdOrName, CancellationToken cancellationToken)
        {
            NetworkInventoryItem? item = networkIdOrName switch
            {
                var id when id == Orfa => Item(Orfa, "orfa"),
                var id when id == Usada => Item(Usada, "usada", containers: new NetworkConnection(new string('a', 64), "worker", null, null)),
                var id when id == Bridge => Item(Bridge, "bridge", system: true),
                var id when id == Cluster => Item(Cluster, "cluster", scope: "swarm"),
                _ => null,
            };
            return Task.FromResult(item is null ? null : new NetworkSearchRow(environmentId, item, DateTimeOffset.UtcNow));
        }

        public Task<NetworkSnapshotOutcome> ReplaceSnapshotAsync(NetworkSnapshot snapshot, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlySet<Guid>> ListProjectedAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<NetworkSearchResult> SearchAsync(NetworkSearchQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
