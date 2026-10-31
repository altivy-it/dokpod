using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using Dokpod.ControlPlane.Api.Licensing;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Licensing;
using Dokpod.Licensing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
using static Dokpod.ControlPlane.Api.Tests.Licensing.LicensingPostgres;
using static Dokpod.ControlPlane.Api.Tests.Licensing.LicensingUs4Fixture;

namespace Dokpod.ControlPlane.Api.Tests.Licensing;

/// <summary>
/// Spec 031 T060 e T071 com PostgreSQL real: o relatório que a instalação exporta é aceito pelo verificador do
/// fornecedor, continua o anterior, e cada adulteração de SC-010 é recusada. Sem sujeito do Keycloak, chave HMAC
/// nem nome de host no documento.
/// </summary>
[Trait("Category", "Integration")]
[Collection(LicensingPostgresCollection.Name)]
public sealed class LicensingReportTests
{
    [Fact]
    public async Task ExportedReport_IsVerifiedContinuesAndRefusesTampering()
    {
        using var fixture = new LicensingUs4Fixture(ConnectionStringOrSkip());
        await fixture.ResetClockAsync();
        var now = DateTimeOffset.UtcNow;
        Assert.Equal(LicenseImportOutcome.Accepted, (await fixture.ImportAsync(fixture.License($"lic_{Guid.NewGuid():N}", now.AddDays(-1), now.AddDays(365)))).Outcome);
        await using (var services = fixture.Create())
        {
            await services.Ledger.OnAgentHelloAsync(Guid.NewGuid(), "nome-de-host-secreto", 32, CapacitySource.Engine, now, Ct);
        }

        Assert.Equal(400, (await ExportAsync(fixture, "curto")).Status);
        var first = await ExportAsync(fixture, "ctr-2026-report-0001");
        Assert.Equal(200, first.Status);
        var report = JsonNode.Parse(first.Body)!.AsObject();
        var context = Context(report, "ctr-2026-report-0001");

        var accepted = LicensingReportVerifier.Verify(Encoding.UTF8.GetBytes(first.Body), context);
        Assert.True(accepted.Code == ReportVerificationCode.Ok, string.Join(" | ", accepted.Findings.Take(5)));
        Assert.Equal("license.report_exported", report["events"]!.AsArray()[^1]!["action"]!.GetValue<string>());

        // Privacidade: nem sujeito do Keycloak, nem chave HMAC, nem nome de host.
        await using (var connection = await OpenAsync(fixture.ConnectionString, Ct))
        {
            var key = await ScalarAsync<byte[]>(connection, null, "SELECT actor_hmac_key FROM dokpod.licensing_state", Ct);
            Assert.DoesNotContain(Convert.ToHexStringLower(key!), first.Body, StringComparison.Ordinal);
            Assert.DoesNotContain(Convert.ToBase64String(key!), first.Body, StringComparison.Ordinal);
        }

        foreach (var forbidden in new[] { "user-us4", "user-report", "user-sub-1", "nome-de-host-secreto" })
        {
            Assert.DoesNotContain(forbidden, first.Body, StringComparison.Ordinal);
        }

        // Próxima auditoria: continua do último elo aceito.
        var second = await ExportAsync(fixture, "rnw-2027-report-0002");
        var next = Context(JsonNode.Parse(second.Body)!.AsObject(), "rnw-2027-report-0002") with
        {
            PreviousHeadSeq = accepted.HeadSeq,
            PreviousHeadHash = accepted.HeadHash,
        };
        Assert.Equal(ReportVerificationCode.Ok, LicensingReportVerifier.Verify(Encoding.UTF8.GetBytes(second.Body), next).Code);
        Assert.Equal(ReportVerificationCode.Discontinuous, LicensingReportVerifier.Verify(Encoding.UTF8.GetBytes(second.Body), next with { PreviousHeadHash = "AAAA" }).Code);
        Assert.Equal(ReportVerificationCode.Discontinuous, LicensingReportVerifier.Verify(Encoding.UTF8.GetBytes(second.Body), next with { InstallationId = Guid.NewGuid() }).Code);
        Assert.Equal(ReportVerificationCode.Stale, LicensingReportVerifier.Verify(Encoding.UTF8.GetBytes(first.Body), next with { PreviousHeadSeq = null }).Code);
        Assert.Equal(ReportVerificationCode.Mismatch, LicensingReportVerifier.Verify(Encoding.UTF8.GetBytes(second.Body), next with { IssuedLicenses = next.IssuedLicenses.Skip(1).ToArray() }).Code);

        // A cadeia é cortada no evento do relatório: eventos gravados depois (como o segundo relatório) não entram.
        await using (var services = fixture.Create())
        {
            var firstEventId = Guid.Parse(report["events"]!.AsArray()[^1]!["eventId"]!.GetValue<string>());
            var cut = await new Dokpod.ControlPlane.Infrastructure.Licensing.LicensingChainReader(services.Context).ReadChainAsync(firstEventId, Ct);
            Assert.Equal(accepted.HeadSeq, cut[^1].ChainSeq);
            Assert.Equal(firstEventId, cut[^1].EventId);
        }

        var tampered = JsonNode.Parse(second.Body)!.AsObject();
        tampered["events"]!.AsArray().RemoveAt(0);
        Assert.Equal(ReportVerificationCode.Tampered, LicensingReportVerifier.Verify(Encoding.UTF8.GetBytes(tampered.ToJsonString()), next).Code);
    }

    /// <summary>O que o fornecedor sabe: aqui, tudo o que a instalação de teste importou (o banco é compartilhado).</summary>
    private static ReportVerificationContext Context(JsonObject report, string challenge)
    {
        var events = report["events"]!.AsArray().Select(e => e!.AsObject()).ToArray();
        var licenses = events.Where(e => e["action"]!.GetValue<string>() == "license.imported")
            .Select(e => new IssuedLicense(e["details"]!["licenseId"]!.GetValue<string>(), e["details"]!["revision"]!.GetValue<int>(), e["details"]!["contentSha256"]!.GetValue<string>()))
            .Distinct()
            .ToArray();
        var revocations = events.Where(e => e["action"]!.GetValue<string>() == "revocation.imported").Select(e => e["details"]!["sequence"]!.GetValue<int>()).ToArray();
        return new ReportVerificationContext(challenge, Guid.Parse(report["installationId"]!.GetValue<string>()), null, null, licenses, revocations);
    }

    private static async Task<(int Status, string Body)> ExportAsync(LicensingUs4Fixture fixture, string challenge)
    {
        await using var services = fixture.Create();
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
        };
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user-report")], "test"));
        context.Request.Headers.Authorization = "Bearer test-token";
        context.Request.Path = "/api/v1/licensing/report";
        context.Request.QueryString = new QueryString($"?challenge={challenge}");
        context.Response.Body = new MemoryStream();

        var result = await LicensingEndpoints.ExportReportAsync(context, new AllowAll(), services.Reports, Ct);
        if (result is ProblemHttpResult problem) return (problem.StatusCode, string.Empty);
        await result.ExecuteAsync(context);
        return (context.Response.StatusCode, Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray()));
    }

    private sealed class AllowAll : IEnvironmentAuthorizationDecider
    {
        public Task<EnvironmentAuthorizationDecision> DecideAsync(string resource, string scope, AuthenticatedActor actor, string accessToken, Guid correlationId, CancellationToken cancellationToken) =>
            Task.FromResult(new EnvironmentAuthorizationDecision(AuthorizationDecisionOutcome.Allowed, null));
    }
}
