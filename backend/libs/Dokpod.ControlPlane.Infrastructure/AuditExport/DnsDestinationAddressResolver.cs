using System.Net;
using System.Net.Sockets;
using Dokpod.ControlPlane.Application.AuditExport;

namespace Dokpod.ControlPlane.Infrastructure.AuditExport;

/// <summary>
/// Resolve o host uma vez por conexão e aplica a <see cref="DestinationAddressPolicy"/> sobre os IPs resolvidos; os
/// transportes conectam exatamente a esses IPs, mantendo o nome para SNI e validação do certificado (threat model L3).
/// </summary>
public sealed class DnsDestinationAddressResolver(DestinationAddressPolicy policy, Func<string, CancellationToken, Task<IPAddress[]>>? resolve = null)
    : IDestinationAddressResolver
{
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> resolve = resolve ?? Dns.GetHostAddressesAsync;

    public async Task<DestinationAddresses> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(host, out var literal)) return policy.Evaluate([literal]);
        try
        {
            return policy.Evaluate(await resolve(host, cancellationToken).ConfigureAwait(false));
        }
        catch (SocketException)
        {
            return new DestinationAddresses([], AuditExportErrorCodes.ConnectionRefused);
        }
    }
}
