namespace Dokpod.ControlPlane.Application.Networks;

/// <summary>Container conectado à rede (spec 019 D-002); endereços vazios quando o container não está em execução.</summary>
public sealed record NetworkConnection(string ContainerId, string ContainerName, string? IPv4Address, string? IPv6Address);

public sealed record NetworkSubnet(string Subnet, string? Gateway);

/// <summary>Rede projetada de um host.</summary>
public sealed record NetworkInventoryItem(
    string NetworkId,
    string Name,
    string Driver,
    string Scope,
    bool Internal,
    bool IPv6,
    bool Attachable,
    bool System,
    DateTimeOffset? CreatedAtUtc,
    IReadOnlyList<NetworkSubnet> Subnets,
    IReadOnlyDictionary<string, string> Labels,
    IReadOnlyList<NetworkConnection> Containers,
    bool Truncated)
{
    public bool InUse => Containers.Count > 0;

    public bool Local => string.Equals(Scope, "local", StringComparison.OrdinalIgnoreCase);
}

public sealed record NetworkSnapshot(Guid EnvironmentId, ulong Revision, DateTimeOffset ObservedAtUtc, IReadOnlyList<NetworkInventoryItem> Networks);

public sealed record NetworkSnapshotPage(string SnapshotId, ulong Revision, uint PageNumber, bool IsLastPage, IReadOnlyList<NetworkInventoryItem> Networks);

public enum NetworkSnapshotOutcome
{
    Accepted,
    Stale,
}

public enum NetworkSort
{
    NameAsc,
    NameDesc,
    CreatedAtDesc,
    CreatedAtAsc,
    Environment,
}

public sealed record NetworkSearchQuery(
    IReadOnlyCollection<Guid> EnvironmentIds,
    bool UnusedOnly,
    bool? System,
    IReadOnlyCollection<string> Drivers,
    string? Text,
    NetworkSort Sort,
    int Offset,
    int Limit);

public sealed record NetworkSearchRow(Guid EnvironmentId, NetworkInventoryItem Network, DateTimeOffset ObservedAtUtc);

/// <summary>Total e sem uso (redes do sistema não contam como "sem uso").</summary>
public sealed record NetworkSummary(int Total, int Unused);

public sealed record NetworkSearchResult(IReadOnlyList<NetworkSearchRow> Rows, NetworkSummary Summary, bool HasMore);

public interface INetworkInventoryStore
{
    Task<NetworkSnapshotOutcome> ReplaceSnapshotAsync(NetworkSnapshot snapshot, CancellationToken cancellationToken);

    /// <summary>Hosts com projeção de redes (os demais aparecem como "inventário indisponível").</summary>
    Task<IReadOnlySet<Guid>> ListProjectedAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken);

    Task<NetworkSearchResult> SearchAsync(NetworkSearchQuery query, CancellationToken cancellationToken);

    /// <summary>Rede pelo identificador (64 hex) ou, para o link do detalhe do container, pelo nome.</summary>
    Task<NetworkSearchRow?> GetAsync(Guid environmentId, string networkIdOrName, CancellationToken cancellationToken);
}

/// <summary>Junta as páginas de um snapshot de redes; fora de ordem ou excesso invalida a sessão (como volumes e imagens).</summary>
public sealed class NetworkSnapshotAccumulator(Guid environmentId, TimeProvider timeProvider, int maximumPages = 100, int maximumNetworks = 20_000)
{
    private readonly Dictionary<string, NetworkInventoryItem> networks = new(StringComparer.Ordinal);
    private string? snapshotId;
    private ulong revision;
    private uint nextPage;

    public NetworkSnapshot? AddPage(NetworkSnapshotPage page, out string? failureCode)
    {
        failureCode = null;
        if (page.PageNumber == 0)
        {
            networks.Clear();
            snapshotId = page.SnapshotId;
            revision = page.Revision;
            nextPage = 0;
        }

        if (snapshotId is null || page.PageNumber != nextPage || page.PageNumber >= maximumPages ||
            !string.Equals(snapshotId, page.SnapshotId, StringComparison.Ordinal) || revision != page.Revision ||
            !Guid.TryParseExact(page.SnapshotId, "D", out _) || page.Revision == 0)
        {
            failureCode = "network_snapshot_invalid";
            return null;
        }

        foreach (var network in page.Networks)
        {
            if (!Dokpod.Domain.Networks.NetworkNames.IsValidId(network.NetworkId) || !Dokpod.Domain.Networks.NetworkNames.IsValidName(network.Name) ||
                networks.Count >= maximumNetworks || !networks.TryAdd(network.NetworkId, network))
            {
                failureCode = "network_snapshot_invalid";
                return null;
            }
        }

        nextPage++;
        if (!page.IsLastPage) return null;
        var snapshot = new NetworkSnapshot(environmentId, revision, timeProvider.GetUtcNow(), networks.Values.ToArray());
        networks.Clear();
        snapshotId = null;
        return snapshot;
    }
}
