using System.Text.Json.Nodes;
using Dokpod.ControlPlane.Api.AuditExport;
using Dokpod.ControlPlane.Api.Tests.Licensing;
using Dokpod.ControlPlane.Application.AuditExport;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.Domain.Auditing;
using Npgsql;
using Xunit;
using static Dokpod.ControlPlane.Api.Tests.AuditExport.AuditExportFixture;
using static Dokpod.ControlPlane.Api.Tests.AuditExport.AuditExportRoutes;
using static Dokpod.ControlPlane.Api.Tests.Licensing.LicensingPostgres;

namespace Dokpod.ControlPlane.Api.Tests.AuditExport;

/// <summary>
/// Spec 027 T016: rotas de destinos com PostgreSQL real, gate real da licença e o Keycloak simulado. Cobre scopes,
/// validação estrita, limite, nome, versão, idempotência, endereço, licença restrita, auditoria sem segredos, L7 e L8.
/// </summary>
[Trait("Category", "Integration")]
[Collection(LicensingPostgresCollection.Name)]
public sealed class AuditExportEndpointsTests
{
    [Fact]
    public async Task Routes_AskTheKeycloakForTheRightScope_AndFailClosed()
    {
        using var fixture = await AuditExportFixture.CreateAsync();
        await using var services = fixture.Create();

        var reader = new TestDecider();
        Assert.Equal(200, (await ListAsync(services, reader)).Status);
        var manager = new TestDecider();
        Assert.Equal(201, (await CreateAsync(services, Body("siem-scope"), manager)).Status);
        Assert.Equal([(AuditExportAuthorization.Resource, AuditExportAuthorization.ReadScope)], reader.Requests);
        Assert.Equal([(AuditExportAuthorization.Resource, AuditExportAuthorization.ManageScope)], manager.Requests);

        Assert.Equal((403, "authorization_denied"), Code(await ListAsync(services, new TestDecider(AuthorizationDecisionOutcome.Denied))));
        Assert.Equal((503, "authorization_unavailable"), Code(await ListAsync(services, new TestDecider(null))));
        Assert.Equal((503, "authorization_unavailable"), Code(await CreateAsync(services, Body("siem-x"), new TestDecider(AuthorizationDecisionOutcome.Indeterminate))));
    }

    [Theory]
    [InlineData(",\"extra\":1")]
    [InlineData("categories")]
    [InlineData("port")]
    public async Task InvalidBody_Is400(string scenario)
    {
        using var fixture = await AuditExportFixture.CreateAsync();
        await using var services = fixture.Create();
        var body = scenario switch
        {
            "categories" => Body("siem-a", categories: "\"nao-existe\""),
            "port" => Body("siem-a", port: 70000),
            _ => Body("siem-a", extra: scenario),
        };

        Assert.Equal((400, AuditExportProblems.InvalidDestination), Code(await CreateAsync(services, body)));
        Assert.Equal((400, "idempotency_key_required"), Code(Read(Context(Body("siem-a")), await AuditExportEndpoints.CreateAsync(Context(Body("siem-a")), new TestDecider(), services.Service, Ct))));
    }

    [Fact]
    public async Task Create_IsLimitedToFive_UniqueByName_AndIdempotent()
    {
        using var fixture = await AuditExportFixture.CreateAsync();
        await using var services = fixture.Create();
        var key = Guid.NewGuid().ToString();

        var first = await CreateAsync(services, Body("siem-1"), key: key);
        var replay = await CreateAsync(services, Body("siem-1"), key: key);
        Assert.Equal((201, 201), (first.Status, replay.Status));
        Assert.Equal(first.Body!["destinationId"]!.ToString(), replay.Body!["destinationId"]!.ToString());
        Assert.Equal("\"1\"", first.ETag);

        Assert.Equal((409, AuditExportProblems.NameTaken), Code(await CreateAsync(services, Body("SIEM-1"))));
        for (var i = 2; i <= 5; i++) Assert.Equal(201, (await CreateAsync(services, Body($"siem-{i}"))).Status);
        Assert.Equal((409, AuditExportProblems.LimitReached), Code(await CreateAsync(services, Body("siem-6"))));
        Assert.Equal(5, (await ListAsync(services)).Body!.AsArray().Count);
    }

