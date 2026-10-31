using System.Text.RegularExpressions;

namespace Dokpod.ControlPlane.Application.Licensing;

public sealed record LicensingHostView(Guid HostId, string Label, bool Removed, IReadOnlyList<Guid> EnvironmentIds, int? Vcpus, string Source, int? NodeEquivalents);

/// <summary>
/// Hosts de licenciamento (spec 031 T039, D-004): um host físico ou VM com vários engines conta uma vez (FR-012). Cada
/// operação é idempotente pelo ID do host e do ambiente; a autorização (<c>licensing:manage</c>) fica na API.
/// </summary>
public sealed partial class LicensingHostService(
    ILicensingHostStore hosts,
    ICapacityLedger ledger,
    ILicenseStateReader stateReader,
    ILicenseStore licenseStore,
    LicenseStateEvaluator evaluator,
    TimeProvider timeProvider)
{
    public const int MaxLabelLength = 128;

    public static bool IsValidLabel(string? label) => label is { Length: > 0 and <= MaxLabelLength } && !ControlCharacters().IsMatch(label);

    public async Task<IReadOnlyList<LicensingHostView>> ListAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var status = evaluator.Evaluate(
            await stateReader.ReadAsync(cancellationToken).ConfigureAwait(false),
            await licenseStore.GetActiveAsync(cancellationToken).ConfigureAwait(false),
            now);
        var input = await ledger.ReadMeteringInputAsync(now.AddDays(-CapacityMeter.WindowDays), cancellationToken).ConfigureAwait(false);
        var snapshot = CapacityMeter.Compute(input, status.VcpuBand ?? 1, now);
        var links = await hosts.ListActiveLinksAsync(cancellationToken).ConfigureAwait(false);
        var capacity = snapshot.Hosts.ToDictionary(host => host.HostId);
        return (await hosts.ListHostsAsync(cancellationToken).ConfigureAwait(false))
            .OrderBy(host => host.Label, StringComparer.Ordinal)
            .Select(host =>
            {
                capacity.TryGetValue(host.HostId, out var measured);
                return new LicensingHostView(
                    host.HostId,
                    host.Label,
                    host.RemovedAtUtc is not null,
                    links.Where(link => link.HostId == host.HostId).Select(link => link.EnvironmentId).Order().ToArray(),
                    measured?.Vcpus,
                    measured?.Source ?? CapacitySource.Unknown,
                    status.VcpuBand is null ? null : measured?.NodeEquivalents);
            })
            .ToArray();
    }

    public Task<LicensingHostChange> CreateAsync(Guid hostId, string label, string actorId, Guid correlationId, CancellationToken cancellationToken) =>
        hosts.CreateHostAsync(hostId, label, actorId, correlationId, timeProvider.GetUtcNow(), cancellationToken);

    public Task<LicensingHostChange> LinkAsync(Guid hostId, Guid environmentId, string actorId, Guid correlationId, CancellationToken cancellationToken) =>
        hosts.LinkAsync(hostId, environmentId, actorId, correlationId, timeProvider.GetUtcNow(), cancellationToken);

    public Task<LicensingHostChange> UnlinkAsync(Guid hostId, Guid environmentId, string actorId, Guid correlationId, CancellationToken cancellationToken) =>
        hosts.UnlinkAsync(hostId, environmentId, actorId, correlationId, timeProvider.GetUtcNow(), cancellationToken);

    public Task<LicensingHostChange> RemoveAsync(Guid hostId, string actorId, Guid correlationId, CancellationToken cancellationToken) =>
        hosts.RemoveHostAsync(hostId, actorId, correlationId, timeProvider.GetUtcNow(), cancellationToken);

    [GeneratedRegex(@"\p{Cc}")]
    private static partial Regex ControlCharacters();
}
