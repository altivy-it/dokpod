using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Auditing;
using Dokpod.ControlPlane.Application.Environments;
using Dokpod.Domain.Auditing;
using Dokpod.Domain.Environments;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests;

public sealed class EnvironmentRegistrationServiceTests
{
    [Fact]
    public async Task ListAsync_OmitsDeniedEnvironments()
    {
        var allowed = CreateRegistration();
        var denied = CreateRegistration();
        var store = new RecordingEnvironmentRegistrationStore
        {
            PageToRead = new EnvironmentRegistrationPage([allowed, denied], null)
        };
        var service = CreateService(
            new ResourceAuthorizationDecider(allowed.EnvironmentId),
            store);

        var result = await service.ListAsync(
            null,
            20,
            AuthenticatedActor.FromSubject("user-1"),
            "access-token",
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        Assert.True(result.Available);
        Assert.Equal([allowed], result.Registrations);
    }

    [Fact]
    public async Task ListAsync_RestrictsScopesToTheGrantsOfTheUserAndKeepsRegistrationWhenGrantsAreUnavailable()
    {
        var reader = CreateRegistration();
        var store = new RecordingEnvironmentRegistrationStore { PageToRead = new EnvironmentRegistrationPage([reader], null) };
        var decider = new ResourceAuthorizationDecider(reader.EnvironmentId);
        var granted = new Dictionary<Guid, IReadOnlySet<string>> { [reader.EnvironmentId] = new HashSet<string> { EnvironmentResourceScopes.Read, "container:exec" } };
        async Task<IReadOnlyList<EnvironmentRegistration>> ListWith(IEnvironmentGrantReader grants, IEnvironmentAuthorizationDecider? perHost = null) =>
            (await new EnvironmentRegistrationService(new EnvironmentAccessService(new RecordingAuditEventWriter(), perHost ?? decider), store, perHost ?? decider, grants)
                .ListAsync(null, 20, AuthenticatedActor.FromSubject("leitor"), "access-token", Guid.NewGuid(), TestContext.Current.CancellationToken))
            .Registrations;

        // Spec 023: só leitura concedida; scope concedido mas fora do cadastro não aparece.
        Assert.Equal([EnvironmentResourceScopes.Read], (await ListWith(new StaticGrantReader(granted))).Single().Scopes);
        // Spec 005: sem environment:read nas permissões lidas, o host não é listado, e nenhuma decisão por host é feita.
        Assert.Empty(await ListWith(new StaticGrantReader(new Dictionary<Guid, IReadOnlySet<string>>()), new ThrowingDecider()));
        Assert.Single(await ListWith(new StaticGrantReader(granted), new ThrowingDecider()));
        // Sem resposta do provedor: volta à decisão por host e mantém o cadastro.
        Assert.Equal(reader.Scopes.Order(), (await ListWith(new StaticGrantReader(null))).Single().Scopes.Order());
    }

    [Fact]
    public void KeycloakGrants_ParsesOnlyDokpodEnvironmentsAndSupportedScopes()
    {
        var id = Guid.NewGuid();
        using var document = System.Text.Json.JsonDocument.Parse($$"""
            [{"rsid":"1","rsname":"urn:dokpod:environment:{{id}}","scopes":["environment:read","container:stop","admin:all"]},
             {"rsid":"2","rsname":"outro-recurso","scopes":["environment:read"]},
             {"rsid":"3","rsname":"urn:dokpod:environment:nao-guid","scopes":["environment:read"]}]
            """);
        var grants = Dokpod.ControlPlane.Api.Authorization.KeycloakEnvironmentGrantReader.Parse(document.RootElement)!;
        Assert.Equal([id], grants.Keys);
        Assert.Equal(["container:stop", "environment:read"], grants[id].Order());
        using var invalid = System.Text.Json.JsonDocument.Parse("{\"result\":true}");
        Assert.Null(Dokpod.ControlPlane.Api.Authorization.KeycloakEnvironmentGrantReader.Parse(invalid.RootElement));
    }

    private sealed class ThrowingDecider : IEnvironmentAuthorizationDecider
    {
        public Task<EnvironmentAuthorizationDecision> DecideAsync(string resource, string scope, AuthenticatedActor actor, string accessToken, Guid correlationId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("decisão por host não deveria ser chamada com as permissões já lidas");
    }

    private sealed class StaticGrantReader(IReadOnlyDictionary<Guid, IReadOnlySet<string>>? grants) : IEnvironmentGrantReader
    {
        public Task<IReadOnlyDictionary<Guid, IReadOnlySet<string>>?> ReadAsync(AuthenticatedActor actor, string accessToken, Guid correlationId, CancellationToken cancellationToken) =>
            Task.FromResult(grants);
    }

    [Fact]
    public async Task ListAsync_WhenAuthorizationIsIndeterminate_ReturnsNoPartialPage()
    {
        var store = new RecordingEnvironmentRegistrationStore
        {
            PageToRead = new EnvironmentRegistrationPage([CreateRegistration()], null)
        };
        var service = CreateService(
            new RecordingAuthorizationDecider(
                new EnvironmentAuthorizationDecision(
                    AuthorizationDecisionOutcome.Indeterminate,
                    "authorization_timeout")),
            store);

        var result = await service.ListAsync(
            null,
            20,
            AuthenticatedActor.FromSubject("user-1"),
            "access-token",
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        Assert.False(result.Available);
        Assert.Empty(result.Registrations);
        Assert.Equal("authorization_timeout", result.FailureCode);
    }

    [Fact]
    public async Task RegisterAsync_WhenAuthorizationIsDenied_DoesNotPersistEnvironment()
    {
        var store = new RecordingEnvironmentRegistrationStore();
        var service = CreateService(
            new RecordingAuthorizationDecider(
                new EnvironmentAuthorizationDecision(AuthorizationDecisionOutcome.Denied, "scope_denied")),
            store);

        var result = await service.RegisterAsync(
            CreateRegistration(),
            AuthenticatedActor.FromSubject("user-1"),
            "access-token",
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        Assert.False(result.Allowed);
        Assert.False(result.Created);
        Assert.Equal("scope_denied", result.FailureCode);
        Assert.Null(store.Registration);
    }

    [Fact]
    public async Task RegisterAsync_WhenAuthorizationIsIndeterminate_PreservesOutcomeAndDoesNotPersistEnvironment()
    {
        var store = new RecordingEnvironmentRegistrationStore();
        var service = CreateService(
            new RecordingAuthorizationDecider(
                new EnvironmentAuthorizationDecision(
                    AuthorizationDecisionOutcome.Indeterminate,
                    "authorization_timeout")),
            store);

        var result = await service.RegisterAsync(
            CreateRegistration(),
            AuthenticatedActor.FromSubject("user-1"),
            "access-token",
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        Assert.False(result.Allowed);
        Assert.False(result.Created);
        Assert.Equal(AuthorizationDecisionOutcome.Indeterminate, result.AuthorizationOutcome);
        Assert.Equal("authorization_timeout", result.FailureCode);
        Assert.Null(store.Registration);
    }

    [Fact]
    public async Task RegisterAsync_WhenAuthorized_PersistsEnvironmentAfterAudit()
    {
        var store = new RecordingEnvironmentRegistrationStore();
        var service = CreateService(
            new RecordingAuthorizationDecider(
                new EnvironmentAuthorizationDecision(AuthorizationDecisionOutcome.Allowed, null)),
            store);

        var registration = CreateRegistration();
        var result = await service.RegisterAsync(
            registration,
            AuthenticatedActor.FromSubject("user-1"),
            "access-token",
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        Assert.True(result.Allowed);
        Assert.True(result.Created);
        Assert.Equal(registration, store.Registration);
    }

    [Fact]
    public async Task GetAsync_WhenAuthorizationIsDenied_DoesNotReadEnvironment()
    {
        var store = new RecordingEnvironmentRegistrationStore();
        var service = CreateService(
            new RecordingAuthorizationDecider(
                new EnvironmentAuthorizationDecision(AuthorizationDecisionOutcome.Denied, "scope_denied")),
            store);

        var result = await service.GetAsync(
            Guid.NewGuid(),
            AuthenticatedActor.FromSubject("user-1"),
            "access-token",
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        Assert.False(result.Allowed);
        Assert.Equal("scope_denied", result.FailureCode);
        Assert.Equal(AuthorizationDecisionOutcome.Denied, result.AuthorizationOutcome);
        Assert.False(store.WasRead);
    }

    [Fact]
    public async Task GetAsync_WhenAuthorized_ReturnsEnvironment()
    {
        var registration = CreateRegistration();
        var store = new RecordingEnvironmentRegistrationStore { RegistrationToRead = registration };
        var service = CreateService(
            new RecordingAuthorizationDecider(
                new EnvironmentAuthorizationDecision(AuthorizationDecisionOutcome.Allowed, null)),
            store);

        var result = await service.GetAsync(
            registration.EnvironmentId,
            AuthenticatedActor.FromSubject("user-1"),
            "access-token",
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        Assert.True(result.Allowed);
        Assert.Equal(registration, result.Registration);
        Assert.True(store.WasRead);
    }

    private static EnvironmentRegistrationService CreateService(
        IEnvironmentAuthorizationDecider authorizationDecider,
        IEnvironmentRegistrationStore store) =>
        new(
            new EnvironmentAccessService(new RecordingAuditEventWriter(), authorizationDecider),
            store,
            authorizationDecider);

    private static EnvironmentRegistration CreateRegistration() =>
        EnvironmentRegistration.Create(
            Guid.NewGuid(),
            "prod-lab",
            "lab",
            enabled: true,
            [EnvironmentResourceScopes.Read, EnvironmentResourceScopes.Manage]);

    private sealed class RecordingAuditEventWriter : IAuditEventWriter
    {
        public Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class RecordingAuthorizationDecider(EnvironmentAuthorizationDecision decision)
        : IEnvironmentAuthorizationDecider
    {
        public Task<EnvironmentAuthorizationDecision> DecideAsync(
            string resource,
            string scope,
            AuthenticatedActor actor,
            string accessToken,
            Guid correlationId,
            CancellationToken cancellationToken) =>
            Task.FromResult(decision);
    }

    private sealed class ResourceAuthorizationDecider(Guid allowedEnvironmentId)
        : IEnvironmentAuthorizationDecider
    {
        public Task<EnvironmentAuthorizationDecision> DecideAsync(
            string resource,
            string scope,
            AuthenticatedActor actor,
            string accessToken,
            Guid correlationId,
            CancellationToken cancellationToken) =>
            Task.FromResult(resource.EndsWith(allowedEnvironmentId.ToString("D"), StringComparison.Ordinal)
                ? new EnvironmentAuthorizationDecision(AuthorizationDecisionOutcome.Allowed, null)
                : new EnvironmentAuthorizationDecision(AuthorizationDecisionOutcome.Denied, "scope_denied"));
    }

    private sealed class RecordingEnvironmentRegistrationStore : IEnvironmentRegistrationStore
    {
        public EnvironmentRegistration? Registration { get; private set; }
        public EnvironmentRegistration? RegistrationToRead { get; init; }
        public EnvironmentRegistrationPage PageToRead { get; init; } = new([], null);
        public bool WasRead { get; private set; }

        public Task<EnvironmentRegistrationPage> ListAsync(
            Guid? afterEnvironmentId,
            int limit,
            CancellationToken cancellationToken) => Task.FromResult(PageToRead);

        public Task<EnvironmentRegistration?> GetAsync(
            Guid environmentId,
            CancellationToken cancellationToken)
        {
            WasRead = true;
            return Task.FromResult(RegistrationToRead);
        }

        public Task<EnvironmentRegistrationStoreResult> CreateAsync(
            EnvironmentRegistration registration,
            CancellationToken cancellationToken)
        {
            Registration = registration;
            return Task.FromResult(EnvironmentRegistrationStoreResult.Created);
        }
    }
}
