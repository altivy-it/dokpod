using System.Net;
using System.Net.Sockets;

namespace Dokpod.ControlPlane.Application.AuditExport;

/// <summary>Resultado da resolução de um destino: os IPs a usar na conexão, ou a recusa (threat model B2, L3).</summary>
public sealed record DestinationAddresses(IReadOnlyList<IPAddress> Allowed, string? ErrorCode)
{
    public bool IsAllowed => ErrorCode is null && Allowed.Count > 0;
}

/// <summary>Resolve o host do destino e devolve só IPs aprovados pela <see cref="DestinationAddressPolicy"/>.</summary>
public interface IDestinationAddressResolver
{
    Task<DestinationAddresses> ResolveAsync(string host, CancellationToken cancellationToken);
}

/// <summary>
/// Endereços proibidos para destinos de exportação (spec 027 D-007): loopback, link-local (inclusive o serviço de
/// metadados <c>169.254.169.254</c>), não especificado, multicast, broadcast e as sub-redes do próprio plano de
/// controle. Redes privadas comuns (10/8, 172.16/12, 192.168/16) são permitidas, porque SIEMs costumam estar nelas.
/// Endereços IPv4 mapeados em IPv6 são avaliados como IPv4.
/// </summary>
public sealed class DestinationAddressPolicy(IReadOnlyList<(IPAddress Network, int PrefixLength)> controlPlaneNetworks)
{
    private static readonly (IPAddress Network, int PrefixLength)[] Blocked =
    [
        (IPAddress.Parse("0.0.0.0"), 8),
        (IPAddress.Parse("127.0.0.0"), 8),
        (IPAddress.Parse("169.254.0.0"), 16),
        (IPAddress.Parse("224.0.0.0"), 4),
        (IPAddress.Parse("255.255.255.255"), 32),
        (IPAddress.Parse("::"), 128),
        (IPAddress.Parse("::1"), 128),
        (IPAddress.Parse("fe80::"), 10),
        (IPAddress.Parse("ff00::"), 8),
    ];

    public DestinationAddressPolicy()
        : this([])
    {
    }

    /// <summary>Lê sub-redes em notação CIDR (por exemplo, <c>10.89.0.0/24</c>); entradas inválidas recusam a configuração.</summary>
    public static DestinationAddressPolicy FromCidrs(IEnumerable<string> cidrs) =>
        new([.. cidrs.Where(cidr => !string.IsNullOrWhiteSpace(cidr)).Select(ParseCidr)]);

    public bool IsBlocked(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        var candidate = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        return Blocked.Concat(controlPlaneNetworks).Any(network => Contains(network.Network, network.PrefixLength, candidate));
    }

    /// <summary>Todos os IPs resolvidos precisam passar; um só proibido recusa o host inteiro (evita rodízio de DNS).</summary>
    public DestinationAddresses Evaluate(IReadOnlyList<IPAddress> resolved) =>
        resolved.Count == 0 || resolved.Any(IsBlocked)
            ? new DestinationAddresses([], AuditExportErrorCodes.AddressBlocked)
            : new DestinationAddresses(resolved, null);

    private static (IPAddress, int) ParseCidr(string cidr)
    {
        var parts = cidr.Trim().Split('/');
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var network) || !int.TryParse(parts[1], out var prefix)
            || prefix < 0 || prefix > (network.AddressFamily == AddressFamily.InterNetwork ? 32 : 128))
        {
            throw new FormatException($"Sub-rede inválida: {cidr}.");
        }

        return (network, prefix);
    }

    private static bool Contains(IPAddress network, int prefixLength, IPAddress address)
    {
        if (network.AddressFamily != address.AddressFamily) return false;
        var networkBytes = network.GetAddressBytes();
        var addressBytes = address.GetAddressBytes();
        var fullBytes = prefixLength / 8;
        for (var i = 0; i < fullBytes; i++)
        {
            if (networkBytes[i] != addressBytes[i]) return false;
        }

        var remainingBits = prefixLength % 8;
        if (remainingBits == 0) return true;
        var mask = (byte)(0xFF << (8 - remainingBits));
        return (networkBytes[fullBytes] & mask) == (addressBytes[fullBytes] & mask);
    }
}
