using System.Text.RegularExpressions;
using Xunit;

namespace Dokpod.Architecture.Tests;

/// <summary>
/// Spec 033 T009 (FR-039, SC-011): o modo administrativo da API e o código do catálogo SQL não têm SQL escrito no código.
/// Todo SQL deles vem dos arquivos do catálogo. A varredura olha os literais de string do C# (comuns, verbatim e raw) e
/// recusa os que parecem um comando SQL. As exceções ficam em <see cref="Allowed"/>, cada uma com o motivo.
/// </summary>
public sealed partial class NoInlineSqlTests
{
    private static readonly string BackendRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    /// <summary>Pastas cobertas. O modo administrativo entra a partir da T010; a pasta ainda pode não existir.</summary>
    private static readonly string[] Scanned =
    [
        "apps/Dokpod.ControlPlane.Api/Admin",
        "libs/Dokpod.ControlPlane.Infrastructure/SqlFiles",
    ];

    /// <summary>Literais aceitos, por arquivo. Vazio hoje.</summary>
    private static readonly (string File, string Literal, string Reason)[] Allowed = [];

    [Fact]
    public void ScannedCode_HasNoInlineSql()
    {
        Assert.True(Directory.Exists(Path.Combine(BackendRoot, Scanned[1])), "A pasta do catálogo SQL precisa existir para a varredura valer.");
        var findings = new List<string>();
        foreach (var folder in Scanned.Select(folder => Path.Combine(BackendRoot, folder)).Where(Directory.Exists))
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(BackendRoot, file).Replace(Path.DirectorySeparatorChar, '/');
                findings.AddRange(InlineSql(File.ReadAllText(file))
                    .Where(literal => !Allowed.Any(allowed => allowed.File == relative && allowed.Literal == literal))
                    .Select(literal => $"{relative}: {literal}"));
            }
        }

        Assert.True(findings.Count == 0, "SQL escrito no código (mova para o catálogo SQL):\n" + string.Join('\n', findings));
    }

    [Theory]
    [InlineData("var sql = \"SELECT id FROM dokpod.environments\";")]
    [InlineData("const string Sql = @\"insert into dokpod.audit_events (id) values (@id)\";")]
    [InlineData("var sql = \"\"\"\n    UPDATE dokpod.agent_identities SET revoked_at = now()\n    \"\"\";")]
    [InlineData("var sql = $\"DELETE FROM dokpod.{table} WHERE id = @id\";")]
    [InlineData("var sql = \"CREATE ROLE dokpod_runtime LOGIN\";")]
    [InlineData("var sql = \"GRANT USAGE ON SCHEMA dokpod TO dokpod_runtime\";")]
    [InlineData("var sql = \"ALTER TABLE dokpod.environments ADD COLUMN x int\";")]
    [InlineData("var sql = \"DROP TABLE dokpod.environments\";")]
    public void Detector_FindsSqlInLiterals(string code) => Assert.NotEmpty(InlineSql(code));

    [Theory]
    [InlineData("throw new InvalidOperationException(\"O arquivo do catálogo SQL está vazio.\");")]
    [InlineData("var path = \"Installation/checks/schema-fingerprint.sql\";")]
    [InlineData("Console.WriteLine(\"Selecione o ambiente e atualize a lista\");")]
    [InlineData("var hint = \"update available\";")]
    public void Detector_IgnoresOrdinaryText(string code) => Assert.Empty(InlineSql(code));

    internal static IEnumerable<string> InlineSql(string source) =>
        StringLiteral().Matches(source)
            .Select(match => match.Value)
            .Where(literal => SqlCommand().IsMatch(literal))
            .Select(literal => literal.Length > 120 ? literal[..120] + "…" : literal);

    // Literais raw ("""…"""), verbatim (@"…", $@"…") e comuns ("…", $"…").
    [GeneratedRegex("\"\"\"[\\s\\S]*?\"\"\"|[$@]{0,2}@\"(?:[^\"]|\"\")*\"|\\$?\"(?:[^\"\\\\\\n]|\\\\.)*\"")]
    private static partial Regex StringLiteral();

    [GeneratedRegex(@"\b(?:SELECT\s+[\s\S]+?\s+FROM|INSERT\s+INTO|UPDATE\s+[\w"".{}]+\s+SET|DELETE\s+FROM|CREATE\s+(?:OR\s+REPLACE\s+)?(?:TABLE|ROLE|DATABASE|INDEX|SCHEMA|FUNCTION|VIEW|TRIGGER|EXTENSION)|ALTER\s+(?:TABLE|ROLE|DATABASE|SCHEMA|FUNCTION|DEFAULT\s+PRIVILEGES)|DROP\s+(?:TABLE|ROLE|DATABASE|INDEX|SCHEMA|FUNCTION|VIEW|TRIGGER)|GRANT\s+[\s\S]+?\s+(?:ON|TO)\s|REVOKE\s+[\s\S]+?\s+(?:ON|FROM)\s|TRUNCATE\s)",
        RegexOptions.IgnoreCase)]
    private static partial Regex SqlCommand();
}
