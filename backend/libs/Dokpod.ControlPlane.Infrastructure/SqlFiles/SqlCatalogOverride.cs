using System.Collections.Concurrent;
using System.Text;

namespace Dokpod.ControlPlane.Infrastructure.SqlFiles;

/// <summary>
/// Spec 033 (FR-041, D-013): raiz sobreposta do catálogo SQL, aberta por <see cref="SqlCatalog.UseOverride"/>. A validação
/// acontece na abertura, antes de qualquer SQL; cada arquivo lido depois fica em <see cref="Uses"/>.
/// </summary>
public sealed class SqlCatalogOverride : IDisposable
{
    private readonly string root;
    private readonly IReadOnlyDictionary<string, string> files;
    private readonly ConcurrentDictionary<string, SqlCatalogFileUse> uses = new(StringComparer.Ordinal);
    private readonly Action release;
    private bool disposed;

    private SqlCatalogOverride(string root, IReadOnlyDictionary<string, string> files, Action release)
    {
        this.root = root;
        this.files = files;
        this.release = release;
    }

    /// <summary>Caminhos da raiz sobreposta, relativos ao catálogo.</summary>
    public IReadOnlyCollection<string> Paths => files.Keys.ToArray();

    /// <summary>Arquivos lidos desde a abertura, com o hash da release e o do conteúdo usado.</summary>
    public IReadOnlyList<SqlCatalogFileUse> Uses => uses.Values.OrderBy(use => use.Path, StringComparer.Ordinal).ToArray();

    internal static SqlCatalogOverride Open(string root, SqlCatalogManifest release, Action onDispose)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var directory = new DirectoryInfo(fullRoot);
        if (!directory.Exists || directory.LinkTarget is not null)
        {
            throw new SqlCatalogOverrideException($"A raiz sobreposta '{root}' não existe ou é um link simbólico.");
        }

        var manifestPath = Path.Combine(fullRoot, SqlCatalogManifest.FileName);
        if (!File.Exists(manifestPath))
        {
            throw new SqlCatalogOverrideException("A raiz sobreposta não tem o manifest.json do catálogo.");
        }

        var overrideManifest = SqlCatalogManifest.Parse(File.ReadAllText(manifestPath, Encoding.UTF8));
        if (!string.Equals(overrideManifest.Version, release.Version, StringComparison.Ordinal))
        {
            throw new SqlCatalogOverrideException(
                $"O manifest.json sobreposto é da versão {overrideManifest.Version}; esta versão é {release.Version}.");
        }

        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in directory.EnumerateFileSystemInfos("*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 }))
        {
            if (entry.LinkTarget is not null)
            {
                throw new SqlCatalogOverrideException($"A raiz sobreposta tem link simbólico: {Relative(fullRoot, entry.FullName)}.");
            }

            if (entry is not FileInfo file)
            {
                continue;
            }

            var path = Relative(fullRoot, file.FullName);
            if (string.Equals(path, SqlCatalogManifest.FileName, StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(path, release.ExpectedSchema.Path, StringComparison.Ordinal))
            {
                throw new SqlCatalogOverrideException($"A impressão do schema esperado ({path}) não pode ser sobreposta.");
            }

            if (release.Find(path) is null)
            {
                throw new SqlCatalogOverrideException($"O arquivo sobreposto '{path}' não tem correspondente nesta versão.");
            }

            found[path] = file.FullName;
        }

        return new SqlCatalogOverride(fullRoot, found, onDispose);
    }

    internal string Resolve(SqlCatalogFile entry, string embedded)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!files.TryGetValue(entry.Path, out var fullPath))
        {
            uses.TryAdd(entry.Path, new SqlCatalogFileUse(entry.Path, entry.Sha256, entry.Sha256));
            return embedded;
        }

        var content = File.ReadAllText(fullPath, Encoding.UTF8);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new SqlCatalogOverrideException($"O arquivo sobreposto '{entry.Path}' está vazio.");
        }

        uses[entry.Path] = new SqlCatalogFileUse(entry.Path, entry.Sha256, SqlCatalog.Sha256Hex(content));
        return content;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        release();
    }

    private static string Relative(string root, string fullPath) =>
        Path.GetRelativePath(root, fullPath).Replace(Path.DirectorySeparatorChar, '/');

    public override string ToString() => $"SqlCatalogOverride({root}, {files.Count} arquivo(s))";
}

/// <summary>Arquivo do catálogo lido numa sobreposição. <see cref="Overridden"/> indica conteúdo diferente do da release.</summary>
public sealed record SqlCatalogFileUse(string Path, string OriginalSha256, string AppliedSha256)
{
    public bool Overridden => !string.Equals(OriginalSha256, AppliedSha256, StringComparison.Ordinal);
}

public sealed class SqlCatalogOverrideException(string message) : Exception(message);
