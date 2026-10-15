using Dokpod.Domain.Auditing;
using Dokpod.Domain.Images;
using Xunit;

namespace Dokpod.Domain.Tests.Registries;

/// <summary>Spec 020 T002: normalização de registro, registro de origem das imagens e auditoria da política.</summary>
public sealed class RegistryNamesTests
{
    [Theory]
    [InlineData("https://index.docker.io/v1/", "docker.io")]
    [InlineData("registry-1.docker.io", "docker.io")]
    [InlineData("Registry.Empresa.com:5000", "registry.empresa.com:5000")]
    [InlineData("http://registry.local:5000/", "registry.local:5000")]
    [InlineData("quay.io/org/app", "quay.io")]
    [InlineData("localhost:5000", "localhost:5000")]
    [InlineData("user:pass@registry.io", null)]
    [InlineData("registro com espaco", null)]
    [InlineData("", null)]
    public void Normalize_KeepsOnlyHostAndPort(string value, string? expected) => Assert.Equal(expected, RegistryNames.Normalize(value));

    [Fact]
    public void IsValid_RequiresCanonicalForm()
    {
        Assert.True(RegistryNames.IsValid("registry.empresa.com:5000"));
        Assert.False(RegistryNames.IsValid("Registry.Empresa.com"));
        Assert.False(RegistryNames.IsValid("https://quay.io"));
        Assert.False(RegistryNames.IsValid("quay.io/org"));
    }

    [Fact]
    public void OfImage_UsesTheFirstTagThenTheDigest()
    {
        Assert.Equal("docker.io", RegistryNames.OfImage(["nginx:1.27"], []));
        Assert.Equal("quay.io", RegistryNames.OfImage(["quay.io/prometheus/prometheus:v2"], ["docker.io/library/nginx@sha256:" + new string('a', 64)]));
        Assert.Equal("registry.local:5000", RegistryNames.OfImage([], ["registry.local:5000/app@sha256:" + new string('b', 64)]));
        Assert.Null(RegistryNames.OfImage(["<none>:<none>"], []));
    }

    [Fact]
    public void PolicyChange_IsAGovernanceAuditWithBothLists()
    {
        var audit = AuditEvent.Create(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), AuditActorKind.User, "sub-1", "registry.policy_changed", Guid.NewGuid(), AuditOutcome.Succeeded)
            .WithSubject("production", new Dictionary<string, string> { ["stage"] = "production", ["previousRegistries"] = "*", ["registries"] = "quay.io" });

        Assert.Equal("governance", AuditCategories.CategoryOf("registry.policy_changed"));
        Assert.Equal("quay.io", audit.Details!["registries"]);
    }
}
