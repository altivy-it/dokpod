namespace Dokpod.ControlPlane.Application.Licensing;

public sealed record HostCapacity(Guid HostId, string Label, int? Vcpus, string Source, int? NodeEquivalents, bool Removed);

/// <summary>Consumo apurado (D-005): atual, maior total simultâneo da janela e hosts com medição desconhecida.</summary>
public sealed record CapacitySnapshot(
    int Current,
    int WindowPeak,
    DateTimeOffset? WindowPeakAtUtc,
    int UnknownHosts,
    IReadOnlyList<HostCapacity> Hosts);

/// <summary>
/// Apuração de nodes equivalentes (spec 031 T038, FR-011 a FR-015). <c>U(t) = Σ teto(vCPU_h(t) / B)</c>, avaliado só
/// nos instantes de mudança (observação, associação, desassociação, remoção), e <c>P = max U(s)</c> na janela.
/// A CPU de um host é a maior capacidade confiável entre seus ambientes associados; sem nova observação, vale a
/// última (host desconectado continua contando). Origem que não é o engine nunca entra no cálculo: o host fica
/// como medição desconhecida e mantém o último valor confiável. Tudo vem dos dados brutos, então trocar a faixa
/// reapura a janela inteira.
/// </summary>
public static class CapacityMeter
{
    public const int WindowDays = 30;

    public static CapacitySnapshot Compute(MeteringInput input, int vcpuBand, DateTimeOffset now, int windowDays = WindowDays)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(vcpuBand);
        var windowStart = now.AddDays(-windowDays);
        var observationsByEnvironment = input.Observations
            .Where(observation => observation.ObservedAtUtc <= now)
            .GroupBy(observation => observation.EnvironmentId)
            .ToDictionary(group => group.Key, group => group.OrderBy(observation => observation.ObservedAtUtc).ToArray());
        var removedAt = input.Hosts.ToDictionary(host => host.HostId, host => host.RemovedAtUtc);

        var instants = new SortedSet<DateTimeOffset> { windowStart, now };
        foreach (var observation in input.Observations) AddInWindow(instants, observation.ObservedAtUtc, windowStart, now);
        foreach (var link in input.Links)
        {
            AddInWindow(instants, link.LinkedAtUtc, windowStart, now);
            if (link.UnlinkedAtUtc is { } unlinked) AddInWindow(instants, unlinked, windowStart, now);
        }

        foreach (var removed in input.Hosts.Select(host => host.RemovedAtUtc).OfType<DateTimeOffset>()) AddInWindow(instants, removed, windowStart, now);

        var peak = -1;
        DateTimeOffset? peakAt = null;
        foreach (var instant in instants)
        {
            var total = Total(input.Links, observationsByEnvironment, removedAt, vcpuBand, instant);
            if (total > peak)
            {
                peak = total;
                peakAt = instant;
            }
        }

        var hosts = input.Hosts
            .OrderBy(host => host.Label, StringComparer.Ordinal)
            .Select(host =>
            {
                var removed = host.RemovedAtUtc is { } at && at <= now;
                var activeEnvironments = ActiveEnvironments(input.Links, host.HostId, now).ToArray();
                var vcpus = removed ? null : HostVcpus(activeEnvironments, observationsByEnvironment, now);
                var unknown = !removed && activeEnvironments.Any(environment => LatestSource(observationsByEnvironment, environment, now) != CapacitySource.Engine);
                var source = vcpus is null || unknown ? CapacitySource.Unknown : CapacitySource.Engine;
                return new HostCapacity(host.HostId, host.Label, vcpus, source, vcpus is { } value ? Ceiling(value, vcpuBand) : null, removed);
            })
            .ToArray();

        return new CapacitySnapshot(
            Total(input.Links, observationsByEnvironment, removedAt, vcpuBand, now),
            Math.Max(peak, 0),
            peakAt,
            hosts.Count(host => !host.Removed && host.Source == CapacitySource.Unknown && ActiveEnvironments(input.Links, host.HostId, now).Any()),
            hosts);
    }

    private static int Total(
        IReadOnlyList<LicensingHostLink> links,
        IReadOnlyDictionary<Guid, CapacityObservation[]> observations,
        IReadOnlyDictionary<Guid, DateTimeOffset?> removedAt,
        int vcpuBand,
        DateTimeOffset instant) =>
        links.Select(link => link.HostId).Distinct()
            .Where(host => !(removedAt.TryGetValue(host, out var removed) && removed is { } at && at <= instant))
            .Sum(host => HostVcpus(ActiveEnvironments(links, host, instant), observations, instant) is { } vcpus ? Ceiling(vcpus, vcpuBand) : 0);

    private static IEnumerable<Guid> ActiveEnvironments(IReadOnlyList<LicensingHostLink> links, Guid hostId, DateTimeOffset instant) =>
        links.Where(link => link.HostId == hostId && link.LinkedAtUtc <= instant && (link.UnlinkedAtUtc is not { } unlinked || instant < unlinked))
            .Select(link => link.EnvironmentId)
            .Distinct();

    private static int? HostVcpus(IEnumerable<Guid> environments, IReadOnlyDictionary<Guid, CapacityObservation[]> observations, DateTimeOffset instant)
    {
        int? best = null;
        foreach (var environment in environments)
        {
            if (!observations.TryGetValue(environment, out var series)) continue;
            var trusted = series.LastOrDefault(observation => observation.ObservedAtUtc <= instant && observation.Vcpus is not null && observation.Source == CapacitySource.Engine);
            if (trusted?.Vcpus is { } vcpus && (best is null || vcpus > best)) best = vcpus;
        }

        return best;
    }

    private static string? LatestSource(IReadOnlyDictionary<Guid, CapacityObservation[]> observations, Guid environment, DateTimeOffset instant) =>
        observations.TryGetValue(environment, out var series) ? series.LastOrDefault(observation => observation.ObservedAtUtc <= instant)?.Source : null;

    private static int Ceiling(int vcpus, int band) => (vcpus + band - 1) / band;

    private static void AddInWindow(SortedSet<DateTimeOffset> instants, DateTimeOffset instant, DateTimeOffset windowStart, DateTimeOffset now)
    {
        if (instant > windowStart && instant <= now) instants.Add(instant);
    }
}
