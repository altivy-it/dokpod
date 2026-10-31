using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Dokpod.ControlPlane.Api.Licensing;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Licensing;
using Dokpod.ControlPlane.Infrastructure.Licensing;
using Dokpod.Licensing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Xunit;
using static Dokpod.ControlPlane.Api.Tests.Licensing.LicensingPostgres;

namespace Dokpod.ControlPlane.Api.Tests.Licensing;

/// <summary>
/// Spec 031 T021: <c>/api/v1/licensing</c> com PostgreSQL real. Os handlers são chamados como o roteamento chama,
/// com um decisor de autorização simulado; o antiforgery é validado no BFF (docs/bff.md), não aqui.
/// </summary>
[Trait("Category", "Integration")]
[Collection(LicensingPostgresCollection.Name)]
public sealed class LicensingEndpointsTests : IDisposable
{
    private readonly ECDsa signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly TrustStore trustStore;

    public LicensingEndpointsTests() => trustStore = new TrustStore([TrustedKey.FromPublicKey("dokpod-test-1", signingKey)]);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        trustStore.Dispose();
        signingKey.Dispose();
    }

    [Fact]
    public async Task Import_AcceptsLicenseAndStatusReturnsItWithETag()
    {
        var connectionString = ConnectionStringOrSkip();
        var version = await CurrentVersionAsync(connectionString);
        var licenseId = NewLicenseId();

        var import = await ImportAsync(connectionString, License(licenseId), ifMatch: $"\"{version}\"");
        var status = await StatusAsync(connectionString);

        Assert.Equal(StatusCodes.Status200OK, import.Status);
        Assert.Equal($"\"{version + 1}\"", import.ETag);
        Assert.Equal(StatusCodes.Status200OK, status.Status);
        Assert.Equal($"\"{version + 1}\"", status.ETag);
    }

    [Fact]
    public async Task Import_RequiresIfMatchAndSlotId()
    {
        var connectionString = ConnectionStringOrSkip();

        var noIfMatch = await ImportAsync(connectionString, License(NewLicenseId()), ifMatch: null);
        var noSlot = await ImportAsync(connectionString, License(NewLicenseId()), ifMatch: "\"0\"", slotId: null);

        Assert.Equal((StatusCodes.Status400BadRequest, "if_match_required"), (noIfMatch.Status, noIfMatch.Code));
        Assert.Equal((StatusCodes.Status400BadRequest, "invalid_slot"), (noSlot.Status, noSlot.Code));
    }

    [Fact]
    public async Task Import_LargerThan64KiB_Is413()
    {
        var connectionString = ConnectionStringOrSkip();
        var body = new byte[JwsCompactReader.MaxDocumentBytes + 1];
        Array.Fill(body, (byte)'A');

        var declared = await ImportAsync(connectionString, body, ifMatch: "\"0\"");
        var undeclared = await ImportAsync(connectionString, body, ifMatch: "\"0\"", declareLength: false);

        Assert.Equal((StatusCodes.Status413PayloadTooLarge, "payload_too_large"), (declared.Status, declared.Code));
        Assert.Equal((StatusCodes.Status413PayloadTooLarge, "payload_too_large"), (undeclared.Status, undeclared.Code));
    }

    [Fact]
    public async Task WithoutScopes_ImportAndStatusAreDeniedEvenWithValidLicense()
    {
        var connectionString = ConnectionStringOrSkip();
        var version = await CurrentVersionAsync(connectionString);

        var import = await ImportAsync(connectionString, License(NewLicenseId()), ifMatch: $"\"{version}\"", allowedScopes: [LicensingAuthorization.ReadScope]);
        var status = await StatusAsync(connectionString, allowedScopes: [LicensingAuthorization.ManageScope]);

        Assert.Equal((StatusCodes.Status403Forbidden, "authorization_denied"), (import.Status, import.Code));
        Assert.Equal((StatusCodes.Status403Forbidden, "authorization_denied"), (status.Status, status.Code));
        Assert.Equal(version, await CurrentVersionAsync(connectionString));
    }

    [Fact]
    public async Task ConcurrentImports_WithSameIfMatch_OneWinsOneConflicts()
    {
        var connectionString = ConnectionStringOrSkip();
        var version = await CurrentVersionAsync(connectionString);
        var first = License(NewLicenseId());
        var second = License(NewLicenseId());

        var results = await Task.WhenAll(
            ImportAsync(connectionString, first, ifMatch: $"\"{version}\"", subject: "user-a"),
            ImportAsync(connectionString, second, ifMatch: $"\"{version}\"", subject: "user-b"));

        Assert.Equal([StatusCodes.Status200OK, StatusCodes.Status412PreconditionFailed], results.Select(r => r.Status).Order());
        Assert.Equal("license_revision_conflict", results.Single(r => r.Status == StatusCodes.Status412PreconditionFailed).Code);
        Assert.Equal(version + 1, await CurrentVersionAsync(connectionString));
    }

    [Fact]
    public async Task MoreThanTenUploadsPerMinute_AreRateLimited()
    {
        var connectionString = ConnectionStringOrSkip();
        using var limiter = new LicensingUploadRateLimiter();
        var subject = $"user-{Guid.NewGuid():N}";
        var statuses = new List<int>();

        for (var attempt = 0; attempt <= LicensingUploadRateLimiter.PermitsPerMinute; attempt++)
        {
            statuses.Add((await ImportAsync(connectionString, [1], ifMatch: null, subject: subject, limiter: limiter)).Status);
        }

        Assert.All(statuses.Take(LicensingUploadRateLimiter.PermitsPerMinute), status => Assert.Equal(StatusCodes.Status400BadRequest, status));
        Assert.Equal(StatusCodes.Status429TooManyRequests, statuses[^1]);
    }

    [Fact]
    public async Task TamperedLicense_Is422AndKeepsTheActiveLicense()
    {
        var connectionString = ConnectionStringOrSkip();
        var version = await CurrentVersionAsync(connectionString);
        var tampered = License(NewLicenseId());
        tampered[^5] = tampered[^5] == (byte)'A' ? (byte)'B' : (byte)'A';

        var result = await ImportAsync(connectionString, tampered, ifMatch: $"\"{version}\"");

        Assert.Equal((StatusCodes.Status422UnprocessableEntity, "license_invalid"), (result.Status, result.Code));
        Assert.DoesNotContain("eyJ", result.Detail ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal(version, await CurrentVersionAsync(connectionString));
    }

    private async Task<(int Status, string? Code, string? Detail, string? ETag)> ImportAsync(
        string connectionString,
        byte[] body,
        string? ifMatch,
        string? slotId = "prd",
        string subject = "user-sub-1",
        IReadOnlyCollection<string>? allowedScopes = null,
        LicensingUploadRateLimiter? limiter = null,
        bool declareLength = true)
    {
        await using var dbContext = CreateContext(connectionString);
        var store = new LicenseStore(dbContext, new LicensingAuditWriter(dbContext));
        var service = new LicenseImportService(store, store, new LicenseStateEvaluator(trustStore), trustStore, TimeProvider.System);
        var context = Context(subject);
        context.Request.Method = "PUT";
        if (slotId is not null) context.Request.QueryString = new QueryString($"?slotId={slotId}");
        if (ifMatch is not null) context.Request.Headers.IfMatch = ifMatch;
        context.Request.Body = new MemoryStream(body);
        if (declareLength) context.Request.ContentLength = body.Length;

        using var ownLimiter = limiter is null ? new LicensingUploadRateLimiter() : null;
        var result = await LicensingEndpoints.ImportAsync(context, new Decider(allowedScopes ?? LicensingAuthorization.Scopes), service, limiter ?? ownLimiter!, Ct);
        return Describe(context, result);
    }

    private async Task<(int Status, string? Code, string? Detail, string? ETag)> StatusAsync(string connectionString, IReadOnlyCollection<string>? allowedScopes = null)
    {
        await using var dbContext = CreateContext(connectionString);
        var store = new LicenseStore(dbContext, new LicensingAuditWriter(dbContext));
        var service = new LicenseStatusService(store, store, new LicenseStateEvaluator(trustStore), TimeProvider.System);
        var context = Context("user-sub-1");
        var result = await LicensingEndpoints.StatusAsync(context, new Decider(allowedScopes ?? LicensingAuthorization.Scopes), service, Ct);
        return Describe(context, result);
    }

    private static (int Status, string? Code, string? Detail, string? ETag) Describe(HttpContext context, IResult result)
    {
        var etag = context.Response.Headers.ETag.ToString();
        return result switch
        {
            ProblemHttpResult problem => (problem.StatusCode, problem.ProblemDetails.Extensions["code"]?.ToString(), problem.ProblemDetails.Detail, null),
            IStatusCodeHttpResult status => (status.StatusCode ?? 0, null, null, etag),
            _ => throw new InvalidOperationException($"Unexpected result {result.GetType().Name}."),
        };
    }

    private static DefaultHttpContext Context(string subject)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", subject)], "test"));
        context.Request.Headers.Authorization = "Bearer test-token";
        context.Request.Path = "/api/v1/licensing/license";
        return context;
    }

    private static async Task<long> CurrentVersionAsync(string connectionString)
    {
        await using var connection = await OpenAsync(connectionString, Ct);
        return await ScalarAsync<long>(connection, null, "SELECT COALESCE((SELECT version FROM dokpod.licensing_state), 0)", Ct);
    }

    private static string NewLicenseId() => $"lic_{Guid.NewGuid():N}";

    private byte[] License(string licenseId)
    {
        var payload = $$"""
            {"schemaVersion":1,"meteringVersion":1,"licenseId":"{{licenseId}}","organizationId":"org_51d2e0","revision":1,
            "product":"dokpod-business","plan":"business","issuedAt":"2026-11-01T00:00:00Z","notBefore":"2026-11-01T00:00:00Z",
            "expiresAt":"2036-11-01T00:00:00Z","capacity":{"mode":"limited","nodeEquivalents":10},"vcpuBand":32,"windowDays":30,
            "graceDays":30,"slots":[{"slotId":"prd","kind":"production","enabled":true,"nodeEquivalents":8}],"capabilities":[]}
            """;
        var input = $"{Base64Url.EncodeToString("""{"alg":"ES256","kid":"dokpod-test-1","typ":"dokpod-license+jws"}"""u8)}.{Base64Url.EncodeToString(Encoding.UTF8.GetBytes(payload))}";
        var signature = signingKey.SignData(Encoding.ASCII.GetBytes(input), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return Encoding.ASCII.GetBytes($"{input}.{Base64Url.EncodeToString(signature)}");
    }

    private sealed class Decider(IReadOnlyCollection<string> allowedScopes) : IEnvironmentAuthorizationDecider
    {
        public Task<EnvironmentAuthorizationDecision> DecideAsync(
            string resource,
            string scope,
            AuthenticatedActor actor,
            string accessToken,
            Guid correlationId,
            CancellationToken cancellationToken)
        {
            Assert.Equal(LicensingAuthorization.Resource, resource);
            return Task.FromResult(new EnvironmentAuthorizationDecision(
                allowedScopes.Contains(scope) ? AuthorizationDecisionOutcome.Allowed : AuthorizationDecisionOutcome.Denied,
                null));
        }
    }
}
