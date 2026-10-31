using Dokpod.ControlPlane.Application.Licensing;
using Dokpod.ControlPlane.Infrastructure.Licensing;
using Xunit;
using static Dokpod.ControlPlane.Api.Tests.Licensing.LicensingPostgres;
using static Dokpod.ControlPlane.Api.Tests.Licensing.LicensingUs4Fixture;

namespace Dokpod.ControlPlane.Api.Tests.Licensing;

/// <summary>
/// Spec 031 T043 (FR-021, SC-005): o gate só recusa novas execuções de capability Business. Leitura do estado e
/// renovação continuam funcionando; nada no gate tem efeito colateral sobre containers, dados ou rotas da CE, que
/// não dependem dele (teste de arquitetura da T005).
/// </summary>
[Trait("Category", "Integration")]
[Collection(LicensingPostgresCollection.Name)]
public sealed class BusinessCapabilityGateTests
{
    [Fact]
    public async Task RestrictedLicense_RefusesNewBusinessExecutionsButStatusAndRenewalWork()
    {
        using var fixture = new LicensingUs4Fixture(ConnectionStringOrSkip());
        await fixture.ResetClockAsync();
        var now = DateTimeOffset.UtcNow;
        var expired = fixture.License($"lic_{Guid.NewGuid():N}", now.AddDays(-400), now.AddDays(-31));
        Assert.Equal(LicenseImportOutcome.Accepted, (await fixture.ImportAsync(expired)).Outcome);

        await using (var services = fixture.Create())
        {
            var decision = await new BusinessCapabilityGate(services.Status).EnsureAsync("change-windows", Ct);
            Assert.False(decision.Allowed);
            Assert.Equal("license_expired", decision.DenialCode);
            Assert.Equal("restricted", (await services.Status.GetAsync(Ct)).Entitlement);
        }

        // Renovação segue disponível em restricted e devolve o direito.
        var renewed = fixture.License($"lic_{Guid.NewGuid():N}", now.AddDays(-1), now.AddDays(365));
        Assert.Equal(LicenseImportOutcome.Accepted, (await fixture.ImportAsync(renewed)).Outcome);
        await using (var services = fixture.Create())
        {
            Assert.True((await new BusinessCapabilityGate(services.Status).EnsureAsync("change-windows", Ct)).Allowed);
        }
    }

    [Fact]
    public async Task CapabilityOutsideTheLicense_IsRefusedAsInvalid()
    {
        using var fixture = new LicensingUs4Fixture(ConnectionStringOrSkip());
        await fixture.ResetClockAsync();
        var now = DateTimeOffset.UtcNow;
        Assert.Equal(LicenseImportOutcome.Accepted, (await fixture.ImportAsync(fixture.License($"lic_{Guid.NewGuid():N}", now.AddDays(-1), now.AddDays(365)))).Outcome);

        await using var services = fixture.Create();
        var decision = await new BusinessCapabilityGate(services.Status).EnsureAsync("sso-templates", Ct);

        Assert.Equal((false, "license_invalid"), (decision.Allowed, decision.DenialCode));
    }

    [Fact]
    public async Task ClockBehindTheTrustedInstant_IsIndeterminateForTheGate()
    {
        using var fixture = new LicensingUs4Fixture(ConnectionStringOrSkip());
        await fixture.ResetClockAsync();
        var now = DateTimeOffset.UtcNow;
        Assert.Equal(LicenseImportOutcome.Accepted, (await fixture.ImportAsync(fixture.License($"lic_{Guid.NewGuid():N}", now.AddDays(-1), now.AddDays(365)))).Outcome);

        try
        {
            await using var services = fixture.Create(new FixedClock(now.AddHours(-1)));
            var decision = await new BusinessCapabilityGate(services.Status).EnsureAsync("change-windows", Ct);

            Assert.Equal((false, "license_state_indeterminate"), (decision.Allowed, decision.DenialCode));
        }
        finally
        {
            await fixture.ResetClockAsync();
        }
    }

    [Fact]
    public void DenialCodes_FollowTheCauseOfTheRestriction()
    {
        LicenseStatus Status(string entitlement, string authenticity, params string[] reasons) =>
            new(entitlement, authenticity, null, "regular", reasons, null, new LicenseStatusInstallation(null, null, null), null, null, 0);

        Assert.Equal("license_capacity_exceeded", BusinessCapabilityGate.DenialCode(Status("restricted", "valid", LicenseReasons.CapacityExceeded)));
        Assert.Equal("license_revoked", BusinessCapabilityGate.DenialCode(Status("restricted", "revoked")));
        Assert.Equal("license_invalid", BusinessCapabilityGate.DenialCode(Status("ce_only", "absent")));
    }
}
