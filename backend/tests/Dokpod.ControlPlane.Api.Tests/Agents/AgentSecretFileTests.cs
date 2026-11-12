using Dokpod.Agent;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests.Agents;

/// <summary>Spec 033 T031 (FR-033, D-006): a senha do PFX do agente vem do arquivo em /run/secrets antes da variável.</summary>
public sealed class AgentSecretFileTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("dokpod-033-agent-secret-").FullName;

    [Fact]
    public void ReadSecret_PrefersTheFile_AndFallsBackToTheVariable()
    {
        var name = "DOKPOD_AGENT_CLIENT_CERTIFICATE_PASSWORD" + "_" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        Environment.SetEnvironmentVariable(name, "valor-do-ambiente");
        try
        {
            Assert.Equal("valor-do-ambiente", AgentOptions.ReadSecret(name, directory));
            File.WriteAllText(Path.Combine(directory, name), "valor-do-arquivo\n");
            Assert.Equal("valor-do-arquivo", AgentOptions.ReadSecret(name, directory));
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Fact]
    public void ReadSecret_WithoutFileOrVariable_IsNull()
    {
        Assert.Null(AgentOptions.ReadSecret("DOKPOD_AGENT_SEM_VALOR_" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(), directory));
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);
}
