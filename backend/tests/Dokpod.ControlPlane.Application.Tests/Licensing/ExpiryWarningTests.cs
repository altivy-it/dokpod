using Dokpod.ControlPlane.Application.Licensing;
using Xunit;
using static Dokpod.ControlPlane.Application.Tests.Licensing.LicensingTestKit;

namespace Dokpod.ControlPlane.Application.Tests.Licensing;

/// <summary>Spec 031 T044 (FR-032): <c>license.expiry_warning</c> uma vez em cada marco, sem repetir após reinício.</summary>
public sealed class ExpiryWarningTests : IDisposable
{
    private readonly LicensingTestKit kit = new();

    public void Dispose() => kit.Dispose();

    [Fact]
    public async Task EachMilestone_IsWarnedOnceEvenAcrossRestarts()
    {
        var store = new InMemoryLicensing(State(), kit.Active());
        var clock = new ManualClock(ExpiresAt.AddDays(-61));

        await Run(store, clock);
        Assert.Empty(Warnings(store));

        clock.Now = ExpiresAt.AddDays(-59);
        await Run(store, clock);
        await Run(store, clock);
        Assert.Equal([60], Warnings(store));

        clock.Now = ExpiresAt.AddDays(-29);
        await Run(store, clock);
        clock.Now = ExpiresAt.AddDays(-6);
        await Run(store, clock);
        await Run(store, clock);
        Assert.Equal([60, 30, 7], Warnings(store));
        Assert.All(
            store.Events.Where(e => e.Action == LicensingEventAction.LicenseExpiryWarning),
            e => Assert.Equal("2027-11-01T00:00:00Z", e.Details["expiresAt"]!.GetValue<string>()));
    }

    [Fact]
    public async Task ExpiredLicense_IsNotWarnedAnymore()
    {
        var store = new InMemoryLicensing(State(), kit.Active());

        await Run(store, new ManualClock(ExpiresAt.AddDays(1)));

        Assert.Empty(Warnings(store));
    }

    private Task Run(InMemoryLicensing store, ManualClock clock)
    {
        // Instância nova a cada execução: o equivalente a reiniciar a API, com a trilha preservada.
        var status = new LicenseStatusService(store, store, kit.Evaluator, clock, store, store);
        return new LicensingMaintenance(status, store, store, store, clock).RunAsync(CancellationToken.None);
    }

    private static int[] Warnings(InMemoryLicensing store) =>
        [.. store.Events.Where(e => e.Action == LicensingEventAction.LicenseExpiryWarning).Select(e => e.Details["daysBefore"]!.GetValue<int>())];
}
