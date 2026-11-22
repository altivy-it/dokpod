using Dokpod.ControlPlane.Api.Tests.Licensing;
using Dokpod.ControlPlane.Application.ChangeWindows;
using Dokpod.ControlPlane.Infrastructure.ChangeWindows;
using Dokpod.Domain.ChangeWindows;
using Dokpod.Domain.Environments;
using Npgsql;
using Xunit;
using static Dokpod.ControlPlane.Api.Tests.Licensing.LicensingPostgres;

namespace Dokpod.ControlPlane.Api.Tests.ChangeWindows;

/// <summary>
/// Spec 026 T012 e T013: restrições de <c>change_windows</c>, <c>change:override</c> nos ambientes cadastrados, Down/Up
/// numa transação desfeita (o EF não reverte migrations neste repositório) e o store com versão esperada.
/// </summary>
[Trait("Category", "Integration")]
[Collection(LicensingPostgresCollection.Name)]
public sealed class ChangeWindowMigrationTests
{
    private const string MigrationScripts = "backend/libs/Dokpod.ControlPlane.Infrastructure/Migrations/Script/202611220001_ChangeWindows";
    private const string Ranges = """[{"days":["mon"],"start":"22:00","end":"06:00"}]""";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("production", "America/Sao_Paulo", Ranges, 1, true)]
    [InlineData("qa", "America/Sao_Paulo", Ranges, 1, false)]
    [InlineData("staging", "", Ranges, 1, false)]
    [InlineData("staging", "America/Sao_Paulo", "[]", 1, false)]
    [InlineData("staging", "America/Sao_Paulo", """{"days":["mon"]}""", 1, false)]
    [InlineData("development", "America/Sao_Paulo", Ranges, 0, false)]
    public async Task Check_AcceptsOnlyTheRulesOfTheDataModel(string stage, string timeZone, string ranges, int version, bool accepted)
    {
        await using var connection = await OpenAsync(ConnectionStringOrSkip(), Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);
        await ExecuteAsync(connection, transaction, "DELETE FROM dokpod.change_windows", Ct);

        var insert = ExecuteAsync(
            connection, transaction,
            """
            INSERT INTO dokpod.change_windows (stage, time_zone, ranges, version, updated_by, updated_at_utc)
            VALUES (@stage, @zone, @ranges::jsonb, @version, 'Ana', now())
            """,
            Ct,
            new NpgsqlParameter("stage", stage),
            new NpgsqlParameter("zone", timeZone),
            new NpgsqlParameter("ranges", ranges),
            new NpgsqlParameter("version", version));

        if (accepted) await insert;
        else Assert.Equal(PostgresErrorCodes.CheckViolation, (await Assert.ThrowsAsync<PostgresException>(() => insert)).SqlState);
        await transaction.RollbackAsync(Ct);
    }

    [Fact]
    public async Task Check_RejectsMoreThanFourteenRanges()
    {
        await using var connection = await OpenAsync(ConnectionStringOrSkip(), Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);
        var fifteen = "[" + string.Join(',', Enumerable.Repeat("""{"days":["mon"],"start":"01:00","end":"02:00"}""", 15)) + "]";

        var exception = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            connection, transaction,
            "INSERT INTO dokpod.change_windows (stage, time_zone, ranges, version, updated_by, updated_at_utc) VALUES ('staging', 'Etc/UTC', @ranges::jsonb, 1, 'Ana', now())",
            Ct, new NpgsqlParameter("ranges", fifteen)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        await transaction.RollbackAsync(Ct);
    }

    [Fact]
    public async Task UpAndDown_AddAndRemoveChangeOverrideWithoutDuplicates()
    {
        await using var connection = await OpenAsync(ConnectionStringOrSkip(), Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);
        var without = await InsertEnvironmentAsync(connection, transaction, """["container:stop","environment:read"]""");
        var with = await InsertEnvironmentAsync(connection, transaction, """["change:override","environment:read"]""");

        await ExecuteAsync(connection, transaction, await File.ReadAllTextAsync(RepositoryFile($"{MigrationScripts}/Down/01-ChangeWindows.sql"), Ct), Ct);
        Assert.Null(await ScalarAsync<string>(connection, transaction, "SELECT to_regclass('dokpod.change_windows')::text", Ct));
        Assert.Equal("""["container:stop","environment:read"]""", await ScopesAsync(connection, transaction, without));
        Assert.Equal("""["environment:read"]""", await ScopesAsync(connection, transaction, with));

        await ExecuteAsync(connection, transaction, await File.ReadAllTextAsync(RepositoryFile($"{MigrationScripts}/Up/01-ChangeWindows.sql"), Ct), Ct);
        await ExecuteAsync(connection, transaction, "UPDATE dokpod.environments SET scopes = '[\"change:override\",\"environment:read\"]' WHERE environment_id = @id", Ct, new NpgsqlParameter("id", with));
        await ExecuteAsync(connection, transaction, "DROP TABLE dokpod.change_windows", Ct);
        await ExecuteAsync(connection, transaction, await File.ReadAllTextAsync(RepositoryFile($"{MigrationScripts}/Up/01-ChangeWindows.sql"), Ct), Ct);

        Assert.True(await ScalarAsync<bool>(connection, transaction, "SELECT to_regclass('dokpod.change_windows') IS NOT NULL", Ct));
        Assert.Equal("""["change:override","container:stop","environment:read"]""", await ScopesAsync(connection, transaction, without));
        Assert.Equal("""["change:override","environment:read"]""", await ScopesAsync(connection, transaction, with));
        Assert.True(await ScalarAsync<bool>(
            connection, transaction, "SELECT has_table_privilege('dokpod_runtime', 'dokpod.change_windows', 'SELECT, INSERT, UPDATE, DELETE')", Ct));
        await transaction.RollbackAsync(Ct);
    }

    [Fact]
    public async Task Store_CreatesOnlyWithoutARow_AndEditsOrRemovesOnlyTheExpectedVersion()
    {
        var connectionString = ConnectionStringOrSkip();
        await using var context = CreateContext(connectionString);
        var store = new ChangeWindowStore(context);
        // Desenvolvimento: estágio que nenhum outro teste usa; a linha é apagada no fim.
        await RunAsync(connectionString, "DELETE FROM dokpod.change_windows WHERE stage = 'development'");
        try
        {
            var window = new ChangeWindow(
                EnvironmentStage.Development,
                "America/Sao_Paulo",
                [
                    new ChangeWindowRange(new HashSet<DayOfWeek> { DayOfWeek.Sunday, DayOfWeek.Monday }, new TimeOnly(22, 0), new TimeOnly(6, 0)),
                    new ChangeWindowRange(new HashSet<DayOfWeek> { DayOfWeek.Saturday }, new TimeOnly(9, 30), new TimeOnly(11, 0)),
                ],
                0,
                "Ana",
                new DateTimeOffset(2026, 11, 22, 18, 0, 0, TimeSpan.Zero));

            var created = await store.SaveAsync(window, null, Ct);
            Assert.Equal(ChangeWindowWriteOutcome.Saved, created.Outcome);
            Assert.Equal(1, created.Window!.Version);
            Assert.Equal(ChangeWindowWriteOutcome.VersionConflict, (await store.SaveAsync(window, null, Ct)).Outcome);

            var stored = await store.GetAsync(EnvironmentStage.Development, Ct);
            Assert.NotNull(stored);
            Assert.Equal("America/Sao_Paulo", stored.TimeZone);
            Assert.Equal(2, stored.Ranges.Count);
            Assert.Equal(new HashSet<DayOfWeek> { DayOfWeek.Sunday, DayOfWeek.Monday }, stored.Ranges[0].Days);
            Assert.Equal(new TimeOnly(9, 30), stored.Ranges[1].Start);
            Assert.Equal("""["mon", "sun"]""", await TextAsync(connectionString, "SELECT (ranges->0->'days')::text FROM dokpod.change_windows WHERE stage = 'development'"));

            Assert.Equal(ChangeWindowWriteOutcome.VersionConflict, (await store.SaveAsync(window with { TimeZone = "Etc/UTC" }, 7, Ct)).Outcome);
            var edited = await store.SaveAsync(window with { TimeZone = "Etc/UTC" }, 1, Ct);
            Assert.Equal(ChangeWindowWriteOutcome.Saved, edited.Outcome);
            Assert.Equal(2, edited.Window!.Version);
            Assert.Equal("America/Sao_Paulo", edited.Previous!.TimeZone);

            Assert.Equal(ChangeWindowWriteOutcome.VersionConflict, (await store.DeleteAsync(EnvironmentStage.Development, 1, Ct)).Outcome);
            Assert.Equal(ChangeWindowWriteOutcome.Removed, (await store.DeleteAsync(EnvironmentStage.Development, 2, Ct)).Outcome);
            Assert.Null(await store.GetAsync(EnvironmentStage.Development, Ct));
            Assert.Equal(ChangeWindowWriteOutcome.NotFound, (await store.DeleteAsync(EnvironmentStage.Development, 2, Ct)).Outcome);
            Assert.Equal(ChangeWindowWriteOutcome.NotFound, (await store.SaveAsync(window, 2, Ct)).Outcome);
        }
        finally
        {
            await RunAsync(connectionString, "DELETE FROM dokpod.change_windows WHERE stage = 'development'");
        }
    }

    private static async Task<Guid> InsertEnvironmentAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string scopes)
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            connection, transaction,
            "INSERT INTO dokpod.environments (environment_id, name, host, enabled, scopes, created_at_utc) VALUES (@id, @name, 'host-t026', true, @scopes, now())",
            Ct, new NpgsqlParameter("id", id), new NpgsqlParameter("name", $"t026-{id:N}"), new NpgsqlParameter("scopes", scopes));
        return id;
    }

    private static Task<string?> ScopesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid id) =>
        ScalarAsync<string>(connection, transaction, "SELECT scopes FROM dokpod.environments WHERE environment_id = @id", Ct, new NpgsqlParameter("id", id));

    private static async Task RunAsync(string connectionString, string sql)
    {
        await using var connection = await OpenAsync(connectionString, Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private static async Task<string?> TextAsync(string connectionString, string sql)
    {
        await using var connection = await OpenAsync(connectionString, Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        return (string?)await command.ExecuteScalarAsync(Ct);
    }
}
