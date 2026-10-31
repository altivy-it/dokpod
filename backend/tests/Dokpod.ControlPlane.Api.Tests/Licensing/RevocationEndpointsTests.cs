using System.Security.Claims;
using Dokpod.ControlPlane.Api.Licensing;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Licensing;
using Dokpod.ControlPlane.Infrastructure.Licensing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using Xunit;
using static Dokpod.ControlPlane.Api.Tests.Licensing.LicensingPostgres;
using static Dokpod.ControlPlane.Api.Tests.Licensing.LicensingUs4Fixture;

namespace Dokpod.ControlPlane.Api.Tests.Licensing;

/// <summary>
/// Spec 031 T050 (História 6, D-011): pacote válido restringe só a Business da licença coberta; sequência que não cresce
/// e organização diferente são recusadas; a CE, o status e a renovação seguem funcionando.
/// </summary>
[Trait("Category", "Integration")]
[Collection(LicensingPostgresCollection.Name)]
public sealed class RevocationEndpointsTests
{
    [Fact]
    public async Task ValidPackage_RestrictsTheLicenseUntilARenewedRevision()
    {
        using var fixture = new LicensingUs4Fixture(ConnectionStringOrSkip());
        await fixture.ResetClockAsync();
        var now = DateTimeOffset.UtcNow;
        var licenseId = $"lic_{Guid.NewGuid():N}";
        Assert.Equal(LicenseImportOutcome.Accepted, (await fixture.ImportAsync(fixture.License(licenseId, now.AddDays(-1), now.AddDays(365)))).Outcome);
        // +2 para haver uma sequência menor e válida (≥ 1) a recusar depois.
        var sequence = await SequenceAsync(fixture) + 2;

        var accepted = await PostAsync(fixture, fixture.Revocation(sequence, licenseId));

        Assert.Equal(StatusCodes.Status200OK, accepted.Status);
        await using (var services = fixture.Create())
        {
            var status = await services.Status.GetAsync(Ct);
            Assert.Equal(("restricted", "revoked"), (status.Entitlement, status.Authenticity));
            Assert.Contains(LicenseReasons.Revoked, status.Reasons);
            Assert.Equal("license_revoked", (await new BusinessCapabilityGate(services.Status).EnsureAsync("change-windows", Ct)).DenialCode);
        }

        await using (var connection = await OpenAsync(fixture.ConnectionString, Ct))
        {
            Assert.Equal(1L, await ScalarAsync<long>(
                connection, null,
                "SELECT count(*) FROM dokpod.licensing_events WHERE action = 'revocation.imported' AND (details->>'sequence')::int = @s",
                Ct,
                new NpgsqlParameter("s", sequence)));
        }

        Assert.Equal((422, "license_obsolete"), await PostAsync(fixture, fixture.Revocation(sequence, licenseId)));
        Assert.Equal((422, "license_obsolete"), await PostAsync(fixture, fixture.Revocation(sequence - 1, licenseId)));

        // A renovação (revisão 2) não está coberta pelo pacote, que revoga até a revisão 1.
        Assert.Equal(LicenseImportOutcome.Accepted, (await fixture.ImportAsync(fixture.License(licenseId, now.AddDays(-1), now.AddDays(365), revision: 2))).Outcome);
        await using var renewed = fixture.Create();
        Assert.Equal("valid", (await renewed.Status.GetAsync(Ct)).Authenticity);
    }

    [Fact]
    public async Task PackageFromAnotherOrganization_IsRefusedAndChangesNothing()
    {
        using var fixture = new LicensingUs4Fixture(ConnectionStringOrSkip());
        await fixture.ResetClockAsync();
        var now = DateTimeOffset.UtcNow;
        var licenseId = $"lic_{Guid.NewGuid():N}";
        Assert.Equal(LicenseImportOutcome.Accepted, (await fixture.ImportAsync(fixture.License(licenseId, now.AddDays(-1), now.AddDays(365)))).Outcome);
        var sequence = await SequenceAsync(fixture);

        Assert.Equal((422, "license_invalid"), await PostAsync(fixture, fixture.Revocation(sequence + 1, licenseId, organizationId: "org_other")));
        Assert.Equal((422, "license_invalid"), await PostAsync(fixture, fixture.License(licenseId, now.AddDays(-1), now.AddDays(365))));

        Assert.Equal(sequence, await SequenceAsync(fixture));
        await using var services = fixture.Create();
        Assert.Equal("valid", (await services.Status.GetAsync(Ct)).Authenticity);
    }

    private static async Task<int> SequenceAsync(LicensingUs4Fixture fixture)
    {
        await using var connection = await OpenAsync(fixture.ConnectionString, Ct);
        return (int)await ScalarAsync<long>(connection, null, "SELECT revocation_sequence FROM dokpod.licensing_state", Ct);
    }

    private static async Task<(int Status, string? Code)> PostAsync(LicensingUs4Fixture fixture, byte[] body)
    {
        await using var services = fixture.Create();
        using var limiter = new LicensingUploadRateLimiter();
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user-us6")], "test"));
        context.Request.Headers.Authorization = "Bearer test-token";
        context.Request.Path = "/api/v1/licensing/revocations";
        context.Request.Body = new MemoryStream(body);
        context.Request.ContentLength = body.Length;

        var result = await LicensingEndpoints.ImportRevocationAsync(context, new AllowAll(), services.Revocations, limiter, Ct);
        return result switch
        {
            ProblemHttpResult problem => (problem.StatusCode, problem.ProblemDetails.Extensions["code"]?.ToString()),
            IStatusCodeHttpResult status => (status.StatusCode ?? 0, null),
            _ => throw new InvalidOperationException(result.GetType().Name),
        };
    }

    private sealed class AllowAll : IEnvironmentAuthorizationDecider
    {
        public Task<EnvironmentAuthorizationDecision> DecideAsync(string resource, string scope, AuthenticatedActor actor, string accessToken, Guid correlationId, CancellationToken cancellationToken) =>
            Task.FromResult(new EnvironmentAuthorizationDecision(AuthorizationDecisionOutcome.Allowed, null));
    }
}
