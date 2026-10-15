using Dokpod.ControlPlane.Application.Agents;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Commands;
using Dokpod.ControlPlane.Application.Tests.Alerts;
using Dokpod.ControlPlane.Application.Volumes;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Volumes;
using Xunit;

namespace Dokpod.ControlPlane.Application.Tests.Volumes;

/// <summary>Spec 018 T008: remoção de volume sem uso pela fila de comandos, com scope, capability e justificativa.</summary>
public sealed class VolumeRemovalServiceTests
{
    private static readonly Guid EnvironmentId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task UnusedVolume_IsQueuedAsAuditedHostCommandWithMaskedJustification()
    {
        var (service, store, decider) = Create(EnvironmentStage.Production);

        var result = await Remove(service, "orfao", "Limpeza do lote antigo " + "pass" + "word=segredo123");

        Assert.Equal(VolumeRemovalOutcome.Accepted, result.Outcome);
        Assert.Equal(EnvironmentResourceScopes.DeleteVolume, decider.Scope);
        var command = store.Command!.Command;
        Assert.Equal(AgentCommandKind.RemoveVolume, command.Kind);
        Assert.Equal("orfao", Assert.IsType<RemoveVolumeParameters>(command.Parameters).Name);
        Assert.Equal(string.Empty, command.ContainerId);
        Assert.Equal("volume.remove", store.AuditEvent!.Action);
        Assert.Equal("orfao", store.AuditEvent.SubjectId);
        Assert.DoesNotContain("segredo123", store.AuditEvent.Details!["justification"]);
    }

    [Fact]
    public async Task VolumeInUseOrUnknown_IsRefusedBeforeQueueing()
    {
        var (service, store, _) = Create(EnvironmentStage.Development);

        Assert.Equal(VolumeRemovalOutcome.InUse, (await Remove(service, "dados", null)).Outcome);
        Assert.Equal(VolumeRemovalOutcome.NotFound, (await Remove(service, "inexistente", null)).Outcome);
        Assert.Equal(VolumeRemovalOutcome.InvalidName, (await Remove(service, "../etc", null)).Outcome);
        Assert.Null(store.Command);
    }

    [Fact]
    public async Task Production_RequiresJustificationAndDevelopmentAcceptsWithout()
    {
        var (production, productionStore, _) = Create(EnvironmentStage.Production);
        var (development, _, _) = Create(EnvironmentStage.Development);

        Assert.Equal(VolumeRemovalOutcome.JustificationRequired, (await Remove(production, "orfao", null)).Outcome);
        Assert.Equal(VolumeRemovalOutcome.JustificationInvalid, (await Remove(production, "orfao", "curta")).Outcome);
        Assert.Null(productionStore.Command);
        Assert.Equal(VolumeRemovalOutcome.Accepted, (await Remove(development, "orfao", null)).Outcome);
    }

    [Fact]
    public async Task DeniedScopeOrAgentWithoutCapability_AreRefused()
    {
        var (denied, _, _) = Create(EnvironmentStage.Development, AuthorizationDecisionOutcome.Denied);
        var (old, _, _) = Create(EnvironmentStage.Development, capabilities: ["ImagePull"]);

        Assert.Equal(VolumeRemovalOutcome.AuthorizationDenied, (await Remove(denied, "orfao", null)).Outcome);
        Assert.Equal(VolumeRemovalOutcome.NotSupported, (await Remove(old, "orfao", null)).Outcome);
    }

    private static Task<VolumeRemovalResult> Remove(VolumeRemovalService service, string name, string? justification) =>
        service.RemoveAsync(EnvironmentId, Guid.NewGuid(), name, DateTimeOffset.UtcNow.AddMinutes(2), justification,
            AuthenticatedActor.FromSubject("user-1"), "token", Guid.NewGuid(), TestContext.Current.CancellationToken);

    private static (VolumeRemovalService Service, Store Store, Decider Decider) Create(
        EnvironmentStage stage,
        AuthorizationDecisionOutcome outcome = AuthorizationDecisionOutcome.Allowed,
        string[]? capabilities = null)
    {
        var store = new Store(AgentCommandEnqueueResult.Created);
        var decider = new Decider(outcome);
        var service = new VolumeRemovalService(
            decider,
            new Registrations(stage),
            new Sessions(new AgentSession(EnvironmentId, Guid.NewGuid(), 17)),
            new AgentCommandQueueService(store, new Delivery(), TimeProvider.System),
            new FakeHostStore(capabilities ?? ["VolumeInventory", "VolumeRemove"], EnvironmentId),
            new Volumes(),
            new JustifiedCommandTests.Justifications(),
            TimeProvider.System);
        return (service, store, decider);
    }

    private sealed class Volumes : IVolumeInventoryStore
    {
        private static readonly IReadOnlyDictionary<string, string> NoLabels = new Dictionary<string, string>();

        public Task<VolumeSearchRow?> GetAsync(Guid environmentId, string name, CancellationToken cancellationToken) =>
            Task.FromResult(name switch
            {
                "orfao" => new VolumeSearchRow(environmentId, new VolumeInventoryItem("orfao", "local", "local", null, NoLabels, [], false), DateTimeOffset.UtcNow),
                "dados" => new VolumeSearchRow(environmentId, new VolumeInventoryItem("dados", "local", "local", null, NoLabels, [new VolumeMount(new string('a', 64), "api", "/dados", false)], false), DateTimeOffset.UtcNow),
                _ => null,
            });

        public Task<VolumeSnapshotOutcome> ReplaceSnapshotAsync(VolumeSnapshot snapshot, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ulong?> GetRevisionAsync(Guid environmentId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlySet<Guid>> ListProjectedAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<VolumeSearchResult> SearchAsync(VolumeSearchQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
