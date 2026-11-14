using System.Diagnostics;
using Dokpod.ControlPlane.Infrastructure.SqlFiles;
using Npgsql;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests.Admin;

/// <summary>
/// Spec 033 T007 (FR-008, D-009, D-013): arquivos de instalação do catálogo SQL contra PostgreSQL real, executados pelo
/// <c>psql</c> como o instalador fará. Os nomes de role e banco têm aspas e maiúsculas, e a senha tem apóstrofo, para provar
/// que nada é concatenado. A senha chega só por arquivo, nunca por <c>-v</c> nem por variável de ambiente. Exige
/// <c>DOKPOD_TEST_POSTGRES_CONNECTION</c> com uma role que crie roles e bancos e o <c>psql</c> no PATH.
/// </summary>
[Trait("Category", "Integration")]
[Collection(AgentCommandsPostgresCollection.Name)]
public sealed class InstallationSqlTests : IDisposable
{
    private readonly string suffix = Guid.NewGuid().ToString("N")[..8];
    private readonly string secrets = Directory.CreateTempSubdirectory("dokpod-033-secrets-").FullName;
    private readonly NpgsqlConnectionStringBuilder admin;
    private readonly string psql;

    public InstallationSqlTests()
    {
        var connection = Environment.GetEnvironmentVariable("DOKPOD_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) Assert.Skip("DOKPOD_TEST_POSTGRES_CONNECTION is not configured.");
        psql = FindPsql() ?? throw new InvalidOperationException("psql não encontrado no PATH; instale o cliente do PostgreSQL para estes testes.");
        admin = new NpgsqlConnectionStringBuilder(connection);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private string Owner => $"T033 Dono\"{suffix}";
    private string Runtime => $"t033_runtime_{suffix}";
    private string Database => $"T033 Banco\"{suffix}";
    private string OwnerCanary => "canario'033-dono-" + suffix;
    private string RuntimeCanary => "canario'033-runtime-" + suffix;

    [Fact]
    public async Task Bootstrap_CreatesRolesDatabaseAndGrants_AndIsIdempotent()
    {
        var ownerFile = WriteSecret("owner", OwnerCanary);
        var runtimeFile = WriteSecret("runtime", RuntimeCanary);
        try
        {
            for (var run = 0; run < 2; run++)
            {
                await RunAsync("Installation/bootstrap/01-create-roles.sql", admin.Database!,
                    ("owner_role", Owner), ("runtime_role", Runtime), ("owner_password_file", ownerFile), ("runtime_password_file", runtimeFile));
                await RunAsync("Installation/bootstrap/02-create-database.sql", admin.Database!, ("database", Database), ("owner_role", Owner));
                await CreateTableAsOwnerAsync();
                await RunAsync("Installation/bootstrap/03-grants.sql", admin.Database!, ("database", Database), ("owner_role", Owner), ("runtime_role", Runtime));
            }

            var owner = await PrivilegesAsync(Owner);
            Assert.Equal("t", owner["exists"]);
            Assert.Equal("t", owner["login"]);
            Assert.Equal("t", owner["schema_owner"]);
            Assert.Equal("t", owner["schema_create"]);
            foreach (var forbidden in new[] { "superuser", "createrole", "createdb", "replication", "bypassrls" })
            {
                Assert.Equal("f", owner[forbidden]);
            }

            var runtime = await PrivilegesAsync(Runtime);
            Assert.Equal("t", runtime["login"]);
            Assert.Equal("t", runtime["database_connect"]);
            // Os privilégios em tabelas vêm das migrations, concedidos ao grupo dokpod_runtime (T016).
            Assert.Equal("t", runtime["runtime_group_member"]);
            Assert.Equal("f", owner["runtime_group_member"]);
            Assert.Equal("f", runtime["schema_create"]);
            Assert.Equal("f", runtime["schema_owner"]);
            Assert.Equal("f", runtime["superuser"]);

            // A senha veio do arquivo: o verificador SCRAM existe e confere com a senha canário.
            await using var connection = await OpenAsync(admin.Database!);
            foreach (var (role, password) in new[] { (Owner, OwnerCanary), (Runtime, RuntimeCanary) })
            {
                await using var command = new NpgsqlCommand("SELECT rolpassword FROM pg_authid WHERE rolname = @role", connection);
                command.Parameters.AddWithValue("role", role);
                var verifier = (string?)await command.ExecuteScalarAsync(Ct);
                Assert.StartsWith("SCRAM-SHA-256$", verifier, StringComparison.Ordinal);
                Assert.True(ScramVerifierMatches(verifier!, password), $"A senha de {role} não é a do arquivo.");
            }
        }
        finally
        {
            await DropAsync();
        }
    }

    [Fact]
    public async Task RolePrivileges_ReportsAMissingRoleAndASuperuser()
    {
        var missing = await PrivilegesAsync($"t033_ausente_{suffix}");
        Assert.Equal("f", missing["exists"]);
        Assert.Equal("f", missing["database_connect"]);

        var superuser = await PrivilegesAsync(admin.Username!);
        Assert.Equal("t", superuser["superuser"]);
    }

    [Fact]
    public async Task ServerVersion_IsANumber()
    {
        var output = await RunAsync("Installation/checks/server-version.sql", admin.Database!);

        Assert.True(int.TryParse(output.Trim(), out var version) && version >= 170000, $"Versão inesperada: {output}");
    }

    [Fact]
    public async Task ConferenceCounts_ListsAuditAndIdentities()
    {
        await using var connection = await OpenAsync(admin.Database!);
        await using var command = new NpgsqlCommand(SqlCatalog.Read("Installation/status/conference-counts.sql"), connection);
        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(Ct);
        while (await reader.ReadAsync(Ct))
        {
            names.Add(reader.GetString(0));
            Assert.True(reader.GetInt64(1) >= 0);
        }

        Assert.Equal(["agent_identities", "audit_events"], names);
    }

    [Fact]
    public async Task PsqlArguments_NeverCarryTheSecret()
    {
        var ownerFile = WriteSecret("owner", OwnerCanary);
        var start = PsqlStart("Installation/bootstrap/01-create-roles.sql", admin.Database!,
            ("owner_role", Owner), ("runtime_role", Runtime), ("owner_password_file", ownerFile), ("runtime_password_file", ownerFile));

        Assert.DoesNotContain(start.ArgumentList, argument => argument.Contains(OwnerCanary, StringComparison.Ordinal));
        Assert.DoesNotContain(start.Environment.Values, value => value?.Contains(OwnerCanary, StringComparison.Ordinal) == true);
        await Task.CompletedTask;
    }

    private async Task CreateTableAsOwnerAsync()
    {
        await using var connection = await OpenAsync(Database);
        await using var command = new NpgsqlCommand(
            $"SET ROLE {Quote(Owner)}; CREATE TABLE IF NOT EXISTS dokpod.t033_exemplo (id integer PRIMARY KEY); RESET ROLE;", connection);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private async Task<Dictionary<string, string>> PrivilegesAsync(string role)
    {
        var database = await DatabaseExistsAsync() ? Database : admin.Database!;
        var output = await RunAsync("Installation/checks/role-privileges.sql", database, ("role", role), ("database", database));
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('|'))
            .ToDictionary(parts => parts[0], parts => parts[1]);
    }

    private async Task<bool> DatabaseExistsAsync()
    {
        await using var connection = await OpenAsync(admin.Database!);
        await using var command = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name)", connection);
        command.Parameters.AddWithValue("name", Database);
        return (bool)(await command.ExecuteScalarAsync(Ct))!;
    }

    private async Task<string> RunAsync(string path, string database, params (string Name, string Value)[] variables)
    {
        using var process = Process.Start(PsqlStart(path, database, variables))!;
        var output = process.StandardOutput.ReadToEndAsync(Ct);
        var error = process.StandardError.ReadToEndAsync(Ct);
        await process.WaitForExitAsync(Ct);
        Assert.True(process.ExitCode == 0, $"psql {path} saiu com {process.ExitCode}: {await error}");
        return await output;
    }

    private ProcessStartInfo PsqlStart(string path, string database, params (string Name, string Value)[] variables)
    {
        var file = Path.Combine(secrets, Path.GetFileName(path));
        File.WriteAllText(file, SqlCatalog.Read(path));
        var start = new ProcessStartInfo(psql) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in new[] { "-X", "-q", "-At", "-F", "|", "-h", admin.Host!, "-p", admin.Port.ToString(), "-U", admin.Username!, "-d", database })
        {
            start.ArgumentList.Add(argument);
        }

        foreach (var (name, value) in variables)
        {
            start.ArgumentList.Add("-v");
            start.ArgumentList.Add($"{name}={value}");
        }

        start.ArgumentList.Add("-f");
        start.ArgumentList.Add(file);
        start.Environment.Remove("PGPASSWORD");
        return start;
    }

