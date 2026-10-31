using System.Text.RegularExpressions;
using Dokpod.ControlPlane.Api.Licensing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests.Licensing;

/// <summary>Spec 031 T012: códigos do contrato, status por categoria e nunca 401 (FR-024).</summary>
public sealed partial class LicensingProblemsTests
{
    [Fact]
    public void Codes_MatchTheOpenApiEnumAndNeverAuthenticate()
    {
        var contract = File.ReadAllText(LicensingPostgres.RepositoryFile("contracts/openapi/dokpod-control-plane.v1.yaml"));
        var contractCodes = ContractEnum().Match(contract).Groups["codes"].Value
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(contractCodes.Order(StringComparer.Ordinal), LicensingProblems.StatusByCode.Keys.Order(StringComparer.Ordinal));
        Assert.DoesNotContain(StatusCodes.Status401Unauthorized, LicensingProblems.StatusByCode.Values);
    }

    [Theory]
    [InlineData(LicensingProblems.LicenseExpired, StatusCodes.Status403Forbidden)]
    [InlineData(LicensingProblems.AuthorizationDenied, StatusCodes.Status403Forbidden)]
    [InlineData(LicensingProblems.LicenseInvalid, StatusCodes.Status422UnprocessableEntity)]
    [InlineData(LicensingProblems.LicenseRevisionConflict, StatusCodes.Status412PreconditionFailed)]
    [InlineData(LicensingProblems.PayloadTooLarge, StatusCodes.Status413PayloadTooLarge)]
    [InlineData(LicensingProblems.RateLimited, StatusCodes.Status429TooManyRequests)]
    public void Create_ReturnsProblemWithStatusAndCode(string code, int status)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/licensing/license";

        var problem = Assert.IsType<ProblemHttpResult>(LicensingProblems.Create(context, code, "detalhe"));

        Assert.Equal(status, problem.StatusCode);
        Assert.Equal(code, problem.ProblemDetails.Extensions["code"]);
    }

    [Fact]
    public void Create_RefusesUnknownCode()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LicensingProblems.Create(new DefaultHttpContext(), "license_unknown", "detalhe"));
    }

    [GeneratedRegex(@"LicensingProblemCode:\s*\n(?:.*\n)*?\s*enum: \[(?<codes>[^\]]+)\]")]
    private static partial Regex ContractEnum();
}
