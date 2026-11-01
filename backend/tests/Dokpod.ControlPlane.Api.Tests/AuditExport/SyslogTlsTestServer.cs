using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Nodes;
using Dokpod.ControlPlane.Application.AuditExport;

namespace Dokpod.ControlPlane.Api.Tests.AuditExport;

/// <summary>CA e certificados gerados na execução; nada fica gravado no repositório.</summary>
internal sealed class TestPki : IDisposable
{
    private readonly RSA caKey = RSA.Create(2048);

    public TestPki()
    {
        var request = new CertificateRequest("CN=Dokpod Test CA", caKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        Ca = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow.AddDays(30));
    }

    public X509Certificate2 Ca { get; }

    public string CaPem => Ca.ExportCertificatePem();

    public X509Certificate2 Issue(string name, bool server, DateTimeOffset? notBefore = null, DateTimeOffset? notAfter = null)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={name}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName(name);
        request.CertificateExtensions.Add(san.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            [new Oid(server ? "1.3.6.1.5.5.7.3.1" : "1.3.6.1.5.5.7.3.2")], false));
        var serial = RandomNumberGenerator.GetBytes(12);
        using var issued = request.Create(Ca, notBefore ?? DateTimeOffset.UtcNow.AddHours(-1), notAfter ?? DateTimeOffset.UtcNow.AddDays(7), serial);
        using var withKey = issued.CopyWithPrivateKey(key);
        return X509CertificateLoader.LoadPkcs12(withKey.Export(X509ContentType.Pkcs12), null);
    }

    /// <summary>Pasta de secret no formato de D-006, dentro de uma raiz temporária.</summary>
    public static string WriteSecret(string root, string secretRef, string? caPem, X509Certificate2? client = null)
    {
        var directory = Directory.CreateDirectory(Path.Combine(root, secretRef)).FullName;
        if (caPem is not null) File.WriteAllText(Path.Combine(directory, "ca.pem"), caPem);
        if (client is not null)
        {
            File.WriteAllText(Path.Combine(directory, "tls.crt"), client.ExportCertificatePem());
            File.WriteAllText(Path.Combine(directory, "tls.key"), client.GetRSAPrivateKey()!.ExportPkcs8PrivateKeyPem());
        }

        return directory;
    }

    public void Dispose()
    {
        Ca.Dispose();
        caKey.Dispose();
    }
}

/// <summary>
/// Servidor syslog TLS em processo (RFC 5425) em <c>127.0.0.1</c>: lê quadros <c>LEN SP MSG</c> e guarda o JSON de
/// cada mensagem. Com <c>requireClientCertificate</c>, exige certificado de cliente emitido pela mesma CA.
/// </summary>
internal sealed class SyslogTlsTestServer : IAsyncDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stop = new();
    private readonly X509Certificate2? certificate;
    private readonly X509Certificate2? clientCa;
    private readonly Task loop;

    private SyslogTlsTestServer(X509Certificate2? certificate, X509Certificate2? clientCa, bool plain, bool stall)
    {
        this.certificate = certificate;
        this.clientCa = clientCa;
        listener.Start();
        loop = AcceptLoopAsync(plain, stall);
    }

    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

    public ConcurrentQueue<string> Messages { get; } = new();

    public ConcurrentQueue<byte[]> RawFirstBytes { get; } = new();

    public int Connections => connections;

    private int connections;

    public static SyslogTlsTestServer Tls(X509Certificate2 certificate, X509Certificate2? clientCa = null) => new(certificate, clientCa, plain: false, stall: false);

    /// <summary>Aceita TCP sem TLS e guarda o que chega, para provar que nada sai em claro.</summary>
    public static SyslogTlsTestServer Plain() => new(null, null, plain: true, stall: false);

    /// <summary>Aceita a conexão e nunca responde ao handshake (tempo limite).</summary>
    public static SyslogTlsTestServer Stalled() => new(null, null, plain: true, stall: true);

    public IReadOnlyList<JsonNode> Records => [.. Messages.Select(message => JsonNode.Parse(message[(message.IndexOf('﻿', StringComparison.Ordinal) + 1)..])!)];

    public async Task<IReadOnlyList<JsonNode>> WaitForAsync(Func<IReadOnlyList<JsonNode>, bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var records = Records;
            if (condition(records)) return records;
            await Task.Delay(100);
        }

        return Records;
    }

    private async Task AcceptLoopAsync(bool plain, bool stall)
    {
        while (!stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(stop.Token);
            }
            catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                return;
            }

            Interlocked.Increment(ref connections);
            _ = Task.Run(() => HandleAsync(client, plain, stall));
        }
    }

    private async Task HandleAsync(TcpClient client, bool plain, bool stall)
    {
        using var _ = client;
        try
        {
            if (plain)
            {
                var buffer = new byte[1024];
                var read = await client.GetStream().ReadAsync(buffer, stop.Token);
                RawFirstBytes.Enqueue(buffer[..read]);
                if (stall) await Task.Delay(Timeout.Infinite, stop.Token);
                return;
            }

            await using var ssl = new SslStream(client.GetStream(), false);
            await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = certificate,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                ClientCertificateRequired = clientCa is not null,
                RemoteCertificateValidationCallback = clientCa is null ? null : (_, remote, _, _) => IssuedBy(remote, clientCa),
            }, stop.Token);
            await ReadFramesAsync(ssl);
        }
        catch (Exception exception) when (exception is IOException or AuthenticationException or OperationCanceledException or SocketException)
        {
        }
    }

    private async Task ReadFramesAsync(Stream stream)
    {
        while (true)
        {
            var length = new StringBuilder();
            int value;
            while ((value = stream.ReadByte()) is >= '0' and <= '9') length.Append((char)value);
            if (value != ' ' || length.Length == 0) return;
            var message = new byte[int.Parse(length.ToString(), CultureInfo.InvariantCulture)];
            await stream.ReadExactlyAsync(message, stop.Token);
            Messages.Enqueue(Encoding.UTF8.GetString(message));
        }
    }

    private static bool IssuedBy(X509Certificate? remote, X509Certificate2 ca)
    {
        if (remote is null) return false;
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(ca);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        using var certificate = X509CertificateLoader.LoadCertificate(remote.GetRawCertData());
        return chain.Build(certificate);
    }

    public async ValueTask DisposeAsync()
    {
        await stop.CancelAsync();
        listener.Stop();
        try
        {
            await loop;
        }
        catch (OperationCanceledException)
        {
        }

        stop.Dispose();
    }
}

/// <summary>Resolvedor de teste: devolve sempre o IP dado, como se o DNS tivesse aprovado o nome.</summary>
internal sealed class FixedResolver(IPAddress address) : IDestinationAddressResolver
{
    public int Calls { get; private set; }

    public Task<DestinationAddresses> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(new DestinationAddresses([address], null));
    }
}
