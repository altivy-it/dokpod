using Xunit;

namespace Dokpod.Architecture.Tests;

/// <summary>
/// Spec 031 T061 (U4): a imagem Business usa <c>Business.Dockerfile</c>, porque o workflow gerenciado de imagens não aceita
/// build-arg. Ele só pode diferir do Dockerfile da CE no comentário de cabeçalho, no padrão de <c>DOKPOD_EDITION</c> e nos
/// rótulos de título e descrição; qualquer outra mudança precisa ir aos dois.
/// </summary>
public sealed class BusinessDockerfileTests
{
    private static readonly string ApiRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../apps/Dokpod.ControlPlane.Api"));

    [Fact]
    public void BusinessDockerfile_DiffersOnlyInEditionAndLabels()
    {
        var community = File.ReadAllLines(Path.Combine(ApiRoot, "Dockerfile"));
        // Ignora só o cabeçalho próprio (comentários logo após a linha `# syntax`).
        var lines = File.ReadAllLines(Path.Combine(ApiRoot, "Business.Dockerfile"));
        var business = lines.Take(1).Concat(lines.Skip(1).SkipWhile(line => line.StartsWith("# ", StringComparison.Ordinal))).ToArray();

        Assert.Equal(community.Length, business.Length);
        var differences = community.Zip(business).Where(pair => pair.First != pair.Second).ToArray();
        Assert.Equal(3, differences.Length);
        Assert.Contains(differences, pair => pair is { First: "ARG DOKPOD_EDITION=", Second: "ARG DOKPOD_EDITION=Business" });
        Assert.Contains(differences, pair => pair.Second.Contains("org.opencontainers.image.title=\"dokpod-api-business\"", StringComparison.Ordinal));
        Assert.Contains(differences, pair => pair.Second.Contains("org.opencontainers.image.description=", StringComparison.Ordinal));
    }

    [Fact]
    public void BusinessTrustStore_HasProductionKeysOnly()
    {
        var keys = Directory.EnumerateFiles(Path.Combine(ApiRoot, "Licensing", "TrustStore"), "*.pem").Select(Path.GetFileNameWithoutExtension).ToArray();

        Assert.NotEmpty(keys);
        Assert.DoesNotContain(keys, key => key!.StartsWith("dokpod-test", StringComparison.Ordinal));
        Assert.All(Directory.EnumerateFiles(Path.Combine(ApiRoot, "Licensing", "TrustStore"), "*.pem"), file =>
        {
            var text = File.ReadAllText(file);
            Assert.StartsWith("-----BEGIN PUBLIC KEY-----", text, StringComparison.Ordinal);
            Assert.DoesNotContain("PRIVATE", text, StringComparison.Ordinal);
        });
    }
}