    [Fact]
    public async Task UpdateAndDelete_RequireTheCurrentVersion()
    {
        using var fixture = await AuditExportFixture.CreateAsync();
        await using var services = fixture.Create();
        var id = Id(await CreateAsync(services, Body("siem-v")));

        Assert.Equal((412, AuditExportProblems.VersionConflict), Code(await UpdateAsync(services, id, Body("siem-v2"), "\"9\"")));
        Assert.Equal((400, "if_match_required"), Code(await UpdateAsync(services, id, Body("siem-v2"), null)));
        var updated = await UpdateAsync(services, id, Body("siem-v2"), "\"1\"");
        Assert.Equal((200, "\"2\""), (updated.Status, updated.ETag));
        Assert.Equal((412, AuditExportProblems.VersionConflict), Code(await DeleteAsync(services, id, "\"1\"")));
        Assert.Equal(204, (await DeleteAsync(services, id, "\"2\"")).Status);
        Assert.Equal((404, AuditExportProblems.NotFound), Code(await DeleteAsync(services, id, "\"2\"")));
        Assert.Equal((404, AuditExportProblems.NotFound), Code(await TestAsync(services, id)));
    }

    [Theory]
    [InlineData("127.0.0.1", null, AuditExportProblems.AddressBlocked)]
    [InlineData("169.254.169.254", null, AuditExportProblems.AddressBlocked)]
    [InlineData("10.20.30.40", "../etc", AuditExportProblems.InvalidSecretRef)]
    public async Task ForbiddenAddressOrSecretRef_Is422(string host, string? secretRef, string code)
    {
        using var fixture = await AuditExportFixture.CreateAsync();
        await using var services = fixture.Create();

        Assert.Equal((422, code), Code(await CreateAsync(services, Body("siem-422", host: host, secretRef: secretRef ?? "siem-soc"))));
    }

    [Fact]
    public async Task RestrictedLicense_BlocksCreateEditReactivateAndTest_ButNotDeactivateOrDelete()
    {
        using var fixture = await AuditExportFixture.CreateAsync();
        Guid id;
        Guid other;
        await using (var services = fixture.Create())
        {
            id = Id(await CreateAsync(services, Body("siem-r")));
            other = Id(await CreateAsync(services, Body("siem-r2")));
        }

        await fixture.ImportLicenseAsync(expired: true);
        await using (var services = fixture.Create())
        {
            Assert.Equal((403, "license_expired"), Code(await CreateAsync(services, Body("siem-r3"))));
            Assert.Equal((403, "license_expired"), Code(await UpdateAsync(services, id, Body("siem-r-novo"), "\"1\"")));
            Assert.Equal((403, "license_expired"), Code(await TestAsync(services, id)));

            var deactivated = await UpdateAsync(services, id, Body("siem-r", active: false), "\"1\"");
            Assert.Equal((200, "disabled"), (deactivated.Status, deactivated.Body!["state"]!.ToString()));
            Assert.Equal((403, "license_expired"), Code(await UpdateAsync(services, id, Body("siem-r", active: true), "\"2\"")));
            Assert.Equal(204, (await DeleteAsync(services, other, "\"1\"")).Status);
            Assert.Equal(200, (await ListAsync(services)).Status);
        }
    }

    [Fact]
    public async Task LicenseWithoutTheCapability_IsLicenseInvalid()
    {
        using var fixture = await AuditExportFixture.CreateAsync(capabilities: "\"change-windows\"");
        await using var services = fixture.Create();

        Assert.Equal((403, "license_invalid"), Code(await CreateAsync(services, Body("siem-c"))));
    }

    [Fact]
    public async Task Changes_AreAuditedOnTheInstallationScope_WithoutSecrets_AndAlwaysIncludeAuditExport()
    {
        using var fixture = await AuditExportFixture.CreateAsync();
        await using var services = fixture.Create();

        var created = await CreateAsync(services, Body("siem-audit", secretRef: "segredo-do-soc", categories: "\"containers\""));
        Assert.Equal(["audit_export", "containers"], created.Body!["categories"]!.AsArray().Select(c => c!.ToString()));
        Assert.Equal(new JsonObject { ["ca"] = false, ["clientCertificate"] = false, ["token"] = false }.ToJsonString(), created.Body["secretFiles"]!.ToJsonString());
        var id = Id(created);
        await UpdateAsync(services, id, Body("siem-audit", secretRef: "segredo-do-soc", categories: "\"containers\",\"images\""), "\"1\"");
        await TestAsync(services, id);
        await DeleteAsync(services, id, "\"2\"");

        await using var connection = await OpenAsync(fixture.ConnectionString, Ct);
        await using var command = new NpgsqlCommand(
            "SELECT action, environment_id, details::text, actor_id FROM dokpod.audit_events WHERE subject_id = @id ORDER BY occurred_at_utc, export_seq", connection);
        command.Parameters.AddWithValue("id", id.ToString("D"));
        var rows = new List<(string Action, Guid Environment, string Details, string Actor)>();
        await using (var reader = await command.ExecuteReaderAsync(Ct))
        {
            while (await reader.ReadAsync(Ct)) rows.Add((reader.GetString(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3)));
        }

        Assert.Equal(["audit_export.destination_created", "audit_export.destination_updated", "audit_export.test_sent", "audit_export.destination_deleted"], rows.Select(r => r.Action));
        Assert.All(rows, row => Assert.Equal((AuditEvent.InstallationScopeId, "user-027"), (row.Environment, row.Actor)));
        Assert.All(rows, row => Assert.DoesNotContain("segredo-do-soc", row.Details, StringComparison.Ordinal));
        Assert.Contains("\"hasSecretRef\": \"true\"", rows[0].Details, StringComparison.Ordinal);
        Assert.Contains("categories", JsonNode.Parse(rows[1].Details)!["changed"]!.ToString(), StringComparison.Ordinal);
        // O evento de teste não entra na trilha: só o registro do envio.
        Assert.Equal(0L, await ScalarAsync<long>(connection, null, "SELECT count(*) FROM dokpod.audit_events WHERE action = 'audit_export.test'", Ct));
    }

