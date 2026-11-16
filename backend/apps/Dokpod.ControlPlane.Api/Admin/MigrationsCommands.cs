using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.ControlPlane.Infrastructure.SqlFiles;
using Microsoft.EntityFrameworkCore;
using Dokpod.ControlPlane.Infrastructure.Migrations;

namespace Dokpod.ControlPlane.Api.Admin;

/// <summary>
/// Spec 033 T012 (FR-009, FR-010, FR-036 a FR-042, D-003): migrations sem SDK e sob controle do usuário. O SQL de cada
/// migration é o que o EF gera sem <c>--idempotent</c>, a partir do catálogo (e da sobreposição, quando montada); o
/// <c>planHash</c> liga a confirmação do <c>apply</c> ao plano que o usuário viu; o <c>verify</c> confere o histórico e a
/// impressão do schema real, porque o histórico não guarda o SQL aplicado.
/// </summary>
internal static partial class MigrationsCommands
{
    public const string BeginMarker = "-- dokpod-migration-begin: ";
    public const string EndMarker = "-- dokpod-migration-end: ";
    public const string SessionPath = "Installation/migrations/session.sql";

    public static Task<int> RunAsync(string command, AdminArguments arguments, ControlPlaneDbContext database, AdminContext context, CancellationToken ct) =>
        command switch
        {
            "status" => StatusAsync(arguments, database, context, ct),
            "plan" => PlanAsync(arguments, database, context, ct),
            "script" => ScriptAsync(arguments, database, context, ct),
            "apply" => ApplyAsync(arguments, database, context, ct),
            "fingerprint" => FingerprintAsync(arguments, database, context, ct),
            "verify" => VerifyAsync(arguments, database, context, ct),
            _ => AdminCommandHost.UsageAsync(context, $"Comando desconhecido: migrations {command}."),
        };

    private static async Task<int> StatusAsync(AdminArguments arguments, ControlPlaneDbContext database, AdminContext context, CancellationToken ct)
    {
        arguments.OnlyAllow();
        var state = await MigrationState.ReadAsync(database, ct).ConfigureAwait(false);
        return await AdminCommandHost.WriteAsync(context, new
        {
            databaseVersion = state.Applied.LastOrDefault(),
            targetVersion = state.Known[^1],
            applied = state.Applied,
            pending = state.Pending,
            newerThanTarget = state.Unknown,
        }).ConfigureAwait(false);
    }

    private static async Task<int> PlanAsync(AdminArguments arguments, ControlPlaneDbContext database, AdminContext context, CancellationToken ct)
    {
        arguments.OnlyAllow();
        var state = await MigrationState.ReadAsync(database, ct).ConfigureAwait(false);
        var plan = MigrationPlan.Build(database, state);
        return await AdminCommandHost.WriteAsync(context, new
        {
            from = state.Applied.LastOrDefault(),
            to = state.Known[^1],
            migrations = plan.Migrations.Select(migration => new
            {
                id = migration.Id,
                sha256 = migration.Sha256,
                overridden = migration.Overrides.Count > 0,
                overrides = migration.Overrides.Select(use => new { path = use.Path, originalSha256 = use.OriginalSha256, appliedSha256 = use.AppliedSha256 }),
                objects = migration.Objects,
            }),
            planHash = plan.Hash,
            newerThanTarget = state.Unknown,
        }, state.Unknown.Count > 0 ? AdminExitCodes.MigrationDivergence : AdminExitCodes.Success).ConfigureAwait(false);
    }

