using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Dokpod.ControlPlane.Application.AuditExport;

namespace Dokpod.ControlPlane.Infrastructure.AuditExport;

/// <summary>Tempos limite do envio (threat model L2) e o <c>HOSTNAME</c> do cabeçalho syslog.</summary>
public sealed record SyslogTlsTransportOptions
{
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(10);

    public TimeSpan SendTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public string? Hostname { get; init; }

    /// <summary>
    /// No TLS 1.3 o servidor só recusa o certificado de cliente (inválido ou ausente) depois do handshake; esperar este
    /// tempo por um alerta antes de escrever evita dar o lote como entregue a quem o descartou.
    /// </summary>
    public TimeSpan Tls13RefusalProbe { get; init; } = TimeSpan.FromMilliseconds(250);
}

/// <summary>
/// Syslog sobre TLS (spec 027 D-004, RFC 5425). TLS 1.2 ou 1.3 com cadeia e nome sempre conferidos, sem opção de
/// desligar (D-007). O host é resolvido e filtrado a cada conexão, e o socket conecta ao IP aprovado, mantendo o nome
/// para SNI e validação (L3). Um lote por conexão, confirmado quando a escrita termina sem erro. Os códigos de falha
/// nunca carregam mensagem do servidor, host ou conteúdo do secret.
/// </summary>
public sealed class SyslogTlsTransport(
    IDestinationAddressResolver resolver,
    IAuditExportSecretReader secrets,
    SyslogTlsTransportOptions? options = null) : IAuditExportTransport
{
    private readonly SyslogTlsTransportOptions options = options ?? new SyslogTlsTransportOptions();

    public string Protocol => AuditExportProtocols.Syslog;

    public async Task<AuditExportSendResult> SendAsync(AuditExportDestination destination, IReadOnlyList<AuditExportRecord> batch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(batch);
        var secret = secrets.Read(destination.SecretRef);
        if (secret.ErrorCode is not null) return AuditExportSendResult.Failed(AuditExportErrorCodes.SecretMissing);

        var addresses = await resolver.ResolveAsync(destination.Host, cancellationToken).ConfigureAwait(false);
        if (!addresses.IsAllowed) return AuditExportSendResult.Failed(addresses.ErrorCode ?? AuditExportErrorCodes.AddressBlocked);

        var trust = new X509Certificate2Collection();
        if (secret.CaPem is not null) trust.ImportFromPem(secret.CaPem);
        using var clientCertificate = LoadClientCertificate(secret);

        var (socket, connectError) = await ConnectAsync(addresses.Allowed, destination.Port, cancellationToken).ConfigureAwait(false);
        if (socket is null) return AuditExportSendResult.Failed(connectError!);

        using var sendTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        sendTimeout.CancelAfter(options.SendTimeout);
        await using var stream = new SslStream(new NetworkStream(socket, ownsSocket: true), leaveInnerStreamOpen: false);
        try
        {
            await stream.AuthenticateAsClientAsync(Authentication(destination.Host, trust, clientCertificate), sendTimeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AuditExportSendResult.Failed(AuditExportErrorCodes.Timeout);
        }
        catch (Exception exception) when (exception is AuthenticationException or IOException)
        {
            return AuditExportSendResult.Failed(AuditExportErrorCodes.TlsError);
        }

        // O servidor syslog nunca escreve: qualquer leitura concluída é alerta TLS ou fechamento, ou seja, recusa.
        var refused = stream.ReadAsync(new byte[1]).AsTask();
        _ = refused.ContinueWith(task => task.Exception, TaskContinuationOptions.OnlyOnFaulted);
        try
        {
            if (stream.SslProtocol == SslProtocols.Tls13
                && await Task.WhenAny(refused, Task.Delay(options.Tls13RefusalProbe, sendTimeout.Token)).ConfigureAwait(false) == refused)
            {
                return AuditExportSendResult.Failed(AuditExportErrorCodes.TlsError);
            }

            foreach (var record in batch)
            {
                await stream.WriteAsync(SyslogFormatter.Frame(record, options.Hostname), sendTimeout.Token).ConfigureAwait(false);
            }

            await stream.FlushAsync(sendTimeout.Token).ConfigureAwait(false);
            if (refused.IsCompleted) return AuditExportSendResult.Failed(AuditExportErrorCodes.TlsError);
            await stream.ShutdownAsync().ConfigureAwait(false);
            return AuditExportSendResult.Ok;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AuditExportSendResult.Failed(AuditExportErrorCodes.Timeout);
        }
        catch (IOException)
        {
            return AuditExportSendResult.Failed(AuditExportErrorCodes.ConnectionRefused);
        }
    }

    private static SslClientAuthenticationOptions Authentication(string host, X509Certificate2Collection trust, X509Certificate2? clientCertificate)
    {
        var authentication = new SslClientAuthenticationOptions
        {
            TargetHost = host,
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
            // Sem callback: qualquer erro de cadeia ou nome recusa a conexão.
        };
        if (trust.Count > 0)
        {
            var policy = new X509ChainPolicy { TrustMode = X509ChainTrustMode.CustomRootTrust, RevocationMode = X509RevocationMode.NoCheck };
            policy.CustomTrustStore.AddRange(trust);
            authentication.CertificateChainPolicy = policy;
        }

        if (clientCertificate is not null) authentication.ClientCertificates = [clientCertificate];
        return authentication;
    }

    private static X509Certificate2? LoadClientCertificate(AuditExportSecret secret)
    {
        if (secret.ClientCertificatePem is null || secret.ClientKeyPem is null) return null;
        using var ephemeral = X509Certificate2.CreateFromPem(secret.ClientCertificatePem, secret.ClientKeyPem);
        // Chave efêmera não serve ao SslStream em todas as plataformas; a cópia PKCS#12 fica só em memória.
        return X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pkcs12), null);
    }

    private async Task<(Socket? Socket, string? Error)> ConnectAsync(IReadOnlyList<IPAddress> addresses, int port, CancellationToken cancellationToken)
    {
        using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectTimeout.CancelAfter(options.ConnectTimeout);
        var error = AuditExportErrorCodes.ConnectionRefused;
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, port), connectTimeout.Token).ConfigureAwait(false);
                return (socket, null);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                socket.Dispose();
                return (null, AuditExportErrorCodes.Timeout);
            }
            catch (SocketException)
            {
                socket.Dispose();
                error = AuditExportErrorCodes.ConnectionRefused;
            }
        }

        return (null, error);
    }
}
