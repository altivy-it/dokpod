using System.Collections.Concurrent;

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
/// Um ciclo de envio (spec 027 T021, D-001, D-003): por destino ativo, assume ou renova o lease, lê até 500 eventos
/// após o cursor, envia e só então avança o cursor. Não consulta o gate da licença: com a licença restrita o envio
/// continua (FR-008). Uma falha grava só o código, sem detalhes do servidor, e espera antes de tentar de novo.
/// </summary>
public sealed class AuditExportSender(
    IAuditExportStore store,
    IAuditTrailExportReader trail,
    IEnumerable<IAuditExportTransport> transports,
    IAuditExportInstallation installation,
    AuditExportBackoff backoff,
    TimeProvider timeProvider)
{
    /// <returns>Quantos eventos foram confirmados pelos destinos neste ciclo.</returns>
    public async Task<int> RunOnceAsync(string owner, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        var delivered = 0;
        Guid? installationId = null;
        foreach (var listed in await store.ListAsync(cancellationToken).ConfigureAwait(false))
        {
            var id = listed.Destination.DestinationId;
            var now = timeProvider.GetUtcNow();
            if (!listed.Destination.Active || backoff.IsWaiting(id, now)) continue;
            if (!await store.TryAcquireLeaseAsync(id, owner, now, AuditExportLimits.LeaseDuration, cancellationToken).ConfigureAwait(false)) continue;

            // Relido com o lease: outra réplica pode ter avançado o cursor antes.
            if (await store.GetAsync(id, cancellationToken).ConfigureAwait(false) is not { Destination.Active: true } state) continue;
            // Horizonte lido antes do lote: tudo abaixo dele já está no lote ou fora do filtro (D-001).
            var horizon = await trail.HorizonAsync(cancellationToken).ConfigureAwait(false);
            var items = await trail.ReadAsync(state.Cursor.Position, state.Destination.Actions, AuditExportLimits.MaxBatch, cancellationToken).ConfigureAwait(false);
            if (items.Count == 0)
            {
                // Eventos fora do filtro ficam para trás: mudar as categorias depois não reenvia o passado.
                if (horizon.CompareTo(state.Cursor.Position) > 0) await store.AdvanceAsync(id, owner, horizon, null, cancellationToken).ConfigureAwait(false);
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
                await store.RecordFailureAsync(id, owner, result.ErrorCode ?? AuditExportErrorCodes.Rejected, after, cancellationToken).ConfigureAwait(false);
                backoff.Fail(id, after);
            }
        }

        return delivered;
    }
}
