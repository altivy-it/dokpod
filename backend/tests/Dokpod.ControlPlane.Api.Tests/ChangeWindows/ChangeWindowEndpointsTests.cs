using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dokpod.ControlPlane.Api.ChangeWindows;
using Dokpod.ControlPlane.Api.Tests.Licensing;
using Dokpod.ControlPlane.Application.Alerts;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.ChangeWindows;
using Dokpod.ControlPlane.Application.Environments;
using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.ControlPlane.Application.Licensing;
using Dokpod.ControlPlane.Application.Realtime;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.ControlPlane.Infrastructure.ChangeWindows;
using Dokpod.ControlPlane.Infrastructure.Licensing;
using Dokpod.Domain.Environments;
using Microsoft.AspNetCore.Http;
using Npgsql;
using Xunit;
using static Dokpod.ControlPlane.Api.Tests.Licensing.LicensingPostgres;

namespace Dokpod.ControlPlane.Api.Tests.ChangeWindows;

/// <summary>
/// Spec 026 T016: rotas de configuração das janelas na composição Business, com PostgreSQL real (store, auditoria e
/// licença sintética da 031) e o Keycloak simulado por host. O cadastro de hosts é em memória, para a regra "todos os
/// hosts legíveis do estágio" não depender de ambientes deixados por outros testes no banco compartilhado.
/// </summary>
[Trait("Category", "Integration")]
[Collection(LicensingPostgresCollection.Name)]
public sealed class ChangeWindowEndpointsTests
{
    private const string Capability = "\"change-windows\"";
    private const string Window = """{"timeZone":"America/Sao_Paulo","ranges":[{"days":["mon","tue","wed","thu","fri"],"start":"22:00","end":"06:00"}]}""";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Put_CreatesWithIfNoneMatch_AndEditsOrDeletesOnlyTheCurrentVersion()
    {
        await using var fixture = await Fixture.CreateAsync();

        var created = await fixture.PutAsync("production", Window, ifNoneMatch: true);
        Assert.Equal((200, "\"1\""), (created.Status, created.ETag));
        Assert.Equal("America/Sao_Paulo", created.Body!["timeZone"]!.ToString());
        Assert.Equal(["mon", "tue", "wed", "thu", "fri"], created.Body["ranges"]![0]!["days"]!.AsArray().Select(day => day!.ToString()));
        Assert.True(created.Body["editable"]!.GetValue<bool>());
        Assert.Equal("Maria", created.Body["updatedBy"]!.ToString());

        Assert.Equal((412, "precondition_failed"), Code(await fixture.PutAsync("production", Window, ifNoneMatch: true)));
        Assert.Equal((412, "precondition_failed"), Code(await fixture.PutAsync("production", Window, ifMatch: "\"9\"")));
        Assert.Equal((400, "if_match_required"), Code(await fixture.PutAsync("production", Window)));
        var edited = await fixture.PutAsync("production", Window.Replace("22:00", "21:00", StringComparison.Ordinal), ifMatch: "\"1\"");
        Assert.Equal((200, "\"2\""), (edited.Status, edited.ETag));

        Assert.Equal((400, "if_match_required"), Code(await fixture.DeleteAsync("production", null)));
        Assert.Equal((412, "precondition_failed"), Code(await fixture.DeleteAsync("production", "\"1\"")));
        Assert.Equal(204, (await fixture.DeleteAsync("production", "\"2\"")).Status);
        Assert.Equal((404, "not_found"), Code(await fixture.PutAsync("qa", Window, ifNoneMatch: true)));
    }

    [Theory]
    [InlineData("""{"timeZone":"America/Sao_Paulo","ranges":[{"days":["mon"],"start":"22:00","end":"06:00"}],"extra":1}""", "extra")]
    [InlineData("""{"timeZone":"Mars/Olympus","ranges":[{"days":["mon"],"start":"22:00","end":"06:00"}]}""", "timeZone")]
    [InlineData("""{"timeZone":"America/Sao_Paulo","ranges":[{"days":["mon"],"start":"22:00","end":"22:00"}]}""", "ranges[0].end")]
    [InlineData("""{"timeZone":"America/Sao_Paulo","ranges":[{"days":["mon","mon"],"start":"22:00","end":"06:00"}]}""", "ranges[0].days")]
    [InlineData("""{"timeZone":"America/Sao_Paulo","ranges":[{"days":["mon"],"start":"24:00","end":"06:00"}]}""", "ranges[0].start")]
    [InlineData("""{"timeZone":"America/Sao_Paulo","ranges":[]}""", "ranges")]
    [InlineData("FIFTEEN", "ranges")]
    public async Task Put_WithAnInvalidWindow_Is400WithTheField(string body, string field)
    {
        await using var fixture = await Fixture.CreateAsync();
        if (body == "FIFTEEN")
        {
            body = """{"timeZone":"Etc/UTC","ranges":[""" + string.Join(',', Enumerable.Repeat("""{"days":["mon"],"start":"01:00","end":"02:00"}""", 15)) + "]}";
        }

        var result = await fixture.PutAsync("staging", body, ifNoneMatch: true);

        Assert.Equal((400, ChangeWindowEndpoints.InvalidCode), Code(result));
        Assert.Contains(field, result.Fields);
        Assert.Null(await fixture.Store.GetAsync(EnvironmentStage.Staging, Ct));
    }

