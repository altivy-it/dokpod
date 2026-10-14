using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;
using Dokpod.ControlPlane.Application.Auditing;
using Dokpod.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dokpod.ControlPlane.Infrastructure;

public sealed class PostgresAuditEventWriter(
    ControlPlaneDbContext dbContext,
    AuditEventMetrics metrics) : IAuditEventWriter
{
    public PostgresAuditEventWriter(ControlPlaneDbContext dbContext)
        : this(dbContext, new AuditEventMetrics())
    {
    }

    public async Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        metrics.RecordAttempt();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var payloadHash = ComputePayloadHash(auditEvent);
            if (dbContext.Database.IsRelational())
            {
                await AppendRelationalAsync(auditEvent, payloadHash, cancellationToken).ConfigureAwait(false);
                return;
            }

            var key = await dbContext.AuditEventKeys
                .AsTracking()
                .SingleOrDefaultAsync(x => x.EventId == auditEvent.EventId, cancellationToken)
                .ConfigureAwait(false);

            if (key is not null)
            {
                EnsureSamePayload(key.PayloadHash, payloadHash, auditEvent.EventId);
                return;
            }

            dbContext.AuditEvents.Add(new AuditEventEntity
            {
                EventId = auditEvent.EventId,
                OccurredAtUtc = auditEvent.OccurredAtUtc,
                CorrelationId = auditEvent.CorrelationId,
                ActorKind = (int)auditEvent.ActorKind,
                ActorId = auditEvent.ActorId,
                Action = auditEvent.Action,
                EnvironmentId = auditEvent.EnvironmentId,
                CommandId = auditEvent.CommandId,
                Outcome = (int)auditEvent.Outcome,
                FailureCode = auditEvent.FailureCode,
                SubjectId = auditEvent.SubjectId,
                Details = auditEvent.Details is null ? null : System.Text.Json.JsonSerializer.Serialize(auditEvent.Details),
            });
            dbContext.AuditEventKeys.Add(new AuditEventKeyEntity
            {
                EventId = auditEvent.EventId,
                PayloadHash = payloadHash,
            });

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            metrics.RecordFailure();
            throw;
        }
        finally
        {
            metrics.RecordDuration(stopwatch.Elapsed);
        }
    }

    private async Task AppendRelationalAsync(
        AuditEvent auditEvent,
        byte[] payloadHash,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is not null)
        {
            await ExecuteAppendAsync(auditEvent, payloadHash, cancellationToken).ConfigureAwait(false);
            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await ExecuteAppendAsync(auditEvent, payloadHash, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            metrics.RecordConflict();
            throw new InvalidOperationException(
                $"Audit event {auditEvent.EventId} already exists with conflicting payload.",
                exception);
        }
    }

    private Task ExecuteAppendAsync(
        AuditEvent auditEvent,
        byte[] payloadHash,
        CancellationToken cancellationToken) => auditEvent.SubjectId is not null
        ? dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT dokpod.dokpod_append_audit_event(
                {auditEvent.EventId},
                {auditEvent.OccurredAtUtc},
                {auditEvent.CorrelationId},
                {(short)auditEvent.ActorKind},
                {auditEvent.ActorId},
                {auditEvent.Action},
                {auditEvent.EnvironmentId},
                {auditEvent.CommandId},
                {(short)auditEvent.Outcome},
                {auditEvent.FailureCode},
                {payloadHash},
                {auditEvent.SubjectId},
                {System.Text.Json.JsonSerializer.Serialize(auditEvent.Details ?? new Dictionary<string, string>())}::jsonb)
            """, cancellationToken)
        : dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT dokpod.dokpod_append_audit_event(
                {auditEvent.EventId},
                {auditEvent.OccurredAtUtc},
                {auditEvent.CorrelationId},
                {(short)auditEvent.ActorKind},
                {auditEvent.ActorId},
                {auditEvent.Action},
                {auditEvent.EnvironmentId},
                {auditEvent.CommandId},
                {(short)auditEvent.Outcome},
                {auditEvent.FailureCode},
                {payloadHash})
            """, cancellationToken);

    private void EnsureSamePayload(byte[] existingHash, byte[] payloadHash, Guid eventId)
    {
        if (existingHash.AsSpan().SequenceEqual(payloadHash))
        {
            return;
        }

        metrics.RecordConflict();
        throw new InvalidOperationException(
            $"Audit event {eventId} already exists with conflicting payload.");
    }

    private static byte[] ComputePayloadHash(AuditEvent auditEvent)
    {
        var fields = new List<object?>
        {
            auditEvent.EventId,
            auditEvent.OccurredAtUtc.ToUniversalTime().ToString("O"),
            auditEvent.CorrelationId,
            (int)auditEvent.ActorKind,
            auditEvent.ActorId,
            auditEvent.Action,
            auditEvent.EnvironmentId,
        };
        if (auditEvent.CommandId is not null)
        {
            fields.Add(auditEvent.CommandId);
        }

        fields.Add((int)auditEvent.Outcome);
        fields.Add(auditEvent.FailureCode ?? string.Empty);
        if (auditEvent.SubjectId is not null)
        {
            fields.Add(auditEvent.SubjectId);
            fields.Add(string.Join(";", (auditEvent.Details ?? new Dictionary<string, string>())
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}={pair.Value}")));
        }
        var payload = string.Join("\u001f", fields);

        return SHA256.HashData(Encoding.UTF8.GetBytes(payload));
    }
}
