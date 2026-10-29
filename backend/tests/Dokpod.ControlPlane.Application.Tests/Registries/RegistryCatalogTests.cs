using Dokpod.ControlPlane.Application.Alerts;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.ControlPlane.Application.Images;
using Dokpod.ControlPlane.Application.Registries;
using Dokpod.ControlPlane.Application.Tests.Terminal;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Hosts;
using Dokpod.Domain.Images;
using Xunit;

namespace Dokpod.ControlPlane.Application.Tests.Registries;

/// <summary>Spec 020 T006-T007: consolidação por registro, filtros, impacto e política editável que vale no pull.</summary>
public sealed class RegistryCatalogTests
{
    private static readonly Guid Prod = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Hml = Guid.Parse("10000000-0000-0000-0000-000000000002");

    private static HostView Host(Guid id, EnvironmentStage stage) =>
        new(EnvironmentRegistration.Create(id, id == Prod ? "srv-prod" : "srv-hml", "srv", true, EnvironmentResourceScopes.Supported, stage), null, null, true, HostSituation.Unknown, null);

    private static readonly HostView[] Hosts = [Host(Prod, EnvironmentStage.Production), Host(Hml, EnvironmentStage.Staging)];

    private sealed class Store : IRegistryInventoryStore
    {
        public Task<IReadOnlyList<RegistryHostConfigRow>> ListAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RegistryHostConfigRow>>(
            [
                new(Prod, new RegistryHostConfig("docker.io", ["mirror.empresa.com"], null, false, false, true, false)),
                new(Prod, new RegistryHostConfig("mirror.empresa.com", [], "docker.io", false, false, false, false)),
                new(Prod, new RegistryHostConfig("registry.empresa.com", [], null, false, false, false, true)),
                new(Hml, new RegistryHostConfig("registry.local:5000", [], null, true, false, false, false)),
            ]);

        public Task<IReadOnlySet<Guid>> ListConfigAvailableAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid> { Prod });

        public Task<IReadOnlyList<RegistryImageSource>> ListImageSourcesAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RegistryImageSource>>(
            [
                new(Prod, ["nginx:1.27"], [], 100),
                new(Prod, ["registry.empresa.com/loja/api:3"], [], 50),
                new(Hml, ["docker.io/library/redis:7"], [], 30),
                new(Hml, ["quay.io/prometheus/prometheus:v2"], [], 20),
                new(Hml, [], [], 10), // sem tag nem digest: sem registro
            ]);

