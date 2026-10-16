using System.Security.Cryptography;
using Dokpod.Agent.Application.Engines;
using Dokpod.Agent.Contracts.V1;
using Dokpod.Domain.Pods;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace Dokpod.Agent.Application.Pods;

/// <summary>Container em que o próprio agente roda (spec 021 D-002); fora de container, nenhum.</summary>
public interface ISelfContainer
{
    bool IsSelf(string containerId);
}

public sealed class NoSelfContainer : ISelfContainer
{
    public bool IsSelf(string containerId) => false;
}

public static class PodStates
{
    /// <summary>Pod que contém o container do agente: parar, pausar e reiniciar são recusados (FR-005).</summary>
    public static bool IsAgentPod(EnginePod pod, ISelfContainer self) => pod.Members.Any(member => self.IsSelf(member.ContainerId));

    /// <summary>Membros (fora a infraestrutura) que não chegaram ao estado esperado da ação (FR-008).</summary>
    public static IReadOnlyList<string> Failed(EnginePod pod, PodActionKind action)
    {
        static bool Is(string status, params string[] values) => values.Any(v => status.StartsWith(v, StringComparison.OrdinalIgnoreCase));
        return pod.Members
            .Where(member => member.ContainerId != pod.InfraId)
            .Where(member => action switch
            {
                PodActionKind.Stop => !Is(member.Status, "exited", "stopped", "created", "configured"),
                PodActionKind.Pause => !Is(member.Status, "paused"),
                _ => !Is(member.Status, "running"),
            })
            .Select(member => member.Name.Length > 0 ? member.Name : member.ContainerId[..12])
            .Take(50)
            .ToArray();
    }
}

/// <summary>
/// Snapshot do inventário de pods (spec 021 D-001): até 500 pods e 256 KiB por página, revisão monotônica que só
/// avança quando o conjunto muda; `engine_without_pods` quando o engine não tem pods (Docker).
/// </summary>
public sealed class PodInventoryBuilder(TimeProvider timeProvider, ISelfContainer self)
{
    public const int MaxPodsPerPage = 500;
    public const int MaxPageBytes = 256 * 1024;
    public const int MaxMembers = 100;
    public const int MaxLabels = 64;

    private string? fingerprint;
    private ulong revision;

    public ulong Revision => Interlocked.Read(ref revision);

    public IReadOnlyList<PodSnapshotPage> Build(IReadOnlyList<EnginePod>? pods, bool force)
    {
        var states = (pods ?? []).OrderBy(p => p.Name, StringComparer.Ordinal).ThenBy(p => p.PodId, StringComparer.Ordinal).Select(ToState).ToArray();
        var withoutPods = pods is null;
        using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        {
            hash.AppendData([withoutPods ? (byte)1 : (byte)0]);
            foreach (var state in states)
            {
                var stable = state.Clone();
                stable.Labels.Clear();
                hash.AppendData(stable.ToByteArray());
                foreach (var (key, value) in state.Labels.OrderBy(l => l.Key, StringComparer.Ordinal)) hash.AppendData(System.Text.Encoding.UTF8.GetBytes(key + "\u001f" + value + "\u001e"));
            }

            var current = Convert.ToHexString(hash.GetHashAndReset());
            if (!force && current == fingerprint) return [];
            if (current != fingerprint)
            {
                Interlocked.Exchange(ref revision, Math.Max(Revision + 1, (ulong)timeProvider.GetUtcNow().ToUnixTimeMilliseconds()));
                fingerprint = current;
            }
        }

        var snapshotId = Guid.NewGuid().ToString("D");
        var pages = new List<PodSnapshotPage>();
        var page = NewPage(snapshotId, 0, withoutPods);
        foreach (var state in states)
        {
            if (page.Pods.Count >= MaxPodsPerPage || page.CalculateSize() + state.CalculateSize() + 8 > MaxPageBytes)
            {
                pages.Add(page);
                page = NewPage(snapshotId, (uint)pages.Count, withoutPods);
            }

            page.Pods.Add(state);
        }

        pages.Add(page);
        pages[^1].IsLastPage = true;
        return pages;
    }

    private PodSnapshotPage NewPage(string snapshotId, uint number, bool withoutPods) =>
        new() { SnapshotId = snapshotId, PodRevision = Revision, PageNumber = number, EngineWithoutPods = withoutPods };

    private PodState ToState(EnginePod pod)
    {
        static string Clip(string value) => value.Length <= 255 ? value : value[..255];
        var state = new PodState
        {
            PodId = pod.PodId,
            Name = pod.Name,
            Status = Clip(pod.Status),
            InfraId = pod.InfraId ?? string.Empty,
            AgentPod = PodStates.IsAgentPod(pod, self),
            Truncated = pod.Members.Count > MaxMembers || pod.Labels.Count > MaxLabels,
        };
        if (pod.CreatedAtUtc is { } created) state.CreatedAt = Timestamp.FromDateTimeOffset(created);
        foreach (var (key, value) in pod.Labels.OrderBy(l => l.Key, StringComparer.Ordinal).Take(MaxLabels)) state.Labels[Clip(key)] = Clip(value);
        state.Networks.AddRange(pod.Networks.Take(16).Select(Clip));
        state.Containers.AddRange(pod.Members.Take(MaxMembers).Select(member => new PodMember
        {
            ContainerId = member.ContainerId,
            Name = Clip(member.Name),
            Status = Clip(member.Status),
            Restarts = (uint)Math.Max(0, member.Restarts),
            Infra = member.ContainerId == pod.InfraId,
        }));
        return state;
    }
}
