using System.Security.Cryptography;
using Dokpod.Agent.Application.Engines;
using Dokpod.Agent.Contracts.V1;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace Dokpod.Agent.Application.Volumes;

/// <summary>
/// Snapshot do inventário de volumes (spec 018 D-001/D-002): volumes do engine com os containers que os montam
/// (todos, inclusive parados), em páginas de até 500 volumes e 256 KiB; revisão monotônica que só avança quando o
/// conjunto muda. O caminho do volume no host nunca entra no snapshot.
/// </summary>
public sealed class VolumeInventoryBuilder(TimeProvider timeProvider)
{
    public const int MaxVolumesPerPage = 500;
    public const int MaxPageBytes = 256 * 1024;
    public const int MaxLabels = 64;
    public const int MaxMounts = 100;
    public const int MaxTextLength = 255;

    private string? fingerprint;
    private ulong revision;

    public ulong Revision => Interlocked.Read(ref revision);

    /// <summary>Páginas do snapshot, ou vazio quando nada mudou desde o último envio (e `force` é falso).</summary>
    public IReadOnlyList<VolumeSnapshotPage> Build(IReadOnlyList<EngineVolume> volumes, IReadOnlyList<EngineContainer> containers, bool force)
    {
        var mounts = containers
            .SelectMany(container => container.Mounts
                .Where(mount => string.Equals(mount.Type, "volume", StringComparison.OrdinalIgnoreCase) && mount.Name is not null)
                .Select(mount => (Volume: mount.Name!, Mount: new VolumeContainerMount
                {
                    ContainerId = container.ContainerId,
                    ContainerName = Clip(container.Name),
                    Destination = Clip(mount.Destination),
                    ReadOnly = mount.ReadOnly,
                })))
            .GroupBy(entry => entry.Volume, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(entry => entry.Mount).OrderBy(m => m.ContainerName, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        var states = volumes
            .OrderBy(volume => volume.Name, StringComparer.Ordinal)
            .Select(volume => ToState(volume, mounts.GetValueOrDefault(volume.Name) ?? []))
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
        var pages = new List<VolumeSnapshotPage>();
        var page = NewPage(snapshotId, 0);
        foreach (var state in states)
        {
            if (page.Volumes.Count >= MaxVolumesPerPage || page.CalculateSize() + state.CalculateSize() + 8 > MaxPageBytes)
            {
                pages.Add(page);
                page = NewPage(snapshotId, (uint)pages.Count);
            }

            page.Volumes.Add(state);
        }

        pages.Add(page);
        pages[^1].IsLastPage = true;
        return pages;
    }

    private VolumeSnapshotPage NewPage(string snapshotId, uint number) =>
        new() { SnapshotId = snapshotId, VolumeRevision = Revision, PageNumber = number };

    public static VolumeState ToState(EngineVolume volume, IReadOnlyList<VolumeContainerMount> mounts)
    {
        var state = new VolumeState
        {
            Name = volume.Name,
            Driver = Clip(volume.Driver),
            Scope = Clip(volume.Scope),
            Truncated = volume.Labels.Count > MaxLabels || mounts.Count > MaxMounts,
        };
        foreach (var (key, value) in volume.Labels.OrderBy(l => l.Key, StringComparer.Ordinal).Take(MaxLabels)) state.Labels[Clip(key)] = Clip(value);
        state.Mounts.AddRange(mounts.Take(MaxMounts));
        if (volume.CreatedAtUtc is { } created) state.CreatedAt = Timestamp.FromDateTimeOffset(created);
        return state;
    }

    private static string Clip(string value) => value.Length <= MaxTextLength ? value : value[..MaxTextLength];

    private static string Fingerprint(IEnumerable<VolumeState> states)
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
