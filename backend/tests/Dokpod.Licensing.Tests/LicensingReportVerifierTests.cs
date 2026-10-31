using System.Text.Json.Nodes;
using Dokpod.Licensing.Tests.ReportVectors;
using Xunit;

namespace Dokpod.Licensing.Tests;

/// <summary>Spec 031 T071 (SC-010): relatório íntegro aceito e um caso por adulteração recusado.</summary>
public sealed class LicensingReportVerifierTests
{
    private const string Challenge = "rnw-2027-org_51d2e0-a91f";
    private const string ContentSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly Guid Installation = Guid.Parse("3f0c5a1e-2b4d-4c6e-8f90-a1b2c3d4e5f6");
    private static readonly DateTimeOffset Start = new(2026, 11, 14, 13, 0, 0, TimeSpan.Zero);
    private static readonly IssuedLicense Issued = new("lic_7f3c9a", 2, ContentSha);

    [Fact]
    public void ReferenceEvent_MatchesTheVectorComputedOutsideDotNet()
    {
        var builder = new ReportBuilder(Installation).Add(
            "capacity.daily_summary",
            Start.AddTicks(1_234_560),
            """{"date":"2026-11-14","hosts":[{"hostId":"8b1e0000-0000-4000-8000-000000000001","maxVcpus":64,"source":"engine"}],"late":false,"peakNodeEquivalents":7,"unknownHosts":0,"vcpuBand":32}""",
            "xJ3AF5eKIuGCRF7Cn9SN-AETTFVCCw_1vyGTLz7_VYM",
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"));

        Assert.Equal("f6409e296295d3d139dc20352f7d7d0704901f24a6684f8b3c4454fd7207176c", Convert.ToHexStringLower(Convert.FromBase64String(Pad(builder.HeadHash))));
    }

    [Fact]
    public void IntactReport_IsOk() => Assert.Equal(ReportVerificationCode.Ok, Verify(Report().Build()).Code);

    [Theory]
    [InlineData("detail-changed")]
    [InlineData("event-removed")]
    [InlineData("events-reordered")]
    [InlineData("record-hash-changed")]
    [InlineData("instant-regressed")]
    [InlineData("head-changed")]
    public void TamperedReport_IsTampered(string scenario)
    {
        var report = Report().Build();
        var events = report["events"]!.AsArray();
        switch (scenario)
        {
            case "detail-changed": events[2]!["details"]!["peakNodeEquivalents"] = 1; break;
            case "event-removed": events.RemoveAt(2); break;
            case "events-reordered":
                var moved = events[1]!.DeepClone();
                events.RemoveAt(1);
                events.Insert(2, moved);
                break;
            case "record-hash-changed": events[1]!["recordHash"] = events[0]!["recordHash"]!.DeepClone(); break;
            case "instant-regressed": events[3]!["occurredAt"] = "2020-01-01T00:00:00.000000Z"; break;
            case "head-changed": report["chain"]!["headSeq"] = 99; break;
        }

        Assert.Equal(ReportVerificationCode.Tampered, Verify(report).Code);
    }

    [Fact]
    public void RestartedChainOrOtherInstallation_IsDiscontinuous()
    {
        var accepted = Report();
        var restarted = new ReportBuilder(Installation)
            .Add("license.imported", Start, $$"""{"contentSha256":"{{ContentSha}}","licenseId":"lic_7f3c9a","revision":2,"slotId":"prd"}""")
            .Add("license.report_exported", Start.AddDays(1), $$"""{"challenge":"{{Challenge}}"}""");

        Assert.Equal(ReportVerificationCode.Discontinuous, Verify(restarted.Build(), previousSeq: accepted.HeadSeq - 1, previousHash: accepted.HeadHash).Code);
        Assert.Equal(ReportVerificationCode.Discontinuous, Verify(accepted.Build(), installation: Guid.NewGuid()).Code);
    }

    [Fact]
    public void ContinuationOfTheAcceptedReport_IsOk()
    {
        var builder = Report();
        var acceptedSeq = builder.HeadSeq;
        var acceptedHash = builder.HeadHash;
        builder.Add("license.report_exported", Start.AddDays(40), """{"challenge":"rnw-2028-next-0001"}""");

        Assert.Equal(ReportVerificationCode.Ok, Verify(builder.Build(), previousSeq: acceptedSeq, previousHash: acceptedHash, challenge: "rnw-2028-next-0001").Code);
    }

    [Fact]
    public void ImportWithoutIssuanceOrIssuanceWithoutImport_IsMismatch()
    {
        Assert.Equal(ReportVerificationCode.Mismatch, Verify(Report().Build(), issued: []).Code);
        Assert.Equal(ReportVerificationCode.Mismatch, Verify(Report().Build(), issued: [Issued, new IssuedLicense("lic_7f3c9a", 3, ContentSha)]).Code);
        Assert.Equal(ReportVerificationCode.Mismatch, Verify(Report().Build(), revocations: [4]).Code);
    }

    [Fact]
    public void ClockRegression_IsReportedWithoutBlocking()
    {
        var builder = new ReportBuilder(Installation)
            .Add("license.imported", Start, $$"""{"contentSha256":"{{ContentSha}}","licenseId":"lic_7f3c9a","revision":2,"slotId":"prd"}""")
            .Add("license.state_changed", Start.AddHours(-3), """{"from":"granted","reasons":["license_state_indeterminate"],"to":"indeterminate"}""")
            .Add("license.report_exported", Start.AddDays(1), $$"""{"challenge":"{{Challenge}}"}""");

        var result = Verify(builder.Build());

        Assert.Equal(ReportVerificationCode.Ok, result.Code);
        Assert.Equal([2L], result.ClockRegressions);
    }

    [Fact]
    public void OldChallenge_IsStale() => Assert.Equal(ReportVerificationCode.Stale, Verify(Report().Build(), challenge: "rnw-2026-old-0001").Code);

    [Fact]
    public void MissingAndLateDays_AreGapsThatDoNotBlock()
    {
        var builder = new ReportBuilder(Installation)
            .Add("license.imported", Start, $$"""{"contentSha256":"{{ContentSha}}","licenseId":"lic_7f3c9a","revision":2,"slotId":"prd"}""")
            .Add("capacity.daily_summary", Start.AddDays(1), Summary("2026-11-14", late: false))
            .Add("capacity.daily_summary", Start.AddDays(4), Summary("2026-11-17", late: true))
            .Add("license.report_exported", Start.AddDays(5), $$"""{"challenge":"{{Challenge}}"}""");

        var result = Verify(builder.Build());

        Assert.Equal(ReportVerificationCode.Ok, result.Code);
        Assert.Equal(
            [new DateOnly(2026, 11, 15), new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 17), new DateOnly(2026, 11, 18)],
            result.Gaps);
    }

