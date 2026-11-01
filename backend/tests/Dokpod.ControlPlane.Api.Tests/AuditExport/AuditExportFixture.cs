using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dokpod.ControlPlane.Api.AuditExport;
using Dokpod.ControlPlane.Api.Tests.Licensing;
using Dokpod.ControlPlane.Application.AuditExport;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Licensing;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.ControlPlane.Infrastructure.AuditExport;
using Dokpod.ControlPlane.Infrastructure.Licensing;
using Microsoft.AspNetCore.Http;
using Xunit;
using static Dokpod.ControlPlane.Api.Tests.Licensing.LicensingPostgres;

namespace Dokpod.ControlPlane.Api.Tests.AuditExport;

/// <summary>
/// Composição Business da exportação sobre PostgreSQL real (spec 027 US1): licença sintética da fixture da 031,
/// gate real, escritor real da trilha e transporte configurável. Cada teste começa sem destinos.
/// </summary>
internal sealed class AuditExportFixture : IDisposable
{
    public const string Capability = "\"audit-siem-export\"";

    private readonly LicensingUs4Fixture licensing;

    private AuditExportFixture(LicensingUs4Fixture licensing)
    {
        this.licensing = licensing;
    }

    public string ConnectionString => licensing.ConnectionString;

    public string SecretsRoot { get; } = Directory.CreateTempSubdirectory("dokpod-027-secrets-").FullName;

    public AuditExportTestLimiter Limiter { get; } = new();

    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<AuditExportFixture> CreateAsync(string? capabilities = Capability, bool expired = false)
    {
        var fixture = new AuditExportFixture(new LicensingUs4Fixture(ConnectionStringOrSkip()));
        await fixture.licensing.ResetClockAsync();
        await fixture.ImportLicenseAsync(capabilities, expired);
        await using var connection = await OpenAsync(fixture.ConnectionString, Ct);
        await ExecuteAsync(connection, null, "DELETE FROM dokpod.audit_export_destinations", Ct);
        return fixture;
    }

    public async Task ImportLicenseAsync(string? capabilities = Capability, bool expired = false)
    {
        var now = DateTimeOffset.UtcNow;
        var license = expired
            ? licensing.License($"lic_{Guid.NewGuid():N}", now.AddDays(-400), now.AddDays(-31), capabilities: capabilities ?? string.Empty)
            : licensing.License($"lic_{Guid.NewGuid():N}", now.AddDays(-1), now.AddDays(365), capabilities: capabilities ?? string.Empty);
        Assert.Equal(LicenseImportOutcome.Accepted, (await licensing.ImportAsync(license)).Outcome);
    }

    public Services Create(IDestinationAddressResolver? resolver = null, params IAuditExportTransport[] transports)
    {
        var licensingServices = licensing.Create();
        var context = licensingServices.Context;
        resolver ??= new DnsDestinationAddressResolver(new DestinationAddressPolicy());
        var secrets = new MountedSecretReader(SecretsRoot);
        IAuditExportTransport[] available = transports.Length > 0 ? transports : [new StubTransport()];
        var store = new AuditExportStore(context);
        var trail = new AuditTrailExportReader(context);
        var installation = new LicensingInstallationIdentity(context);
        var service = new AuditExportDestinationService(
            store, trail, new BusinessCapabilityGate(licensingServices.Status), new PostgresAuditEventWriter(context),
            resolver, secrets, available, installation, Limiter, TimeProvider.System);
        var sender = new AuditExportSender(store, trail, available, installation, new AuditExportBackoff(), TimeProvider.System);
        return new Services(licensingServices, service, sender, store);
    }

    public sealed record Services(LicensingUs4Fixture.Services Licensing, AuditExportDestinationService Service, AuditExportSender Sender, AuditExportStore Store)
        : IAsyncDisposable
    {
        public ControlPlaneDbContext Context => Licensing.Context;

        public ValueTask DisposeAsync() => Licensing.DisposeAsync();
    }

    public static string Body(string name, string host = "10.20.30.40", int port = 6514, string? secretRef = "siem-soc", string categories = "\"containers\"", bool active = true, string extra = "") =>
        $$"""
        {"name":"{{name}}","protocol":"syslog","host":"{{host}}","port":{{port}},"path":null,"secretRef":{{(secretRef is null ? "null" : $"\"{secretRef}\"")}},"categories":[{{categories}}],"active":{{(active ? "true" : "false")}}{{extra}}}
        """;

    public void Dispose()
    {
        Limiter.Dispose();
        licensing.Dispose();
        Directory.Delete(SecretsRoot, recursive: true);
    }
}

/// <summary>Resposta de rota já materializada: status, código do problema, corpo JSON e <c>ETag</c>.</summary>
internal sealed record RouteResult(int Status, string? Code, JsonNode? Body, string? ETag);

internal static class AuditExportRoutes
{
    public static DefaultHttpContext Context(string? body = null, string? ifMatch = null, string? idempotencyKey = null, string subject = "user-027")
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", subject), new Claim("preferred_username", "Maria")], "test"));
        context.Request.Headers.Authorization = "Bearer test-token";
        context.Request.Path = AuditExportEndpoints.Route;
        if (ifMatch is not null) context.Request.Headers.IfMatch = ifMatch;
        if (idempotencyKey is not null) context.Request.Headers["Idempotency-Key"] = idempotencyKey;
        if (body is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            context.Request.Body = new MemoryStream(bytes);
            context.Request.ContentLength = bytes.Length;
        }

        return context;
    }

    public static RouteResult Read(HttpContext context, IResult result) => result switch
    {
        Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult problem => new(problem.StatusCode, problem.ProblemDetails.Extensions["code"]?.ToString(), null, null),
        IValueHttpResult value and IStatusCodeHttpResult status => new(
            status.StatusCode ?? 200, null, JsonSerializer.SerializeToNode(value.Value), context.Response.Headers.ETag.ToString() is { Length: > 0 } etag ? etag : null),
        IStatusCodeHttpResult status => new(status.StatusCode ?? 0, null, null, null),
        _ => throw new InvalidOperationException(result.GetType().Name),
    };
}

/// <summary>Decisor do Keycloak de teste: permite, nega ou falha, e guarda os scopes pedidos.</summary>
internal sealed class TestDecider(AuthorizationDecisionOutcome? outcome = AuthorizationDecisionOutcome.Allowed) : IEnvironmentAuthorizationDecider
{
    public List<(string Resource, string Scope)> Requests { get; } = [];

    public Task<EnvironmentAuthorizationDecision> DecideAsync(string resource, string scope, AuthenticatedActor actor, string accessToken, Guid correlationId, CancellationToken cancellationToken)
    {
        Requests.Add((resource, scope));
        return outcome is { } value
            ? Task.FromResult(new EnvironmentAuthorizationDecision(value, value == AuthorizationDecisionOutcome.Allowed ? null : "denied"))
            : throw new HttpRequestException("keycloak indisponível");
    }
}

/// <summary>Transporte que só registra os lotes, para rotas que não precisam de rede.</summary>
internal sealed class StubTransport(AuditExportSendResult? result = null) : IAuditExportTransport
{
    public List<IReadOnlyList<AuditExportRecord>> Batches { get; } = [];

    public string Protocol => AuditExportProtocols.Syslog;

    public Task<AuditExportSendResult> SendAsync(AuditExportDestination destination, IReadOnlyList<AuditExportRecord> batch, CancellationToken cancellationToken)
    {
        Batches.Add(batch);
        return Task.FromResult(result ?? AuditExportSendResult.Ok);
    }
}
