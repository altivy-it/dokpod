using System.Diagnostics;
using Dokpod.ControlPlane.Api.Admin;
using Dokpod.ControlPlane.Application.Commands;
using Dokpod.ControlPlane.Application.Environments;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.ControlPlane.Infrastructure.SqlFiles;
using Dokpod.Domain.Auditing;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Inventory;
using Npgsql;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests.Admin;

/// <summary>
/// Spec 033 T016 (D-009): o banco montado como o instalador fará. Primeiro o bootstrap do catálogo pelo <c>psql</c>, com a
/// credencial administrativa; depois as migrations pelo modo administrativo, com a role dona (sem SUPERUSER, CREATEROLE
/// nem CREATEDB); por fim a role de runtime, membro do grupo <c>dokpod_runtime</c>, faz o trabalho da aplicação sem DDL e
/// sem burlar a auditoria.
/// </summary>
[Trait("Category", "Integration")]
[Collection(AgentCommandsPostgresCollection.Name)]
public sealed class RuntimeRolePrivilegesTests : IAsyncDisposable
{
    private readonly string suffix = Guid.NewGuid().ToString("N")[..8];
    private readonly string secrets = Directory.CreateTempSubdirectory("dokpod-033-runtime-").FullName;
    private readonly NpgsqlConnectionStringBuilder admin;

    public RuntimeRolePrivilegesTests()
    {
        var connection = Environment.GetEnvironmentVariable("DOKPOD_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) Assert.Skip("DOKPOD_TEST_POSTGRES_CONNECTION is not configured.");
        admin = new NpgsqlConnectionStringBuilder(connection);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private string Owner => $"t033_owner_{suffix}";
    private string Runtime => $"t033_app_{suffix}";
    private string Database => $"dokpod_t033rt_{suffix}";
    private string OwnerCanary => "canario-033-owner-" + suffix;
    private string RuntimeCanary => "canario-033-app-" + suffix;

    [Fact]
    public async Task OwnerMigrates_AndTheRuntimeRoleWorksWithoutDdl()
    {
        var ownerFile = Write("owner-password", OwnerCanary);
        var runtimeFile = Write("runtime-password", RuntimeCanary);
        await PsqlAsync("Installation/bootstrap/01-create-roles.sql", admin.Database!,
            ("owner_role", Owner), ("runtime_role", Runtime), ("owner_password_file", ownerFile), ("runtime_password_file", runtimeFile));
        await PsqlAsync("Installation/bootstrap/02-create-database.sql", admin.Database!, ("database", Database), ("owner_role", Owner));

        // Migrations com a role dona, pelo modo administrativo.
        File.WriteAllText(Path.Combine(secrets, AdminContext.ConnectionFile), Connection(Owner, OwnerCanary));
        var context = new AdminContext(secrets, Path.Combine(secrets, "sem-sobreposicao"), new StringWriter(), new StringWriter());
        var plan = new StringWriter();
        Assert.Equal(AdminExitCodes.Success, await AdminCommandHost.RunAsync(["migrations", "plan"], context with { Out = plan }, Ct));
        var planHash = System.Text.Json.JsonDocument.Parse(plan.ToString()).RootElement.GetProperty("planHash").GetString()!;
        var errors = new StringWriter();
        Assert.True(
            await AdminCommandHost.RunAsync(["migrations", "apply", "--confirm", planHash], context with { Error = errors }, Ct) == AdminExitCodes.Success,
            errors.ToString());
        Assert.Equal(AdminExitCodes.Success, await AdminCommandHost.RunAsync(["migrations", "verify"], context, Ct));
        await PsqlAsync("Installation/bootstrap/03-grants.sql", admin.Database!, ("database", Database), ("owner_role", Owner), ("runtime_role", Runtime));

        await using var runtime = ControlPlaneDbContextFactory.Create(Connection(Runtime, RuntimeCanary));
        var environmentId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        Assert.Equal(EnvironmentRegistrationStoreResult.Created, await new PostgresEnvironmentRegistrationStore(runtime).CreateAsync(
            EnvironmentRegistration.Create(environmentId, "host-runtime", "host-runtime.lab", true, EnvironmentResourceScopes.Supported), Ct));
        await new PostgresAuditEventWriter(runtime).AppendAsync(
            AuditEvent.Create(Guid.NewGuid(), now, Guid.NewGuid(), AuditActorKind.System, "installer", "environment.register", environmentId, AuditOutcome.Succeeded), Ct);
        // O registro da identidade é do modo administrativo (role dona); o runtime só consulta e revoga.
        await using (var owner = ControlPlaneDbContextFactory.Create(Connection(Owner, OwnerCanary)))
        {
            Assert.Equal(Application.Agents.AgentIdentityEnrollmentOutcome.Created,
                await new PostgresAgentIdentityRegistry(owner).RegisterAsync(environmentId, new string('C', 64), Ct));
        }

        var identities = new PostgresAgentIdentityRegistry(runtime);
        Assert.NotNull(await identities.FindByFingerprintAsync(new string('C', 64), Ct));
        await new PostgresInventoryProjectionStore(runtime).ReplaceSnapshotAsync(new InventorySnapshot(environmentId, 1,
            new Dictionary<string, ContainerInventory> { [new string('a', 64)] = new(new string('a', 64), "svc", "registry.lab/app:1.0", "running", "r", now) }), Ct);
        var command = new PersistedAgentCommand(
            new AgentCommand(environmentId, Guid.NewGuid(), AgentCommandKind.RestartContainer, new string('a', 64), "revision-01", new string('A', 64), now.AddMinutes(1), 1),
            ControlPlaneCommandState.Pending, now, now);
        Assert.Equal(AgentCommandEnqueueResult.Created, await new PostgresAgentCommandStore(runtime).EnqueueAsync(command, Ct));
        // Retenção temporal: função SECURITY DEFINER com EXECUTE para o grupo dokpod_runtime.
        await new PostgresOperationalEventStore(runtime).ApplyRetentionAsync(TimeSpan.FromDays(30), Ct);
        await new PostgresHostMetricsStore(runtime).ApplyRetentionAsync(TimeSpan.FromDays(30), Ct);
        Assert.True(await identities.RevokeEnvironmentAsync(environmentId, now, Ct));

        // Sem DDL e sem burlar a auditoria (só a função de append grava).
        await AssertDeniedAsync("CREATE TABLE dokpod.t033_nao (id integer)");
        await AssertDeniedAsync("DELETE FROM dokpod.audit_events");
        await AssertDeniedAsync("DROP TABLE dokpod.environments");
        await AssertDeniedAsync("SELECT dokpod.dokpod_ensure_monthly_partitions(DATE '2039-06-01')");
        await AssertDeniedAsync($"INSERT INTO dokpod.agent_identities (certificate_fingerprint, environment_id) VALUES ('{new string('D', 64)}', '{environmentId}')");
    }

    private async Task AssertDeniedAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(Connection(Runtime, RuntimeCanary));
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(Ct));
        Assert.True(error.SqlState is "42501" or "42809", $"{sql}: {error.SqlState} {error.MessageText}");
    }

