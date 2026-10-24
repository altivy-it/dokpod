using Dokpod.Agent.Contracts.V1;
using Grpc.Core;
using Grpc.Net.Client;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Dokpod.Agent.Infrastructure.Protocol;

public sealed class AgentControlClient : IAsyncDisposable
{
    private readonly GrpcChannel channel;
    private readonly AgentControl.AgentControlClient client;

    private AgentControlClient(GrpcChannel channel)
    {
        this.channel = channel;
        client = new AgentControl.AgentControlClient(channel);
    }

    public static AgentControlClient Create(
        Uri endpoint,
        X509Certificate2 clientCertificate,
        X509Certificate2Collection? serverCaCertificates = null)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(clientCertificate);
        if (!string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The agent control endpoint must use HTTPS.", nameof(endpoint));
        }

        var handler = new HttpClientHandler();
        handler.ClientCertificates.Add(clientCertificate);
        if (serverCaCertificates is not null)
        {
            handler.ServerCertificateCustomValidationCallback = (_, certificate, _, errors) =>
                certificate is not null && ValidateServerCertificate(certificate, errors, serverCaCertificates);
        }

        return new AgentControlClient(GrpcChannel.ForAddress(endpoint, new GrpcChannelOptions
        {
            HttpHandler = handler,
        }));
    }

    /// <summary>
    /// Raízes confiáveis para o servidor: um arquivo PEM pode trazer mais de uma (raiz antiga e nova durante a
    /// troca da CA); um arquivo DER traz uma só.
    /// </summary>
    public static X509Certificate2Collection LoadTrustedRoots(string path)
    {
        var roots = new X509Certificate2Collection();
        roots.ImportFromPemFile(path);
        if (roots.Count == 0)
        {
            roots.Add(X509CertificateLoader.LoadCertificateFromFile(path));
        }

        return roots;
    }

    /// <summary>
    /// A cadeia é refeita só com as raízes configuradas; o nome do servidor continua obrigatório (o callback
    /// recebe a divergência de nome em <paramref name="errors"/> e não pode descartá-la).
    /// </summary>
    internal static bool ValidateServerCertificate(
        X509Certificate2 serverCertificate,
        SslPolicyErrors errors,
        X509Certificate2Collection trustedRoots)
    {
        if ((errors & ~SslPolicyErrors.RemoteCertificateChainErrors) != SslPolicyErrors.None)
        {
            return false;
        }

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.AddRange(trustedRoots);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        return chain.Build(serverCertificate);
    }

    public AsyncDuplexStreamingCall<AgentMessage, ControlPlaneMessage> Connect(
        CancellationToken cancellationToken) => client.Connect(cancellationToken: cancellationToken);

    /// <summary>Canal separado do terminal (spec 014 D-001), no mesmo canal mTLS.</summary>
    public AsyncDuplexStreamingCall<ExecFrame, ExecFrame> Exec(CancellationToken cancellationToken) =>
        client.Exec(cancellationToken: cancellationToken);

    public ValueTask DisposeAsync()
    {
        channel.Dispose();
        return ValueTask.CompletedTask;
    }
}