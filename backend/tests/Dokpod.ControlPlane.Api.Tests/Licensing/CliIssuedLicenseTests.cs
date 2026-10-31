using Dokpod.ControlPlane.Application.Licensing;
using Dokpod.Licensing;
using Xunit;
using static Dokpod.ControlPlane.Api.Tests.Licensing.LicensingPostgres;

namespace Dokpod.ControlPlane.Api.Tests.Licensing;

/// <summary>
/// Spec 031 T059 (quickstart, cenário 5, passo 5): o arquivo emitido pela CLI <c>dokpod-license-admin</c> é aceito pela
/// importação real da composição Business, com PostgreSQL. Roda só com o artefato e a chave pública informados:
/// <c>DOKPOD_CLI_ISSUED_LICENSE</c>, <c>DOKPOD_CLI_PUBLIC_KEY</c> (PEM) e <c>DOKPOD_CLI_KEY_ID</c>.
/// </summary>
[Trait("Category", "Integration")]
[Collection(LicensingPostgresCollection.Name)]
public sealed class CliIssuedLicenseTests
{
    [Fact]
    public async Task LicenseIssuedByTheCli_IsImportedByTheBusinessInstallation()
    {
        var license = Environment.GetEnvironmentVariable("DOKPOD_CLI_ISSUED_LICENSE");
        var publicKey = Environment.GetEnvironmentVariable("DOKPOD_CLI_PUBLIC_KEY");
        var keyId = Environment.GetEnvironmentVariable("DOKPOD_CLI_KEY_ID");
        if (string.IsNullOrWhiteSpace(license) || string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(keyId))
        {
            Assert.Skip("DOKPOD_CLI_ISSUED_LICENSE, DOKPOD_CLI_PUBLIC_KEY e DOKPOD_CLI_KEY_ID não configuradas.");
        }

        using var fixture = new LicensingUs4Fixture(ConnectionStringOrSkip(), TrustedKey.FromPublicKeyPem(keyId, File.ReadAllText(publicKey)));
        await fixture.ResetClockAsync();

        var result = await fixture.ImportAsync(File.ReadAllBytes(license));

        Assert.True(result.Outcome == LicenseImportOutcome.Accepted, result.ProblemCode);
        Assert.Equal("valid", result.Status!.Authenticity);
    }
}
