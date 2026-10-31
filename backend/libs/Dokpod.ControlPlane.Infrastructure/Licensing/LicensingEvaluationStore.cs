using System.Globalization;
using Dokpod.ControlPlane.Application.Licensing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace Dokpod.ControlPlane.Infrastructure.Licensing;

/// <summary>
/// Gravação da avaliação do licenciamento (spec 031 T045, D-006): o instante confiável só avança, prazos só são
/// gravados se ainda vazios (nunca recomeçam) e o excesso só é limpo quando regularizado. Tudo na mesma transação
/// dos eventos da cadeia.
/// </summary>
public sealed class LicensingEvaluationStore(ControlPlaneDbContext dbContext, ILicensingAuditWriter auditWriter) : ILicensingEvaluationStore
{
    public async Task<string?> LastRecordedEntitlementAsync(CancellationToken cancellationToken) =>
        (await LicensingSql.QueryAsync(
            dbContext,
            "SELECT details->>'to' FROM dokpod.licensing_events WHERE action = 'license.state_changed' ORDER BY chain_seq DESC LIMIT 1",
            [],
            reader => reader.IsDBNull(0) ? null : reader.GetString(0),
            cancellationToken).ConfigureAwait(false)).FirstOrDefault();

    public async Task ApplyAsync(LicensingStateUpdate update, IReadOnlyList<LicensingEvent> events, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(events);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var dbTransaction = transaction.GetDbTransaction();
        var connection = dbTransaction.Connection!;
        await LicensingSql.ExecuteAsync(connection, dbTransaction, "SELECT 1 FROM dokpod.licensing_state FOR UPDATE", [], cancellationToken).ConfigureAwait(false);
        await LicensingSql.ExecuteAsync(
            connection,
            dbTransaction,
            """
            UPDATE dokpod.licensing_state SET
                last_trusted_instant_utc = GREATEST(last_trusted_instant_utc, COALESCE(@trusted, last_trusted_instant_utc)),
                expiry_grace_until_utc = COALESCE(expiry_grace_until_utc, @expiry),
                excess_started_at_utc = CASE WHEN @clear THEN NULL ELSE COALESCE(excess_started_at_utc, @excessStarted) END,
                excess_grace_until_utc = CASE WHEN @clear THEN NULL ELSE COALESCE(excess_grace_until_utc, @excessUntil) END
            """,
            [
                Instant("trusted", update.LastTrustedInstantUtc),
                Instant("expiry", update.ExpiryGraceUntilUtc),
                Instant("excessStarted", update.ExcessStartedAtUtc),
                Instant("excessUntil", update.ExcessGraceUntilUtc),
                new NpgsqlParameter("clear", update.ClearExcess),
            ],
            cancellationToken).ConfigureAwait(false);
        foreach (var licensingEvent in events)
        {
            await auditWriter.AppendAsync(licensingEvent, cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlySet<int>> RecordedExpiryWarningsAsync(DateTimeOffset expiresAt, CancellationToken cancellationToken) =>
        (await LicensingSql.QueryAsync(
            dbContext,
            "SELECT (details->>'daysBefore')::int FROM dokpod.licensing_events WHERE action = 'license.expiry_warning' AND details->>'expiresAt' = @expiresAt",
            [new NpgsqlParameter("expiresAt", LicenseStatusService.Iso(expiresAt))],
            reader => reader.GetInt32(0),
            cancellationToken).ConfigureAwait(false)).ToHashSet();

    public async Task<DateOnly?> LastDailySummaryDateAsync(CancellationToken cancellationToken)
    {
        var value = (await LicensingSql.QueryAsync(
            dbContext,
            "SELECT max(details->>'date') FROM dokpod.licensing_events WHERE action = 'capacity.daily_summary'",
            [],
            reader => reader.IsDBNull(0) ? null : reader.GetString(0),
            cancellationToken).ConfigureAwait(false)).FirstOrDefault();
        return value is null ? null : DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public async Task<bool> ResetTrustedInstantAsync(DateTimeOffset now, long expectedVersion, LicensingEvent reset, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var dbTransaction = transaction.GetDbTransaction();
        var connection = dbTransaction.Connection!;
        var version = await LicensingSql.ScalarAsync<long?>(connection, dbTransaction, "SELECT version FROM dokpod.licensing_state FOR UPDATE", [], cancellationToken).ConfigureAwait(false);
        if (version != expectedVersion) return false;

        // Única escrita que baixa o instante confiável: a saída administrativa de indeterminate (L2), auditada.
        await LicensingSql.ExecuteAsync(
            connection,
            dbTransaction,
            "UPDATE dokpod.licensing_state SET last_trusted_instant_utc = @now, version = version + 1",
            [new NpgsqlParameter("now", now)],
            cancellationToken).ConfigureAwait(false);
        await auditWriter.AppendAsync(reset, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static NpgsqlParameter Instant(string name, DateTimeOffset? value) =>
        new(name, NpgsqlDbType.TimestampTz) { Value = (object?)value ?? DBNull.Value };
}
