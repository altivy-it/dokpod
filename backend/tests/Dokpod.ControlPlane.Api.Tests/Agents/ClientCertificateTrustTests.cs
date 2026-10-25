using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Dokpod.ControlPlane.Api;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests.Agents;

/// <summary>Troca da CA dos agentes com sobreposição (threat model consolidado da spec 005, L1).</summary>
public sealed class ClientCertificateTrustTests : IDisposable
{
    private const string ClientAuth = "1.3.6.1.5.5.7.3.2";
    private readonly string directory = Directory.CreateTempSubdirectory("dokpod-client-ca-").FullName;

    [Fact]
    public void ValidateClientCertificate_BundleAcceptsAgentsFromOldAndNewRoot()
    {
        using var oldRoot = CreateRoot("CN=Dokpod CA antiga");
        using var newRoot = CreateRoot("CN=Dokpod CA nova");
        var path = Path.Combine(directory, "agents-ca.crt");
        File.WriteAllText(path, oldRoot.ExportCertificatePem() + "\n" + newRoot.ExportCertificatePem() + "\n");
        var roots = ApiHost.LoadTrustedRoots(path);

        using var oldAgent = CreateAgent(oldRoot, ClientAuth);
        using var newAgent = CreateAgent(newRoot, ClientAuth);

        Assert.True(ApiHost.ValidateClientCertificate(oldAgent, roots));
        Assert.True(ApiHost.ValidateClientCertificate(newAgent, roots));
    }

    [Fact]
    public void ValidateClientCertificate_RejectsAgentFromRootRemovedAfterRotation()
    {
        using var oldRoot = CreateRoot("CN=Dokpod CA antiga");
        using var newRoot = CreateRoot("CN=Dokpod CA nova");
        using var oldAgent = CreateAgent(oldRoot, ClientAuth);

        Assert.False(ApiHost.ValidateClientCertificate(oldAgent, [newRoot]));
    }

    [Fact]
    public void ValidateClientCertificate_RejectsCertificateWithoutClientAuthentication()
    {
        using var root = CreateRoot("CN=Dokpod CA");
        using var server = CreateAgent(root, "1.3.6.1.5.5.7.3.1");

        Assert.False(ApiHost.ValidateClientCertificate(server, [root]));
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);

    private static X509Certificate2 CreateRoot(string subject)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(30));
    }

    private static X509Certificate2 CreateAgent(X509Certificate2 root, string eku)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=dokpod-agent", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid(eku)], true));
        return request.Create(root, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(20), RandomNumberGenerator.GetBytes(8));
    }
}
