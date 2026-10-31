using System.Security.Claims;
using Dokpod.ControlPlane.Api.Licensing;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Licensing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using Xunit;
using static Dokpod.ControlPlane.Api.Tests.Licensing.LicensingPostgres;
using static Dokpod.ControlPlane.Api.Tests.Licensing.LicensingUs4Fixture;

namespace Dokpod.ControlPlane.Api.Tests.Licensing;

/// <summary>
/// Spec 031 T069 (threat model L2): <c>POST /api/v1/licensing/trusted-instant</c> só tira de <c>indeterminate</c> por
/// relógio, regrava o instante com o relógio atual, não cria tolerância e fica na cadeia com a justificativa.
/// </summary>
[Trait("Category", "Integration")]
[Collection(LicensingPostgresCollection.Name)]
public sealed class TrustedInstantResetTests
{
    [Fact]
    public async Task Reset_LeavesIndeterminateOnlyOnceWithIfMatchAndReason()
    {
        using var fixture = new LicensingUs4Fixture(ConnectionStringOrSkip());
        await fixture.ResetClockAsync();
        var now = DateTimeOffset.UtcNow;
        Assert.Equal(LicenseImportOutcome.Accepted, (await fixture.ImportAsync(fixture.License($"lic_{Guid.NewGuid():N}", now.AddDays(-1), now.AddDays(365)))).Outcome);

        try
        {
            await using (var connection = await OpenAsync(fixture.ConnectionString, Ct))
            {
                // Relógio adiantado por engano: o instante confiável ficou um dia à frente.
                await ExecuteAsync(connection, null, "UPDATE dokpod.licensing_state SET last_trusted_instant_utc = now() + interval '1 day'", Ct);
            }

            var version = await VersionAsync(fixture);
            Assert.Equal((400, "invalid_reason"), await ResetAsync(fixture, "curta", $"\"{version}\""));
            Assert.Equal((412, "license_revision_conflict"), await ResetAsync(fixture, "relógio do host adiantado por engano", $"\"{version + 7}\""));
            Assert.Equal((200, null), await ResetAsync(fixture, "relógio do host adiantado por engano", $"\"{version}\""));

            await using (var services = fixture.Create())
            {
                var status = await services.Status.GetAsync(Ct);
                Assert.NotEqual("indeterminate", status.Entitlement);
                Assert.NotEqual("expiry", status.Grace?.Cause);
                Assert.Null((await fixture.DeadlinesAsync()).ExpiryUntil);
            }

            Assert.Equal((409, "license_not_indeterminate"), await ResetAsync(fixture, "relógio do host adiantado por engano", $"\"{version + 1}\""));
            await using var check = await OpenAsync(fixture.ConnectionString, Ct);
            Assert.Equal("relógio do host adiantado por engano", await ScalarAsync<string>(
                check, null,
                "SELECT details->>'reason' FROM dokpod.licensing_events WHERE action = 'license.trusted_instant_reset' ORDER BY chain_seq DESC LIMIT 1",
                Ct));
        }
        finally
        {
            await fixture.ResetClockAsync();
        }
    }

    private static async Task<long> VersionAsync(LicensingUs4Fixture fixture)
    {
        await using var connection = await OpenAsync(fixture.ConnectionString, Ct);
        return await ScalarAsync<long>(connection, null, "SELECT version FROM dokpod.licensing_state", Ct);
    }

    private static async Task<(int Status, string? Code)> ResetAsync(LicensingUs4Fixture fixture, string reason, string ifMatch)
    {
        await using var services = fixture.Create();
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user-us4")], "test"));
        context.Request.Headers.Authorization = "Bearer test-token";
        context.Request.Headers.IfMatch = ifMatch;
        context.Request.Path = "/api/v1/licensing/trusted-instant";

        var result = await LicensingEndpoints.ResetTrustedInstantAsync(
            new TrustedInstantResetRequest(reason), context, new AllowAll(), services.Status, services.Licenses, services.Evaluation, TimeProvider.System, Ct);
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
