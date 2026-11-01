using System.Security.Cryptography;
using Dokpod.ControlPlane.Api.Tests.Licensing;
using Npgsql;
using NpgsqlTypes;
using Xunit;
using static Dokpod.ControlPlane.Api.Tests.Licensing.LicensingPostgres;

namespace Dokpod.ControlPlane.Api.Tests.AuditExport;

/// <summary>
/// Spec 027 T004: colunas de ordem da trilha sem reescrever partições, índice herdado por todas as partições,
/// restrições das tabelas de destino e cursor, e Down. Tudo em transações desfeitas no fim.
/// </summary>
[Trait("Category", "Integration")]
[Collection(LicensingPostgresCollection.Name)]
public sealed class AuditExportMigrationTests
{
    private const string MigrationScripts = "backend/libs/Dokpod.ControlPlane.Infrastructure/Migrations/Script/202611010001_AuditExport";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task NewAuditEvents_GetTransactionOrder_AndEveryPartitionHasTheIndex()
    {
        await using var connection = await OpenAsync(ConnectionStringOrSkip(), Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);

        var first = await AppendAuditEventAsync(connection, transaction);
        var second = await AppendAuditEventAsync(connection, transaction);

        var currentTx = await ScalarAsync<string>(connection, transaction, "SELECT pg_current_xact_id()::text", Ct);
        Assert.Equal(currentTx, await ScalarAsync<string>(connection, transaction, "SELECT tx_id::text FROM dokpod.audit_events WHERE event_id = @id", Ct, new NpgsqlParameter("id", first)));
        var firstSeq = await ScalarAsync<long>(connection, transaction, "SELECT export_seq FROM dokpod.audit_events WHERE event_id = @id", Ct, new NpgsqlParameter("id", first));
        var secondSeq = await ScalarAsync<long>(connection, transaction, "SELECT export_seq FROM dokpod.audit_events WHERE event_id = @id", Ct, new NpgsqlParameter("id", second));
        Assert.True(secondSeq > firstSeq);

        var partitions = await ScalarAsync<long>(connection, transaction, "SELECT count(*) FROM pg_inherits WHERE inhparent = 'dokpod.audit_events'::regclass", Ct);
        var indexed = await ScalarAsync<long>(
            connection,
            transaction,
            """
            SELECT count(*) FROM pg_inherits i
            JOIN pg_index x ON x.indrelid = i.inhrelid
            JOIN pg_class ix ON ix.oid = x.indexrelid
            JOIN pg_inherits pi ON pi.inhrelid = ix.oid AND pi.inhparent = 'dokpod.ix_audit_events_export_order'::regclass
            WHERE i.inhparent = 'dokpod.audit_events'::regclass
            """,
            Ct);
        Assert.True(partitions > 0);
        Assert.Equal(partitions, indexed);

        await transaction.RollbackAsync(Ct);
    }

    [Fact]
    public async Task UpAfterDown_DoesNotRewriteExistingPartitions_AndLeavesOldRowsUnordered()
    {
        await using var connection = await OpenAsync(ConnectionStringOrSkip(), Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);
        var existing = await AppendAuditEventAsync(connection, transaction);
        var partition = await ScalarAsync<string>(
            connection, transaction, "SELECT tableoid::regclass::text FROM dokpod.audit_events WHERE event_id = @id", Ct, new NpgsqlParameter("id", existing));
        var fileBefore = await ScalarAsync<long>(connection, transaction, $"SELECT pg_relation_filenode('{partition}')::bigint", Ct);

        await ExecuteAsync(connection, transaction, await File.ReadAllTextAsync(RepositoryFile($"{MigrationScripts}/Down/01-AuditExport.sql"), Ct), Ct);
        Assert.Null(await ScalarAsync<string>(connection, transaction, "SELECT to_regclass('dokpod.audit_export_destinations')::text", Ct));
        Assert.Equal(0L, await ScalarAsync<long>(
            connection, transaction, "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'dokpod' AND table_name = 'audit_events' AND column_name IN ('tx_id', 'export_seq')", Ct));

        await ExecuteAsync(connection, transaction, await File.ReadAllTextAsync(RepositoryFile($"{MigrationScripts}/Up/01-AuditExport.sql"), Ct), Ct);

        Assert.Equal(fileBefore, await ScalarAsync<long>(connection, transaction, $"SELECT pg_relation_filenode('{partition}')::bigint", Ct));
        Assert.Null(await ScalarAsync<string>(connection, transaction, "SELECT tx_id::text FROM dokpod.audit_events WHERE event_id = @id", Ct, new NpgsqlParameter("id", existing)));
        var fresh = await AppendAuditEventAsync(connection, transaction);
        Assert.NotNull(await ScalarAsync<string>(connection, transaction, "SELECT tx_id::text FROM dokpod.audit_events WHERE event_id = @id", Ct, new NpgsqlParameter("id", fresh)));

        await transaction.RollbackAsync(Ct);
        Assert.NotNull(await ScalarAsync<string>(connection, null, "SELECT to_regclass('dokpod.audit_export_cursors')::text", Ct));
    }

