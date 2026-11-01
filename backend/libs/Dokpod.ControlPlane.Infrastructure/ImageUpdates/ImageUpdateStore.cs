using System.Data.Common;
using Dokpod.ControlPlane.Application.ImageUpdates;
using Dokpod.Domain.Images;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using static Dokpod.ControlPlane.Infrastructure.AuditExport.AuditExportSql;

namespace Dokpod.ControlPlane.Infrastructure.ImageUpdates;

/// <summary>
/// Agenda por host com lease e último resultado por referência (spec 025 data-model.md, D-006, D-007). O instante de cada
/// resultado é o do recebimento aqui, não o relógio do agente: assim "não renovada por este pedido" é comparável com o
/// instante do pedido mesmo com relógios diferentes.
/// </summary>
public sealed class ImageUpdateStore(ControlPlaneDbContext dbContext) : IImageUpdateStore
{
    private const string Columns =
        "environment_id, reference, platform, local_image_id, state, reason, remote_digest, checked_at_utc, last_success_at_utc, outdated_since_utc";

    public async Task EnsureScheduledAsync(IReadOnlyCollection<Guid> environmentIds, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        if (environmentIds.Count == 0) return;
        await ExecuteAsync(
            dbContext,
            """
            INSERT INTO dokpod.image_update_schedules (environment_id, next_due_at_utc)
            SELECT e.environment_id, @now FROM dokpod.environments e WHERE e.environment_id = ANY(@ids)
            ON CONFLICT (environment_id) DO NOTHING
            """,
            [Ids(environmentIds), P("now", nowUtc)],
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> ListDueAsync(DateTimeOffset nowUtc, int limit, CancellationToken cancellationToken) =>
        await QueryAsync(
            dbContext,
            """
            SELECT environment_id FROM dokpod.image_update_schedules
            WHERE next_due_at_utc <= @now AND (lease_owner IS NULL OR lease_until_utc <= @now)
            ORDER BY next_due_at_utc
            LIMIT @limit
            """,
            [P("now", nowUtc), P("limit", limit)],
            reader => reader.GetGuid(0),
            cancellationToken).ConfigureAwait(false);

    public async Task<bool> TryAcquireAsync(Guid environmentId, string owner, DateTimeOffset nowUtc, CancellationToken cancellationToken) =>
        await ExecuteAsync(
            dbContext,
            """
            UPDATE dokpod.image_update_schedules SET lease_owner = @owner, lease_until_utc = @until
            WHERE environment_id = @id AND next_due_at_utc <= @now
              AND (lease_owner IS NULL OR lease_owner = @owner OR lease_until_utc <= @now)
            """,
            [P("id", environmentId), P("owner", owner), P("now", nowUtc), P("until", nowUtc + ImageUpdateLimits.LeaseDuration)],
            cancellationToken).ConfigureAwait(false) == 1;

    public async Task<bool> MarkRequestedAsync(
        Guid environmentId, string owner, Guid requestId, DateTimeOffset nowUtc, DateTimeOffset nextDueAtUtc, CancellationToken cancellationToken) =>
        await ExecuteAsync(
            dbContext,
            """
            UPDATE dokpod.image_update_schedules
            SET pending_request_id = @request, last_requested_at_utc = @now, next_due_at_utc = @next, lease_owner = NULL, lease_until_utc = NULL
            WHERE environment_id = @id AND lease_owner = @owner
            """,
            [P("id", environmentId), P("owner", owner), P("request", requestId), P("now", nowUtc), P("next", nextDueAtUtc)],
            cancellationToken).ConfigureAwait(false) == 1;

    public async Task ReleaseAsync(Guid environmentId, string owner, CancellationToken cancellationToken) =>
        await ExecuteAsync(
            dbContext,
            "UPDATE dokpod.image_update_schedules SET lease_owner = NULL, lease_until_utc = NULL WHERE environment_id = @id AND lease_owner = @owner",
            [P("id", environmentId), P("owner", owner)],
            cancellationToken).ConfigureAwait(false);

    public async Task<ImageUpdateManualRequest> RequestNowAsync(Guid environmentId, DateTimeOffset nowUtc, bool manual, CancellationToken cancellationToken)
    {
        await EnsureScheduledAsync([environmentId], nowUtc, cancellationToken).ConfigureAwait(false);
        var updated = await QueryAsync(
            dbContext,
            manual
                ? """
                  UPDATE dokpod.image_update_schedules SET next_due_at_utc = @now, last_manual_at_utc = @now
                  WHERE environment_id = @id AND (last_manual_at_utc IS NULL OR last_manual_at_utc <= @cooldown)
                  RETURNING next_due_at_utc
                  """
                : """
                  UPDATE dokpod.image_update_schedules SET next_due_at_utc = LEAST(next_due_at_utc, @now)
                  WHERE environment_id = @id
                  RETURNING next_due_at_utc
                  """,
            [P("id", environmentId), P("now", nowUtc), P("cooldown", nowUtc - ImageUpdateLimits.ManualCooldown)],
            reader => Instant(reader, 0)!.Value,
            cancellationToken).ConfigureAwait(false);
        if (updated.Count == 1) return new ImageUpdateManualRequest(ImageUpdateManualOutcome.Accepted, updated[0], null);

        var schedule = (await GetScheduleAsync(environmentId, cancellationToken).ConfigureAwait(false))!;
        return new ImageUpdateManualRequest(ImageUpdateManualOutcome.RateLimited, schedule.NextDueAtUtc, schedule.LastManualAtUtc + ImageUpdateLimits.ManualCooldown);
    }

    public async Task<ImageUpdateApplyOutcome> ApplyPageAsync(
        Guid environmentId, Guid requestId, IReadOnlyList<ImageUpdateResult> results, bool isLastPage, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var requestedAt = (await QueryAsync(
            dbContext,
            """
            SELECT last_requested_at_utc FROM dokpod.image_update_schedules
            WHERE environment_id = @id AND pending_request_id = @request AND last_requested_at_utc >= @deadline
            FOR UPDATE
            """,
            [P("id", environmentId), P("request", requestId), P("deadline", nowUtc - ImageUpdateLimits.ResultDeadline)],
            reader => Instant(reader, 0),
            cancellationToken).ConfigureAwait(false)).SingleOrDefault();
        if (requestedAt is null) return ImageUpdateApplyOutcome.Discarded;

        foreach (var result in results) await UpsertAsync(environmentId, result, nowUtc, cancellationToken).ConfigureAwait(false);
        if (isLastPage)
        {
            // Referências que não vieram neste pedido saíram de uso (ou mudaram de plataforma); as decididas aqui no mesmo
            // instante do pedido (fora da política, sem capability) ficam.
            await ExecuteAsync(
                dbContext,
                "DELETE FROM dokpod.image_update_checks WHERE environment_id = @id AND checked_at_utc < @requested",
                [P("id", environmentId), P("requested", requestedAt.Value)],
                cancellationToken).ConfigureAwait(false);
            await ExecuteAsync(
                dbContext,
                "UPDATE dokpod.image_update_schedules SET pending_request_id = NULL WHERE environment_id = @id",
                [P("id", environmentId)],
                cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ImageUpdateApplyOutcome.Applied;
    }

    public async Task UpsertLocalAsync(Guid environmentId, IReadOnlyList<ImageUpdateResult> results, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        foreach (var result in results) await UpsertAsync(environmentId, result, nowUtc, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ImageUpdateCheck>> ListAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken)
    {
        if (environmentIds.Count == 0) return [];
        return await QueryAsync(
            dbContext,
            $"SELECT {Columns} FROM dokpod.image_update_checks WHERE environment_id = ANY(@ids)",
            [Ids(environmentIds)],
            Read,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<ImageUpdateScheduleState?> GetScheduleAsync(Guid environmentId, CancellationToken cancellationToken) =>
        (await QueryAsync(
            dbContext,
            """
            SELECT environment_id, next_due_at_utc, last_requested_at_utc, last_manual_at_utc, pending_request_id, lease_owner, lease_until_utc
            FROM dokpod.image_update_schedules WHERE environment_id = @id
            """,
            [P("id", environmentId)],
            reader => new ImageUpdateScheduleState(
                reader.GetGuid(0), Instant(reader, 1)!.Value, Instant(reader, 2), Instant(reader, 3),
                reader.IsDBNull(4) ? null : reader.GetGuid(4), reader.IsDBNull(5) ? null : reader.GetString(5), Instant(reader, 6)),
            cancellationToken).ConfigureAwait(false)).SingleOrDefault();

    /// <summary>
    /// Transições de data-model.md: <c>outdated_since</c> nasce na entrada em <c>update_available</c>, fica em <c>failed</c> e
    /// some nos demais; <c>failed</c> preserva o último digest e a última verificação boa (SC-002).
    /// </summary>
    private Task<int> UpsertAsync(Guid environmentId, ImageUpdateResult result, DateTimeOffset nowUtc, CancellationToken cancellationToken) =>
        ExecuteAsync(
            dbContext,
            """
            INSERT INTO dokpod.image_update_checks AS c (
                environment_id, reference, platform, local_image_id, state, reason, remote_digest, checked_at_utc, last_success_at_utc, outdated_since_utc)
            VALUES (
                @id, @reference, @platform, @image, @state, @reason,
                CASE WHEN @state IN ('up_to_date', 'update_available', 'failed') THEN @digest END,
                @now,
                CASE WHEN @state IN ('up_to_date', 'update_available') THEN @now END,
                CASE WHEN @state = 'update_available' THEN @now END)
            ON CONFLICT (environment_id, reference, platform) DO UPDATE SET
                local_image_id = EXCLUDED.local_image_id,
                state = EXCLUDED.state,
                reason = EXCLUDED.reason,
                remote_digest = CASE
                    WHEN EXCLUDED.state IN ('up_to_date', 'update_available') THEN EXCLUDED.remote_digest
                    WHEN EXCLUDED.state = 'failed' THEN c.remote_digest
                    END,
                checked_at_utc = EXCLUDED.checked_at_utc,
                last_success_at_utc = CASE WHEN EXCLUDED.state IN ('up_to_date', 'update_available') THEN EXCLUDED.checked_at_utc ELSE c.last_success_at_utc END,
                outdated_since_utc = CASE
                    WHEN EXCLUDED.state = 'update_available' THEN COALESCE(c.outdated_since_utc, EXCLUDED.checked_at_utc)
                    WHEN EXCLUDED.state = 'failed' THEN c.outdated_since_utc
                    END
            """,
            [
                P("id", environmentId), P("reference", result.Reference), P("platform", result.Platform), P("image", result.LocalImageId),
                P("state", result.State), P("reason", result.Reason), P("digest", result.RemoteDigest), P("now", nowUtc),
            ],
            cancellationToken);

    private static NpgsqlParameter Ids(IReadOnlyCollection<Guid> ids) => new("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = ids.ToArray() };

    private static ImageUpdateCheck Read(DbDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.IsDBNull(3) ? null : reader.GetString(3),
        reader.GetString(4),
        reader.IsDBNull(5) ? null : reader.GetString(5),
        reader.IsDBNull(6) ? null : reader.GetString(6),
        Instant(reader, 7)!.Value,
        Instant(reader, 8),
        Instant(reader, 9));
}
