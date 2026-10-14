using System.Security.Cryptography;
using System.Text;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Images;
using Xunit;

namespace Dokpod.Domain.Tests.Images;

public sealed class ImageReferenceTests
{
    [Theory]
    [InlineData("nginx", "docker.io/library/nginx:latest", true)]
    [InlineData("nginx:1.27", "docker.io/library/nginx:1.27", true)]
    [InlineData("library/redis:7.4-alpine", "docker.io/library/redis:7.4-alpine", true)]
    [InlineData("quay.io/prometheus/prometheus:v2.54.1", "quay.io/prometheus/prometheus:v2.54.1", true)]
    [InlineData("registry.empresa.com:5000/pedidos/api:3.14.2", "registry.empresa.com:5000/pedidos/api:3.14.2", true)]
    [InlineData("localhost/app", "localhost/app:latest", true)]
    [InlineData("Registry.Empresa.com/app:1", "registry.empresa.com/app:1", true)]
    [InlineData("index.docker.io/nginx", "docker.io/library/nginx:latest", true)]
    public void Parses_AndNormalizes(string value, string canonical, bool mutable)
    {
        Assert.True(ImageReference.TryParse(value, out var reference));
        Assert.Equal(canonical, reference.Canonical);
        Assert.Equal(mutable, reference.IsMutableTag);
    }

    [Fact]
    public void DigestReferenceIsImmutable()
    {
        var digest = "sha256:" + new string('a', 64);
        Assert.True(ImageReference.TryParse($"ghcr.io/org/app@{digest}", out var reference));
        Assert.False(reference.IsMutableTag);
        Assert.Null(reference.Tag);
        Assert.Equal($"ghcr.io/org/app@{digest}", reference.Canonical);
    }

    [Theory]
    [InlineData("")]
    [InlineData("user:senha@registry.empresa.com/app")]
    [InlineData("registry.empresa.com/app?x=1")]
    [InlineData("registry.empresa.com/app#frag")]
    [InlineData("nginx latest")]
    [InlineData("Nginx")]
    [InlineData("registry.empresa.com/App:1")]
    [InlineData("nginx:")]
    [InlineData("nginx:-tag")]
    [InlineData("nginx@sha256:abc")]
    [InlineData("../etc/passwd")]
    [InlineData("nginx;rm -rf /")]
    [InlineData("registry.empresa.com/app\n")]
    [InlineData("a/")]
    public void Rejects_ReferencesOutsideTheGrammar(string value)
    {
        Assert.False(ImageReference.TryParse(value, out _));
    }

    [Fact]
    public void Rejects_ReferencesLongerThanTheLimit()
    {
        Assert.False(ImageReference.TryParse("registry.empresa.com/" + new string('a', 250), out _));
    }

    [Fact]
    public void Matches_UsesNormalizedTagsAndDigests()
    {
        Assert.True(ImageReference.TryParse("nginx", out var reference));
        Assert.True(reference.Matches(["nginx:latest"], []));
        Assert.True(reference.Matches(["docker.io/library/nginx:latest"], []));
        Assert.False(reference.Matches(["nginx:1.27"], []));
        var digest = "sha256:" + new string('b', 64);
        Assert.True(ImageReference.TryParse($"nginx@{digest}", out var pinned));
        Assert.True(pinned.Matches([], [$"nginx@{digest}"]));
    }

    [Fact]
    public void PayloadHash_V1IsUnchangedAndV2CoversParameters()
    {
        var v1 = AgentCommandKinds.PayloadHash(AgentCommandKind.StartContainer, "abc", "rev", null);
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("v1\n1\nabc\nrev")));
        Assert.Equal(expected, v1);

        Assert.True(ImageReference.TryParse("nginx", out var a));
        Assert.True(ImageReference.TryParse("docker.io/library/nginx:latest", out var b));
        Assert.True(ImageReference.TryParse("nginx:1.27", out var c));
        var hashA = AgentCommandKinds.PayloadHash(AgentCommandKind.PullImage, string.Empty, string.Empty, new PullImageParameters(a));
        Assert.Equal(hashA, AgentCommandKinds.PayloadHash(AgentCommandKind.PullImage, string.Empty, string.Empty, new PullImageParameters(b)));
        Assert.NotEqual(hashA, AgentCommandKinds.PayloadHash(AgentCommandKind.PullImage, string.Empty, string.Empty, new PullImageParameters(c)));
        Assert.Throws<ArgumentException>(() => AgentCommandKinds.PayloadHash(AgentCommandKind.PullImage, string.Empty, string.Empty, null));
    }
}
