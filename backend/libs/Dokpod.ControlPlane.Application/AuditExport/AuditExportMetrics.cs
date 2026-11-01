using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Dokpod.ControlPlane.Application.Hosts;

namespace Dokpod.ControlPlane.Application.AuditExport;

/// <summary>
/// Métricas da exportação (spec 027 T027), no mesmo meter do plano de controle. O rótulo é só o ID do destino (no
/// máximo 5, conjunto fechado); nunca host, nome ou segredo. Fica fora de <see cref="ControlPlaneMetrics"/> para a CE
/// não carregar nada da exportação.
/// </summary>
public sealed class AuditExportMetrics : IDisposable
{
    private readonly Meter meter = new(ControlPlaneMetrics.MeterName, "1.0.0");
    private readonly ConcurrentDictionary<Guid, (long Pending, double LagSeconds)> destinations = new();
    private readonly Counter<long> failures;
    private readonly Counter<long> gaps;

    public AuditExportMetrics()
    {
        meter.CreateObservableGauge(
            "dokpod.audit_export.pending",
            () => destinations.Select(entry => new Measurement<long>(entry.Value.Pending, Tag(entry.Key))),
            "{event}",
            "Eventos confirmados ainda não entregues ao destino (contados até 10.001).");
        meter.CreateObservableGauge(
            "dokpod.audit_export.lag_seconds",
            () => destinations.Select(entry => new Measurement<double>(entry.Value.LagSeconds, Tag(entry.Key))),
            "s",
            "Idade do evento pendente mais antigo do destino.");
        failures = meter.CreateCounter<long>("dokpod.audit_export.failures", "{failure}", "Envios recusados ou sem resposta, por código.");
        gaps = meter.CreateCounter<long>("dokpod.audit_export.gaps", "{gap}", "Lacunas: pendentes com mais de 72 h pulados (D-002).");
    }

    /// <summary>Meter desta instância (o listener de teste filtra por ela).</summary>
    public Meter Meter => meter;

    public void Observe(Guid destinationId, AuditExportPending pending, DateTimeOffset nowUtc) =>
        destinations[destinationId] = (pending.Capped ? pending.Count + 1 : pending.Count, pending.OldestAtUtc is { } oldest ? Math.Max(0, (nowUtc - oldest).TotalSeconds) : 0);

    /// <summary>Destinos excluídos ou desativados deixam de ser medidos.</summary>
    public void Retain(IReadOnlyCollection<Guid> active)
    {
        foreach (var id in destinations.Keys.Where(id => !active.Contains(id))) destinations.TryRemove(id, out _);
    }

    public void Failed(Guid destinationId, string errorCode) =>
        failures.Add(1, new TagList { { "destination.id", destinationId.ToString("D") }, { "error.code", errorCode } });

    public void Gap(Guid destinationId) => gaps.Add(1, Tag(destinationId));

    private static KeyValuePair<string, object?> Tag(Guid destinationId) => new("destination.id", destinationId.ToString("D"));

    public void Dispose() => meter.Dispose();
}
