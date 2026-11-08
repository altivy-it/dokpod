using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.Domain.Images;

namespace Dokpod.ControlPlane.Application.ImageUpdates;

/// <summary>
/// Métricas do aviso de nova versão (spec 025 T026), no mesmo meter do plano de controle. As dimensões são o estado e o
/// motivo (conjuntos fechados de <see cref="ImageUpdateStates"/> e <see cref="ImageUpdateReasons"/>) e o ID do host;
/// nunca referência, registro, repositório ou digest. Fica fora de <see cref="ControlPlaneMetrics"/> para a CE não
/// carregar nada da verificação.
/// </summary>
public sealed class ImageUpdateMetrics : IDisposable
{
    private readonly Meter meter = new(ControlPlaneMetrics.MeterName, "1.0.0");
    private readonly ConcurrentDictionary<Guid, long> outdated = new();
    private readonly Counter<long> checks;

    public ImageUpdateMetrics()
    {
        checks = meter.CreateCounter<long>(
            "dokpod.image_update.checks", "{check}", "Resultados de verificação gravados, por estado e motivo.");
        meter.CreateObservableGauge(
            "dokpod.image_update.outdated",
            () => outdated.Select(entry => new Measurement<long>(entry.Value, new KeyValuePair<string, object?>("environment.id", entry.Key.ToString("D")))),
            "{reference}",
            "Referências do host com update_available no último resultado completo.");
    }

    /// <summary>Meter desta instância (o listener de teste filtra por ela).</summary>
    public Meter Meter => meter;

    public void Checked(IEnumerable<ImageUpdateResult> results)
    {
        foreach (var group in results.GroupBy(r => (r.State, r.Reason)))
        {
            checks.Add(group.Count(), new TagList { { "image_update.state", group.Key.State }, { "image_update.reason", group.Key.Reason ?? string.Empty } });
        }
    }

    /// <summary>Referências distintas em <c>update_available</c> nos resultados gravados do host.</summary>
    public void Observe(Guid environmentId, IEnumerable<ImageUpdateCheck> stored) =>
        outdated[environmentId] = stored.Where(c => c.State == ImageUpdateStates.UpdateAvailable).Select(c => c.Reference).Distinct(StringComparer.Ordinal).LongCount();

    public void Dispose() => meter.Dispose();
}
