using System.Net.Sockets;
using System.Text;

namespace Dokpod.Agent.Infrastructure.Engines.Docker;

/// <summary>
/// Conexão crua ao socket do engine para endpoints que sequestram a conexão (`/exec/{id}/start`, spec 014).
/// O `HttpClient` não expõe o stream bidirecional após `101 Switching Protocols`; aqui o HTTP/1.1 é escrito à mão.
/// </summary>
public sealed class EngineRawConnector(string socketPath)
{
    private const int MaxHeaderBytes = 16 * 1024;

    /// <summary>Envia o pedido e devolve (status, cabeçalho bruto, stream posicionado após os cabeçalhos).</summary>
    public async Task<(int Status, string Head, Stream Stream)> SendUpgradeAsync(string path, string jsonBody, CancellationToken cancellationToken)
    {
        if (!Path.IsPathFullyQualified(socketPath)) throw new InvalidOperationException("Engine socket path must be absolute.");
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(Path.GetFullPath(socketPath)), cancellationToken);
            var stream = new NetworkStream(socket, ownsSocket: true);
            var body = Encoding.UTF8.GetBytes(jsonBody);
            var head = $"POST {path} HTTP/1.1\r\nHost: docker\r\nContent-Type: application/json\r\nConnection: Upgrade\r\nUpgrade: tcp\r\nContent-Length: {body.Length}\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(head), cancellationToken);
            await stream.WriteAsync(body, cancellationToken);
            await stream.FlushAsync(cancellationToken);

            // Lê byte a byte até o fim dos cabeçalhos para não consumir saída do TTY.
            var buffer = new List<byte>(512);
            var one = new byte[1];
            while (buffer.Count < MaxHeaderBytes)
            {
                if (await stream.ReadAsync(one, cancellationToken) == 0) break;
                buffer.Add(one[0]);
                var n = buffer.Count;
                if (n >= 4 && buffer[n - 4] == '\r' && buffer[n - 3] == '\n' && buffer[n - 2] == '\r' && buffer[n - 1] == '\n') break;
            }

            var text = Encoding.ASCII.GetString(buffer.ToArray());
            var statusLine = text.Split("\r\n", 2)[0].Split(' ');
            var status = statusLine.Length > 1 && int.TryParse(statusLine[1], out var parsed) ? parsed : 0;
            return (status, text, stream);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
