using Dokpod.ControlPlane.Application.Auditing;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Inventory;
using Dokpod.ControlPlane.Application.Queries;
using Dokpod.Domain.Auditing;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Inventory;
using Xunit;

namespace Dokpod.ControlPlane.Application.Tests.Queries;

/// <summary>Testes negativos da 011 (T021): IDOR indistinguível, scope ausente e falha fechada.</summary>
public sealed class ContainerInsightsAuthorizationTests
{
    private static readonly Guid Env = Guid.Parse("00000000-0000-4000-8000-000000000011");
    private static readonly AuthenticatedActor Actor = AuthenticatedActor.FromSubject("u-1");

    private static ContainerInsightsService Service(Func<string, AuthorizationDecisionOutcome> decide, bool exists = true, Store? store = null) =>
        new(new Decider(decide), store ?? new Store(exists), new NoAudit(), new AgentQueryDispatcher(TimeProvider.System), TimeProvider.System);

    [Fact]
    public async Task UnauthorizedEnvironment_LooksExactlyLikeMissingContainer()
    {
        var store = new Store(exists: true);
        var denied = await Service(_ => AuthorizationDecisionOutcome.Denied, store: store)
            .AuthorizeAsync(Env, "abc", EnvironmentResourceScopes.ReadContainerLogs, Actor, "t", Guid.NewGuid(), TestContext.Current.CancellationToken);
        var missing = await Service(_ => AuthorizationDecisionOutcome.Allowed, exists: false)
            .AuthorizeAsync(Env, "abc", EnvironmentResourceScopes.ReadContainerLogs, Actor, "t", Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Equal(missing, denied);
        Assert.Equal(InsightAccess.NotFound, denied.Access);
        Assert.Equal(0, store.Lookups); // nem consulta o inventário sem leitura no ambiente
    }

    [Theory]
    [InlineData("container:inspect")]
    [InlineData("container:logs")]
    public async Task MissingSpecificScope_IsDeniedAfterReadIsAllowed(string scope)
    {
        var result = await Service(s => s == EnvironmentResourceScopes.Read ? AuthorizationDecisionOutcome.Allowed : AuthorizationDecisionOutcome.Denied)
            .AuthorizeAsync(Env, "abc", scope, Actor, "t", Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Equal(InsightAccess.Denied, result.Access);
        Assert.Null(result.Container);
    }

    [Fact]
    public async Task IndeterminateDecision_FailsClosedAsUnavailable()
    {
        var result = await Service(_ => AuthorizationDecisionOutcome.Indeterminate)
            .AuthorizeAsync(Env, "abc", EnvironmentResourceScopes.InspectContainer, Actor, "t", Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Equal(InsightAccess.Unavailable, result.Access);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task InvalidContainerId_IsNotFoundWithoutAskingKeycloak(string containerId)
    {
        var result = await Service(_ => throw new InvalidOperationException("não deve decidir"))
            .AuthorizeAsync(Env, containerId, EnvironmentResourceScopes.Read, Actor, "t", Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Equal(InsightAccess.NotFound, result.Access);
    }

    [Fact]
    public async Task OversizedContainerId_IsNotFound()
    {
        var result = await Service(_ => AuthorizationDecisionOutcome.Allowed)
            .AuthorizeAsync(Env, new string('a', 129), EnvironmentResourceScopes.Read, Actor, "t", Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Equal(InsightAccess.NotFound, result.Access);
    }

    private sealed class Decider(Func<string, AuthorizationDecisionOutcome> decide) : IEnvironmentAuthorizationDecider
    {
        public Task<EnvironmentAuthorizationDecision> DecideAsync(string resource, string scope, AuthenticatedActor actor, string accessToken, Guid correlationId, CancellationToken cancellationToken)
        {
            var outcome = decide(scope);
            return Task.FromResult(new EnvironmentAuthorizationDecision(outcome, outcome == AuthorizationDecisionOutcome.Allowed ? null : "authorization_denied"));
        }
    }

    private sealed class Store(bool exists) : IContainerSearchStore
    {
        public int Lookups { get; private set; }

        public Task<ContainerInventory?> GetAsync(Guid environmentId, string containerId, CancellationToken cancellationToken)
        {
            Lookups++;
            return Task.FromResult(exists ? new ContainerInventory(containerId, "api", "img", "Running", "r", DateTimeOffset.UtcNow) : null);
        }

        public Task<ContainerSearchResult> SearchAsync(ContainerSearchQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class NoAudit : IAuditEventWriter
    {
        public Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
