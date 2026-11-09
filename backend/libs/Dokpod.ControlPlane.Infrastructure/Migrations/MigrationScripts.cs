using System.Reflection;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

namespace Dokpod.ControlPlane.Infrastructure.Migrations;

/// <summary>
/// Spec 033 (D-003): SQL de cada migration, no formato do script do EF sem <c>--idempotent</c> (transação própria e linha
/// do histórico), gerado pelo gerador de SQL do próprio EF a partir das operações da migration.
/// <para>
/// Não usa <c>IMigrator.GenerateScript(de, até)</c> nem <c>Migrate(alvo)</c>: o EF só reconhece um ID com 14 dígitos de
/// data (<c>yyyyMMddHHmmss_</c>), e os IDs deste projeto têm 12 (<c>yyyyMMddNNNN_</c>), então ele os trata como nome e não
/// os encontra. Por isso <c>dotnet ef migrations script &lt;de&gt;</c> também falha aqui.
/// </para>
/// </summary>
public static class MigrationScripts
{
    /// <summary>Um bloco por migration, na ordem; o primeiro de um banco vazio inclui a criação do histórico.</summary>
    public static IReadOnlyList<MigrationScript> Generate(ControlPlaneDbContext database, IReadOnlyList<string> migrationIds, bool fromEmptyDatabase)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(migrationIds);
        var assembly = database.GetService<IMigrationsAssembly>();
        var generator = database.GetService<IMigrationsSqlGenerator>();
        var history = database.GetService<IHistoryRepository>();
        var sqlHelper = database.GetService<ISqlGenerationHelper>();
        var provider = database.Database.ProviderName!;
        var productVersion = ProductVersion();

        var scripts = new List<MigrationScript>(migrationIds.Count);
        foreach (var id in migrationIds)
        {
            if (!assembly.Migrations.TryGetValue(id, out var type))
            {
                throw new InvalidOperationException($"A migration '{id}' não existe nesta versão.");
            }

            var migration = assembly.CreateMigration(type, provider);
            var builder = new StringBuilder();
            if (fromEmptyDatabase && scripts.Count == 0)
            {
                builder.Append(history.GetCreateIfNotExistsScript()).Append('\n');
            }

            builder.Append(sqlHelper.StartTransactionStatement).Append('\n');
            foreach (var command in generator.Generate(migration.UpOperations, null, MigrationsSqlGenerationOptions.Script))
            {
                builder.Append(command.CommandText.TrimEnd()).Append('\n');
            }

            builder.Append(history.GetInsertScript(new HistoryRow(id, productVersion)).TrimEnd()).Append('\n')
                .Append(sqlHelper.CommitTransactionStatement).Append('\n');
            scripts.Add(new MigrationScript(id, builder.ToString().Replace("\r\n", "\n", StringComparison.Ordinal)));
        }

        return scripts;
    }

    /// <summary>
    /// Executa os blocos na ordem, cada um na própria transação, exatamente como foram gerados e mostrados, depois do SQL de
    /// sessão do catálogo (fuso e schema), o mesmo que abre o arquivo exportado.
    /// </summary>
    public static async Task ApplyAsync(ControlPlaneDbContext database, string session, IReadOnlyList<MigrationScript> scripts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(scripts);
        var connection = database.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var script in scripts.Prepend(new MigrationScript("session", session)))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = script.Sql;
            command.CommandTimeout = 0;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static string ProductVersion()
    {
        var version = typeof(DbContext).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        var metadata = version.IndexOf('+', StringComparison.Ordinal);
        return metadata < 0 ? version : version[..metadata];
    }
}

public sealed record MigrationScript(string Id, string Sql);