    [Fact]
    public async Task WithoutManageOnAReadableHostOfTheStage_PutAndDeleteAre403_AndListShowsReadOnly()
    {
        await using var fixture = await Fixture.CreateAsync();
        Assert.Equal(200, (await fixture.PutAsync("production", Window, ifNoneMatch: true)).Status);
        fixture.DenyManage(fixture.ProductionB);

        Assert.Equal((403, "authorization_denied"), Code(await fixture.PutAsync("production", Window, ifMatch: "\"1\"")));
        Assert.Equal((403, "authorization_denied"), Code(await fixture.DeleteAsync("production", "\"1\"")));
        var list = await fixture.ListAsync();
        Assert.Equal(200, list.Status);
        var stages = list.Body!["stages"]!.AsArray();
        Assert.Equal(["production", "staging", "development"], stages.Select(stage => stage!["stage"]!.ToString()));
        Assert.False(stages[0]!["editable"]!.GetValue<bool>());
        Assert.True(stages[1]!["editable"]!.GetValue<bool>());
        Assert.Equal(1, stages[0]!["version"]!.GetValue<int>());
        Assert.Contains(stages[0]!["status"]!["state"]!.ToString(), new[] { "open", "closed" });
        Assert.Equal("none", stages[1]!["status"]!["state"]!.ToString());
        Assert.Empty(stages[1]!["ranges"]!.AsArray());

        fixture.DenyManage(fixture.ProductionA, fixture.Staging);
        Assert.Equal((403, "authorization_denied"), Code(await fixture.ListAsync()));
    }

    [Fact]
    public async Task RestrictedLicense_BlocksPut_ButNotDeleteOrList()
    {
        await using var fixture = await Fixture.CreateAsync();
        Assert.Equal(200, (await fixture.PutAsync("production", Window, ifNoneMatch: true)).Status);

        await fixture.ImportLicenseAsync(expired: true);

        Assert.Equal((403, "license_expired"), Code(await fixture.PutAsync("production", Window, ifMatch: "\"1\"")));
        Assert.Equal((403, "license_expired"), Code(await fixture.PutAsync("staging", Window, ifNoneMatch: true)));
        Assert.Equal(200, (await fixture.ListAsync()).Status);
        Assert.Equal(204, (await fixture.DeleteAsync("production", "\"1\"")).Status);
    }

    [Fact]
    public async Task LicenseWithoutTheCapability_IsLicenseInvalid()
    {
        await using var fixture = await Fixture.CreateAsync(capabilities: "\"audit-siem-export\"");

        Assert.Equal((403, "license_invalid"), Code(await fixture.PutAsync("production", Window, ifNoneMatch: true)));
    }

    [Fact]
    public async Task PutAndDelete_AuditEveryHostOfTheStage_WithPreviousAndNewValues()
    {
        await using var fixture = await Fixture.CreateAsync();

        Assert.Equal(200, (await fixture.PutAsync("production", Window, ifNoneMatch: true)).Status);
        Assert.Equal(200, (await fixture.PutAsync("production", """{"timeZone":"Etc/UTC","ranges":[{"days":["sat"],"start":"09:30","end":"11:00"}]}""", ifMatch: "\"1\"")).Status);
        Assert.Equal(204, (await fixture.DeleteAsync("production", "\"2\"")).Status);

        foreach (var host in new[] { fixture.ProductionA, fixture.ProductionB })
        {
            var events = await fixture.AuditAsync(host);
            Assert.Equal(["change_window.updated", "change_window.updated", "change_window.removed"], events.Select(item => item.Action));
            Assert.All(events, item => Assert.Equal("production", item.Subject));
            Assert.Equal("mon,tue,wed,thu,fri 22:00-06:00", events[0].Details["ranges"]);
            Assert.False(events[0].Details.ContainsKey("previousRanges"));
            Assert.Equal(("Etc/UTC", "America/Sao_Paulo"), (events[1].Details["timeZone"], events[1].Details["previousTimeZone"]));
            Assert.Equal("sat 09:30-11:00", events[2].Details["previousRanges"]);
        }

        Assert.Empty(await fixture.AuditAsync(fixture.Staging));
    }

