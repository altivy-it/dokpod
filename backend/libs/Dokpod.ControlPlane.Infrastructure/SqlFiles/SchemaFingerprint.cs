using System.Data.Common;
using System.Text;
using System.Text.Json;

namespace Dokpod.ControlPlane.Infrastructure.SqlFiles;

/// <summary>
/// Spec 033 (FR-042): impressão do schema <c>dokpod</c>. O histórico de migrations não guarda o SQL aplicado, então a
/// conferência compara objetos (tabelas, views, colunas, índices, restrições, funções e gatilhos), sem partições e sem
/// concessões. A esperada da versão fica em <c>schema/expected-schema.json</c> do catálogo.
/// </summary>
public sealed record SchemaFingerprint(string Sha256, IReadOnlyList<SchemaObject> Objects)
{
    public const string QueryPath = "Installation/checks/schema-fingerprint.sql";

    public static SchemaFingerprint FromObjects(IEnumerable<SchemaObject> objects)
    {
        var ordered = objects
            .OrderBy(item => item.Kind, StringComparer.Ordinal)
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .ToArray();
        var canonical = new StringBuilder();
        foreach (var item in ordered)
        {
            canonical.Append(item.Kind).Append('\t').Append(item.Name).Append('\t').Append(item.DefinitionSha256).Append('\n');
        }

        return new SchemaFingerprint(SqlCatalog.Sha256Hex(canonical.ToString()), ordered);
    }

    public static SchemaFingerprint Parse(string json) =>
        JsonSerializer.Deserialize<SchemaFingerprint>(json, SqlCatalogManifest.JsonOptions)
        ?? throw new InvalidDataException("A impressão do schema está vazia.");

    /// <summary>Impressão esperada desta versão, embutida no catálogo.</summary>
    public static SchemaFingerprint Expected() => Parse(SqlCatalog.Read(SqlCatalog.Manifest.ExpectedSchema.Path));

    public string ToJson() => JsonSerializer.Serialize(this, SqlCatalogManifest.JsonOptions) + "\n";

    /// <summary>Lê a impressão do banco pela consulta do catálogo; a conexão já precisa estar aberta.</summary>
    public static async Task<SchemaFingerprint> ReadAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await using var command = connection.CreateCommand();
        command.CommandText = SqlCatalog.Read(QueryPath);
        var objects = new List<SchemaObject>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            objects.Add(new SchemaObject(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        }

        return FromObjects(objects);
    }
}

public sealed record SchemaObject(string Kind, string Name, string DefinitionSha256);
