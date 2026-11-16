using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Dokpod.ControlPlane.Api.Admin;
using Dokpod.Licensing;
using Npgsql;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests.Admin;

/// <summary>
/// Spec 033 T014 (FR-024, FR-025, FR-031): <c>admin environments</c>, <c>admin agent-identities</c> e
/// <c>admin licensing import</c> contra PostgreSQL real, num banco migrado só para o teste.
/// </summary>
[Trait("Category", "Integration")]
[Collection(AgentCommandsPostgresCollection.Name)]
public sealed class AdminBootstrapTests
{
    private static readonly string Fingerprint = new('b', 64);

    [Fact]
    public async Task EnvironmentRegister_IsIdempotent_AuditedAsInstaller_AndRefusesDivergentData()
    {
        await using var database = await MigratedAsync();
        var id = Guid.NewGuid();

        var created = await RegisterEnvironmentAsync(database, id, "host-local");
        var again = await RegisterEnvironmentAsync(database, id, "host-local");
        var divergent = await RegisterEnvironmentAsync(database, id, "outro-nome");

        Assert.Equal(AdminExitCodes.Success, created.ExitCode);
        Assert.True(created.Output.GetProperty("created").GetBoolean());
        Assert.Equal(AdminExitCodes.Success, again.ExitCode);
        Assert.False(again.Output.GetProperty("created").GetBoolean());
        Assert.Equal(AdminExitCodes.IdentityConflict, divergent.ExitCode);
        Assert.Equal("production", await database.ScalarAsync<string>(
            "SELECT stage FROM dokpod.environments WHERE environment_id = @id", new NpgsqlParameter("id", id)));
        Assert.Equal(1L, await CountAsync(database, "environment.register", id, outcome: 0));
        Assert.Equal(1L, await CountAsync(database, "environment.register", id, outcome: 1));
        Assert.Equal("Ana Operadora", await database.ScalarAsync<string>(
            "SELECT actor_name FROM dokpod.audit_events WHERE environment_id = @id AND actor_id = 'installer' AND actor_kind = 2 LIMIT 1",
            new NpgsqlParameter("id", id)));
    }

    [Fact]
    public async Task AgentIdentity_RegisterIsIdempotent_RefusesAnotherEnvironment_AndRevokeUsesTheEndpointAction()
    {
        await using var database = await MigratedAsync();
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        await RegisterEnvironmentAsync(database, a, "host-a");
        await RegisterEnvironmentAsync(database, b, "host-b");

        var created = await RegisterIdentityAsync(database, a, Fingerprint);
        var again = await RegisterIdentityAsync(database, a, Fingerprint.ToUpperInvariant());
        var elsewhere = await RegisterIdentityAsync(database, b, Fingerprint);
        var revoked = await database.RunAsync("agent-identities", "revoke", "--environment", a.ToString(), "--actor", "Ana Operadora", "--reason", "host desativado");
        var afterRevoke = await RegisterIdentityAsync(database, a, Fingerprint);

        Assert.True(created.Output.GetProperty("created").GetBoolean());
        Assert.False(again.Output.GetProperty("created").GetBoolean());
        Assert.Equal(AdminExitCodes.IdentityConflict, elsewhere.ExitCode);
        Assert.Equal(AdminExitCodes.Success, revoked.ExitCode);
        Assert.True(revoked.Output.GetProperty("revoked").GetBoolean());
        Assert.Equal(AdminExitCodes.IdentityConflict, afterRevoke.ExitCode);
        Assert.Equal(Fingerprint.ToUpperInvariant(), await database.ScalarAsync<string>(
            "SELECT certificate_fingerprint FROM dokpod.agent_identities WHERE environment_id = @id", new NpgsqlParameter("id", a)));
        Assert.Equal(1L, await CountAsync(database, "environment.agent_identity_register", a, outcome: 0));
        Assert.Equal(1L, await CountAsync(database, "environment.agent_identity_register", b, outcome: 1));
        Assert.Equal(1L, await CountAsync(database, "environment.revoke", a, outcome: 0));
    }

    [Theory]
    [InlineData("--fingerprint", "abc")]
    [InlineData("--environment", "não-é-uuid")]
    public async Task AgentIdentity_InvalidArguments_AreUsageErrors(string option, string value)
    {
        await using var database = await MigratedAsync();
        var environment = Guid.NewGuid().ToString();
        var args = new List<string> { "agent-identities", "register", "--environment", environment, "--fingerprint", Fingerprint, "--actor", "Ana", "--reason", "teste" };
        args[args.IndexOf(option) + 1] = value;

        Assert.Equal(AdminExitCodes.Usage, (await database.RunAsync(args.ToArray())).ExitCode);
    }

