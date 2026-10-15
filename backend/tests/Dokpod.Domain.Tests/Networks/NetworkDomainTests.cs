using Dokpod.Domain.Auditing;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Networks;
using Xunit;

namespace Dokpod.Domain.Tests.Networks;

/// <summary>Spec 019 T002: identificador e nome de rede, redes do sistema, comando de remoção e auditoria.</summary>
public sealed class NetworkDomainTests
{
    private static readonly string Id = new('a', 64);

    [Theory]
    [InlineData("rede-pedidos", true)]
    [InlineData("app_net.v2", true)]
    [InlineData("-rede", false)]
    [InlineData("rede/../etc", false)]
    [InlineData("", false)]
    public void NetworkNames_FollowTheEngineGrammar(string name, bool valid) => Assert.Equal(valid, NetworkNames.IsValidName(name));

    [Fact]
    public void NetworkIds_Are64LowercaseHex()
    {
        Assert.True(NetworkNames.IsValidId(Id));
        Assert.False(NetworkNames.IsValidId(new string('A', 64)));
        Assert.False(NetworkNames.IsValidId("abc"));
    }

    [Theory]
    [InlineData("bridge", false, true)]
    [InlineData("host", false, true)]
    [InlineData("none", false, true)]
    [InlineData("podman", false, true)]
    [InlineData("docker_gwbridge", false, true)]
    [InlineData("ingress", true, true)]
    [InlineData("rede-pedidos", false, false)]
    public void SystemNetworks_AreRecognizedByNameOrIngress(string name, bool ingress, bool system) => Assert.Equal(system, NetworkNames.IsSystem(name, ingress));

    [Fact]
    public void RemoveNetwork_IsAHostTargetCommandHashedByIdOnly()
    {
        var a = AgentCommandKinds.PayloadHash(AgentCommandKind.RemoveNetwork, string.Empty, string.Empty, new RemoveNetworkParameters(Id, "rede"));
        var renamed = AgentCommandKinds.PayloadHash(AgentCommandKind.RemoveNetwork, string.Empty, string.Empty, new RemoveNetworkParameters(Id, "outro-nome"));
        var other = AgentCommandKinds.PayloadHash(AgentCommandKind.RemoveNetwork, string.Empty, string.Empty, new RemoveNetworkParameters(new string('b', 64), "rede"));

        Assert.True(AgentCommandKinds.IsHostTarget(AgentCommandKind.RemoveNetwork));
        Assert.Equal(a, renamed);
        Assert.NotEqual(a, other);
        Assert.Throws<ArgumentException>(() => new RemoveNetworkParameters("abc", "rede"));
        Assert.Throws<ArgumentException>(() => new RemoveNetworkParameters(Id, "../etc"));
    }

    [Fact]
    public void NetworkRemoval_IsAnAuditedCommandActionInTheNetworksCategoryWithItsScope()
    {
        var audit = AuditEvent.Create(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), AuditActorKind.User, "sub-1", "network.remove", Guid.NewGuid(), AuditOutcome.Succeeded, null, Guid.NewGuid())
            .WithSubject("rede", new Dictionary<string, string> { ["name"] = "rede", ["networkId"] = Id });

        Assert.Equal("networks", AuditCategories.CategoryOf("network.remove"));
        Assert.Throws<ArgumentException>(() => AuditEvent.Create(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), AuditActorKind.User, "sub-1", "network.remove", Guid.NewGuid(), AuditOutcome.Succeeded));
        Assert.Equal("rede", audit.SubjectId);
        Assert.Contains(EnvironmentResourceScopes.DeleteNetwork, EnvironmentResourceScopes.Supported);
    }
}
