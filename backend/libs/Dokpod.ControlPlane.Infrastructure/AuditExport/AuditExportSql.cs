using System.Data.Common;
using System.Globalization;
using Dokpod.ControlPlane.Application.AuditExport;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Dokpod.ControlPlane.Infrastructure.AuditExport;

/// <summary>Acesso SQL da exportação pela conexão do <see cref="ControlPlaneDbContext"/>, respeitando a transação corrente.</summary>
internal static class AuditExportSql
{
    public static async Task<List<T>> QueryAsync<T>(
        ControlPlaneDbContext dbContext, string sql, IReadOnlyList<NpgsqlParameter> parameters, Func<DbDataReader, T> read, CancellationToken cancellationToken)
    {
        var (connection, opened) = await OpenAsync(dbContext, cancellationToken).ConfigureAwait(false);
        try
        {
            await using var command = Command(dbContext, connection, sql, parameters);
            var results = new List<T>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) results.Add(read(reader));
            return results;
        }
        finally
        {
            if (opened) await connection.CloseAsync().ConfigureAwait(false);
        }
    }

    public static async Task<int> ExecuteAsync(ControlPlaneDbContext dbContext, string sql, IReadOnlyList<NpgsqlParameter> parameters, CancellationToken cancellationToken)
    {
        var (connection, opened) = await OpenAsync(dbContext, cancellationToken).ConfigureAwait(false);
        try
        {
            await using var command = Command(dbContext, connection, sql, parameters);
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (opened) await connection.CloseAsync().ConfigureAwait(false);
        }
    }

    /// <summary><c>xid8</c> trafega como texto: o parâmetro é <c>@x::text::xid8</c> e a leitura é <c>::text</c>.</summary>
    public static ulong Xid8(DbDataReader reader, int ordinal) => ulong.Parse(reader.GetString(ordinal), CultureInfo.InvariantCulture);

    public static NpgsqlParameter Xid8Parameter(string name, ulong value) => new(name, value.ToString(CultureInfo.InvariantCulture));

    public static DateTimeOffset? Instant(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(ordinal), DateTimeKind.Utc));

    public static NpgsqlParameter P(string name, object? value) => new(name, value ?? DBNull.Value);

    private static async Task<(DbConnection Connection, bool Opened)> OpenAsync(ControlPlaneDbContext dbContext, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var opened = connection.State != System.Data.ConnectionState.Open;
        if (opened) await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return (connection, opened);
    }

    private static DbCommand Command(ControlPlaneDbContext dbContext, DbConnection connection, string sql, IReadOnlyList<NpgsqlParameter> parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
        foreach (var parameter in parameters) command.Parameters.Add(parameter.Clone());
        return command;
    }
}
