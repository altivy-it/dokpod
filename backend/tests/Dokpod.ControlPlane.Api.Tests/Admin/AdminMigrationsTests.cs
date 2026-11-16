using System.Text.Json;
using Dokpod.ControlPlane.Api.Admin;
using Dokpod.ControlPlane.Infrastructure.SqlFiles;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests.Admin;

/// <summary>
/// Spec 033 T011 e T012 (FR-009, FR-010, FR-036 a FR-042, SC-010, SC-011): <c>admin migrations</c> contra PostgreSQL real,
/// com um banco novo por teste. O SQL exportado aplicado pelo <c>psql</c> e o <c>apply</c> chegam ao mesmo schema e ao
/// mesmo histórico (comparados pela impressão do schema, a mesma do <c>verify</c>).
/// </summary>
[Trait("Category", "Integration")]
[Collection(AgentCommandsPostgresCollection.Name)]
public sealed class AdminMigrationsTests
{
    private const string LastMigration = "202611080001_GrantRuntimeEnvironments";
    private const string ImageUpdate = "202611010002_ImageUpdateCheck";
    private const string Before = "202611010001_AuditExport";
    private const string LastUpFile = "Migrations/Script/202611010002_ImageUpdateCheck/Up/01-ImageUpdateCheck.sql";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EmptyDatabase_ExportedScriptAndApply_ReachTheSameSchemaAndHistory()
    {
        await using var byScript = await AdminTestDatabase.CreateAsync();
        await using var byApply = await AdminTestDatabase.CreateAsync();
        var known = await byScript.MigrationIdsAsync();

        var status = await byScript.RunAsync("migrations", "status");
        Assert.Equal(AdminExitCodes.Success, status.ExitCode);
        Assert.Empty(status.Strings("applied"));
        Assert.Equal(known, status.Strings("pending"));
        Assert.Equal(JsonValueKind.Null, status.Output.GetProperty("databaseVersion").ValueKind);

        var plan = await byScript.RunAsync("migrations", "plan");
        var planHash = plan.Output.GetProperty("planHash").GetString()!;
        Assert.Equal(known.Length, plan.Output.GetProperty("migrations").GetArrayLength());
        Assert.All(plan.Output.GetProperty("migrations").EnumerateArray(), migration => Assert.Matches("^[0-9a-f]{64}$", migration.GetProperty("sha256").GetString()));
        Assert.Contains("table environments", plan.Output.GetProperty("migrations").EnumerateArray()
            .SelectMany(migration => migration.GetProperty("objects").GetProperty("created").EnumerateArray().Select(item => item.GetString())));

        var file = Path.Combine(Path.GetTempPath(), $"dokpod-033-{Guid.NewGuid():N}.sql");
        try
        {
            var script = await byScript.RunAsync("migrations", "script", "--out", file);
            Assert.Equal(planHash, script.Output.GetProperty("planHash").GetString());
            var text = await File.ReadAllTextAsync(file, Ct);
            Assert.StartsWith("-- dokpod-migrations: 1\n", text, StringComparison.Ordinal);
            Assert.Contains($"-- plan-hash: {planHash}\n", text, StringComparison.Ordinal);
            Assert.Contains($"-- to: {LastMigration}\n", text, StringComparison.Ordinal);
            // Sem o controle de reexecução do --idempotent, que embrulha cada migration num bloco DO.
            Assert.DoesNotContain("IF NOT EXISTS(SELECT 1 FROM dokpod.\"__EFMigrationsHistory\"", text, StringComparison.Ordinal);

            var applied = await byScript.PsqlFileAsync(file);
            Assert.True(applied.ExitCode == 0, applied.Error);
            Assert.Equal(AdminExitCodes.Success, (await byApply.RunAsync("migrations", "apply", "--confirm", planHash)).ExitCode);

            var scripted = await byScript.RunAsync("migrations", "fingerprint");
            var migrated = await byApply.RunAsync("migrations", "fingerprint");
            Assert.Equal(migrated.Output.GetProperty("sha256").GetString(), scripted.Output.GetProperty("sha256").GetString());
            Assert.Equal(SchemaFingerprint.Expected().Sha256, scripted.Output.GetProperty("sha256").GetString());
            Assert.Equal(await byApply.HistoryAsync(), await byScript.HistoryAsync());

            var verified = await byScript.RunAsync("migrations", "verify", "--script", file);
            Assert.Equal(AdminExitCodes.Success, verified.ExitCode);
            Assert.True(verified.Output.GetProperty("ok").GetBoolean());

            // Reaplicar o arquivo falha na chave primária do histórico, dentro da transação, sem mudar nada.
            var again = await byScript.PsqlFileAsync(file);
            Assert.NotEqual(0, again.ExitCode);
            Assert.Equal(await byApply.HistoryAsync(), await byScript.HistoryAsync());

            // Um bloco alterado no arquivo entregue aparece em fileHashMismatch.
            var tampered = text.Replace("CREATE TABLE dokpod.image_update_schedules (", "CREATE TABLE dokpod.image_update_schedules_x (", StringComparison.Ordinal);
            Assert.NotEqual(text, tampered);
            await File.WriteAllTextAsync(file, tampered, Ct);
            var mismatch = await byScript.RunAsync("migrations", "verify", "--script", file);
            Assert.Equal(AdminExitCodes.MigrationDivergence, mismatch.ExitCode);
            Assert.Equal([ImageUpdate], mismatch.Strings("fileHashMismatch"));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task BeforeDatabase_AppliesOnlyWithThePlanHashItShowed()
    {
        await using var database = await AdminTestDatabase.CreateAsync();
        await database.MigrateToAsync(Before);

        var status = await database.RunAsync("migrations", "status");
        Assert.Equal([ImageUpdate, LastMigration], status.Strings("pending"));
        Assert.Equal(Before, status.Output.GetProperty("databaseVersion").GetString());
        var plan = await database.RunAsync("migrations", "plan");
        Assert.Equal(2, plan.Output.GetProperty("migrations").GetArrayLength());
        var migration = plan.Output.GetProperty("migrations")[0];
        Assert.False(migration.GetProperty("overridden").GetBoolean());
        Assert.Contains("table dokpod.image_update_checks", migration.GetProperty("objects").GetProperty("created").EnumerateArray().Select(item => item.GetString()));

        var wrong = await database.RunAsync("migrations", "apply", "--confirm", new string('0', 64));
        Assert.Equal(AdminExitCodes.MigrationDivergence, wrong.ExitCode);
        Assert.Equal([ImageUpdate, LastMigration], (await database.RunAsync("migrations", "status")).Strings("pending"));

        var applied = await database.RunAsync("migrations", "apply", "--confirm", plan.Output.GetProperty("planHash").GetString()!);
        Assert.Equal(AdminExitCodes.Success, applied.ExitCode);
        Assert.Equal([ImageUpdate, LastMigration], applied.Strings("applied"));
        Assert.Equal(AdminExitCodes.Success, (await database.RunAsync("migrations", "verify")).ExitCode);
    }

    [Fact]
    public async Task DatabaseWithAnUnknownMigration_IsRefused()
    {
        await using var database = await AdminTestDatabase.CreateAsync();
        await database.MigrateAllAsync();
        await database.ExecuteAsync("""INSERT INTO dokpod."__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('209901010001_Futura', '10.0.0')""");

        var status = await database.RunAsync("migrations", "status");
        Assert.Equal(["209901010001_Futura"], status.Strings("newerThanTarget"));
        Assert.Equal(AdminExitCodes.MigrationDivergence, (await database.RunAsync("migrations", "apply", "--confirm", new string('0', 64))).ExitCode);
        Assert.Equal(AdminExitCodes.MigrationDivergence, (await database.RunAsync("migrations", "script", "--out", Path.Combine(Path.GetTempPath(), "nunca.sql"))).ExitCode);
        var verify = await database.RunAsync("migrations", "verify");
        Assert.Equal(AdminExitCodes.MigrationDivergence, verify.ExitCode);
        Assert.Equal(["209901010001_Futura"], verify.Strings("unexpected"));
    }

    [Theory]
    [InlineData("ALTER TABLE dokpod.environments ADD COLUMN t033_extra integer", "ALTER TABLE dokpod.environments DROP COLUMN t033_extra", "column", "environments.t033_extra")]
    [InlineData("CREATE INDEX ix_t033_extra ON dokpod.environments (name)", "DROP INDEX dokpod.ix_t033_extra", "index", "environments.ix_t033_extra")]
    [InlineData("ALTER TABLE dokpod.environments ADD CONSTRAINT ck_t033_extra CHECK (length(name) < 1000)", "ALTER TABLE dokpod.environments DROP CONSTRAINT ck_t033_extra", "constraint", "environments.ck_t033_extra")]
    [InlineData("CREATE FUNCTION dokpod.t033_extra() RETURNS integer LANGUAGE sql AS 'SELECT 1'", "DROP FUNCTION dokpod.t033_extra()", "function", "t033_extra()")]
    [InlineData("CREATE TRIGGER t033_extra BEFORE UPDATE ON dokpod.environments FOR EACH ROW EXECUTE FUNCTION suppress_redundant_updates_trigger()", "DROP TRIGGER t033_extra ON dokpod.environments", "trigger", "environments.t033_extra")]
    public async Task ManualSchemaChange_IsDetectedWithoutTheScript(string change, string revert, string kind, string name)
    {
        await using var database = await AdminTestDatabase.CreateAsync();
        await database.MigrateAllAsync();
        Assert.Equal(AdminExitCodes.Success, (await database.RunAsync("migrations", "verify")).ExitCode);

        await database.ExecuteAsync(change);
        var changed = await database.RunAsync("migrations", "verify");
        Assert.Equal(AdminExitCodes.MigrationDivergence, changed.ExitCode);
        Assert.False(changed.Output.GetProperty("schemaAccepted").GetBoolean());
        Assert.Contains(changed.Output.GetProperty("schemaDiff").EnumerateArray(),
            item => item.GetProperty("kind").GetString() == kind && item.GetProperty("name").GetString() == name);

        await database.ExecuteAsync(revert);
        Assert.Equal(AdminExitCodes.Success, (await database.RunAsync("migrations", "verify")).ExitCode);
    }

    [Fact]
    public async Task PartitionsCreatedAtRuntime_DoNotChangeTheFingerprint()
    {
        await using var database = await AdminTestDatabase.CreateAsync();
        await database.MigrateAllAsync();

        await database.ExecuteAsync("SELECT dokpod.dokpod_ensure_monthly_partitions(DATE '2039-06-01')");

        Assert.Equal(AdminExitCodes.Success, (await database.RunAsync("migrations", "verify")).ExitCode);
    }

    [Fact]
    public async Task OverriddenMigration_IsShownApplied_AndOnlyTheRecordedFingerprintIsAccepted()
    {
        await using var database = await AdminTestDatabase.CreateAsync();
        await database.MigrateToAsync(Before);
        var root = Directory.CreateTempSubdirectory("dokpod-033-override-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, SqlCatalogManifest.FileName), SqlCatalog.Manifest.ToJson());
            var altered = SqlCatalog.Read(LastUpFile) + "\nCREATE INDEX ix_t033_dba ON dokpod.image_update_checks (environment_id);\n";
            var path = Path.Combine(root, LastUpFile);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, altered);
            database.SqlOverrideDirectory = root;

            var plan = await database.RunAsync("migrations", "plan");
            var migrations = plan.Output.GetProperty("migrations").EnumerateArray().ToArray();
            var migration = Assert.Single(migrations, item => item.GetProperty("id").GetString() == ImageUpdate);
            Assert.True(migration.GetProperty("overridden").GetBoolean());
            Assert.False(Assert.Single(migrations, item => item.GetProperty("id").GetString() == LastMigration).GetProperty("overridden").GetBoolean());
            var use = Assert.Single(migration.GetProperty("overrides").EnumerateArray());
            Assert.Equal(LastUpFile, use.GetProperty("path").GetString());
            Assert.Equal(SqlCatalog.Sha256Hex(altered), use.GetProperty("appliedSha256").GetString());
            Assert.Equal(AdminExitCodes.Success, (await database.RunAsync("migrations", "apply", "--confirm", plan.Output.GetProperty("planHash").GetString()!)).ExitCode);

            var refused = await database.RunAsync("migrations", "verify");
            Assert.Equal(AdminExitCodes.MigrationDivergence, refused.ExitCode);
            Assert.Contains(refused.Output.GetProperty("schemaDiff").EnumerateArray(), item => item.GetProperty("name").GetString() == "image_update_checks.ix_t033_dba");

            var fingerprint = (await database.RunAsync("migrations", "fingerprint")).Output.GetProperty("sha256").GetString()!;
            var accepted = Path.Combine(root, "..", $"dokpod-033-accepted-{Guid.NewGuid():N}.json");
            await File.WriteAllTextAsync(accepted, JsonSerializer.Serialize(new[] { fingerprint }), Ct);
            try
            {
                var verified = await database.RunAsync("migrations", "verify", "--accepted-fingerprints", accepted);
                Assert.Equal(AdminExitCodes.Success, verified.ExitCode);
                Assert.True(verified.Output.GetProperty("schemaAccepted").GetBoolean());
            }
            finally
            {
                File.Delete(accepted);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Usage_IsReportedWithCodeTwo()
    {
        await using var database = await AdminTestDatabase.CreateAsync();

        Assert.Equal(AdminExitCodes.Usage, (await database.RunAsync("migrations", "status", "--connection", "Host=x")).ExitCode);
        Assert.Equal(AdminExitCodes.Usage, (await database.RunAsync("migrations", "apply")).ExitCode);
        Assert.Equal(AdminExitCodes.Usage, (await database.RunAsync("migrations", "desconhecido")).ExitCode);
        Assert.Equal(AdminExitCodes.Success, (await database.RunAsync("--help")).ExitCode);

        var noConnection = new AdminContext(Path.GetTempPath() + "/dokpod-033-inexistente", "/nao-existe", new StringWriter(), new StringWriter());
        Assert.Equal(AdminExitCodes.Usage, await AdminCommandHost.RunAsync(["migrations", "status"], noConnection, Ct));
    }
}
