using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Xunit;

namespace Dokpod.Architecture.Tests;

/// <summary>
/// Spec 033 T006 (FR-039, FR-040, D-013): o manifesto do catálogo SQL lista todo arquivo <c>.sql</c> de
/// <c>Migrations/Script</c> e de <c>Sql/</c>, com hash conferido e a versão do produto. Para atualizar os hashes depois de
/// mudar um arquivo ou criar uma migration, rode com <c>DOKPOD_UPDATE_SQL_CATALOG=1</c>: a atualização preserva finalidade,
/// executor e parâmetros declarados e acrescenta migrations novas como <c>migration</c>/<c>ef</c>. Arquivo novo fora das
/// migrations precisa ser declarado à mão no manifesto.
/// </summary>
public sealed class SqlCatalogTests
{
    private static readonly string BackendRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    private static readonly string InfrastructureRoot = Path.Combine(BackendRoot, "libs/Dokpod.ControlPlane.Infrastructure");
    private static readonly string ManifestPath = Path.Combine(InfrastructureRoot, "Sql/manifest.json");

    [Fact]
    public void Manifest_ListsEverySqlFileWithItsHash_AndTheProductVersion()
    {
        var onDisk = CatalogFiles();
        var manifest = JsonNode.Parse(File.ReadAllText(ManifestPath))!.AsObject();
        var version = XDocument.Load(Path.Combine(BackendRoot, "Directory.Build.props")).Descendants("VersionPrefix").Single().Value;

        if (Environment.GetEnvironmentVariable("DOKPOD_UPDATE_SQL_CATALOG") == "1")
        {
            Update(manifest, onDisk, version);
        }

        Assert.Equal(version, (string?)manifest["version"]);
        var files = manifest["files"]!.AsArray().Select(node => node!.AsObject()).ToArray();
        var declared = files.Select(file => (string)file["path"]!).ToArray();
        Assert.Equal(declared.Distinct(StringComparer.Ordinal).Count(), declared.Length);
        Assert.Equal(onDisk.Keys.Order(StringComparer.Ordinal), declared.Order(StringComparer.Ordinal));

        foreach (var file in files)
        {
            var path = (string)file["path"]!;
            Assert.True(Sha256(onDisk[path]) == (string?)file["sha256"], $"Hash divergente em {path}; rode com DOKPOD_UPDATE_SQL_CATALOG=1.");
            var purpose = (string)file["purpose"]!;
            var executor = (string)file["executor"]!;
            Assert.Contains(purpose, (string[])["migration", "bootstrap", "check", "status"]);
            Assert.Contains(executor, (string[])["ef", "psql", "admin"]);
            Assert.Equal(path.StartsWith("Migrations/Script/", StringComparison.Ordinal), purpose == "migration");
            Assert.Equal(purpose == "migration", executor == "ef");
            foreach (var parameter in file["parameters"]!.AsArray().Select(node => node!.AsObject()))
            {
                Assert.Matches("^[a-z][a-z0-9_]*$", (string)parameter["name"]!);
                Assert.Contains((string)parameter["kind"]!, (string[])["identifier", "literal", "secret"]);
            }
        }

        var expected = manifest["expectedSchema"]!.AsObject();
        var expectedPath = (string)expected["path"]!;
        Assert.Equal("schema/expected-schema.json", expectedPath);
        Assert.True(Sha256(Path.Combine(InfrastructureRoot, "Sql", expectedPath)) == (string?)expected["sha256"],
            "Hash divergente na impressão do schema; rode com DOKPOD_UPDATE_SQL_CATALOG=1.");
    }

