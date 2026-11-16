using Dokpod.ControlPlane.Api.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests.Configuration;

/// <summary>Spec 033 T017 (FR-033, D-006): segredo por arquivo em /run/secrets vence a variável de ambiente.</summary>
public sealed class SecretFilesConfigurationTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("dokpod-033-secret-files-").FullName;

    [Fact]
    public void FileInTheSecretsDirectory_WinsOverTheEnvironmentValue()
    {
        File.WriteAllText(Path.Combine(directory, "ConnectionStrings__ControlPlane"), "valor-do-arquivo");
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:ControlPlane"] = "valor-do-ambiente" });

        SecretFiles.AddTo(configuration, directory);

        Assert.Equal("valor-do-arquivo", configuration["ConnectionStrings:ControlPlane"]);
    }

    [Fact]
    public void WithoutTheDirectory_TheCurrentValuesStay()
    {
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:ControlPlane"] = "valor-do-ambiente" });

        SecretFiles.AddTo(configuration, Path.Combine(directory, "nao-existe"));

        Assert.Equal("valor-do-ambiente", configuration["ConnectionStrings:ControlPlane"]);
    }

    [Fact]
    public void Read_PrefersTheFile_AndFallsBackToTheVariable()
    {
        var name = "DOKPOD_API_CERTIFICATE_PASSWORD" + "_" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        Environment.SetEnvironmentVariable(name, "valor-do-ambiente");
        try
        {
            Assert.Equal("valor-do-ambiente", SecretFiles.Read(name, directory));
            File.WriteAllText(Path.Combine(directory, name), "valor-do-arquivo\n");
            Assert.Equal("valor-do-arquivo", SecretFiles.Read(name, directory));
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Fact]
    public void HiddenFiles_AreIgnored()
    {
        File.WriteAllText(Path.Combine(directory, ".oculto"), "x");
        var configuration = new ConfigurationManager();

        SecretFiles.AddTo(configuration, directory);

        Assert.Null(configuration[".oculto"]);
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);
}
