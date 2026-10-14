using Dokpod.Agent.Infrastructure.Engines;
using Xunit;

namespace Dokpod.Agent.Infrastructure.Tests;

public sealed class EngineSocketResolverTests
{
    [Fact]
    public void Resolve_ConfiguredPathAlwaysWins()
    {
        Assert.Equal("/custom.sock", EngineSocketResolver.Resolve("/custom.sock", false, "/run/user/1000", _ => false));
    }

    [Fact]
    public void Resolve_WindowsUsesDockerNamedPipe()
    {
        Assert.Equal("docker_engine", EngineSocketResolver.Resolve(null, true, null, _ => true));
    }

    [Theory]
    [InlineData("/run/docker.sock", "/run/docker.sock")]
    [InlineData("/run/user/1000/podman/podman.sock", "/run/user/1000/podman/podman.sock")]
    [InlineData("/run/podman/podman.sock", "/run/podman/podman.sock")]
    [InlineData(null, "/run/docker.sock")]
    public void Resolve_LinuxProbesDockerThenRootlessThenRootfulPodman(string? existing, string expected)
    {
        Assert.Equal(expected, EngineSocketResolver.Resolve(null, false, "/run/user/1000", path => path == existing));
    }

    [Fact]
    public void Resolve_PrefersDockerWhenBothEnginesArePresent()
    {
        Assert.Equal("/run/docker.sock", EngineSocketResolver.Resolve(null, false, "/run/user/1000", _ => true));
    }
}
