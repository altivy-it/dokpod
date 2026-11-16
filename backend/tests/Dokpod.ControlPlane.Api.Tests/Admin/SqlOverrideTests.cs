using Dokpod.ControlPlane.Infrastructure.Migrations;
using Dokpod.ControlPlane.Infrastructure.SqlFiles;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests.Admin;

/// <summary>
/// Spec 033 T008 (FR-041, D-013): a raiz sobreposta do catálogo SQL só vale dentro de <see cref="SqlCatalog.UseOverride"/>,
/// que o modo administrativo abre; fora dela, o runtime sempre lê o que está embutido. A abertura recusa versão divergente,
/// arquivo sem correspondente, link simbólico e a impressão do schema esperado.
/// </summary>
public sealed class SqlOverrideTests : IDisposable
{
    private const string Migration = "202611010002_ImageUpdateCheck/Up/01-ImageUpdateCheck.sql";
    private readonly string root = Directory.CreateTempSubdirectory("dokpod-033-sql-override-").FullName;

    [Fact]
    public void InsideTheScope_TheOverriddenFileWins_AndTheUseIsRecorded()
    {
        WriteManifest(SqlCatalog.Manifest.Version);
        var path = SqlCatalog.MigrationsPrefix + Migration;
        var altered = SqlCatalog.Read(path) + "\n-- índice com outro fillfactor, revisado pelo DBA\n";
        Write(path, altered);

        using (var scope = SqlCatalog.UseOverride(root))
        {
            Assert.Equal(altered, MigrationSqlScriptLoader.Load(Migration));
            Assert.Equal(SqlCatalog.ReadEmbedded("Installation/checks/server-version.sql"), SqlCatalog.Read("Installation/checks/server-version.sql"));

            var use = Assert.Single(scope.Uses, item => item.Path == path);
            Assert.True(use.Overridden);
            Assert.Equal(SqlCatalog.Manifest.Find(path)!.Sha256, use.OriginalSha256);
            Assert.Equal(SqlCatalog.Sha256Hex(altered), use.AppliedSha256);
            Assert.False(Assert.Single(scope.Uses, item => item.Path == "Installation/checks/server-version.sql").Overridden);
            Assert.Same(scope, SqlCatalog.ActiveOverride);
        }

        Assert.Null(SqlCatalog.ActiveOverride);
        Assert.NotEqual(altered, MigrationSqlScriptLoader.Load(Migration));
    }

    [Fact]
    public void OutsideTheScope_TheRuntimeReadsOnlyTheEmbeddedFile()
    {
        WriteManifest(SqlCatalog.Manifest.Version);
        Write(SqlCatalog.MigrationsPrefix + Migration, "SELECT 'não deveria ser lido';\n");

        Assert.Null(SqlCatalog.ActiveOverride);
        Assert.Equal(SqlCatalog.ReadEmbedded(SqlCatalog.MigrationsPrefix + Migration), MigrationSqlScriptLoader.Load(Migration));
        Assert.Equal(SqlCatalog.Manifest.Find(SqlCatalog.MigrationsPrefix + Migration)!.Sha256, SqlCatalog.Sha256Hex(MigrationSqlScriptLoader.Load(Migration)));
    }

    [Fact]
    public void ManifestOfAnotherVersion_IsRefused()
    {
        WriteManifest("0.0.1");

        var error = Assert.Throws<SqlCatalogOverrideException>(() => SqlCatalog.UseOverride(root));
        Assert.Contains("0.0.1", error.Message, StringComparison.Ordinal);
        Assert.Null(SqlCatalog.ActiveOverride);
    }

    [Fact]
    public void MissingManifest_IsRefused()
    {
        Write(SqlCatalog.MigrationsPrefix + Migration, "SELECT 1;\n");

        Assert.Throws<SqlCatalogOverrideException>(() => SqlCatalog.UseOverride(root));
    }

    [Theory]
    [InlineData("Installation/checks/inventado.sql")]
    [InlineData("Migrations/Script/209901010001_Inventada/Up/01-Inventada.sql")]
    [InlineData("leia-me.txt")]
    public void FileWithoutACounterpart_IsRefused(string path)
    {
        WriteManifest(SqlCatalog.Manifest.Version);
        Write(path, "SELECT 1;\n");

        var error = Assert.Throws<SqlCatalogOverrideException>(() => SqlCatalog.UseOverride(root));
        Assert.Contains(path, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheExpectedSchema_CannotBeOverridden()
    {
        WriteManifest(SqlCatalog.Manifest.Version);
        Write(SqlCatalog.Manifest.ExpectedSchema.Path, "{}");

        Assert.Throws<SqlCatalogOverrideException>(() => SqlCatalog.UseOverride(root));
    }

    [Fact]
    public void SymbolicLink_IsRefused()
    {
        WriteManifest(SqlCatalog.Manifest.Version);
        var outside = Path.Combine(Directory.CreateTempSubdirectory("dokpod-033-outside-").FullName, "alvo.sql");
        File.WriteAllText(outside, "SELECT 1;\n");
        var link = Path.Combine(root, "Installation/checks/server-version.sql");
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        File.CreateSymbolicLink(link, outside);

        Assert.Throws<SqlCatalogOverrideException>(() => SqlCatalog.UseOverride(root));
    }

    [Fact]
    public void NestedScopes_AreRefused()
    {
        WriteManifest(SqlCatalog.Manifest.Version);

        using var scope = SqlCatalog.UseOverride(root);
        Assert.Throws<InvalidOperationException>(() => SqlCatalog.UseOverride(root));
    }

    [Fact]
    public void FileOutsideTheManifest_IsNeverRead()
    {
        Assert.Throws<InvalidOperationException>(() => SqlCatalog.Read("Installation/checks/inventado.sql"));
    }

    [Fact]
    public void EmbeddedManifest_IsTheSourceFile()
    {
        var source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../libs/Dokpod.ControlPlane.Infrastructure/Sql/manifest.json"));

        Assert.Equal(SqlCatalogManifest.Parse(File.ReadAllText(source)).ToJson(), SqlCatalog.Manifest.ToJson());
        Assert.All(SqlCatalog.Manifest.Files, file => Assert.Equal(file.Sha256, SqlCatalog.Sha256Hex(SqlCatalog.ReadEmbedded(file.Path))));
    }

    private void WriteManifest(string version) =>
        File.WriteAllText(Path.Combine(root, SqlCatalogManifest.FileName), (SqlCatalog.Manifest with { Version = version }).ToJson());

    private void Write(string path, string content)
    {
        var full = Path.Combine(root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    public void Dispose() => Directory.Delete(root, recursive: true);
}