    [Fact]
    public async Task OtlpDestination_GetsTheDefaultPath_AndSyslogRefusesAPath()
    {
        using var fixture = await AuditExportFixture.CreateAsync();
        await using var services = fixture.Create();

        var otlp = await CreateAsync(services, Body("otel-soc", port: 4318).Replace("\"syslog\"", "\"otlp\""));
        Assert.Equal((201, "otlp", "/v1/logs"), (otlp.Status, otlp.Body!["protocol"]!.ToString(), otlp.Body["path"]!.ToString()));
        var custom = await CreateAsync(services, Body("otel-2", port: 4318).Replace("\"syslog\"", "\"otlp\"").Replace("\"path\":null", "\"path\":\"/coletor/v1/logs\""));
        Assert.Equal("/coletor/v1/logs", custom.Body!["path"]!.ToString());

        Assert.Equal((400, AuditExportProblems.InvalidDestination), Code(await CreateAsync(services, Body("syslog-path").Replace("\"path\":null", "\"path\":\"/v1/logs\""))));
    }

    [Fact]
    public async Task SixthTestInAMinute_IsRateLimited()
    {
        using var fixture = await AuditExportFixture.CreateAsync();
        var transport = new StubTransport(AuditExportSendResult.Failed(AuditExportErrorCodes.ConnectionRefused));
        await using var services = fixture.Create(null, transport);
        var id = Id(await CreateAsync(services, Body("siem-l8")));

        for (var i = 0; i < AuditExportLimits.MaxTestsPerMinute; i++)
        {
            var result = await TestAsync(services, id);
            Assert.Equal((200, false, "connection_refused"), (result.Status, result.Body!["delivered"]!.GetValue<bool>(), result.Body["errorCode"]!.ToString()));
        }

        Assert.Equal((429, AuditExportProblems.RateLimited), Code(await TestAsync(services, id)));
        Assert.Equal(AuditExportLimits.MaxTestsPerMinute, transport.Batches.Count);
        Assert.All(transport.Batches, batch => Assert.True(Assert.Single(batch).Test));
    }

    private static (int, string?) Code(RouteResult result) => (result.Status, result.Code);

    private static Guid Id(RouteResult result) => Guid.Parse(result.Body!["destinationId"]!.ToString());

    private static async Task<RouteResult> ListAsync(AuditExportFixture.Services services, TestDecider? decider = null)
    {
        var context = Context();
        return Read(context, await AuditExportEndpoints.ListAsync(context, decider ?? new TestDecider(), services.Service, Ct));
    }

    private static async Task<RouteResult> CreateAsync(AuditExportFixture.Services services, string body, TestDecider? decider = null, string? key = null)
    {
        var context = Context(body, idempotencyKey: key ?? Guid.NewGuid().ToString());
        return Read(context, await AuditExportEndpoints.CreateAsync(context, decider ?? new TestDecider(), services.Service, Ct));
    }

    private static async Task<RouteResult> UpdateAsync(AuditExportFixture.Services services, Guid id, string body, string? ifMatch)
    {
        var context = Context(body, ifMatch);
        return Read(context, await AuditExportEndpoints.UpdateAsync(id, context, new TestDecider(), services.Service, Ct));
    }

    private static async Task<RouteResult> DeleteAsync(AuditExportFixture.Services services, Guid id, string ifMatch)
    {
        var context = Context(ifMatch: ifMatch);
        return Read(context, await AuditExportEndpoints.DeleteAsync(id, context, new TestDecider(), services.Service, Ct));
    }

    private static async Task<RouteResult> TestAsync(AuditExportFixture.Services services, Guid id)
    {
        var context = Context();
        return Read(context, await AuditExportEndpoints.TestAsync(id, context, new TestDecider(), services.Service, Ct));
    }
}
