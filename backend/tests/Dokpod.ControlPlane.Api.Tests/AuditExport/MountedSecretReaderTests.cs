using Dokpod.ControlPlane.Application.AuditExport;
using Dokpod.ControlPlane.Infrastructure.AuditExport;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests.AuditExport;

/// <summary>Spec 027 T011 com filesystem real (D-006; threat model L4, L5, L9): travessia, symlinks, limites e releitura.</summary>
public sealed class MountedSecretReaderTests : IDisposable
{
    private const string Certificate = "-----BEGIN CERTIFICATE-----\nMIIB\n-----END CERTIFICATE-----\n";
    private const string Key = "-----BEGIN PRIVATE KEY-----\nMIIE\n-----END PRIVATE KEY-----\n";

    private readonly string workspace = Directory.CreateTempSubdirectory("dokpod-secrets-").FullName;
    private readonly string root;
    private readonly MountedSecretReader reader;

    public MountedSecretReaderTests()
    {
        root = Directory.CreateDirectory(Path.Combine(workspace, "root")).FullName;
        reader = new MountedSecretReader(root);
    }

    [Fact]
    public void CompleteSecret_IsReadAndDescribedWithoutContent()
    {
        Write("siem-soc", ("ca.pem", Certificate), ("tls.crt", Certificate), ("tls.key", Key), ("token", "abc.DEF-123\n"));

        var secret = reader.Read("siem-soc");

        Assert.Null(secret.ErrorCode);
        Assert.Equal("abc.DEF-123", secret.Token);
        Assert.Equal(new AuditExportSecretFiles(true, true, true), reader.Describe("siem-soc"));
        Assert.Equal(AuditExportSecret.None, reader.Read(null));
    }

    [Theory]
    [InlineData("../root")]
    [InlineData("..")]
    [InlineData("Siem")]
    [InlineData("a/b")]
    public void InvalidOrTraversingRef_IsMissing(string secretRef)
    {
        Assert.Equal(AuditExportErrorCodes.SecretMissing, reader.Read(secretRef).ErrorCode);
        Assert.Equal(new AuditExportSecretFiles(false, false, false), reader.Describe(secretRef));
    }

    [Fact]
    public void SymlinkOutsideTheRoot_IsRefused_ButInsideIsAccepted()
    {
        var outside = Directory.CreateDirectory(Path.Combine(workspace, "outside")).FullName;
        File.WriteAllText(Path.Combine(outside, "ca.pem"), Certificate);
        Directory.CreateSymbolicLink(Path.Combine(root, "evil"), outside);
        var mixed = Directory.CreateDirectory(Path.Combine(root, "mixed")).FullName;
        File.CreateSymbolicLink(Path.Combine(mixed, "ca.pem"), Path.Combine(outside, "ca.pem"));

        // Como num secret do Kubernetes: o arquivo é symlink para ..data/ dentro da própria pasta.
        var kube = Directory.CreateDirectory(Path.Combine(root, "kube", "..data")).FullName;
        File.WriteAllText(Path.Combine(kube, "ca.pem"), Certificate);
        File.CreateSymbolicLink(Path.Combine(root, "kube", "ca.pem"), Path.Combine(kube, "ca.pem"));

        Assert.Equal(AuditExportErrorCodes.SecretMissing, reader.Read("evil").ErrorCode);
        Assert.Equal("ca.pem_outside_secret", reader.Read("mixed").Reason);
        Assert.Null(reader.Read("kube").ErrorCode);
        Assert.True(reader.Describe("kube").Ca);
    }

    [Theory]
    [InlineData("big", "ca.pem_too_large")]
    [InlineData("crlf", "token_invalid")]
    [InlineData("space", "token_invalid")]
    [InlineData("half", "client_certificate_incomplete")]
    [InlineData("not-pem", "ca_not_pem")]
    public void InvalidFiles_AreMissingWithAReasonAndNoContent(string scenario, string reason)
    {
        switch (scenario)
        {
            case "big": Write(scenario, ("ca.pem", Certificate + new string('A', MountedSecretReader.MaxFileBytes))); break;
            case "crlf": Write(scenario, ("token", "abc\r\nX-Injected: 1")); break;
            case "space": Write(scenario, ("token", " abc")); break;
            case "half": Write(scenario, ("tls.crt", Certificate)); break;
            default: Write(scenario, ("ca.pem", "texto qualquer")); break;
        }

        var secret = reader.Read(scenario);

        Assert.Equal((AuditExportErrorCodes.SecretMissing, reason), (secret.ErrorCode, secret.Reason));
        Assert.Null(secret.Token);
        Assert.Null(secret.CaPem);
    }

    [Fact]
    public void RotatedFile_IsReadOnTheNextConnection()
    {
        Write("rotate", ("token", "primeiro"));
        Assert.Equal("primeiro", reader.Read("rotate").Token);

        Write("rotate", ("token", "segundo"));

        Assert.Equal("segundo", reader.Read("rotate").Token);
    }

    public void Dispose() => Directory.Delete(workspace, recursive: true);

    private void Write(string secretRef, params (string Name, string Content)[] files)
    {
        var directory = Directory.CreateDirectory(Path.Combine(root, secretRef)).FullName;
        foreach (var (name, content) in files) File.WriteAllText(Path.Combine(directory, name), content);
    }
}
