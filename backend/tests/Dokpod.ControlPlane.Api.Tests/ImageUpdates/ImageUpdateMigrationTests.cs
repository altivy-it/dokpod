using Dokpod.ControlPlane.Api.Tests.Licensing;
using Npgsql;
using Xunit;
using static Dokpod.ControlPlane.Api.Tests.Licensing.LicensingPostgres;

namespace Dokpod.ControlPlane.Api.Tests.ImageUpdates;

/// <summary>
/// Spec 025 T005: restrições de <c>image_update_schedules</c> e <c>image_update_checks</c>, cascata ao excluir o
/// ambiente, condição de alerta <c>image_outdated</c> e Down/Up. Tudo em transações desfeitas no fim.
/// </summary>
[Trait("Category", "Integration")]
[Collection(LicensingPostgresCollection.Name)]
public sealed class ImageUpdateMigrationTests
{
    private const string MigrationScripts = "backend/libs/Dokpod.ControlPlane.Infrastructure/Migrations/Script/202611010002_ImageUpdateCheck";
    private static readonly string Digest = "sha256:" + new string('a', 64);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("up_to_date", null, true)]
    [InlineData("update_available", null, true)]
    [InlineData("not_checkable", "registry_not_allowed", true)]
    [InlineData("failed", "rate_limited", true)]
    [InlineData("up_to_date", "timeout", false)]
    [InlineData("not_checkable", null, false)]
    [InlineData("failed", "local_image", false)]
    [InlineData("container_behind", null, false)]
    public async Task Check_AcceptsOnlyTheStatesAndReasonsOfTheDataModel(string state, string? reason, bool accepted)
    {
        await using var connection = await OpenAsync(ConnectionStringOrSkip(), Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);
        var environment = await InsertEnvironmentAsync(connection, transaction);

        var insert = InsertCheckAsync(connection, transaction, environment, state, reason);

        if (accepted) await insert;
        else Assert.Equal(PostgresErrorCodes.CheckViolation, (await Assert.ThrowsAsync<PostgresException>(() => insert)).SqlState);
        await transaction.RollbackAsync(Ct);
    }

    [Fact]
    public async Task ScheduleLease_RequiresAnExpiry_AndDeletingTheEnvironmentCascades()
    {
        await using var connection = await OpenAsync(ConnectionStringOrSkip(), Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);
        var environment = await InsertEnvironmentAsync(connection, transaction);

        await transaction.SaveAsync("lease", Ct);
        var exception = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            connection, transaction,
            "INSERT INTO dokpod.image_update_schedules (environment_id, next_due_at_utc, lease_owner) VALUES (@id, now(), 'replica-a')",
            Ct, new NpgsqlParameter("id", environment)));
        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        await transaction.RollbackAsync("lease", Ct);

        await ExecuteAsync(
            connection, transaction,
            "INSERT INTO dokpod.image_update_schedules (environment_id, next_due_at_utc, lease_owner, lease_until_utc) VALUES (@id, now(), 'replica-a', now())",
            Ct, new NpgsqlParameter("id", environment));
        await InsertCheckAsync(connection, transaction, environment, "up_to_date", null);
        await ExecuteAsync(connection, transaction, "DELETE FROM dokpod.environments WHERE environment_id = @id", Ct, new NpgsqlParameter("id", environment));

        Assert.Equal(0L, await ScalarAsync<long>(
            connection, transaction,
            "SELECT (SELECT count(*) FROM dokpod.image_update_schedules WHERE environment_id = @id) + (SELECT count(*) FROM dokpod.image_update_checks WHERE environment_id = @id)",
            Ct, new NpgsqlParameter("id", environment)));
        await transaction.RollbackAsync(Ct);
    }

    [Theory]
    [InlineData(7, true)]
    [InlineData(1, true)]
    [InlineData(365, true)]
    [InlineData(0, false)]
    [InlineData(366, false)]
    [InlineData(null, false)]
    public async Task AlertRule_ImageOutdated_NeedsDaysBetween1And365(int? days, bool accepted)
    {
        await using var connection = await OpenAsync(ConnectionStringOrSkip(), Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);

        var insert = InsertRuleAsync(connection, transaction, "image_outdated", days);

        if (accepted) await insert;
        else Assert.Equal(PostgresErrorCodes.CheckViolation, (await Assert.ThrowsAsync<PostgresException>(() => insert)).SqlState);
        await transaction.RollbackAsync(Ct);
    }

    [Fact]
    public async Task EventRule_CannotCarryDays()
    {
        await using var connection = await OpenAsync(ConnectionStringOrSkip(), Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => InsertRuleAsync(connection, transaction, "event", 7));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        await transaction.RollbackAsync(Ct);
    }

    [Fact]
    public async Task DownThenUp_RemovesTheImageRulesAndTheirAlerts_AndKeepsTheOthers()
    {
        await using var connection = await OpenAsync(ConnectionStringOrSkip(), Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);
        var imageRule = await InsertRuleAsync(connection, transaction, "image_outdated", 7);
        var eventRule = await InsertRuleAsync(connection, transaction, "event", null);
        await ExecuteAsync(
            connection, transaction,
            """
            INSERT INTO dokpod.alerts (alert_id, rule_id, environment_id, target_key, severity, state, condition_text, opened_at_utc, last_occurred_at_utc)
            VALUES (gen_random_uuid(), @rule, gen_random_uuid(), 'container:t025', 'medium', 'open', 'imagem desatualizada', now(), now())
            """,
            Ct, new NpgsqlParameter("rule", imageRule));

        await ExecuteAsync(connection, transaction, await File.ReadAllTextAsync(RepositoryFile($"{MigrationScripts}/Down/01-ImageUpdateCheck.sql"), Ct), Ct);

        Assert.Null(await ScalarAsync<string>(connection, transaction, "SELECT to_regclass('dokpod.image_update_checks')::text", Ct));
        Assert.Equal(0L, await ScalarAsync<long>(
            connection, transaction, "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'dokpod' AND table_name = 'alert_rules' AND column_name = 'outdated_days'", Ct));
        Assert.Equal(0L, await ScalarAsync<long>(connection, transaction, "SELECT count(*) FROM dokpod.alerts WHERE rule_id = @rule", Ct, new NpgsqlParameter("rule", imageRule)));
        Assert.Equal(1L, await ScalarAsync<long>(connection, transaction, "SELECT count(*) FROM dokpod.alert_rules WHERE rule_id = @rule", Ct, new NpgsqlParameter("rule", eventRule)));

        await ExecuteAsync(connection, transaction, await File.ReadAllTextAsync(RepositoryFile($"{MigrationScripts}/Up/01-ImageUpdateCheck.sql"), Ct), Ct);

        Assert.True(await ScalarAsync<bool>(connection, transaction, "SELECT to_regclass('dokpod.image_update_checks') IS NOT NULL", Ct));
        await transaction.RollbackAsync(Ct);
    }

    private static async Task<Guid> InsertEnvironmentAsync(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            connection, transaction,
            "INSERT INTO dokpod.environments (environment_id, name, host, enabled, scopes, created_at_utc) VALUES (@id, @name, 'host-t025', true, 'environment:read', now())",
            Ct, new NpgsqlParameter("id", id), new NpgsqlParameter("name", $"t025-{id:N}"));
        return id;
    }

    private static Task InsertCheckAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid environment, string state, string? reason) => ExecuteAsync(
        connection, transaction,
        """
        INSERT INTO dokpod.image_update_checks (environment_id, reference, platform, local_image_id, state, reason, remote_digest, checked_at_utc)
        VALUES (@id, 'docker.io/library/nginx:1.28', 'linux/amd64', @digest, @state, @reason, @digest, now())
        """,
        Ct,
        new NpgsqlParameter("id", environment),
        new NpgsqlParameter("digest", Digest),
        new NpgsqlParameter("state", state),
        new NpgsqlParameter("reason", (object?)reason ?? DBNull.Value));

    private static async Task<Guid> InsertRuleAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string kind, int? days)
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            connection, transaction,
            """
            INSERT INTO dokpod.alert_rules (rule_id, name, condition_kind, event_type, outdated_days, severity)
            VALUES (@id, @name, @kind, @eventType, @days, 'medium')
            """,
            Ct,
            new NpgsqlParameter("id", id),
            new NpgsqlParameter("name", $"t025-{id:N}"),
            new NpgsqlParameter("kind", kind),
            new NpgsqlParameter("eventType", kind == "event" ? "container.died" : DBNull.Value),
            new NpgsqlParameter("days", (object?)days ?? DBNull.Value));
        return id;
    }
}