    private static (int Status, string? Code) Code(RouteResult result) => (result.Status, result.Code);

    private sealed record RouteResult(int Status, string? Code, JsonNode? Body, string? ETag, IReadOnlyList<string> Fields);

    private sealed record AuditRow(string Action, string Subject, IReadOnlyDictionary<string, string> Details);

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly LicensingUs4Fixture licensing;
        private readonly HashSet<Guid> deniedManage = [];
        private readonly string subject = $"user-026-{Guid.NewGuid():N}";
        private LicensingUs4Fixture.Services services;

        private Fixture(LicensingUs4Fixture licensing)
        {
            this.licensing = licensing;
            services = licensing.Create();
        }

        public Guid ProductionA { get; } = Guid.NewGuid();
        public Guid ProductionB { get; } = Guid.NewGuid();
        public Guid Staging { get; } = Guid.NewGuid();

        public ChangeWindowStore Store => new(services.Context);

        public static async Task<Fixture> CreateAsync(string capabilities = Capability)
        {
            var fixture = new Fixture(new LicensingUs4Fixture(ConnectionStringOrSkip()));
            await fixture.licensing.ResetClockAsync();
            await fixture.ImportLicenseAsync(capabilities: capabilities);
            await fixture.ClearAsync();
            return fixture;
        }

        public async Task ImportLicenseAsync(string capabilities = Capability, bool expired = false)
        {
            var now = DateTimeOffset.UtcNow;
            var license = expired
                ? licensing.License($"lic_{Guid.NewGuid():N}", now.AddDays(-400), now.AddDays(-31), capabilities: capabilities)
                : licensing.License($"lic_{Guid.NewGuid():N}", now.AddDays(-1), now.AddDays(365), capabilities: capabilities);
            Assert.Equal(LicenseImportOutcome.Accepted, (await licensing.ImportAsync(license)).Outcome);
            await services.DisposeAsync();
            services = licensing.Create();
        }

        public void DenyManage(params Guid[] hosts) => deniedManage.UnionWith(hosts);

        public Task<RouteResult> ListAsync() =>
            RunAsync(Context(null, null, false), (context, authorized, service) => ChangeWindowEndpoints.ListAsync(context, authorized, service, Ct));

        public Task<RouteResult> PutAsync(string stage, string body, string? ifMatch = null, bool ifNoneMatch = false) =>
            RunAsync(Context(body, ifMatch, ifNoneMatch), (context, authorized, service) => ChangeWindowEndpoints.SaveAsync(stage, context, authorized, service, Ct));

        public Task<RouteResult> DeleteAsync(string stage, string? ifMatch) =>
            RunAsync(Context(null, ifMatch, false), (context, authorized, service) => ChangeWindowEndpoints.DeleteAsync(stage, context, authorized, service, Ct));

        public async Task<IReadOnlyList<AuditRow>> AuditAsync(Guid host)
        {
            await using var connection = await OpenAsync(licensing.ConnectionString, Ct);
            await using var command = new NpgsqlCommand(
                "SELECT action, subject_id, details::text FROM dokpod.audit_events WHERE environment_id = @id AND action LIKE 'change_window.%' ORDER BY occurred_at_utc, action DESC",
                connection);
            command.Parameters.AddWithValue("id", host);
            var rows = new List<AuditRow>();
            await using var reader = await command.ExecuteReaderAsync(Ct);
            while (await reader.ReadAsync(Ct))
            {
                rows.Add(new AuditRow(reader.GetString(0), reader.GetString(1), JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(2))!));
            }

