using System.Security.Claims;
using Dokpod.ControlPlane.Api.Licensing;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Licensing;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.ControlPlane.Infrastructure.Licensing;
using Dokpod.Licensing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using Xunit;
using static Dokpod.ControlPlane.Api.Tests.Licensing.LicensingPostgres;

namespace Dokpod.ControlPlane.Api.Tests.Licensing;

/// <summary>Spec 031 T034 (FR-012, FR-025): hosts de licenciamento com PostgreSQL real.</summary>
[Trait("Category", "Integration")]
[Collection(LicensingPostgresCollection.Name)]
public sealed class LicensingHostEndpointsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Writes_RequireLicensingManageAndReadsRequireLicensingRead()
    {
        var connectionString = ConnectionStringOrSkip();
        var hostId = Guid.NewGuid();
        var readOnly = new[] { LicensingAuthorization.ReadScope };

        Assert.Equal(StatusCodes.Status403Forbidden, (await Run(connectionString, readOnly, (c, d, s) => LicensingHostEndpoints.CreateAsync(hostId, new("rack-1"), c, d, s, Ct))).Status);
        Assert.Equal(StatusCodes.Status403Forbidden, (await Run(connectionString, readOnly, (c, d, s) => LicensingHostEndpoints.LinkAsync(hostId, Guid.NewGuid(), c, d, s, Ct))).Status);
        Assert.Equal(StatusCodes.Status403Forbidden, (await Run(connectionString, readOnly, (c, d, s) => LicensingHostEndpoints.UnlinkAsync(hostId, Guid.NewGuid(), c, d, s, Ct))).Status);
        Assert.Equal(StatusCodes.Status403Forbidden, (await Run(connectionString, readOnly, (c, d, s) => LicensingHostEndpoints.RemoveAsync(hostId, c, d, s, Ct))).Status);
        Assert.Equal(StatusCodes.Status403Forbidden, (await Run(connectionString, [LicensingAuthorization.ManageScope], (c, d, s) => LicensingHostEndpoints.ListAsync(c, d, s, Ct))).Status);
    }

    [Fact]
    public async Task Lifecycle_IsIdempotentAuditedAndKeepsOneActiveHostPerEnvironment()
    {
        var connectionString = ConnectionStringOrSkip();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var environment = Guid.NewGuid();
        var all = LicensingAuthorization.Scopes;

        Assert.Equal(StatusCodes.Status201Created, (await Run(connectionString, all, (c, d, s) => LicensingHostEndpoints.CreateAsync(first, new("rack-1"), c, d, s, Ct))).Status);
        Assert.Equal(StatusCodes.Status200OK, (await Run(connectionString, all, (c, d, s) => LicensingHostEndpoints.CreateAsync(first, new("rack-1"), c, d, s, Ct))).Status);
        Assert.Equal(StatusCodes.Status400BadRequest, (await Run(connectionString, all, (c, d, s) => LicensingHostEndpoints.CreateAsync(Guid.NewGuid(), new(""), c, d, s, Ct))).Status);
        await Run(connectionString, all, (c, d, s) => LicensingHostEndpoints.CreateAsync(second, new("rack-2"), c, d, s, Ct));

        Assert.Equal(StatusCodes.Status200OK, (await Run(connectionString, all, (c, d, s) => LicensingHostEndpoints.LinkAsync(first, environment, c, d, s, Ct))).Status);
        Assert.Equal(StatusCodes.Status200OK, (await Run(connectionString, all, (c, d, s) => LicensingHostEndpoints.LinkAsync(first, environment, c, d, s, Ct))).Status);
        Assert.Equal(StatusCodes.Status200OK, (await Run(connectionString, all, (c, d, s) => LicensingHostEndpoints.LinkAsync(second, environment, c, d, s, Ct))).Status);
        Assert.Equal(StatusCodes.Status404NotFound, (await Run(connectionString, all, (c, d, s) => LicensingHostEndpoints.LinkAsync(Guid.NewGuid(), environment, c, d, s, Ct))).Status);

        await using (var connection = await OpenAsync(connectionString, Ct))
        {
            Assert.Equal(second, await ScalarAsync<Guid>(connection, null, "SELECT host_id FROM dokpod.licensing_host_environments WHERE environment_id = @e AND unlinked_at_utc IS NULL", Ct, new NpgsqlParameter("e", environment)));
            Assert.Equal(1L, await ScalarAsync<long>(connection, null, "SELECT count(*) FROM dokpod.licensing_host_environments WHERE environment_id = @e AND unlinked_at_utc IS NULL", Ct, new NpgsqlParameter("e", environment)));
        }

        Assert.Equal(StatusCodes.Status204NoContent, (await Run(connectionString, all, (c, d, s) => LicensingHostEndpoints.RemoveAsync(second, c, d, s, Ct))).Status);
        Assert.Equal(StatusCodes.Status204NoContent, (await Run(connectionString, all, (c, d, s) => LicensingHostEndpoints.RemoveAsync(second, c, d, s, Ct))).Status);
        Assert.Equal(StatusCodes.Status409Conflict, (await Run(connectionString, all, (c, d, s) => LicensingHostEndpoints.LinkAsync(second, environment, c, d, s, Ct))).Status);
        Assert.Equal(StatusCodes.Status204NoContent, (await Run(connectionString, all, (c, d, s) => LicensingHostEndpoints.UnlinkAsync(first, environment, c, d, s, Ct))).Status);

        var actions = await ActionsAsync(connectionString, environment, [first, second]);
        Assert.Equal(
            ["host.linked:first", "host.unlinked:first", "host.linked:second", "host.unlinked:second", "host.removed:second"],
            actions.Select(item => $"{item.Action}:{(item.HostId == first ? "first" : "second")}"));

        var list = await Run(connectionString, all, (c, d, s) => LicensingHostEndpoints.ListAsync(c, d, s, Ct));
        Assert.Equal(StatusCodes.Status200OK, list.Status);
    }

    private static async Task<(int Status, string? Code)> Run(
        string connectionString,
        IReadOnlyCollection<string> scopes,
        Func<HttpContext, IEnvironmentAuthorizationDecider, LicensingHostService, Task<IResult>> handler)
    {
        await using var dbContext = CreateContext(connectionString);
        using var trustStore = new TrustStore([]);
        var writer = new LicensingAuditWriter(dbContext);
        var hosts = new LicensingHostStore(dbContext, writer);
        var licenses = new LicenseStore(dbContext, writer);
        var service = new LicensingHostService(hosts, new CapacityLedger(dbContext, hosts), licenses, licenses, new LicenseStateEvaluator(trustStore), TimeProvider.System);
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user-sub-1")], "test"));
        context.Request.Headers.Authorization = "Bearer test-token";
        context.Request.Path = "/api/v1/licensing/hosts";

        var result = await handler(context, new Decider(scopes), service);
        return result switch
        {
            ProblemHttpResult problem => (problem.StatusCode, problem.ProblemDetails.Extensions["code"]?.ToString()),
            IStatusCodeHttpResult status => (status.StatusCode ?? 0, null),
            _ => throw new InvalidOperationException(result.GetType().Name),
        };
    }

    private static async Task<List<(string Action, Guid HostId)>> ActionsAsync(string connectionString, Guid environment, Guid[] hosts)
    {
        await using var connection = await OpenAsync(connectionString, Ct);
        await using var command = new NpgsqlCommand(
            """
            SELECT action, (details->>'hostId')::uuid FROM dokpod.licensing_events
            WHERE (details->>'hostId')::uuid = ANY(@hosts) AND (details->>'environmentId' = @environment OR action = 'host.removed')
            ORDER BY chain_seq
            """,
            connection);
        command.Parameters.AddWithValue("hosts", hosts);
        command.Parameters.AddWithValue("environment", environment.ToString("D"));
        var actions = new List<(string, Guid)>();
        await using var reader = await command.ExecuteReaderAsync(Ct);
        while (await reader.ReadAsync(Ct)) actions.Add((reader.GetString(0), reader.GetGuid(1)));
        return actions;
    }

    private sealed class Decider(IReadOnlyCollection<string> allowedScopes) : IEnvironmentAuthorizationDecider
    {
        public Task<EnvironmentAuthorizationDecision> DecideAsync(string resource, string scope, AuthenticatedActor actor, string accessToken, Guid correlationId, CancellationToken cancellationToken) =>
            Task.FromResult(new EnvironmentAuthorizationDecision(allowedScopes.Contains(scope) ? AuthorizationDecisionOutcome.Allowed : AuthorizationDecisionOutcome.Denied, null));
    }
}