    private static ReportBuilder Report() => new ReportBuilder(Installation)
        .Add("license.imported", Start, $$"""{"contentSha256":"{{ContentSha}}","licenseId":"lic_7f3c9a","revision":2,"slotId":"prd"}""", "actor-ref-1")
        .Add("host.linked", Start.AddMinutes(1), """{"environmentId":"e1000000-0000-4000-8000-000000000001","hostId":"a1000000-0000-4000-8000-000000000001"}""")
        .Add("capacity.daily_summary", Start.AddDays(1), Summary("2026-11-14", late: false))
        .Add("capacity.daily_summary", Start.AddDays(2), Summary("2026-11-15", late: false))
        .Add("license.report_exported", Start.AddDays(2).AddHours(1), $$"""{"challenge":"{{Challenge}}"}""", "actor-ref-1");

    private static string Summary(string date, bool late) =>
        $$"""{"date":"{{date}}","hosts":[],"late":{{(late ? "true" : "false")}},"peakNodeEquivalents":3,"unknownHosts":0,"vcpuBand":16}""";

    private static ReportVerificationResult Verify(
        JsonObject report,
        Guid? installation = null,
        long? previousSeq = null,
        string? previousHash = null,
        IReadOnlyList<IssuedLicense>? issued = null,
        IReadOnlyList<int>? revocations = null,
        string challenge = Challenge) =>
        LicensingReportVerifier.Verify(
            ReportBuilder.Bytes(report),
            new ReportVerificationContext(challenge, installation ?? Installation, previousSeq, previousHash, issued ?? [Issued], revocations ?? []));

    private static string Pad(string base64Url)
    {
        var text = base64Url.Replace('-', '+').Replace('_', '/');
        return text.PadRight(text.Length + ((4 - (text.Length % 4)) % 4), '=');
    }
}
