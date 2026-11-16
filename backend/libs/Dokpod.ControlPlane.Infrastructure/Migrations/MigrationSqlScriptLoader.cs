using Dokpod.ControlPlane.Infrastructure.SqlFiles;

namespace Dokpod.ControlPlane.Infrastructure.Migrations;

/// <summary>
/// Corpo SQL das migrations, lido do catálogo (spec 033, D-013): os mesmos arquivos de <c>Migrations/Script</c> que a release
/// publica para revisão. Com uma sobreposição aberta pelo modo administrativo, o arquivo sobreposto de mesmo caminho vence.
/// </summary>
internal static class MigrationSqlScriptLoader
{
    internal static string Load(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        return SqlCatalog.Read(SqlCatalog.MigrationsPrefix + relativePath);
    }
}
