using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Dokpod.ControlPlane.Application.Licensing;

/// <summary>Relatório de apuração (contracts/report-format.md) antes de serializar.</summary>
public sealed record LicensingReport(
    Guid InstallationId,
    string? SlotId,
    DateTimeOffset GeneratedAtUtc,
    string Challenge,
    LicenseStatus Status,
    IReadOnlyList<LicensingHostView> Hosts,
    IReadOnlyList<LicensingChainRecord> Events);

/// <summary>
/// Exportação local do relatório (spec 031 T060, FR-033 a FR-035). Grava <c>license.report_exported</c> com o desafio
/// antes de montar o documento, então esse evento é o último da cadeia exportada. Funciona sem licença importada
/// (auditoria inicial da contratação). A cadeia é cortada nesse evento, então gravações concorrentes ou a própria
/// leitura do estado não o tiram do fim. Nada é enviado: o cliente entrega o arquivo ao fornecedor (D-013).
/// </summary>
public sealed partial class LicensingReportService(
    LicenseStatusService statusService,
    LicensingHostService hostService,
    ILicenseStateReader stateReader,
    ILicensingChainReader chainReader,
    ILicensingAuditWriter auditWriter,
    TimeProvider timeProvider)
{
    public static bool IsValidChallenge(string? challenge) => challenge is not null && ChallengePattern().IsMatch(challenge);

    public async Task<LicensingReport> ExportAsync(string challenge, string actorId, Guid correlationId, CancellationToken cancellationToken)
    {
        if (!IsValidChallenge(challenge)) throw new ArgumentException("Challenge must match [A-Za-z0-9_-]{8,64}.", nameof(challenge));
        var now = timeProvider.GetUtcNow();
        var reportEventId = Guid.NewGuid();
        await auditWriter.AppendAsync(
            new LicensingEvent(reportEventId, now, actorId, correlationId, LicensingEventAction.LicenseReportExported, new JsonObject { ["challenge"] = challenge }),
            cancellationToken).ConfigureAwait(false);

        var status = await statusService.GetAsync(cancellationToken).ConfigureAwait(false);
        var hosts = await hostService.ListAsync(cancellationToken).ConfigureAwait(false);
        var state = await stateReader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Licensing state must exist after the report event.");
        var events = await chainReader.ReadChainAsync(reportEventId, cancellationToken).ConfigureAwait(false);
        return new LicensingReport(state.InstallationId, state.InstallationSlotId, now, challenge, status, hosts, events);
    }

    [GeneratedRegex(@"\A[A-Za-z0-9_-]{8,64}\z")]
    private static partial Regex ChallengePattern();
}