        public Task<bool> ReplaceAsync(RegistryInventorySnapshot snapshot, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private static ImagePullPolicy Policy() => new() { AllowedRegistriesByStage = new(StringComparer.OrdinalIgnoreCase) { ["production"] = ["registry.empresa.com", "docker.io"] } };

    [Fact]
    public async Task Entries_MergeImageOriginsConfigurationCredentialAndPolicyPerHost()
    {
        var entries = await new RegistryCatalog(new Store(), Policy()).EntriesAsync(Hosts, TestContext.Current.CancellationToken);
        RegistryHostEntry Entry(Guid host, string registry) => entries.Single(e => e.Host.Registration.EnvironmentId == host && e.Registry == registry);

        Assert.Equal(["images", "search"], Entry(Prod, "docker.io").Origins);
        Assert.Equal((1, 100L), (Entry(Prod, "docker.io").Images, Entry(Prod, "docker.io").SizeBytes));
        Assert.Equal(["mirror"], Entry(Prod, "mirror.empresa.com").Origins);
        Assert.False(Entry(Prod, "mirror.empresa.com").Allowed); // espelho fora da lista de Produção
        Assert.Equal(["credential", "images"], Entry(Prod, "registry.empresa.com").Origins.Order());
        Assert.True(Entry(Hml, "registry.local:5000").Insecure);
        Assert.True(Entry(Hml, "quay.io").Allowed); // Homologação sem lista = qualquer registro
        Assert.False(Entry(Hml, "quay.io").ConfigAvailable);
        Assert.Equal(6, entries.Count);
    }

    [Fact]
    public async Task Page_FiltersByOriginInsecureAndPolicyWithSummaryBeforeToggles()
    {
        var entries = await new RegistryCatalog(new Store(), Policy()).EntriesAsync(Hosts, TestContext.Current.CancellationToken);

        var all = RegistryCatalog.Page(entries, new RegistryQuery([], false, false, null, RegistrySort.ImagesDesc, 0, 50));
        var insecure = RegistryCatalog.Page(entries, new RegistryQuery([], true, false, null, RegistrySort.RegistryAsc, 0, 50));
        var outside = RegistryCatalog.Page(entries, new RegistryQuery([], false, true, null, RegistrySort.RegistryAsc, 0, 50));
        var credential = RegistryCatalog.Page(entries, new RegistryQuery([RegistryOrigins.Credential], false, false, null, RegistrySort.RegistryAsc, 0, 50));

        Assert.Equal(5, all.Summary.Total);
        Assert.Equal((1, 1), (all.Summary.Insecure, all.Summary.OutsidePolicy));
        var hub = all.Rows.First();
        Assert.Equal(("docker.io", 2, 2), (hub.Registry, hub.Hosts, hub.Images));
        Assert.Equal(["registry.local:5000"], insecure.Rows.Select(r => r.Registry));
        Assert.Equal(["mirror.empresa.com"], outside.Rows.Select(r => r.Registry));
        Assert.Equal(["registry.empresa.com"], credential.Rows.Select(r => r.Registry));
        Assert.Equal(["quay.io"], RegistryCatalog.Page(entries, new RegistryQuery([], false, false, "quay", RegistrySort.RegistryAsc, 0, 50)).Rows.Select(r => r.Registry));
    }

    [Fact]
    public async Task Impact_CountsImagesAndHostsOfTheStageOutsideTheProposedList()
    {
        var entries = await new RegistryCatalog(new Store(), Policy()).EntriesAsync(Hosts, TestContext.Current.CancellationToken);

        Assert.Equal(new RegistryImpact(1, 1), RegistryCatalog.Impact(entries, EnvironmentStage.Staging, ["quay.io"]));
        Assert.Equal(new RegistryImpact(0, 0), RegistryCatalog.Impact(entries, EnvironmentStage.Staging, []));
        Assert.Equal(new RegistryImpact(1, 1), RegistryCatalog.Impact(entries, EnvironmentStage.Production, ["registry.empresa.com"]));
    }

    [Fact]
    public async Task SavedPolicy_OverridesConfigurationForPullAuditsBothListsAndRequiresManageOnAllStageHosts()
    {
        var policy = Policy();
        var store = new PolicyStore();
        var audit = new TerminalSessionServiceTests.AuditSink();
        var caller = new AlertCaller(AuthenticatedActor.FromSubject("admin"), "token", Guid.NewGuid(), Hosts.Select(h => h.Registration).ToArray());
        var allowed = new RegistryPolicyService(store, policy, new Decider(AuthorizationDecisionOutcome.Allowed), new GrantReader(Hosts.Select(h => h.Registration.EnvironmentId)), audit, TimeProvider.System);
        var denied = new RegistryPolicyService(store, policy, new Decider(AuthorizationDecisionOutcome.Denied), new GrantReader([]), audit, TimeProvider.System);
        Assert.True(ImageReference.TryParse("docker.io/library/alpine:3", out var alpine));
        var ct = TestContext.Current.CancellationToken;

        Assert.True(policy.Allows(EnvironmentStage.Staging, alpine));
        Assert.Equal(RegistryPolicyOutcome.Forbidden, await denied.SaveAsync(caller, EnvironmentStage.Staging, ["quay.io"], ct));
        Assert.Equal(RegistryPolicyOutcome.Invalid, await allowed.SaveAsync(caller, EnvironmentStage.Staging, ["https://quay.io"], ct));
        Assert.Equal(RegistryPolicyOutcome.Invalid, await allowed.SaveAsync(caller, EnvironmentStage.Staging, ["user:pass@quay.io"], ct));
        Assert.Equal(RegistryPolicyOutcome.Saved, await allowed.SaveAsync(caller, EnvironmentStage.Staging, ["Quay.io", "quay.io"], ct));

        Assert.False(policy.Allows(EnvironmentStage.Staging, alpine));
        var saved = Assert.Single(store.Saved);
        Assert.Equal(["quay.io"], saved.Registries);
        var change = Assert.Single(audit.Events);
        Assert.Equal(("registry.policy_changed", Hml), (change.Action, change.EnvironmentId));
        Assert.Equal(("*", "quay.io"), (change.Details!["previousRegistries"], change.Details["registries"]));

        var views = await allowed.ListAsync(caller, ct);
        Assert.True(views.Single(v => v.Stage == EnvironmentStage.Staging).FromConsole);
        Assert.Equal(["registry.empresa.com", "docker.io"], views.Single(v => v.Stage == EnvironmentStage.Production).Registries); // ordem da configuração
        Assert.All(views, v => Assert.True(v.CanEdit));
    }

    private sealed class PolicyStore : IRegistryPolicyStore
    {
        public List<StoredRegistryPolicy> Saved { get; } = [];

        public Task<IReadOnlyList<StoredRegistryPolicy>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<StoredRegistryPolicy>>(Saved.ToArray());

        public Task SaveAsync(StoredRegistryPolicy policy, CancellationToken cancellationToken)
        {
            Saved.RemoveAll(p => p.Stage == policy.Stage);
            Saved.Add(policy);
            return Task.CompletedTask;
        }
    }
}
