using System.Security.Cryptography;
using Dokpod.Agent.Application.Engines;
using Dokpod.Agent.Contracts.V1;
using Dokpod.Domain.Networks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace Dokpod.Agent.Application.Networks;

/// <summary>
/// Snapshot do inventário de redes (spec 019 D-001/D-002): redes do engine com os containers conectados (todos,
/// inclusive parados), em páginas de até 500 redes e 256 KiB; revisão monotônica que só avança quando o conjunto
/// muda. As opções do driver nunca entram no snapshot.
/// </summary>
public sealed class NetworkInventoryBuilder(TimeProvider timeProvider)
{
    public const int MaxNetworksPerPage = 500;
    public const int MaxPageBytes = 256 * 1024;
    public const int MaxSubnets = 16;
    public const int MaxLabels = 64;
    public const int MaxContainers = 200;
    public const int MaxTextLength = 255;

    private string? fingerprint;
    private ulong revision;

    public ulong Revision => Interlocked.Read(ref revision);

    /// <summary>Containers conectados à rede: pelo identificador e, quando o engine não o informa, pelo nome.</summary>
    public static IEnumerable<EngineContainer> ConnectedTo(EngineNetwork network, IEnumerable<EngineContainer> containers) =>
        containers.Where(container => container.Networks.Any(attachment => Matches(network.NetworkId, network.Name, attachment)));

    public static bool Matches(string networkId, string name, EngineNetworkAttachment attachment) =>
        attachment.NetworkId is { Length: > 0 } id ? string.Equals(id, networkId, StringComparison.Ordinal) : string.Equals(attachment.Name, name, StringComparison.Ordinal);

    /// <summary>Páginas do snapshot, ou vazio quando nada mudou desde o último envio (e `force` é falso).</summary>
    public IReadOnlyList<NetworkSnapshotPage> Build(IReadOnlyList<EngineNetwork> networks, IReadOnlyList<EngineContainer> containers, bool force)
    {
        var states = networks
            .OrderBy(network => network.Name, StringComparer.Ordinal)
            .ThenBy(network => network.NetworkId, StringComparer.Ordinal)
            .Select(network => ToState(network, containers))
            .ToArray();
        var current = Fingerprint(states);
        if (!force && current == fingerprint) return [];
        if (current != fingerprint)
        {
            var clock = (ulong)timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
            Interlocked.Exchange(ref revision, Math.Max(Revision + 1, clock));
            fingerprint = current;
        }

        var snapshotId = Guid.NewGuid().ToString("D");
        var pages = new List<NetworkSnapshotPage>();
        var page = NewPage(snapshotId, 0);
        foreach (var state in states)
        {
            if (page.Networks.Count >= MaxNetworksPerPage || page.CalculateSize() + state.CalculateSize() + 8 > MaxPageBytes)
            {
                pages.Add(page);
                page = NewPage(snapshotId, (uint)pages.Count);
            }

            page.Networks.Add(state);
        }

        pages.Add(page);
        pages[^1].IsLastPage = true;
        return pages;
    }

    private NetworkSnapshotPage NewPage(string snapshotId, uint number) =>
        new() { SnapshotId = snapshotId, NetworkRevision = Revision, PageNumber = number };

    public static NetworkState ToState(EngineNetwork network, IReadOnlyList<EngineContainer> containers)
    {
        var connected = ConnectedTo(network, containers)
            .Select(container =>
            {
                var attachment = container.Networks.First(a => Matches(network.NetworkId, network.Name, a));
                return new NetworkContainerConnection
                {
                    ContainerId = container.ContainerId,
                    ContainerName = Clip(container.Name),
                    Ipv4Address = Clip(attachment.IPv4Address ?? string.Empty),
                    Ipv6Address = Clip(attachment.IPv6Address ?? string.Empty),
                };
            })
            .OrderBy(c => c.ContainerName, StringComparer.Ordinal)
            .ToArray();
        var state = new NetworkState
        {
            NetworkId = network.NetworkId,
            Name = network.Name,
            Driver = Clip(network.Driver),
            Scope = Clip(network.Scope),
            Internal = network.Internal,
            Ipv6 = network.IPv6,
            Attachable = network.Attachable,
            System = NetworkNames.IsSystem(network.Name, network.Ingress),
            Truncated = network.Subnets.Count > MaxSubnets || network.Labels.Count > MaxLabels || connected.Length > MaxContainers,
        };
        state.Subnets.AddRange(network.Subnets.Take(MaxSubnets).Select(s => new NetworkSubnet { Subnet = Clip(s.Subnet), Gateway = Clip(s.Gateway ?? string.Empty) }));
        foreach (var (key, value) in network.Labels.OrderBy(l => l.Key, StringComparer.Ordinal).Take(MaxLabels)) state.Labels[Clip(key)] = Clip(value);
        state.Containers.AddRange(connected.Take(MaxContainers));
        if (network.CreatedAtUtc is { } created) state.CreatedAt = Timestamp.FromDateTimeOffset(created);
        return state;
    }

    private static string Clip(string value) => value.Length <= MaxTextLength ? value : value[..MaxTextLength];

    private static string Fingerprint(IEnumerable<NetworkState> states)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var state in states)
        {
            // Labels em ordem fixa: o map do protobuf não garante ordem de serialização.
            var stable = state.Clone();
            stable.Labels.Clear();
            hash.AppendData(stable.ToByteArray());
            foreach (var (key, value) in state.Labels.OrderBy(l => l.Key, StringComparer.Ordinal))
            {
                hash.AppendData(System.Text.Encoding.UTF8.GetBytes(key + "\u001f" + value + "\u001e"));
            }
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }
}
