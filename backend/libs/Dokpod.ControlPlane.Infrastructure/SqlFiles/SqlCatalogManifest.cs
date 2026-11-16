using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dokpod.ControlPlane.Infrastructure.SqlFiles;

/// <summary>
/// Spec 033 (FR-039, FR-040, D-013): manifesto do catálogo SQL. Os caminhos são relativos à raiz do catálogo, que na imagem
/// é <c>/app/sql</c>: <c>Migrations/Script/...</c> para as migrations do EF Core e <c>Installation/...</c> para os arquivos do
/// instalador. A fonte fica em <c>Sql/manifest.json</c> deste projeto.
/// </summary>
public sealed record SqlCatalogManifest(
    string Version,
    SqlCatalogExpectedSchema ExpectedSchema,
    IReadOnlyList<SqlCatalogFile> Files)
{
    public const string FileName = "manifest.json";

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static SqlCatalogManifest Parse(string json) =>
        JsonSerializer.Deserialize<SqlCatalogManifest>(json, JsonOptions)
        ?? throw new InvalidDataException("O manifesto do catálogo SQL está vazio.");

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions) + "\n";

    public SqlCatalogFile? Find(string path) => Files.FirstOrDefault(file => string.Equals(file.Path, path, StringComparison.Ordinal));
}

/// <summary>Impressão do schema esperado da versão (FR-042); não pode ser sobreposta.</summary>
public sealed record SqlCatalogExpectedSchema(string Path, string Sha256);

public sealed record SqlCatalogFile(
    string Path,
    SqlCatalogPurpose Purpose,
    SqlCatalogExecutor Executor,
    IReadOnlyList<SqlCatalogParameter> Parameters,
    string Sha256);

public sealed record SqlCatalogParameter(string Name, SqlCatalogParameterKind Kind);

public enum SqlCatalogPurpose
{
    Migration,
    Bootstrap,
    Check,
    Status,
}

/// <summary><c>ef</c>: migrations do EF Core; <c>psql</c>: variáveis <c>:"x"</c> e <c>:'x'</c>; <c>admin</c>: parâmetros <c>@x</c> do Npgsql.</summary>
public enum SqlCatalogExecutor
{
    Ef,
    Psql,
    Admin,
}

/// <summary>Um parâmetro <c>secret</c> só chega por arquivo montado, nunca por linha de comando nem variável de ambiente.</summary>
public enum SqlCatalogParameterKind
{
    Identifier,
    Literal,
    Secret,
}
