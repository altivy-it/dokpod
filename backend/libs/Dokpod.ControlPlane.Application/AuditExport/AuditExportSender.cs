using System.Collections.Concurrent;
using System.Globalization;
using Dokpod.ControlPlane.Application.Auditing;
using Dokpod.Domain.Auditing;

namespace Dokpod.ControlPlane.Application.AuditExport;

/// <summary>Espera exponencial por destino depois de falha (1 s, 2 s, 4 s… até 60 s), em memória da réplica.</summary>
public sealed class AuditExportBackoff
{
    private readonly ConcurrentDictionary<Guid, (int Failures, DateTimeOffset NextAttemptUtc)> entries = new();

    public bool IsWaiting(Guid destinationId, DateTimeOffset nowUtc) =>
        entries.TryGetValue(destinationId, out var entry) && nowUtc < entry.NextAttemptUtc;

    public void Fail(Guid destinationId, DateTimeOffset nowUtc) =>
        entries.AddOrUpdate(
            destinationId,
            _ => (1, nowUtc + Delay(1)),
            (_, entry) => (entry.Failures + 1, nowUtc + Delay(entry.Failures + 1)));

    public void Reset(Guid destinationId) => entries.TryRemove(destinationId, out _);

    public static TimeSpan Delay(int failures) =>
        TimeSpan.FromSeconds(Math.Min(AuditExportLimits.MaxBackoff.TotalSeconds, Math.Pow(2, Math.Clamp(failures - 1, 0, 16))));
}