    /// <summary>D-013: segredo de arquivo psql nunca vai por -v; o próprio arquivo o lê de um caminho montado.</summary>
    [Fact]
    public void PsqlSecrets_AreReadFromFilesInsideTheScript()
    {
        var manifest = JsonNode.Parse(File.ReadAllText(ManifestPath))!.AsObject();
        foreach (var file in manifest["files"]!.AsArray().Select(node => node!.AsObject()).Where(file => (string)file["executor"]! == "psql"))
        {
            var text = File.ReadAllText(CatalogFiles()[(string)file["path"]!]);
            foreach (var secret in file["parameters"]!.AsArray().Select(node => node!.AsObject()).Where(p => (string)p["kind"]! == "secret"))
            {
                var name = (string)secret["name"]!;
                Assert.EndsWith("_file", name, StringComparison.Ordinal);
                Assert.Contains($"`cat :'{name}'`", text, StringComparison.Ordinal);
            }

            Assert.Contains(@"\set ON_ERROR_STOP on", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ApiImages_CopyTheCatalogToAppSql()
    {
        foreach (var dockerfile in new[] { "Dockerfile", "Business.Dockerfile" })
        {
            var text = File.ReadAllText(Path.Combine(BackendRoot, "apps/Dokpod.ControlPlane.Api", dockerfile));
            Assert.Contains("/src/backend/libs/Dokpod.ControlPlane.Infrastructure/Sql/ /app/sql/", text, StringComparison.Ordinal);
            Assert.Contains("/src/backend/libs/Dokpod.ControlPlane.Infrastructure/Migrations/Script/ /app/sql/Migrations/Script/", text, StringComparison.Ordinal);
        }
    }

    /// <summary>Caminho do manifesto → arquivo no disco: <c>Migrations/Script/...</c> e os arquivos de <c>Sql/</c> sem o prefixo.</summary>
    private static Dictionary<string, string> CatalogFiles()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(Path.Combine(InfrastructureRoot, "Migrations/Script"), "*.sql", SearchOption.AllDirectories))
        {
            files["Migrations/Script/" + Relative(Path.Combine(InfrastructureRoot, "Migrations/Script"), file)] = file;
        }

        foreach (var file in Directory.EnumerateFiles(Path.Combine(InfrastructureRoot, "Sql"), "*.sql", SearchOption.AllDirectories))
        {
            files[Relative(Path.Combine(InfrastructureRoot, "Sql"), file)] = file;
        }

        return files;
    }

    private static void Update(JsonObject manifest, Dictionary<string, string> onDisk, string version)
    {
        var existing = (manifest["files"] as JsonArray ?? [])
            .Select(node => node!.AsObject())
            .ToDictionary(file => (string)file["path"]!, StringComparer.Ordinal);
        var files = new JsonArray();
        foreach (var (path, fullPath) in onDisk.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            JsonObject entry;
            if (existing.TryGetValue(path, out var known))
            {
                entry = new JsonObject
                {
                    ["path"] = path,
                    ["purpose"] = (string)known["purpose"]!,
                    ["executor"] = (string)known["executor"]!,
                    ["parameters"] = known["parameters"]!.DeepClone(),
                };
            }
            else if (path.StartsWith("Migrations/Script/", StringComparison.Ordinal))
            {
                entry = new JsonObject { ["path"] = path, ["purpose"] = "migration", ["executor"] = "ef", ["parameters"] = new JsonArray() };
            }
            else
            {
                Assert.Fail($"{path} não está no manifesto; declare finalidade, executor e parâmetros antes de atualizar.");
                return;
            }

            entry["sha256"] = Sha256(fullPath);
            files.Add(entry);
        }

        manifest["version"] = version;
        manifest["files"] = files;
        manifest["expectedSchema"]!["sha256"] = Sha256(Path.Combine(InfrastructureRoot, "Sql", (string)manifest["expectedSchema"]!["path"]!));
        File.WriteAllText(ManifestPath, manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }

    // Mesmo hash de SqlCatalog.Sha256Hex: SHA-256 do texto em UTF-8, sem BOM.
    private static string Sha256(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(File.ReadAllText(path, Encoding.UTF8))));

    private static string Relative(string root, string path) => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
}
