using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Dokpod.Agent.Infrastructure.Protocol;
using Xunit;

namespace Dokpod.Agent.Infrastructure.Tests.Protocol;

public sealed class AgentControlClientTrustTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("dokpod-trust-").FullName;

    [Fact]
    public void ValidateServerCertificate_AcceptsChainFromConfiguredRoot()
    {
        using var root = CreateRoot("CN=Dokpod CA");
        using var server = CreateServer(root, "api");

        Assert.True(AgentControlClient.ValidateServerCertificate(
            server, SslPolicyErrors.RemoteCertificateChainErrors, [root]));
    }

    [Theory]
    [InlineData(SslPolicyErrors.RemoteCertificateNameMismatch)]
    [InlineData(SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateChainErrors)]
    [InlineData(SslPolicyErrors.RemoteCertificateNotAvailable)]
    public void ValidateServerCertificate_RejectsNameMismatchEvenWithTrustedChain(SslPolicyErrors errors)
    {
        using var root = CreateRoot("CN=Dokpod CA");
        using var server = CreateServer(root, "outro-host");

        Assert.False(AgentControlClient.ValidateServerCertificate(server, errors, [root]));
    }

    [Fact]
    public void ValidateServerCertificate_RejectsUnknownRoot()
    {
        using var root = CreateRoot("CN=Dokpod CA");
        using var other = CreateRoot("CN=Outra CA");
        using var server = CreateServer(other, "api");

        Assert.False(AgentControlClient.ValidateServerCertificate(
            server, SslPolicyErrors.RemoteCertificateChainErrors, [root]));
    }

    [Fact]
    public void LoadTrustedRoots_PemBundleAcceptsOldAndNewRootDuringRotation()
    {
        using var oldRoot = CreateRoot("CN=Dokpod CA antiga");
        using var newRoot = CreateRoot("CN=Dokpod CA nova");
        var path = Path.Combine(directory, "bundle.crt");
        File.WriteAllText(path, oldRoot.ExportCertificatePem() + "\n" + newRoot.ExportCertificatePem() + "\n");

        var roots = AgentControlClient.LoadTrustedRoots(path);

        Assert.Equal(2, roots.Count);
        using var fromOld = CreateServer(oldRoot, "api");
        using var fromNew = CreateServer(newRoot, "api");
        Assert.True(AgentControlClient.ValidateServerCertificate(fromOld, SslPolicyErrors.RemoteCertificateChainErrors, roots));
        Assert.True(AgentControlClient.ValidateServerCertificate(fromNew, SslPolicyErrors.RemoteCertificateChainErrors, roots));
    }

    [Fact]
    public void LoadTrustedRoots_DerFileStillLoadsSingleRoot()
    {
        using var root = CreateRoot("CN=Dokpod CA");
        var path = Path.Combine(directory, "ca.der");
        File.WriteAllBytes(path, root.Export(X509ContentType.Cert));

        var roots = AgentControlClient.LoadTrustedRoots(path);

        Assert.Equal(root.Thumbprint, Assert.Single(roots).Thumbprint);
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

    private static X509Certificate2 CreateServer(X509Certificate2 root, string name)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={name}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], true));
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName(name);
        request.CertificateExtensions.Add(san.Build());
        return request.Create(root, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(20), RandomNumberGenerator.GetBytes(8));
    }
}