/// <summary>
/// Um ciclo de envio (spec 027 T021, T026, D-001, D-002, D-003): por destino ativo, assume ou renova o lease, pula a
/// lacuna de pendentes com mais de 72 h (gravando <c>audit_export.gap</c>), lê até 500 eventos após o cursor, envia e
/// só então avança o cursor. Falha, inclusive <c>rejected</c>, grava só o código e espera antes de tentar de novo; o
/// cursor fica parado. Não consulta o gate da licença: com a licença restrita o envio continua (FR-008).
/// </summary>
public sealed class AuditExportSender(
    IAuditExportStore store,
    IAuditTrailExportReader trail,
    IEnumerable<IAuditExportTransport> transports,
    IAuditExportInstallation installation,
    IAuditEventWriter audit,
    AuditExportBackoff backoff,
    AuditExportMetrics metrics,
    TimeProvider timeProvider)
{
    public const string ActorId = "dokpod-audit-export";
    public const string GapFailureCode = "retention_window_exceeded";

    /// <returns>Quantos eventos foram confirmados pelos destinos neste ciclo.</returns>
    public async Task<int> RunOnceAsync(string owner, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        var delivered = 0;
        Guid? installationId = null;
        var listed = await store.ListAsync(cancellationToken).ConfigureAwait(false);
        metrics.Retain([.. listed.Where(item => item.Destination.Active).Select(item => item.Destination.DestinationId)]);
        foreach (var entry in listed)
        {
            var id = entry.Destination.DestinationId;
            var now = timeProvider.GetUtcNow();
            if (!entry.Destination.Active) continue;
            if (backoff.IsWaiting(id, now))
            {
                await ObserveAsync(entry, now, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (!await store.TryAcquireLeaseAsync(id, owner, now, AuditExportLimits.LeaseDuration, cancellationToken).ConfigureAwait(false)) continue;

            // Relido com o lease: outra réplica pode ter avançado o cursor antes.
            if (await store.GetAsync(id, cancellationToken).ConfigureAwait(false) is not { Destination.Active: true } state) continue;
            if (await SkipGapAsync(state, owner, now, cancellationToken).ConfigureAwait(false))
            {
                state = await store.GetAsync(id, cancellationToken).ConfigureAwait(false);
                if (state is null) continue;
            }

            // Horizonte lido antes do lote: tudo abaixo dele já está no lote ou fora do filtro (D-001).
            var horizon = await trail.HorizonAsync(cancellationToken).ConfigureAwait(false);
            var items = await trail.ReadAsync(state.Cursor.Position, state.Destination.Actions, AuditExportLimits.MaxBatch, cancellationToken).ConfigureAwait(false);
            if (items.Count == 0)
            {
                // Eventos fora do filtro ficam para trás: mudar as categorias depois não reenvia o passado.
                if (horizon.CompareTo(state.Cursor.Position) > 0) await store.AdvanceAsync(id, owner, horizon, null, cancellationToken).ConfigureAwait(false);
                await ObserveAsync(state, now, cancellationToken).ConfigureAwait(false);
                continue;
            }

            installationId ??= await installation.GetIdAsync(cancellationToken).ConfigureAwait(false);
            var batch = items.Select(item => AuditExportRecord.FromEvent(item.Event, installationId.Value)).ToArray();
            var transport = transports.FirstOrDefault(candidate => candidate.Protocol == state.Destination.Protocol);
            var result = transport is null
                ? AuditExportSendResult.Failed(AuditExportErrorCodes.Rejected)
                : await transport.SendAsync(state.Destination, batch, cancellationToken).ConfigureAwait(false);

            var after = timeProvider.GetUtcNow();
            if (result.Delivered)
            {
                var last = items[^1].Position;
                var target = items.Count < AuditExportLimits.MaxBatch && horizon.CompareTo(last) > 0 ? horizon : last;
                await store.AdvanceAsync(id, owner, target, after, cancellationToken).ConfigureAwait(false);
                backoff.Reset(id);
                delivered += items.Count;
            }
            else
            {
                var code = result.ErrorCode ?? AuditExportErrorCodes.Rejected;
                await store.RecordFailureAsync(id, owner, code, after, cancellationToken).ConfigureAwait(false);
                backoff.Fail(id, after);
                metrics.Failed(id, code);
            }

            if (await store.GetAsync(id, cancellationToken).ConfigureAwait(false) is { } current) await ObserveAsync(current, after, cancellationToken).ConfigureAwait(false);
        }

        return delivered;
    }

    /// <summary>
    /// D-002: se o próximo pendente passou de 72 h, o cursor pula até o primeiro evento dentro da janela e a trilha
    /// ganha <c>audit_export.gap</c> na mesma transação, com o primeiro e o último instante pulados e a quantidade.
    /// </summary>
    private async Task<bool> SkipGapAsync(AuditExportDestinationState state, string owner, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var destination = state.Destination;
        var gap = await trail.GapAsync(state.Cursor.Position, destination.Actions, now - AuditExportLimits.GapWindow, cancellationToken).ConfigureAwait(false);
        if (gap is null) return false;

        var skipped = await store.SkipGapAsync(destination.DestinationId, owner, gap.Last, now, ct =>
        {
            var auditEvent = AuditEvent.Create(
                    Guid.NewGuid(), now, Guid.NewGuid(), AuditActorKind.System, ActorId, "audit_export.gap", AuditEvent.InstallationScopeId,
                    AuditOutcome.Failed, GapFailureCode)
                .WithSubject(destination.DestinationId.ToString("D"), new Dictionary<string, string>
                {
                    ["destinationId"] = destination.DestinationId.ToString("D"),
                    ["fromUtc"] = Iso(gap.FromUtc),
                    ["toUtc"] = Iso(gap.ToUtc),
                    ["count"] = gap.Count.ToString(CultureInfo.InvariantCulture),
                });
            return audit.AppendAsync(auditEvent, ct);
        }, cancellationToken).ConfigureAwait(false);
        if (skipped) metrics.Gap(destination.DestinationId);
        return skipped;
    }

    private async Task ObserveAsync(AuditExportDestinationState state, DateTimeOffset now, CancellationToken cancellationToken) =>
        metrics.Observe(
            state.Destination.DestinationId,
            await trail.PendingAsync(state.Cursor.Position, state.Destination.Actions, AuditExportLimits.MaxPendingCount, cancellationToken).ConfigureAwait(false),
            now);

    private static string Iso(DateTimeOffset instant) =>
        instant.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture);
}