    // A credencial vai por passfile, como o instalador entrega (PGPASSFILE), e não pela connection string.
    private string Connection(string role, string canary)
    {
        var passfile = Path.Combine(secrets, $"pgpass-{role}");
        File.WriteAllText(passfile, $"*:*:*:{role}:{canary}\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(passfile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        var builder = new NpgsqlConnectionStringBuilder(admin.ConnectionString) { Database = Database, Username = role, Passfile = passfile, SearchPath = "dokpod" };
        builder.Remove("Password"); // a da role administrativa, quando o CI usa scram, venceria o passfile
        return builder.ConnectionString;
    }

    private async Task PsqlAsync(string path, string database, params (string Name, string Value)[] variables)
    {
        var psql = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(':')
            .Select(directory => Path.Combine(directory, "psql")).FirstOrDefault(File.Exists)
            ?? throw new InvalidOperationException("psql não encontrado no PATH; instale o cliente do PostgreSQL para estes testes.");
        var file = Write(Path.GetFileName(path), SqlCatalog.Read(path));
        var start = new ProcessStartInfo(psql) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in new[] { "-X", "-q", "-h", admin.Host!, "-p", admin.Port.ToString(), "-U", admin.Username!, "-d", database })
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
        using var process = Process.Start(start)!;
        _ = process.StandardOutput.ReadToEndAsync(Ct);
        var error = await process.StandardError.ReadToEndAsync(Ct);
        await process.WaitForExitAsync(Ct);
        Assert.True(process.ExitCode == 0, $"psql {path}: {error}");
    }

    private string Write(string name, string content)
    {
        var path = Path.Combine(secrets, name);
        File.WriteAllText(path, content);
        return path;
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(admin.ConnectionString) { SearchPath = null }.ConnectionString);
        await connection.OpenAsync(Ct);
        foreach (var sql in new[] { $"DROP DATABASE IF EXISTS \"{Database}\" WITH (FORCE)", $"DROP ROLE IF EXISTS \"{Runtime}\"", $"DROP ROLE IF EXISTS \"{Owner}\"" })
        {
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(Ct);
        }

        Directory.Delete(secrets, recursive: true);
    }
}
