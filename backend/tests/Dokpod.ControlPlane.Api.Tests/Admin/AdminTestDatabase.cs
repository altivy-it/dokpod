using System.Diagnostics;
using System.Text.Json;
using Dokpod.ControlPlane.Api.Admin;
using Dokpod.ControlPlane.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests.Admin;

/// <summary>
/// Spec 033: banco PostgreSQL próprio de um teste do modo administrativo, criado vazio a partir de
/// <c>DOKPOD_TEST_POSTGRES_CONNECTION</c> e apagado no fim. A conexão vai para um diretório de segredos temporário, como o
/// instalador monta em <c>/run/secrets</c>.
/// </summary>
internal sealed class AdminTestDatabase : IAsyncDisposable
{
    private readonly NpgsqlConnectionStringBuilder admin;
    private readonly string secrets = Directory.CreateTempSubdirectory("dokpod-033-admin-secrets-").FullName;

    private AdminTestDatabase(NpgsqlConnectionStringBuilder admin, string name)
    {
        this.admin = admin;
        Name = name;
        ConnectionString = new NpgsqlConnectionStringBuilder(admin.ConnectionString) { Database = name }.ConnectionString;
        File.WriteAllText(Path.Combine(secrets, AdminContext.ConnectionFile), ConnectionString);
    }

    public string Name { get; }

    public string ConnectionString { get; }

    public string SqlOverrideDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "dokpod-033-sem-sobreposicao");

    public string Edition { get; set; } = Api.ProductEdition.Current;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<AdminTestDatabase> CreateAsync()
    {
        var connection = Environment.GetEnvironmentVariable("DOKPOD_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) Assert.Skip("DOKPOD_TEST_POSTGRES_CONNECTION is not configured.");
        var admin = new NpgsqlConnectionStringBuilder(connection);
        var database = new AdminTestDatabase(admin, $"dokpod_t033_{Guid.NewGuid():N}"[..28]);
        await database.AdminExecuteAsync($"CREATE DATABASE \"{database.Name}\"");
        return database;
    }

    public async Task<AdminRun> RunAsync(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var context = new AdminContext(secrets, SqlOverrideDirectory, output, error) { Edition = Edition };

        var exitCode = await AdminCommandHost.RunAsync(args, context, Ct);
        var text = output.ToString().Trim();
        return new AdminRun(exitCode, text.StartsWith('{') ? JsonDocument.Parse(text).RootElement.Clone() : null, error.ToString());
    }

    /// <summary>Leva o banco até a migration indicada pelo próprio EF, como uma instalação de versão anterior.</summary>
    public async Task MigrateToAsync(string migration)
    {
        await using var context = ControlPlaneDbContextFactory.Create(ConnectionString);
        var ids = context.Database.GetMigrations().TakeWhile(id => id != migration).Append(migration).ToArray();
        await Dokpod.ControlPlane.Infrastructure.Migrations.MigrationScripts.ApplyAsync(
            context, Dokpod.ControlPlane.Infrastructure.SqlFiles.SqlCatalog.Read("Installation/migrations/session.sql"),
            Dokpod.ControlPlane.Infrastructure.Migrations.MigrationScripts.Generate(context, ids, fromEmptyDatabase: true), Ct);
    }

    public async Task MigrateAllAsync()
    {
        await using var context = ControlPlaneDbContextFactory.Create(ConnectionString);
        await context.Database.MigrateAsync(Ct);
    }

    public async Task<string[]> MigrationIdsAsync()
    {
        await using var context = ControlPlaneDbContextFactory.Create(ConnectionString);
        return context.Database.GetMigrations().ToArray();
    }

    public async Task<string[]> HistoryAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("SELECT \"MigrationId\" FROM dokpod.\"__EFMigrationsHistory\" ORDER BY 1", connection);
        var ids = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(Ct);
        while (await reader.ReadAsync(Ct)) ids.Add(reader.GetString(0));
        return ids.ToArray();
    }

    public async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }

    public async Task<T?> ScalarAsync<T>(string sql, params NpgsqlParameter[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        return (T?)await command.ExecuteScalarAsync(Ct);
    }

    /// <summary>Aplica um arquivo com o <c>psql</c>, como o DBA faria; devolve o código de saída.</summary>
    public async Task<(int ExitCode, string Error)> PsqlFileAsync(string file)
    {
        var psql = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(':')
            .Select(directory => Path.Combine(directory, "psql")).FirstOrDefault(File.Exists)
            ?? throw new InvalidOperationException("psql não encontrado no PATH; instale o cliente do PostgreSQL para estes testes.");
        var start = new ProcessStartInfo(psql) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in new[] { "-X", "-q", "-v", "ON_ERROR_STOP=1", "-h", admin.Host!, "-p", admin.Port.ToString(), "-U", admin.Username!, "-d", Name, "-f", file })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        _ = process.StandardOutput.ReadToEndAsync(Ct);
        var error = await process.StandardError.ReadToEndAsync(Ct);
        await process.WaitForExitAsync(Ct);
        return (process.ExitCode, error);
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await AdminExecuteAsync($"DROP DATABASE IF EXISTS \"{Name}\" WITH (FORCE)");
        Directory.Delete(secrets, recursive: true);
    }

    private async Task AdminExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(admin.ConnectionString) { SearchPath = null }.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }
}

internal sealed record AdminRun(int ExitCode, JsonElement? Json, string Error)
{
    public JsonElement Output => Json ?? throw new InvalidOperationException($"Sem saída JSON (código {ExitCode}): {Error}");

    public string[] Strings(string property) => Output.GetProperty(property).EnumerateArray().Select(item => item.GetString()!).ToArray();
}