    private static async Task<int> ScriptAsync(AdminArguments arguments, ControlPlaneDbContext database, AdminContext context, CancellationToken ct)
    {
        arguments.OnlyAllow("out");
        var output = Path.GetFullPath(arguments.Required("out"));
        var state = await MigrationState.ReadAsync(database, ct).ConfigureAwait(false);
        if (state.Unknown.Count > 0)
        {
            return await DivergenceAsync(context, state).ConfigureAwait(false);
        }

        var plan = MigrationPlan.Build(database, state);
        var script = new StringBuilder()
            .Append("-- dokpod-migrations: 1\n")
            .Append(CultureInfo.InvariantCulture, $"-- product: dokpod {SqlCatalog.Manifest.Version}\n")
            .Append(CultureInfo.InvariantCulture, $"-- from: {state.Applied.LastOrDefault() ?? string.Empty}\n")
            .Append(CultureInfo.InvariantCulture, $"-- to: {state.Known[^1]}\n")
            .Append(CultureInfo.InvariantCulture, $"-- plan-hash: {plan.Hash}\n");
        foreach (var migration in plan.Migrations)
        {
            script.Append(CultureInfo.InvariantCulture, $"-- migration: {migration.Id} sha256={migration.Sha256}\n");
            foreach (var use in migration.Overrides)
            {
                script.Append(CultureInfo.InvariantCulture, $"-- override: {use.Path} original={use.OriginalSha256} applied={use.AppliedSha256}\n");
            }
        }

        script.Append(CultureInfo.InvariantCulture, $"-- generated-at: {DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ssZ}\n")
            .Append("-- Aplicar com: psql -v ON_ERROR_STOP=1 -f <arquivo>  (cada migration tem a própria transação)\n\n")
            .Append(SqlCatalog.Read(SessionPath));
        foreach (var migration in plan.Migrations)
        {
            script.Append('\n').Append(BeginMarker).Append(migration.Id).Append(" sha256=").Append(migration.Sha256).Append('\n')
                .Append(migration.Sql)
                .Append(EndMarker).Append(migration.Id).Append('\n');
        }

        var content = script.ToString();
        await File.WriteAllTextAsync(output, content, new UTF8Encoding(false), ct).ConfigureAwait(false);
        return await AdminCommandHost.WriteAsync(context, new { path = output, sha256 = SqlCatalog.Sha256Hex(content), planHash = plan.Hash }).ConfigureAwait(false);
    }

    private static async Task<int> ApplyAsync(AdminArguments arguments, ControlPlaneDbContext database, AdminContext context, CancellationToken ct)
    {
        arguments.OnlyAllow("confirm");
        var confirmed = arguments.Required("confirm");
        var state = await MigrationState.ReadAsync(database, ct).ConfigureAwait(false);
        if (state.Unknown.Count > 0)
        {
            return await DivergenceAsync(context, state).ConfigureAwait(false);
        }

        var plan = MigrationPlan.Build(database, state);
        if (!string.Equals(confirmed, plan.Hash, StringComparison.Ordinal))
        {
            await context.Error.WriteLineAsync("O planHash confirmado não é o do plano atual; rode 'migrations plan' de novo e confira.").ConfigureAwait(false);
            return await AdminCommandHost.WriteAsync(context, new { applied = Array.Empty<string>(), planHash = plan.Hash },
                AdminExitCodes.MigrationDivergence).ConfigureAwait(false);
        }

        var started = TimeProvider.System.GetTimestamp();
        // Executa exatamente os blocos do plano confirmado, na ordem, cada um na própria transação.
        await MigrationScripts.ApplyAsync(database, SqlCatalog.Read(SessionPath),
            plan.Migrations.Select(migration => new MigrationScript(migration.Id, migration.Sql)).ToArray(), ct).ConfigureAwait(false);

        return await AdminCommandHost.WriteAsync(context, new
        {
            applied = state.Pending,
            durationMs = (long)TimeProvider.System.GetElapsedTime(started).TotalMilliseconds,
        }).ConfigureAwait(false);
    }

    private static async Task<int> FingerprintAsync(AdminArguments arguments, ControlPlaneDbContext database, AdminContext context, CancellationToken ct)
    {
        arguments.OnlyAllow();
        var actual = await ReadFingerprintAsync(database, ct).ConfigureAwait(false);
        return await AdminCommandHost.WriteAsync(context, new { sha256 = actual.Sha256, objects = actual.Objects }).ConfigureAwait(false);
    }

