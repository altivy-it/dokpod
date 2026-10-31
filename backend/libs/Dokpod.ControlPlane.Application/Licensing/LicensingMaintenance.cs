using System.Globalization;
using System.Text.Json.Nodes;

namespace Dokpod.ControlPlane.Application.Licensing;

/// <summary>
/// Rotina periódica do licenciamento (spec 031 T047): reavalia e grava o estado, emite os avisos de vencimento uma vez
/// por marco (FR-032) e grava um <c>capacity.daily_summary</c> por dia UTC encerrado (FR-035), com <c>late: true</c>
/// para os dias que a API passou parada. O passado é limitado aos 35 dias de dados brutos retidos.
/// </summary>
public sealed class LicensingMaintenance(
    LicenseStatusService statusService,
    ILicensingEvaluationStore evaluationStore,
    ICapacityLedger ledger,
    ILicensingAuditWriter auditWriter,
    TimeProvider timeProvider)
{
    public static readonly IReadOnlyList<int> ExpiryWarningDays = [60, 30, 7];
    public const int MaxBackfillDays = 35;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var assessment = await statusService.AssessAsync(cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        await WarnAsync(assessment.Status, now, cancellationToken).ConfigureAwait(false);
        await SummarizeAsync(assessment.Status.VcpuBand, now, cancellationToken).ConfigureAwait(false);
    }

    private async Task WarnAsync(LicenseStatus status, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (status.License is not { } license || status.Validity != "active") return;

        var remaining = license.ExpiresAt - now;
        var recorded = await evaluationStore.RecordedExpiryWarningsAsync(license.ExpiresAt, cancellationToken).ConfigureAwait(false);
        foreach (var days in ExpiryWarningDays.Where(days => remaining <= TimeSpan.FromDays(days) && !recorded.Contains(days)))
        {
            await auditWriter.AppendAsync(
                new LicensingEvent(Guid.NewGuid(), now, null, Guid.NewGuid(), LicensingEventAction.LicenseExpiryWarning, new JsonObject
                {
                    ["daysBefore"] = days,
                    ["expiresAt"] = LicenseStatusService.Iso(license.ExpiresAt),
                }),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SummarizeAsync(int? vcpuBand, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var last = await evaluationStore.LastDailySummaryDateAsync(cancellationToken).ConfigureAwait(false);
        var first = last?.AddDays(1) ?? today.AddDays(-1);
        if (first < today.AddDays(-MaxBackfillDays)) first = today.AddDays(-MaxBackfillDays);

        for (var day = first; day < today; day = day.AddDays(1))
        {
            var dayStart = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var dayEnd = dayStart.AddDays(1);
            var input = await ledger.ReadMeteringInputAsync(dayStart, cancellationToken).ConfigureAwait(false);
            // Sem licença (auditoria inicial da contratação) não há faixa: o resumo leva só a CPU de cada host.
            var snapshot = CapacityMeter.Compute(input, vcpuBand ?? 1, dayEnd, windowDays: 1);
            var hosts = new JsonArray();
            foreach (var host in snapshot.Hosts.Where(host => !host.Removed).OrderBy(host => host.HostId))
            {
                hosts.Add(new JsonObject { ["hostId"] = host.HostId.ToString("D"), ["maxVcpus"] = host.Vcpus, ["source"] = host.Source });
            }

            await auditWriter.AppendAsync(
                new LicensingEvent(Guid.NewGuid(), now, null, Guid.NewGuid(), LicensingEventAction.CapacityDailySummary, new JsonObject
                {
                    ["date"] = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ["vcpuBand"] = vcpuBand,
                    ["peakNodeEquivalents"] = vcpuBand is null ? null : snapshot.WindowPeak,
                    ["unknownHosts"] = snapshot.UnknownHosts,
                    ["hosts"] = hosts,
                    ["late"] = day < today.AddDays(-1),
                }),
                cancellationToken).ConfigureAwait(false);
        }
    }
}