    [Theory]
    [InlineData("missing-audit-export-category", "ck_audit_export_destinations_categories")]
    [InlineData("syslog-with-path", "ck_audit_export_destinations_path")]
    [InlineData("otlp-without-path", "ck_audit_export_destinations_path")]
    [InlineData("bad-secret-ref", "ck_audit_export_destinations_secret_ref")]
    [InlineData("bad-port", "ck_audit_export_destinations_port")]
    [InlineData("bad-protocol", "ck_audit_export_destinations_protocol")]
    [InlineData("padded-name", "ck_audit_export_destinations_name")]
    public async Task DestinationConstraints_RejectInvalidRows(string scenario, string constraint)
    {
        await using var connection = await OpenAsync(ConnectionStringOrSkip(), Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);
        var (protocol, path, secretRef, port, name, categories) = scenario switch
        {
            "missing-audit-export-category" => ("syslog", null, null, 6514, "siem", new[] { "containers" }),
            "syslog-with-path" => ("syslog", "/v1/logs", null, 6514, "siem", new[] { "audit_export" }),
            "otlp-without-path" => ("otlp", null, null, 4318, "siem", new[] { "audit_export" }),
            "bad-secret-ref" => ("syslog", null, "../etc", 6514, "siem", new[] { "audit_export" }),
            "bad-port" => ("syslog", null, null, 70000, "siem", new[] { "audit_export" }),
            "bad-protocol" => ("udp", null, null, 514, "siem", new[] { "audit_export" }),
            _ => ("syslog", (string?)null, (string?)null, 6514, " siem ", new[] { "audit_export" }),
        };

        var error = await Assert.ThrowsAsync<PostgresException>(() => InsertDestinationAsync(connection, transaction, name, protocol, path, secretRef, port, categories));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal(constraint, error.ConstraintName);
    }

    [Fact]
    public async Task Destinations_HaveUniqueNamesIgnoringCase_AndCursorsFollowTheirDestination()
    {
        await using var connection = await OpenAsync(ConnectionStringOrSkip(), Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);
        var name = $"siem-{Guid.NewGuid():N}"[..20];
        var id = await InsertDestinationAsync(connection, transaction, name, "otlp", "/v1/logs", "siem-soc", 4318, ["audit_export", "containers"]);
        await ExecuteAsync(
            connection, transaction, "INSERT INTO dokpod.audit_export_cursors (destination_id, last_tx_id) VALUES (@id, pg_current_xact_id())", Ct, new NpgsqlParameter("id", id));

        await ExecuteAsync(connection, transaction, "SAVEPOINT duplicate_name", Ct);
        var duplicate = await Assert.ThrowsAsync<PostgresException>(() => InsertDestinationAsync(connection, transaction, name.ToUpperInvariant(), "syslog", null, null, 6514, ["audit_export"]));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
        await ExecuteAsync(connection, transaction, "ROLLBACK TO SAVEPOINT duplicate_name", Ct);

        await ExecuteAsync(connection, transaction, "DELETE FROM dokpod.audit_export_destinations WHERE destination_id = @id", Ct, new NpgsqlParameter("id", id));
        Assert.Equal(0L, await ScalarAsync<long>(connection, transaction, "SELECT count(*) FROM dokpod.audit_export_cursors WHERE destination_id = @id", Ct, new NpgsqlParameter("id", id)));

        await transaction.RollbackAsync(Ct);
    }

    internal static async Task<Guid> AppendAuditEventAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, string action = "container.restart", DateTimeOffset? at = null)
    {
        var eventId = Guid.NewGuid();
        await ExecuteAsync(
            connection,
            transaction,
            "SELECT dokpod.dokpod_append_audit_event(@id, @at, @correlation, 1::smallint, 'tester', @action, @environment, NULL, 1::smallint, NULL, @hash, NULL, NULL)",
            Ct,
            new NpgsqlParameter("id", eventId),
            new NpgsqlParameter("at", (at ?? DateTimeOffset.UtcNow).UtcDateTime),
            new NpgsqlParameter("correlation", Guid.NewGuid()),
            new NpgsqlParameter("action", action),
            new NpgsqlParameter("environment", Guid.NewGuid()),
            new NpgsqlParameter("hash", NpgsqlDbType.Bytea) { Value = SHA256.HashData(eventId.ToByteArray()) });
        return eventId;
    }

    private static async Task<Guid> InsertDestinationAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string name, string protocol, string? path, string? secretRef, int port, string[] categories)
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO dokpod.audit_export_destinations (destination_id, name, protocol, host, port, path, secret_ref, categories, created_at_utc, updated_at_utc)
            VALUES (@id, @name, @protocol, 'siem.exemplo.interno', @port, @path, @secret, @categories, now(), now())
            """,
            Ct,
            new NpgsqlParameter("id", id),
            new NpgsqlParameter("name", name),
            new NpgsqlParameter("protocol", protocol),
            new NpgsqlParameter("port", port),
            new NpgsqlParameter("path", NpgsqlDbType.Varchar) { Value = (object?)path ?? DBNull.Value },
            new NpgsqlParameter("secret", NpgsqlDbType.Varchar) { Value = (object?)secretRef ?? DBNull.Value },
            new NpgsqlParameter("categories", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = categories });
        return id;
    }
}
