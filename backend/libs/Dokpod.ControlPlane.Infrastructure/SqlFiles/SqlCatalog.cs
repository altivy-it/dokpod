using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Dokpod.ControlPlane.Infrastructure.SqlFiles;

/// <summary>
/// Spec 033 (FR-039 a FR-041, D-013): leitura do catálogo SQL. Por padrão vem dos recursos embutidos nesta assembly, os mesmos
/// arquivos que a imagem copia para <c>/app/sql</c>. Dentro de <see cref="UseOverride"/>, um arquivo de mesmo caminho na raiz
/// sobreposta vence, e cada uso fica registrado com o hash original e o aplicado. Só o modo administrativo da API abre essa
/// raiz; o runtime nunca a usa.
/// </summary>
public static class SqlCatalog
{
    public const string MigrationsPrefix = "Migrations/Script/";
    private const string LogicalPrefix = "dokpod-sql/";

    private static readonly Assembly CatalogAssembly = typeof(SqlCatalog).Assembly;
    private static readonly AsyncLocal<SqlCatalogOverride?> CurrentOverride = new();
    private static readonly Lazy<IReadOnlyDictionary<string, string>> ResourceNames = new(IndexResources);
    private static readonly Lazy<SqlCatalogManifest> EmbeddedManifest = new(() => SqlCatalogManifest.Parse(ReadEmbedded(SqlCatalogManifest.FileName)));

    /// <summary>Manifesto embutido na versão em execução.</summary>
    public static SqlCatalogManifest Manifest => EmbeddedManifest.Value;

    /// <summary>Sobreposição ativa no fluxo atual, ou nenhuma.</summary>
    public static SqlCatalogOverride? ActiveOverride => CurrentOverride.Value;

    /// <summary>Lê um arquivo do catálogo pelo caminho do manifesto; arquivo fora do manifesto é recusado.</summary>
    public static string Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var entry = Manifest.Find(path) ?? (string.Equals(path, Manifest.ExpectedSchema.Path, StringComparison.Ordinal)
            ? null
            : throw new InvalidOperationException($"O arquivo '{path}' não está no manifesto do catálogo SQL."));

        var embedded = ReadEmbedded(path);
        if (entry is null || CurrentOverride.Value is not { } active)
        {
            return embedded;
        }

        return active.Resolve(entry, embedded);
    }

    /// <summary>
    /// Abre a raiz sobreposta para o fluxo atual (e as tarefas que ele iniciar). Exige o manifesto da mesma versão e recusa
    /// arquivo sem correspondente na release, link simbólico e a impressão do schema esperado.
    /// </summary>
    public static SqlCatalogOverride UseOverride(string root)
    {
        if (CurrentOverride.Value is not null)
        {
            throw new InvalidOperationException("Já existe uma sobreposição do catálogo SQL ativa neste fluxo.");
        }

        var scope = SqlCatalogOverride.Open(root, Manifest, () => CurrentOverride.Value = null);
        CurrentOverride.Value = scope;
        return scope;
    }

    public static string Sha256Hex(string content) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    internal static string ReadEmbedded(string path)
    {
        if (!ResourceNames.Value.TryGetValue(path, out var resourceName))
        {
            throw new InvalidOperationException($"O arquivo '{path}' do catálogo SQL não está embutido nesta versão.");
        }

        using var stream = CatalogAssembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"O recurso '{resourceName}' do catálogo SQL não pôde ser aberto.");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var content = reader.ReadToEnd();

        return !string.IsNullOrWhiteSpace(content)
            ? content
            : throw new InvalidOperationException($"O arquivo '{path}' do catálogo SQL está vazio.");
    }

    // Arquivos de Sql/ entram com o nome lógico `dokpod-sql/<caminho>`; as migrations mantêm o nome padrão do MSBuild
    // (`<assembly>.Migrations.Script._<id>.Up.<arquivo>`), que o MigrationSqlScriptLoader sempre usou.
    private static IReadOnlyDictionary<string, string> IndexResources()
    {
        var index = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in CatalogAssembly.GetManifestResourceNames())
        {
            var normalized = name.Replace('\\', '/');
            if (normalized.StartsWith(LogicalPrefix, StringComparison.Ordinal))
            {
                index[normalized[LogicalPrefix.Length..]] = name;
            }
        }

        foreach (var file in EmbeddedMigrationPaths())
        {
            var suffix = $".Migrations.Script._{file[MigrationsPrefix.Length..].Replace('/', '.')}";
            var name = CatalogAssembly.GetManifestResourceNames().SingleOrDefault(candidate => candidate.EndsWith(suffix, StringComparison.Ordinal));
            if (name is not null)
            {
                index[file] = name;
            }
        }

        return index;
    }

    private static IEnumerable<string> EmbeddedMigrationPaths()
    {
        var manifestName = CatalogAssembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.Replace('\\', '/') == LogicalPrefix + SqlCatalogManifest.FileName);
        if (manifestName is null)
        {
            return [];
        }

        using var stream = CatalogAssembly.GetManifestResourceStream(manifestName)!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return SqlCatalogManifest.Parse(reader.ReadToEnd()).Files
            .Where(file => file.Path.StartsWith(MigrationsPrefix, StringComparison.Ordinal))
            .Select(file => file.Path)
            .ToArray();
    }
}
