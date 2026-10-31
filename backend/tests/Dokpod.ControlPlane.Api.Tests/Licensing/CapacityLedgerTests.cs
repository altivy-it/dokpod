using Dokpod.ControlPlane.Application.Licensing;
using Dokpod.ControlPlane.Infrastructure.Licensing;
using Npgsql;
using Xunit;
using static Dokpod.ControlPlane.Api.Tests.Licensing.LicensingPostgres;

namespace Dokpod.ControlPlane.Api.Tests.Licensing;

/// <summary>Spec 031 T033: observações na conexão, host criado por ambiente, retenção com âncora (D-005).</summary>
[Trait("Category", "Integration")]
[Collection(LicensingPostgresCollection.Name)]
public sealed class CapacityLedgerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EachAgentConnection_RecordsAnObservationOnTheSameAutoCreatedHost()
    {
        var connectionString = ConnectionStringOrSkip();
        var environment = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await HelloAsync(connectionString, environment, 32, CapacitySource.Engine, now.AddMinutes(-2));
        await HelloAsync(connectionString, environment, 16, CapacitySource.ProcessFallback, now.AddMinutes(-1));

        await using var connection = await OpenAsync(connectionString, Ct);
        var hosts = await ScalarAsync<long>(connection, null, "SELECT count(DISTINCT host_id) FROM dokpod.licensing_capacity_observations WHERE environment_id = @e", Ct, new NpgsqlParameter("e", environment));
        var trusted = await ScalarAsync<long>(connection, null, "SELECT count(*) FROM dokpod.licensing_capacity_observations WHERE environment_id = @e AND vcpus = 32 AND source = 'engine'", Ct, new NpgsqlParameter("e", environment));
        var fallback = await ScalarAsync<long>(connection, null, "SELECT count(*) FROM dokpod.licensing_capacity_observations WHERE environment_id = @e AND vcpus IS NULL AND source = 'process_fallback'", Ct, new NpgsqlParameter("e", environment));
        var linked = await ScalarAsync<long>(connection, null, "SELECT count(*) FROM dokpod.licensing_host_environments WHERE environment_id = @e AND unlinked_at_utc IS NULL", Ct, new NpgsqlParameter("e", environment));

        Assert.Equal((1L, 1L, 1L, 1L), (hosts, trusted, fallback, linked));
    }

    [Fact]
    public async Task Retention_AnchorsBeforeDeletingSoAQuietHostKeepsItsCapacity()
    {
        var connectionString = ConnectionStringOrSkip();
        var environment = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await HelloAsync(connectionString, environment, 48, CapacitySource.Engine, now.AddDays(-60));
        var hostId = await HostOfAsync(connectionString, environment);

        await using (var context = CreateContext(connectionString))
        {
            await new CapacityLedger(context, new LicensingHostStore(context, new LicensingAuditWriter(context))).ApplyRetentionAsync(TimeSpan.FromDays(35), Ct);
        }

        var first = await AnchorAsync(connectionString, hostId);
        await using (var context = CreateContext(connectionString))
        {
            var ledger = new CapacityLedger(context, new LicensingHostStore(context, new LicensingAuditWriter(context)));
            await ledger.ApplyRetentionAsync(TimeSpan.FromDays(35), Ct);
            var input = await ledger.ReadMeteringInputAsync(now.AddDays(-30), Ct);
            var mine = new MeteringInput(
                input.Hosts.Where(host => host.HostId == hostId).ToArray(),
                input.Links.Where(link => link.HostId == hostId).ToArray(),
                input.Observations.Where(observation => observation.HostId == hostId).ToArray());

            var snapshot = CapacityMeter.Compute(mine, 16, now);

            Assert.Equal((3, 3), (snapshot.Current, snapshot.WindowPeak));
            await Assert.ThrowsAsync<PostgresException>(() => ledger.ApplyRetentionAsync(TimeSpan.FromDays(34), Ct));
        }

        await using var connection = await OpenAsync(connectionString, Ct);
        Assert.Equal(0L, await ScalarAsync<long>(connection, null, "SELECT count(*) FROM dokpod.licensing_capacity_observations WHERE environment_id = @e", Ct, new NpgsqlParameter("e", environment)));
        Assert.Equal(48, first.Vcpus);
        Assert.Equal(first, await AnchorAsync(connectionString, hostId));
    }

    private static async Task HelloAsync(string connectionString, Guid environment, int cpus, string source, DateTimeOffset at)
    {
        await using var context = CreateContext(connectionString);
        var ledger = new CapacityLedger(context, new LicensingHostStore(context, new LicensingAuditWriter(context)));
        await ledger.OnAgentHelloAsync(environment, "ledger-test", cpus, source, at, Ct);
    }

    private static async Task<Guid> HostOfAsync(string connectionString, Guid environment)
    {
        await using var connection = await OpenAsync(connectionString, Ct);
        return await ScalarAsync<Guid>(connection, null, "SELECT host_id FROM dokpod.licensing_host_environments WHERE environment_id = @e AND unlinked_at_utc IS NULL", Ct, new NpgsqlParameter("e", environment));
    }

    private static async Task<(int? Vcpus, DateTime ObservedAt)> AnchorAsync(string connectionString, Guid hostId)
    {
        await using var connection = await OpenAsync(connectionString, Ct);
        await using var command = new NpgsqlCommand("SELECT vcpus, observed_at_utc FROM dokpod.licensing_capacity_anchors WHERE host_id = @h", connection);
        command.Parameters.AddWithValue("h", hostId);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct), "âncora não gravada");
        return (reader.IsDBNull(0) ? null : reader.GetInt32(0), reader.GetDateTime(1));
    }
}