    private static async Task<int> VerifyAsync(AdminArguments arguments, ControlPlaneDbContext database, AdminContext context, CancellationToken ct)
    {
        arguments.OnlyAllow("script", "accepted-fingerprints");
        var state = await MigrationState.ReadAsync(database, ct).ConfigureAwait(false);
        var missing = state.Known.Except(state.Applied, StringComparer.Ordinal).ToArray();
        var expected = SchemaFingerprint.Expected();
        var actual = await ReadFingerprintAsync(database, ct).ConfigureAwait(false);
        var accepted = await AcceptedFingerprintsAsync(arguments.Optional("accepted-fingerprints"), ct).ConfigureAwait(false);
        var schemaAccepted = actual.Sha256 == expected.Sha256 || accepted.Contains(actual.Sha256);
        var fileMismatch = arguments.Optional("script") is { } script
            ? FileHashMismatches(await File.ReadAllTextAsync(script, ct).ConfigureAwait(false))
            : [];
        var ok = missing.Length == 0 && state.Unknown.Count == 0 && schemaAccepted && fileMismatch.Count == 0;

        return await AdminCommandHost.WriteAsync(context, new
        {
            ok,
            missing,
            unexpected = state.Unknown,
            schemaSha256 = actual.Sha256,
            expectedSchemaSha256 = expected.Sha256,
            schemaAccepted,
            schemaDiff = SchemaDiff(expected, actual),
            fileHashMismatch = fileMismatch,
        }, ok ? AdminExitCodes.Success : AdminExitCodes.MigrationDivergence).ConfigureAwait(false);
    }

    /// <summary>Recalcula o hash de cada bloco do arquivo exportado e compara com o marcador e o cabeçalho.</summary>
    internal static IReadOnlyList<string> FileHashMismatches(string script)
    {
        var header = HeaderMigration().Matches(script).ToDictionary(match => match.Groups["id"].Value, match => match.Groups["sha"].Value, StringComparer.Ordinal);
        var mismatches = new List<string>();
        foreach (Match block in Block().Matches(script))
        {
            var id = block.Groups["id"].Value;
            var actual = SqlCatalog.Sha256Hex(block.Groups["sql"].Value);
            if (actual != block.Groups["sha"].Value || !header.TryGetValue(id, out var declared) || declared != actual)
            {
                mismatches.Add(id);
            }
        }

        return mismatches;
    }

    private static object[] SchemaDiff(SchemaFingerprint expected, SchemaFingerprint actual)
    {
        var want = expected.Objects.ToDictionary(item => (item.Kind, item.Name), item => item.DefinitionSha256);
        var have = actual.Objects.ToDictionary(item => (item.Kind, item.Name), item => item.DefinitionSha256);
        return want.Keys.Union(have.Keys)
            .Where(key => want.GetValueOrDefault(key) != have.GetValueOrDefault(key))
            .OrderBy(key => key.Kind, StringComparer.Ordinal).ThenBy(key => key.Name, StringComparer.Ordinal)
            .Select(key => (object)new { kind = key.Kind, name = key.Name, expected = want.GetValueOrDefault(key), actual = have.GetValueOrDefault(key) })
            .ToArray();
    }

    private static async Task<HashSet<string>> AcceptedFingerprintsAsync(string? path, CancellationToken ct)
    {
        if (path is null)
        {
            return [];
        }

        var values = JsonSerializer.Deserialize<string[]>(await File.ReadAllTextAsync(path, ct).ConfigureAwait(false))
            ?? throw new AdminUsageException("--accepted-fingerprints precisa ser uma lista JSON de hashes.");
        return values.ToHashSet(StringComparer.Ordinal);
    }

