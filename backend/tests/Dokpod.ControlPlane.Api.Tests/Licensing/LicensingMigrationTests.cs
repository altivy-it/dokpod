using Npgsql;
using NpgsqlTypes;
using Xunit;
using static Dokpod.ControlPlane.Api.Tests.Licensing.LicensingPostgres;

namespace Dokpod.ControlPlane.Api.Tests.Licensing;

/// <summary>
/// Spec 031 T006: schema, partições, retenção e Down da migration de licenciamento em PostgreSQL real. Toda
/// alteração acontece numa transação desfeita no fim, porque <c>licensing_events</c> é append-only.
/// </summary>
[Trait("Category", "Integration")]
[Collection(LicensingPostgresCollection.Name)]
public sealed class LicensingMigrationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Schema_HasMonthlyPartitionsDefaultsAndChainColumns()
    {
        await using var connection = await OpenAsync(ConnectionStringOrSkip(), Ct);

        // licensing_events nunca é purgada: os 132 meses de 2026-01 a 2036-12 ficam. As observações passam pela
        // retenção de 35 dias (que remove partições antigas), então só os meses a partir do atual são exigidos.
        var monthsAhead = await ScalarAsync<int>(
            connection,
            null,
            "SELECT ((2036 - extract(year FROM now())::int) * 12 + (12 - extract(month FROM now())::int) + 1)",
            Ct);
        foreach (var (table, minimumMonth, expected) in new[] { ("licensing_events", "2026_01", 132L), ("licensing_capacity_observations", null as string, (long)monthsAhead) })
        {
            Assert.Equal('p', await ScalarAsync<char>(connection, null, $"SELECT relkind FROM pg_class WHERE oid = 'dokpod.{table}'::regclass", Ct));
            Assert.NotNull(await ScalarAsync<string>(connection, null, $"SELECT to_regclass('dokpod.{table}_default')::text", Ct));
            Assert.Equal(expected, await ScalarAsync<long>(
                connection,
                null,
                $$"""
                SELECT count(*) FROM pg_inherits i JOIN pg_class c ON c.oid = i.inhrelid
                WHERE i.inhparent = 'dokpod.{{table}}'::regclass AND c.relname ~ '^{{table}}_20(2[6-9]|3[0-6])_(0[1-9]|1[0-2])$'
                  AND substring(c.relname FROM '(\d{4}_\d{2})$') >= COALESCE(@minimum, to_char(now(), 'YYYY_MM'))
                """,
                Ct,
                new NpgsqlParameter("minimum", NpgsqlTypes.NpgsqlDbType.Text) { Value = (object?)minimumMonth ?? DBNull.Value }));
        }

        var expectedColumns = new (string Table, string Column, string Nullable)[]
        {
            ("license_revisions", "content", "YES"),
            ("license_revisions", "content_sha256", "NO"),
            ("licensing_state", "installation_id", "NO"),
            ("licensing_state", "actor_hmac_key", "NO"),
            ("licensing_state", "chain_head_seq", "NO"),
            ("licensing_state", "chain_head_hash", "YES"),
            ("licensing_events", "chain_seq", "NO"),
            ("licensing_events", "prev_hash", "NO"),
            ("licensing_events", "record_hash", "NO"),
            ("licensing_events", "actor_id", "YES"),
            ("licensing_events", "actor_ref", "YES"),
            ("licensing_capacity_anchors", "host_id", "NO"),
        };
        foreach (var (table, column, nullable) in expectedColumns)
        {
            Assert.Equal(nullable, await ScalarAsync<string>(
                connection,
                null,
                "SELECT is_nullable FROM information_schema.columns WHERE table_schema = 'dokpod' AND table_name = @table AND column_name = @column",
                Ct,
                new NpgsqlParameter("table", table),
                new NpgsqlParameter("column", column)));
        }
    }

    [Fact]
    public async Task AcceptedRevision_IsUniquePerLicenseAndRevision_WhileRejectedRepeats()
    {
        await using var connection = await OpenAsync(ConnectionStringOrSkip(), Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);
        var licenseId = $"lic_{Guid.NewGuid():N}";

        await InsertRevisionAsync(connection, transaction, licenseId, "accepted", content: [1], failureCode: null);
        await InsertRevisionAsync(connection, transaction, licenseId, "rejected", content: null, failureCode: "license_obsolete");
        await InsertRevisionAsync(connection, transaction, licenseId, "rejected", content: null, failureCode: "license_obsolete");
        var duplicate = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertRevisionAsync(connection, transaction, licenseId, "accepted", content: [2], failureCode: null));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
    }

    [Fact]
    public async Task RejectedRevision_CannotKeepContent()
    {
        await using var connection = await OpenAsync(ConnectionStringOrSkip(), Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertRevisionAsync(connection, transaction, "lic_rejected", "rejected", content: [1], failureCode: "license_invalid"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
    }

    [Theory]
    [InlineData("UPDATE dokpod.licensing_events SET action = 'host.linked' WHERE event_id = @event")]
    [InlineData("DELETE FROM dokpod.licensing_events WHERE event_id = @event")]
    public async Task LicensingEvents_AreAppendOnly(string mutation)
    {
        await using var connection = await OpenAsync(ConnectionStringOrSkip(), Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);
        var eventId = Guid.NewGuid();
        await InsertEventAsync(connection, transaction, eventId, new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero));

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteAsync(connection, transaction, mutation, Ct, new NpgsqlParameter("event", eventId)));

        Assert.Contains("append-only", error.MessageText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rows_RouteToMonthlyAndDefaultPartitions()
    {
        await using var connection = await OpenAsync(ConnectionStringOrSkip(), Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);
        var monthly = Guid.NewGuid();
        var outside = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        await InsertEventAsync(connection, transaction, monthly, new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero));
        await InsertEventAsync(connection, transaction, outside, new DateTimeOffset(2041, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await InsertObservationAsync(connection, transaction, hostId, new DateTimeOffset(2041, 1, 1, 0, 0, 0, TimeSpan.Zero), 8);

        Assert.Equal("licensing_events_2026_11", await ScalarAsync<string>(
            connection, transaction, "SELECT c.relname::text FROM dokpod.licensing_events e JOIN pg_class c ON c.oid = e.tableoid WHERE e.event_id = @id", Ct, new NpgsqlParameter("id", monthly)));
        Assert.Equal("licensing_events_default", await ScalarAsync<string>(
            connection, transaction, "SELECT c.relname::text FROM dokpod.licensing_events e JOIN pg_class c ON c.oid = e.tableoid WHERE e.event_id = @id", Ct, new NpgsqlParameter("id", outside)));
        Assert.Equal("licensing_capacity_observations_default", await ScalarAsync<string>(
            connection, transaction, "SELECT c.relname::text FROM dokpod.licensing_capacity_observations o JOIN pg_class c ON c.oid = o.tableoid WHERE o.host_id = @id", Ct, new NpgsqlParameter("id", hostId)));
    }

    [Fact]
    public async Task EnsureMonthlyPartitions_CreatesFollowingMonthsOfBothTables()
    {
        await using var connection = await OpenAsync(ConnectionStringOrSkip(), Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);

        await ExecuteAsync(connection, transaction, "SELECT dokpod.dokpod_ensure_monthly_partitions(DATE '2037-03-01')", Ct);

        foreach (var partition in new[] { "licensing_events_2037_02", "licensing_capacity_observations_2037_02" })
        {
            Assert.NotNull(await ScalarAsync<string>(connection, transaction, $"SELECT to_regclass('dokpod.{partition}')::text", Ct));
        }
    }

    [Fact]
    public async Task TemporalRetention_AcceptsObservationsAndRefusesLicensingEvents()
    {
        await using var connection = await OpenAsync(ConnectionStringOrSkip(), Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);

        await ScalarAsync<long>(
            connection,
            transaction,
            "SELECT dokpod.dokpod_apply_temporal_retention('dokpod.licensing_capacity_observations'::regclass, 'observed_at_utc', interval '36 days')",
            Ct);
        await ExecuteAsync(connection, transaction, "SAVEPOINT before_events", Ct);
        var error = await Assert.ThrowsAsync<PostgresException>(() => ScalarAsync<long>(
            connection,
            transaction,
            "SELECT dokpod.dokpod_apply_temporal_retention('dokpod.licensing_events'::regclass, 'occurred_at_utc', interval '36 days')",
            Ct));

        Assert.Contains("not eligible", error.MessageText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RetainLicensingCapacity_AnchorsBeforeDeletingAndNeverRegresses()
    {
        await using var connection = await OpenAsync(ConnectionStringOrSkip(), Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);
        var now = await ScalarAsync<DateTime>(connection, transaction, "SELECT now()", Ct);
        var start = new DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Utc));
        var hostId = Guid.NewGuid();
        await InsertObservationAsync(connection, transaction, hostId, start.AddDays(-60), 8);
        await InsertObservationAsync(connection, transaction, hostId, start.AddDays(-50), 16);
        await InsertObservationAsync(connection, transaction, hostId, start.AddDays(-10), 32);

        await ScalarAsync<long>(connection, transaction, "SELECT dokpod.dokpod_retain_licensing_capacity(interval '35 days')", Ct);

        Assert.Equal(16, await ScalarAsync<int>(
            connection, transaction, "SELECT vcpus FROM dokpod.licensing_capacity_anchors WHERE host_id = @host", Ct, new NpgsqlParameter("host", hostId)));
        Assert.Equal(1L, await ScalarAsync<long>(
            connection, transaction, "SELECT count(*) FROM dokpod.licensing_capacity_observations WHERE host_id = @host", Ct, new NpgsqlParameter("host", hostId)));

        await InsertObservationAsync(connection, transaction, hostId, start.AddDays(-70), 4);
        await ScalarAsync<long>(connection, transaction, "SELECT dokpod.dokpod_retain_licensing_capacity(interval '35 days')", Ct);
        Assert.Equal(16, await ScalarAsync<int>(
            connection, transaction, "SELECT vcpus FROM dokpod.licensing_capacity_anchors WHERE host_id = @host", Ct, new NpgsqlParameter("host", hostId)));

        await ExecuteAsync(connection, transaction, "SAVEPOINT before_short_retention", Ct);
        var error = await Assert.ThrowsAsync<PostgresException>(() => ScalarAsync<long>(
            connection, transaction, "SELECT dokpod.dokpod_retain_licensing_capacity(interval '34 days')", Ct));
        Assert.Contains("below 35 days", error.MessageText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DownScript_RemovesLicensingAndRestoresPreviousFunctions()
    {
        await using var connection = await OpenAsync(ConnectionStringOrSkip(), Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);
        var down = await File.ReadAllTextAsync(
            RepositoryFile("backend/libs/Dokpod.ControlPlane.Infrastructure/Migrations/Script/202610310001_Licensing/Down/01-Licensing.sql"),
            Ct);

        await ExecuteAsync(connection, transaction, down, Ct);

        foreach (var table in new[] { "license_revisions", "licensing_state", "licensing_events", "licensing_capacity_observations", "licensing_capacity_anchors", "licensing_hosts" })
        {
            Assert.Null(await ScalarAsync<string>(connection, transaction, $"SELECT to_regclass('dokpod.{table}')::text", Ct));
        }

        Assert.Null(await ScalarAsync<string>(
            connection, transaction, "SELECT to_regprocedure('dokpod.dokpod_retain_licensing_capacity(interval)')::text", Ct));
        Assert.DoesNotContain("licensing", await ScalarAsync<string>(
            connection, transaction, "SELECT pg_get_functiondef('dokpod.dokpod_ensure_monthly_partitions(date)'::regprocedure)", Ct), StringComparison.Ordinal);
        Assert.DoesNotContain("licensing", await ScalarAsync<string>(
            connection, transaction, "SELECT pg_get_functiondef('dokpod.dokpod_apply_temporal_retention(regclass, text, interval, integer)'::regprocedure)", Ct), StringComparison.Ordinal);

        await transaction.RollbackAsync(Ct);
        Assert.NotNull(await ScalarAsync<string>(connection, null, "SELECT to_regclass('dokpod.licensing_events')::text", Ct));
    }

    private static Task InsertRevisionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string licenseId,
        string outcome,
        byte[]? content,
        string? failureCode) =>
        ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO dokpod.license_revisions (
                revision_id, document_type, received_at_utc, received_by, content_sha256, content,
                license_id, revision, outcome, failure_code)
            VALUES (@id, 'license', now(), 'test-subject', @sha, @content, @license, 1, @outcome, @failure)
            """,
            Ct,
            new NpgsqlParameter("id", Guid.NewGuid()),
            new NpgsqlParameter("sha", NpgsqlDbType.Bytea) { Value = new byte[32] },
            new NpgsqlParameter("content", NpgsqlDbType.Bytea) { Value = (object?)content ?? DBNull.Value },
            new NpgsqlParameter("license", licenseId),
            new NpgsqlParameter("outcome", outcome),
            new NpgsqlParameter("failure", NpgsqlDbType.Text) { Value = (object?)failureCode ?? DBNull.Value });

    private static Task InsertEventAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid eventId, DateTimeOffset occurredAtUtc) =>
        ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO dokpod.licensing_events (
                event_id, chain_seq, prev_hash, record_hash, occurred_at_utc, correlation_id, action, details)
            VALUES (@event, 1, @hash, @hash, @at, @event, 'host.removed', '{}'::jsonb)
            """,
            Ct,
            new NpgsqlParameter("event", eventId),
            new NpgsqlParameter("hash", NpgsqlDbType.Bytea) { Value = new byte[32] },
            new NpgsqlParameter("at", occurredAtUtc));

    private static Task InsertObservationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid hostId,
        DateTimeOffset observedAtUtc,
        int vcpus) =>
        ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO dokpod.licensing_capacity_observations (host_id, observed_at_utc, vcpus, source, environment_id)
            VALUES (@host, @at, @vcpus, 'engine', @host)
            """,
            Ct,
            new NpgsqlParameter("host", hostId),
            new NpgsqlParameter("at", observedAtUtc),
            new NpgsqlParameter("vcpus", vcpus));
}
