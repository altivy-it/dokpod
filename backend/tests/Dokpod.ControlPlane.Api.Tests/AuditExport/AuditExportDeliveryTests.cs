using System.Net;
using System.Text.Json.Nodes;
using Dokpod.ControlPlane.Api.Tests.Licensing;
using Dokpod.ControlPlane.Application.AuditExport;
using Dokpod.ControlPlane.Infrastructure.AuditExport;
using Npgsql;
using Xunit;
using static Dokpod.ControlPlane.Api.Tests.AuditExport.AuditExportFixture;
using static Dokpod.ControlPlane.Api.Tests.Licensing.LicensingPostgres;

namespace Dokpod.ControlPlane.Api.Tests.AuditExport;

/// <summary>
/// Spec 027 T017: envio ponta a ponta com PostgreSQL real e servidor syslog TLS em processo. Os eventos chegam com os
/// campos da trilha, só das categorias marcadas; mudar filtros não reenvia o passado; destino novo começa no fim da
/// trilha; com a licença restrita o envio continua (FR-008, SC-005).
/// </summary>
[Trait("Category", "Integration")]
[Collection(LicensingPostgresCollection.Name)]
public sealed class AuditExportDeliveryTests : IAsyncLifetime
{
    private const string Owner = "replica-t017";
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);
    private readonly TestPki pki = new();
    private X509CertificateHolder? server;
    private AuditExportFixture? fixture;

    private AuditExportFixture Fixture => fixture!;

    private SyslogTlsTestServer Server => server!.Server;

    public async ValueTask InitializeAsync()
    {
        fixture = await CreateAsync();
        TestPki.WriteSecret(Fixture.SecretsRoot, "siem-soc", pki.CaPem);
        server = new X509CertificateHolder(pki.Issue("siem.test", server: true));
    }

    [Fact]
    public async Task TrailEvents_ArriveWithTheSameFields_OnlyForSelectedCategories_AndFilterChangesDoNotResend()
    {
        await using var services = Create();
        var id = await CreateDestinationAsync(services, "\"containers\"");

        var restart = await AppendAsync("container.restart");
        var pull = await AppendAsync("image.pull");
        var records = await DeliverAsync(services, items => items.Any(r => Id(r) == restart));
        var sent = Assert.Single(records, r => Id(r) == restart);
        // O helper grava com outcome 1 (falha), como a trilha guardou.
        Assert.Equal(("container.restart", "containers", "failed", "tester", false), (
            sent["action"]!.ToString(), sent["category"]!.ToString(), sent["outcome"]!.ToString(), sent["actor"]!["id"]!.ToString(), sent["test"]!.GetValue<bool>()));
        Assert.Equal(await EnvironmentOfAsync(restart), Guid.Parse(sent["environmentId"]!.ToString()));
        Assert.Equal(1, sent["formatVersion"]!.GetValue<int>());
        Assert.DoesNotContain(records, r => Id(r) == pull);
        Assert.NotNull(await EnvironmentOfAsync(pull));

        // O cursor já passou do image.pull fora do filtro: marcar "images" depois não o reenvia.
        await DeliverAsync(services, _ => false, TimeSpan.FromSeconds(2));
        var state = (await services.Store.GetAsync(id, Ct))!;
        await services.Service.UpdateAsync(
            id,
            new AuditExportDestinationInput(state.Destination.Name, "syslog", "siem.test", Server.Port, null, "siem-soc", ["containers", "images"], true),
            state.Destination.Version,
            new AuditExportCaller("user-027", null, Guid.NewGuid()),
            Ct);
        var newPull = await AppendAsync("image.pull");
        records = await DeliverAsync(services, items => items.Any(r => Id(r) == newPull));

        Assert.Contains(records, r => Id(r) == newPull);
        Assert.DoesNotContain(records, r => Id(r) == pull);
        Assert.Single(records, r => Id(r) == restart);
    }

    [Fact]
    public async Task NewDestination_StartsAtTheEndOfTheTrail()
    {
        await using var services = Create();
        var before = await AppendAsync("container.stop");
        await CreateDestinationAsync(services, "\"containers\"");
        var after = await AppendAsync("container.start");

        var records = await DeliverAsync(services, items => items.Any(r => Id(r) == after));

        Assert.Contains(records, r => Id(r) == after);
        Assert.DoesNotContain(records, r => Id(r) == before);
    }

    [Fact]
    public async Task RestrictedLicense_KeepsSending()
    {
        Guid id;
        await using (var services = Create())
        {
            id = await CreateDestinationAsync(services, "\"containers\"");
        }

        await Fixture.ImportLicenseAsync(expired: true);
        await using (var services = Create())
        {
            var restricted = await services.Service.TestAsync(id, new AuditExportCaller("user-027", null, Guid.NewGuid()), Ct);
            Assert.Equal((AuditExportServiceOutcome.LicenseDenied, "license_expired"), (restricted.Outcome, restricted.DenialCode));

            var restart = await AppendAsync("container.restart");
            var records = await DeliverAsync(services, items => items.Any(r => Id(r) == restart));

            Assert.Contains(records, r => Id(r) == restart);
            var listed = Assert.Single(await services.Service.ListAsync(Ct));
            Assert.Equal(AuditExportStates.Ok, listed.State);
            Assert.NotNull(listed.Cursor.LastSentAtUtc);
        }
    }

    private AuditExportFixture.Services Create() =>
        Fixture.Create(
            new FixedResolver(IPAddress.Loopback),
            new SyslogTlsTransport(new FixedResolver(IPAddress.Loopback), new MountedSecretReader(Fixture.SecretsRoot)));

    private async Task<Guid> CreateDestinationAsync(AuditExportFixture.Services services, string categories)
    {
        var result = await services.Service.CreateAsync(
            new AuditExportDestinationInput("siem-t017", "syslog", "siem.test", Server.Port, null, "siem-soc", [.. categories.Split(',').Select(c => c.Trim('"'))], true),
            Guid.NewGuid().ToString(),
            new AuditExportCaller("user-027", "Maria", Guid.NewGuid()),
            Ct);
        Assert.Equal(AuditExportServiceOutcome.Created, result.Outcome);
        return result.View!.Destination.DestinationId;
    }

    /// <summary>Roda ciclos até a condição valer; transações abertas por outros testes podem segurar o <c>xmin</c>.</summary>
    private async Task<IReadOnlyList<JsonNode>> DeliverAsync(AuditExportFixture.Services services, Func<IReadOnlyList<JsonNode>, bool> condition, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? Wait);
        while (DateTime.UtcNow < deadline)
        {
            await services.Sender.RunOnceAsync(Owner, Ct);
            if (condition(Server.Records)) break;
            await Task.Delay(200, Ct);
        }

        return Server.Records;
    }

    private async Task<Guid> AppendAsync(string action)
    {
        await using var connection = await OpenAsync(Fixture.ConnectionString, Ct);
        return await AuditExportMigrationTests.AppendAuditEventAsync(connection, null, action);
    }

    private async Task<Guid?> EnvironmentOfAsync(Guid eventId)
    {
        await using var connection = await OpenAsync(Fixture.ConnectionString, Ct);
        return await ScalarAsync<Guid?>(connection, null, "SELECT environment_id FROM dokpod.audit_events WHERE event_id = @id", Ct, new NpgsqlParameter("id", eventId));
    }

    private static Guid Id(JsonNode record) => Guid.Parse(record["eventId"]!.ToString());

    public async ValueTask DisposeAsync()
    {
        if (server is not null) await server.DisposeAsync();
        fixture?.Dispose();
        pki.Dispose();
    }

    /// <summary>Servidor e certificado com o mesmo ciclo de vida do teste.</summary>
    private sealed class X509CertificateHolder(System.Security.Cryptography.X509Certificates.X509Certificate2 certificate) : IAsyncDisposable
    {
        public SyslogTlsTestServer Server { get; } = SyslogTlsTestServer.Tls(certificate);

        public async ValueTask DisposeAsync()
        {
            await Server.DisposeAsync();
            certificate.Dispose();
        }
    }
}
