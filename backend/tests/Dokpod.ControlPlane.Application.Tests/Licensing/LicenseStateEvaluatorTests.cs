using Dokpod.ControlPlane.Application.Licensing;
using Xunit;
using static Dokpod.ControlPlane.Application.Tests.Licensing.LicensingTestKit;

namespace Dokpod.ControlPlane.Application.Tests.Licensing;

/// <summary>Spec 031 T041 (FR-018 a FR-021, D-006): eixos, prazos que não se somam, relógio confiável e cotas.</summary>
public sealed class LicenseStateEvaluatorTests : IDisposable
{
    private static readonly DateTimeOffset Active = new(2027, 3, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly LicensingTestKit kit = new();

    public void Dispose() => kit.Dispose();

    [Fact]
    public void ExpiredTenDaysAgo_IsGrantedInGraceUntilExpiryPlus30Days()
    {
        var assessment = kit.Evaluator.Assess(State(), kit.Active(), ExpiresAt.AddDays(10), Peak(0));

        Assert.Equal(("granted_in_grace", "expired_in_grace"), (assessment.Status.Entitlement, assessment.Status.Validity));
        Assert.Equal(new LicenseStatusGrace("expiry", ExpiresAt, ExpiresAt.AddDays(30)), assessment.Status.Grace);
        Assert.Equal(ExpiresAt.AddDays(30), assessment.Update.ExpiryGraceUntilUtc);
        Assert.Contains(LicenseReasons.Expired, assessment.Status.Reasons);
    }

    [Fact]
    public void AfterTheDeadline_IsRestricted()
    {
        var status = kit.Evaluator.Assess(State(expiryGraceUntil: ExpiresAt.AddDays(30)), kit.Active(), ExpiresAt.AddDays(31), Peak(0)).Status;

        Assert.Equal(("restricted", "expired"), (status.Entitlement, status.Validity));
        Assert.Null(status.Grace);
    }

    [Fact]
    public void ExpiryAndExcessTogether_UseTheEarliestDeadline()
    {
        var excessUntil = ExpiresAt.AddDays(5);
        var status = kit.Evaluator.Assess(State(excessStarted: ExpiresAt.AddDays(-25), excessUntil: excessUntil), kit.Active(), ExpiresAt.AddDays(2), Peak(5)).Status;

        Assert.Equal("granted_in_grace", status.Entitlement);
        Assert.Equal(("excess", excessUntil), (status.Grace!.Cause, status.Grace.Until));
        Assert.Equal("restricted", kit.Evaluator.Assess(State(excessStarted: ExpiresAt.AddDays(-25), excessUntil: excessUntil), kit.Active(), ExpiresAt.AddDays(6), Peak(5)).Status.Entitlement);
    }

    [Fact]
    public void NotBeforeInTheFuture_IsNotStartedWithoutRights()
    {
        var status = kit.Evaluator.Assess(State(), kit.Active(Payload(notBefore: "2027-06-01T00:00:00Z")), Active, Peak(0)).Status;

        Assert.Equal(("not_started", "restricted"), (status.Validity, status.Entitlement));
        Assert.Contains(LicenseReasons.NotStarted, status.Reasons);
    }

    [Fact]
    public void ClockMoreThanFiveMinutesBehind_IsIndeterminateWithoutNewGrace()
    {
        var assessment = kit.Evaluator.Assess(State(lastTrusted: ExpiresAt.AddDays(40)), kit.Active(), ExpiresAt.AddDays(10), Peak(5));

        Assert.Equal("indeterminate", assessment.Status.Entitlement);
        Assert.Null(assessment.Status.Grace);
        Assert.True(assessment.Update.IsEmpty);
    }

    [Fact]
    public void ClockUpToFiveMinutesBehind_KeepsTheStateAndDoesNotAdvanceTheInstant()
    {
        var assessment = kit.Evaluator.Assess(State(lastTrusted: Active.AddMinutes(4)), kit.Active(), Active, Peak(0));

        Assert.Equal("granted", assessment.Status.Entitlement);
        Assert.Null(assessment.Update.LastTrustedInstantUtc);
        Assert.Equal(Active, kit.Evaluator.Assess(State(lastTrusted: Active.AddMinutes(-1)), kit.Active(), Active, Peak(0)).Update.LastTrustedInstantUtc);
    }

    [Fact]
    public void PeakAboveSlotQuota_StartsExcessGrace()
    {
        var assessment = kit.Evaluator.Assess(State(), kit.Active(), Active, Peak(5));

        Assert.Equal(("excess_in_grace", "granted_in_grace"), (assessment.Status.Metering, assessment.Status.Entitlement));
        Assert.Equal((Active, Active.AddDays(30)), (assessment.Update.ExcessStartedAtUtc!.Value, assessment.Update.ExcessGraceUntilUtc!.Value));
    }

    [Fact]
    public void PeakWithinSlotQuota_IsRegularEvenWithCapacityTen()
    {
        var status = kit.Evaluator.Assess(State(slot: "hml"), kit.Active(), Active, Peak(5)).Status;

        Assert.Equal(("regular", "granted", 6), (status.Metering, status.Entitlement, status.Installation.Quota!.Value));
    }

    [Fact]
    public void UnlimitedCapacity_NeverExceedsAndNeverComesFromZero()
    {
        var status = kit.Evaluator.Assess(State(), kit.Active(Payload(unlimited: true)), Active, Peak(1000)).Status;

        Assert.Equal(("regular", "granted"), (status.Metering, status.Entitlement));
        Assert.Null(status.Installation.Quota);
    }

    [Fact]
    public void ZeroQuotaSlot_IsDisabledAndAnyUseIsExcess()
    {
        var status = kit.Evaluator.Assess(State(slot: "hml"), kit.Active(Payload(productionQuota: 10, stagingQuota: 0)), Active, Peak(1)).Status;

        Assert.Equal((0, "excess_in_grace"), (status.Installation.Quota!.Value, status.Metering));
    }

    [Fact]
    public void RegularizedCapacity_ClearsTheExcess()
    {
        var assessment = kit.Evaluator.Assess(State(excessStarted: Active.AddDays(-3), excessUntil: Active.AddDays(27)), kit.Active(), Active, Peak(4));

        Assert.True(assessment.Update.ClearExcess);
        Assert.Equal(("regular", "granted"), (assessment.Status.Metering, assessment.Status.Entitlement));
    }

    [Fact]
    public void PersistedExcessDeadline_IsNeverRecalculated()
    {
        var persisted = Active.AddDays(-20).AddDays(30);
        var assessment = kit.Evaluator.Assess(State(excessStarted: Active.AddDays(-20), excessUntil: persisted), kit.Active(), Active, Peak(5));

        Assert.Equal(persisted, assessment.Status.Grace!.Until);
        Assert.Null(assessment.Update.ExcessStartedAtUtc);
    }

    [Fact]
    public void UnknownMeasurement_IsReportedWithoutRestricting()
    {
        var status = kit.Evaluator.Assess(State(), kit.Active(), Active, Peak(2, unknownHosts: 1)).Status;

        Assert.Equal(("unknown", "granted"), (status.Metering, status.Entitlement));
        Assert.Contains(LicenseReasons.MeteringUnknown, status.Reasons);
    }
}
