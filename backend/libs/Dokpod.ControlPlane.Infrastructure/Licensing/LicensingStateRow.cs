using System.Data.Common;
using System.Security.Cryptography;
using Npgsql;
using NpgsqlTypes;

namespace Dokpod.ControlPlane.Infrastructure.Licensing;

/// <summary>
/// Cria a linha única de <c>licensing_state</c> na primeira gravação da composição Business, com
/// <c>installation_id</c> e chave HMAC aleatórios (report-format.md); nunca derivados de hardware ou rede.
/// </summary>
internal static class LicensingStateRow
{
    internal static async Task EnsureAsync(DbConnection connection, DbTransaction transaction, DateTimeOffset instant, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO dokpod.licensing_state (installation_id, actor_hmac_key, last_trusted_instant_utc)
            VALUES (@installation, @key, @at)
            ON CONFLICT (singleton) DO NOTHING
            """;
        command.Parameters.Add(new NpgsqlParameter("installation", Guid.NewGuid()));
        command.Parameters.Add(new NpgsqlParameter("key", NpgsqlDbType.Bytea) { Value = RandomNumberGenerator.GetBytes(32) });
        command.Parameters.Add(new NpgsqlParameter("at", instant));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
