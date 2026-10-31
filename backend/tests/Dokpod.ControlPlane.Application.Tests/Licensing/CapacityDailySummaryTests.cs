using Dokpod.ControlPlane.Application.Licensing;
using Xunit;
using static Dokpod.ControlPlane.Application.Tests.Licensing.LicensingTestKit;

namespace Dokpod.ControlPlane.Application.Tests.Licensing;

/// <summary>Spec 031 T070 (FR-035): um <c>capacity.daily_summary</c> por dia UTC encerrado; atrasados com <c>late</c>.</summary>
public sealed class CapacityDailySummaryTests : IDisposable
{
    private static readonly DateTimeOffset Day = new(2027, 3, 10, 0, 0, 0, TimeSpan.Zero);
    private readonly LicensingTestKit kit = new();

    public void Dispose() => kit.Dispose();

    [Fact]
    public async Task ClosedDay_IsSummarizedOnceWithPeakBandHostsAndSource()
    {
        var host = Guid.NewGuid();
        var environment = Guid.NewGuid();
        var metering = new MeteringInput(
            [new LicensingHostRecord(host, "rack-1", Day.AddDays(-5), null)],
            [new LicensingHostLink(host, environment, Day.AddDays(-5), null)],
            [new CapacityObservation(host, Day.AddDays(-5), 48, CapacitySource.Engine, environment)]);
        var store = new InMemoryLicensing(State(), kit.Active(), metering);
        var clock = new ManualClock(Day.AddHours(10));

        await Run(store, clock);
        await Run(store, clock);

        var summary = Assert.Single(Summaries(store));
        Assert.Equal("2027-03-09", summary.Details["date"]!.GetValue<string>());
        Assert.Equal((16, 3, 0, false), (
            summary.Details["vcpuBand"]!.GetValue<int>(),
            summary.Details["peakNodeEquivalents"]!.GetValue<int>(),
            summary.Details["unknownHosts"]!.GetValue<int>(),
            summary.Details["late"]!.GetValue<bool>()));
        var reported = summary.Details["hosts"]!.AsArray().Single()!;
        Assert.Equal((host.ToString("D"), 48, "engine"), (reported["hostId"]!.GetValue<string>(), reported["maxVcpus"]!.GetValue<int>(), reported["source"]!.GetValue<string>()));
        Assert.Null(summary.ActorId);
    }

    [Fact]
    public async Task DaysWithTheApiStopped_AreWrittenLaterAsLate()
    {
        var store = new InMemoryLicensing(State(), kit.Active());
        var clock = new ManualClock(Day.AddHours(10));
        await Run(store, clock);

        clock.Now = Day.AddDays(3).AddHours(1);
        await Run(store, clock);

        Assert.Equal(
            [("2027-03-09", false), ("2027-03-10", true), ("2027-03-11", true), ("2027-03-12", false)],
            Summaries(store).Select(e => (e.Details["date"]!.GetValue<string>(), e.Details["late"]!.GetValue<bool>())));
    }

    private Task Run(InMemoryLicensing store, ManualClock clock)
    {
        var status = new LicenseStatusService(store, store, kit.Evaluator, clock, store, store);
        return new LicensingMaintenance(status, store, store, store, clock).RunAsync(CancellationToken.None);
    }

    private static LicensingEvent[] Summaries(InMemoryLicensing store) =>
        [.. store.Events.Where(e => e.Action == LicensingEventAction.CapacityDailySummary)];
}