            return rows;
        }

        private async Task<RouteResult> RunAsync(DefaultHttpContext context, Func<HttpContext, AuthorizedEnvironmentsService, ChangeWindowSettingsService, Task<IResult>> route)
        {
            var decider = new HostDecider(deniedManage);
            var registrations = new Registrations([
                EnvironmentRegistration.Create(ProductionA, "prod-a", "srv-a", true, [EnvironmentResourceScopes.Read, EnvironmentResourceScopes.Manage], EnvironmentStage.Production),
                EnvironmentRegistration.Create(ProductionB, "prod-b", "srv-b", true, [EnvironmentResourceScopes.Read, EnvironmentResourceScopes.Manage], EnvironmentStage.Production),
                EnvironmentRegistration.Create(Staging, "stg-a", "srv-c", true, [EnvironmentResourceScopes.Read, EnvironmentResourceScopes.Manage], EnvironmentStage.Staging),
            ]);
            var grants = new Grants(registrations.All, deniedManage);
            var database = services.Context;
            var audit = new PostgresAuditEventWriter(database);
            var authorized = new AuthorizedEnvironmentsService(new EnvironmentRegistrationService(new EnvironmentAccessService(audit, decider), registrations, decider, grants));
            var governance = new AlertGovernanceService(new PostgresAlertStore(database), new PostgresActionJustificationStore(database), decider, grants, audit, new NullControlPlaneNotifier(), TimeProvider.System);
            var store = new ChangeWindowStore(database);
            var service = new ChangeWindowSettingsService(store, store, governance, new BusinessCapabilityGate(services.Status), audit, TimeProvider.System);
            var result = await route(context, authorized, service);
            return Read(context, result);
        }

        private DefaultHttpContext Context(string? body, string? ifMatch, bool ifNoneMatch)
        {
            var context = new DefaultHttpContext();
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", subject), new Claim("preferred_username", "Maria")], "test"));
            context.Request.Headers.Authorization = "Bearer test-token";
            context.Request.Path = ChangeWindowEndpoints.Route;
            if (ifMatch is not null) context.Request.Headers.IfMatch = ifMatch;
            if (ifNoneMatch) context.Request.Headers.IfNoneMatch = "*";
            var bytes = Encoding.UTF8.GetBytes(body ?? string.Empty);
            context.Request.Body = new MemoryStream(bytes);
            context.Request.ContentLength = bytes.Length;
            return context;
        }

        private static RouteResult Read(HttpContext context, IResult result) => result switch
        {
            Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult problem => new(
                problem.StatusCode,
                problem.ProblemDetails.Extensions["code"]?.ToString(),
                null,
                null,
                problem.ProblemDetails.Extensions.TryGetValue("errors", out var errors) && errors is not null
                    ? JsonSerializer.SerializeToNode(errors)!.AsArray().Select(error => error!["field"]!.ToString()).ToArray()
                    : []),
            IValueHttpResult value and IStatusCodeHttpResult status => new(
                status.StatusCode ?? 200, null, JsonSerializer.SerializeToNode(value.Value), context.Response.Headers.ETag.ToString() is { Length: > 0 } etag ? etag : null, []),
            IStatusCodeHttpResult status => new(status.StatusCode ?? 0, null, null, null, []),
            _ => throw new InvalidOperationException(result.GetType().Name),
        };

        private async Task ClearAsync()
        {
            await using var connection = await OpenAsync(licensing.ConnectionString, Ct);
            await ExecuteAsync(connection, null, "DELETE FROM dokpod.change_windows", Ct);
        }

        public async ValueTask DisposeAsync()
        {
            await ClearAsync();
            await services.DisposeAsync();
            licensing.Dispose();
        }
    }

    /// <summary>Keycloak por host: tudo permitido, menos `environment:manage` nos hosts negados.</summary>
    private sealed class HostDecider(IReadOnlySet<Guid> deniedManage) : IEnvironmentAuthorizationDecider
    {
        public Task<EnvironmentAuthorizationDecision> DecideAsync(string resource, string scope, AuthenticatedActor actor, string accessToken, Guid correlationId, CancellationToken cancellationToken)
        {
            var denied = scope == EnvironmentResourceScopes.Manage && deniedManage.Any(host => resource.EndsWith(host.ToString("D"), StringComparison.Ordinal));
            return Task.FromResult(denied
                ? new EnvironmentAuthorizationDecision(AuthorizationDecisionOutcome.Denied, "denied")
                : new EnvironmentAuthorizationDecision(AuthorizationDecisionOutcome.Allowed, null));
        }
    }

    private sealed class Grants(IReadOnlyList<EnvironmentRegistration> hosts, IReadOnlySet<Guid> deniedManage) : IEnvironmentGrantReader
    {
        public Task<IReadOnlyDictionary<Guid, IReadOnlySet<string>>?> ReadAsync(AuthenticatedActor actor, string accessToken, Guid correlationId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlySet<string>>?>(hosts.ToDictionary(
                host => host.EnvironmentId,
                host => (IReadOnlySet<string>)host.Scopes.Where(scope => scope != EnvironmentResourceScopes.Manage || !deniedManage.Contains(host.EnvironmentId)).ToHashSet()));
    }

    private sealed class Registrations(IReadOnlyList<EnvironmentRegistration> all) : IEnvironmentRegistrationStore
    {
        public IReadOnlyList<EnvironmentRegistration> All => all;

        public Task<EnvironmentRegistrationPage> ListAsync(Guid? afterEnvironmentId, int limit, CancellationToken cancellationToken) =>
            Task.FromResult(new EnvironmentRegistrationPage(afterEnvironmentId is null ? all : [], null));

        public Task<EnvironmentRegistrationStoreResult> CreateAsync(EnvironmentRegistration registration, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<EnvironmentRegistration?> GetAsync(Guid environmentId, CancellationToken cancellationToken) =>
            Task.FromResult(all.FirstOrDefault(host => host.EnvironmentId == environmentId));
    }
}