    private async Task<NpgsqlConnection> OpenAsync(string database)
    {
        var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(admin.ConnectionString) { Database = database, SearchPath = null }.ConnectionString);
        await connection.OpenAsync(Ct);
        return connection;
    }

    private async Task DropAsync()
    {
        await using var connection = await OpenAsync(admin.Database!);
        foreach (var sql in new[]
        {
            $"DROP DATABASE IF EXISTS {Quote(Database)} WITH (FORCE)",
            $"DROP ROLE IF EXISTS {Quote(Runtime)}",
            $"DROP ROLE IF EXISTS {Quote(Owner)}",
        })
        {
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(Ct);
        }
    }

    private string WriteSecret(string name, string value)
    {
        var path = Path.Combine(secrets, name + "-password");
        File.WriteAllText(path, value);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        return path;
    }

    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    private static string? FindPsql() =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(':')
            .Select(directory => Path.Combine(directory, "psql"))
            .FirstOrDefault(File.Exists);

    // RFC 5802/7677: StoredKey = H(HMAC(SaltedPassword, "Client Key")); o verificador é SCRAM-SHA-256$iter:salt$StoredKey:ServerKey.
    private static bool ScramVerifierMatches(string verifier, string password)
    {
        var parts = verifier.Split('$');
        var header = parts[1].Split(':');
        var keys = parts[2].Split(':');
        var salted = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
            System.Text.Encoding.UTF8.GetBytes(password), Convert.FromBase64String(header[1]), int.Parse(header[0], System.Globalization.CultureInfo.InvariantCulture),
            System.Security.Cryptography.HashAlgorithmName.SHA256, 32);
        var clientKey = System.Security.Cryptography.HMACSHA256.HashData(salted, "Client Key"u8);
        var storedKey = System.Security.Cryptography.SHA256.HashData(clientKey);
        return Convert.ToBase64String(storedKey) == keys[0];
    }

    public void Dispose() => Directory.Delete(secrets, recursive: true);
}
