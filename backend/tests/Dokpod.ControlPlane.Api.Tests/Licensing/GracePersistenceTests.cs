using Dokpod.ControlPlane.Application.Licensing;
using Npgsql;
using Xunit;
using static Dokpod.ControlPlane.Api.Tests.Licensing.LicensingPostgres;
using static Dokpod.ControlPlane.Api.Tests.Licensing.LicensingUs4Fixture;

namespace Dokpod.ControlPlane.Api.Tests.Licensing;

/// <summary>
/// Spec 031 T042 (SC-006, FR-019): reinício da API e reimportação da mesma licença não estendem prazo nenhum; a
/// restauração de backup não estende a tolerância de expiração, que vem do <c>expiresAt</c> assinado.
/// </summary>
[Trait("Category", "Integration")]
[Collection(LicensingPostgresCollection.Name)]
public sealed class GracePersistenceTests
{
    [Fact]
    public async Task ExpiryGrace_SurvivesRestartReimportAndBackupRestore()
    {
        using var fixture = new LicensingUs4Fixture(ConnectionStringOrSkip());
        await fixture.ResetClockAsync();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(-10);
        var license = fixture.License($"lic_{Guid.NewGuid():N}", expiresAt.AddDays(-365), expiresAt);
        Assert.Equal(LicenseImportOutcome.Accepted, (await fixture.ImportAsync(license)).Outcome);

        await using (var first = fixture.Create())
        {
            var status = await first.Status.GetAsync(Ct);
            Assert.Equal(("granted_in_grace", "expiry"), (status.Entitlement, status.Grace!.Cause));
            Assert.Equal(expiresAt.AddDays(30).ToUnixTimeSeconds(), status.Grace.Until.ToUnixTimeSeconds());
        }

        var persisted = (await fixture.DeadlinesAsync()).ExpiryUntil;
        Assert.NotNull(persisted);

        await using (var restarted = fixture.Create())
        {
            await restarted.Status.GetAsync(Ct);
        }

        Assert.Equal(LicenseImportOutcome.Unchanged, (await fixture.ImportAsync(license)).Outcome);
        Assert.Equal(persisted, (await fixture.DeadlinesAsync()).ExpiryUntil);

        // Backup anterior à detecção: prazo vazio no banco, recalculado do expiresAt assinado, nunca maior.
        await using (var connection = await OpenAsync(fixture.ConnectionString, Ct))
        {
            await ExecuteAsync(connection, null, "UPDATE dokpod.licensing_state SET expiry_grace_until_utc = NULL", Ct);
        }

        await using (var restored = fixture.Create())
        {
            Assert.Equal(persisted!.Value.ToUnixTimeSeconds(), (await restored.Status.GetAsync(Ct)).Grace!.Until.ToUnixTimeSeconds());
        }

        Assert.Equal(persisted, (await fixture.DeadlinesAsync()).ExpiryUntil);
    }

    [Fact]
    public async Task ExcessGrace_StartsOnceAndSurvivesRestartAndReimport()
    {
        using var fixture = new LicensingUs4Fixture(ConnectionStringOrSkip());
        await fixture.ResetClockAsync();
        var now = DateTimeOffset.UtcNow;
        var license = fixture.License($"lic_{Guid.NewGuid():N}", now.AddDays(-30), now.AddDays(300), productionQuota: 1);
        Assert.Equal(LicenseImportOutcome.Accepted, (await fixture.ImportAsync(license)).Outcome);
        await using (var setup = fixture.Create())
        {
            await setup.Ledger.OnAgentHelloAsync(Guid.NewGuid(), "us4-excess", 64, CapacitySource.Engine, now.AddMinutes(-1), Ct);
        }

        try
        {
            await AssertExcessGraceIsStableAsync(fixture, license, now);
        }
        finally
        {
            // O relógio fixo avança o instante confiável dois dias; os demais testes rodam no relógio real.
            await fixture.ResetClockAsync();
            await using var connection = await OpenAsync(fixture.ConnectionString, Ct);
            await ExecuteAsync(connection, null, "UPDATE dokpod.licensing_state SET excess_started_at_utc = NULL, excess_grace_until_utc = NULL", Ct);
        }
    }

    private static async Task AssertExcessGraceIsStableAsync(LicensingUs4Fixture fixture, byte[] license, DateTimeOffset now)
    {
        DateTimeOffset until;
        await using (var first = fixture.Create(new FixedClock(now)))
        {
            var status = await first.Status.GetAsync(Ct);
            Assert.Equal(("excess_in_grace", "granted_in_grace"), (status.Metering, status.Entitlement));
            until = status.Grace!.Until;
        }

        var later = new FixedClock(now.AddDays(2));
        await using (var restarted = fixture.Create(later))
        {
            Assert.Equal(until.ToUnixTimeSeconds(), (await restarted.Status.GetAsync(Ct)).Grace!.Until.ToUnixTimeSeconds());
        }

        Assert.Equal(LicenseImportOutcome.Unchanged, (await fixture.ImportAsync(license, later)).Outcome);
        await using (var reimported = fixture.Create(later))
        {
            Assert.Equal(until.ToUnixTimeSeconds(), (await reimported.Status.GetAsync(Ct)).Grace!.Until.ToUnixTimeSeconds());
        }

        await using var connection = await OpenAsync(fixture.ConnectionString, Ct);
        Assert.Equal(1L, await ScalarAsync<long>(
            connection, null,
            "SELECT count(*) FROM dokpod.licensing_events WHERE action = 'license.grace_started' AND details->>'cause' = 'excess' AND details->>'until' = @until",
            Ct,
            new NpgsqlParameter("until", Iso(until))));
    }
}