    [Fact]
    public async Task AgentIdentity_ForAnUnknownEnvironment_IsAUsageError()
    {
        await using var database = await MigratedAsync();

        Assert.Equal(AdminExitCodes.Usage, (await RegisterIdentityAsync(database, Guid.NewGuid(), Fingerprint)).ExitCode);
    }

    [Fact]
    public async Task LicensingImport_DoesNotExistInTheCommunityEdition()
    {
        await using var database = await MigratedAsync();
        database.Edition = ProductEdition.Community;
        var file = Path.GetTempFileName();
        try
        {
            var result = await database.RunAsync("licensing", "import", "--file", file, "--slot", "prd", "--actor", "Ana", "--reason", "teste");

            Assert.Equal(AdminExitCodes.Usage, result.ExitCode);
            Assert.Contains("Edição sem licenciamento", result.Error, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task LicensingImport_InTheBusinessEdition_UsesTheSameValidations_AndIsIdempotent()
    {
        await using var database = await MigratedAsync();
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        database.Edition = ProductEdition.Business;
        var original = Dokpod.ControlPlane.Api.Licensing.LicensingAdminCommands.TrustStoreFactory;
        Dokpod.ControlPlane.Api.Licensing.LicensingAdminCommands.TrustStoreFactory = () => new TrustStore([TrustedKey.FromPublicKey("dokpod-test-1", signingKey)]);
        var file = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(file, License(signingKey, $"lic_{Guid.NewGuid():N}"), TestContext.Current.CancellationToken);
            var imported = await ImportAsync(database, file, "prd");
            var again = await ImportAsync(database, file, "prd");
            var unknownSlot = await ImportAsync(database, file, "hml");

            Assert.Equal(AdminExitCodes.Success, imported.ExitCode);
            Assert.True(imported.Output.GetProperty("imported").GetBoolean());
            Assert.Equal(AdminExitCodes.Success, again.ExitCode);
            Assert.False(again.Output.GetProperty("imported").GetBoolean());
            Assert.Equal(AdminExitCodes.Usage, unknownSlot.ExitCode);
            Assert.Equal("license_slot_unknown", unknownSlot.Output.GetProperty("problemCode").GetString());

            await File.WriteAllTextAsync(file, "isto não é uma licença", TestContext.Current.CancellationToken);
            var invalid = await ImportAsync(database, file, "prd");
            Assert.Equal("license_invalid", invalid.Output.GetProperty("problemCode").GetString());
        }
        finally
        {
            Dokpod.ControlPlane.Api.Licensing.LicensingAdminCommands.TrustStoreFactory = original;
            File.Delete(file);
        }
    }

    private static async Task<AdminTestDatabase> MigratedAsync()
    {
        var database = await AdminTestDatabase.CreateAsync();
        await database.MigrateAllAsync();
        return database;
    }

    private static Task<AdminRun> RegisterEnvironmentAsync(AdminTestDatabase database, Guid id, string name) =>
        database.RunAsync("environments", "register", "--id", id.ToString(), "--name", name, "--host", "host-local.lab", "--stage", "production",
            "--actor", "Ana Operadora", "--reason", "instalação inicial");

    private static Task<AdminRun> RegisterIdentityAsync(AdminTestDatabase database, Guid id, string fingerprint) =>
        database.RunAsync("agent-identities", "register", "--environment", id.ToString(), "--fingerprint", fingerprint,
            "--actor", "Ana Operadora", "--reason", "instalação inicial");

    private static Task<AdminRun> ImportAsync(AdminTestDatabase database, string file, string slot) =>
        database.RunAsync("licensing", "import", "--file", file, "--slot", slot, "--actor", "Ana Operadora", "--reason", "licença inicial");

    private static async Task<long> CountAsync(AdminTestDatabase database, string action, Guid environmentId, int outcome) =>
        await database.ScalarAsync<long>(
            "SELECT count(*) FROM dokpod.audit_events WHERE action = @action AND environment_id = @id AND outcome = @outcome AND actor_id = 'installer'",
            new NpgsqlParameter("action", action), new NpgsqlParameter("id", environmentId), new NpgsqlParameter("outcome", outcome));

    // Mesmo formato do LicensingEndpointsTests (spec 031): JWS compacto ES256 com kid de teste.
    private static byte[] License(ECDsa signingKey, string licenseId)
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
}