    private static async Task<SchemaFingerprint> ReadFingerprintAsync(ControlPlaneDbContext database, CancellationToken ct)
    {
        var connection = database.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);
        }

        return await SchemaFingerprint.ReadAsync(connection, ct).ConfigureAwait(false);
    }

    private static async Task<int> DivergenceAsync(AdminContext context, MigrationState state)
    {
        await context.Error.WriteLineAsync("O banco tem migrations que esta versão não conhece; as migrations são forward-only.").ConfigureAwait(false);
        return await AdminCommandHost.WriteAsync(context, new { newerThanTarget = state.Unknown }, AdminExitCodes.MigrationDivergence).ConfigureAwait(false);
    }

    [GeneratedRegex(@"^-- migration: (?<id>\S+) sha256=(?<sha>[0-9a-f]{64})$", RegexOptions.Multiline)]
    private static partial Regex HeaderMigration();

    [GeneratedRegex(@"^-- dokpod-migration-begin: (?<id>\S+) sha256=(?<sha>[0-9a-f]{64})\n(?<sql>[\s\S]*?)^-- dokpod-migration-end: \k<id>$", RegexOptions.Multiline)]
    private static partial Regex Block();

    /// <summary>Migrations conhecidas por esta versão, aplicadas no banco, pendentes e desconhecidas (de versão mais nova).</summary>
    private sealed record MigrationState(IReadOnlyList<string> Known, IReadOnlyList<string> Applied, IReadOnlyList<string> Pending, IReadOnlyList<string> Unknown)
    {
        public static async Task<MigrationState> ReadAsync(ControlPlaneDbContext database, CancellationToken ct)
        {
            var known = database.Database.GetMigrations().ToArray();
            var applied = (await database.Database.GetAppliedMigrationsAsync(ct).ConfigureAwait(false)).ToArray();
            return new MigrationState(
                known,
                applied,
                known.Except(applied, StringComparer.Ordinal).ToArray(),
                applied.Except(known, StringComparer.Ordinal).ToArray());
        }
    }

    private sealed record PlannedMigration(string Id, string Sql, string Sha256, IReadOnlyList<SqlCatalogFileUse> Overrides, object Objects);

    private sealed partial record MigrationPlan(IReadOnlyList<PlannedMigration> Migrations, string Hash)
    {
        public static MigrationPlan Build(ControlPlaneDbContext database, MigrationState state)
        {
            var migrations = new List<PlannedMigration>();
            foreach (var script in MigrationScripts.Generate(database, state.Pending, fromEmptyDatabase: state.Applied.Count == 0))
            {
                var sql = Normalize(script.Sql);
                var overrides = SqlCatalog.ActiveOverride?.Uses
                    .Where(use => use.Overridden && use.Path.StartsWith(SqlCatalog.MigrationsPrefix + script.Id + "/", StringComparison.Ordinal))
                    .ToArray() ?? [];
                migrations.Add(new PlannedMigration(script.Id, sql, SqlCatalog.Sha256Hex(sql), overrides, Objects(sql)));
            }

            var canonical = string.Join('\n', migrations.Select(migration => $"{migration.Id}:{migration.Sha256}"));
            return new MigrationPlan(migrations, SqlCatalog.Sha256Hex(canonical));
        }

        private static string Normalize(string sql)
        {
            var text = sql.Replace("\r\n", "\n", StringComparison.Ordinal).TrimStart('﻿');
            return text.EndsWith('\n') ? text : text + "\n";
        }

        /// <summary>Resumo dos objetos afetados, a partir das linhas que começam um comando de definição.</summary>
        private static object Objects(string sql)
        {
            var created = new SortedSet<string>(StringComparer.Ordinal);
            var altered = new SortedSet<string>(StringComparer.Ordinal);
            var dropped = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Match match in Definition().Matches(sql))
            {
                var name = $"{match.Groups["kind"].Value.ToLowerInvariant()} {match.Groups["name"].Value.Trim('"')}";
                (match.Groups["verb"].Value.ToUpperInvariant() switch
                {
                    "CREATE" => created,
                    "ALTER" => altered,
                    _ => dropped,
                }).Add(name);
            }

            return new { created, altered, dropped };
        }

        [GeneratedRegex(@"^\s*(?<verb>CREATE|ALTER|DROP)(?:\s+OR\s+REPLACE)?(?:\s+UNIQUE)?\s+(?<kind>TABLE|INDEX|FUNCTION|PROCEDURE|VIEW|TRIGGER|SCHEMA|SEQUENCE|TYPE|EXTENSION)(?:\s+IF\s+(?:NOT\s+)?EXISTS)?\s+(?:ONLY\s+)?(?<name>[\w.""]+)", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
        private static partial Regex Definition();
    }
}
