using Dokpod.Domain.Auditing;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Volumes;
using Xunit;

namespace Dokpod.Domain.Tests.Volumes;

/// <summary>Spec 018 T002: nomes de volume, comando de remoção e auditoria.</summary>
public sealed class VolumeDomainTests
{
    [Theory]
    [InlineData("dados", true)]
    [InlineData("app_db-data.v2", true)]
    [InlineData("-dados", false)]
    [InlineData("dados/../etc", false)]
    [InlineData("dados com espaco", false)]
    [InlineData("", false)]
    public void VolumeNames_FollowTheEngineGrammar(string name, bool valid) => Assert.Equal(valid, VolumeNames.IsValid(name));

    [Fact]
    public void VolumeNames_RecognizeAnonymousVolumes()
    {
        Assert.True(VolumeNames.IsAnonymous(new string('a', 64)));
        Assert.False(VolumeNames.IsAnonymous("dados"));
        Assert.False(VolumeNames.IsAnonymous(new string('A', 64)));
    }

    [Fact]
    public void RemoveVolume_IsAHostTargetCommandWithAStableHashPerVolume()
    {
        var a = AgentCommandKinds.PayloadHash(AgentCommandKind.RemoveVolume, string.Empty, string.Empty, new RemoveVolumeParameters("dados"));
        var b = AgentCommandKinds.PayloadHash(AgentCommandKind.RemoveVolume, string.Empty, string.Empty, new RemoveVolumeParameters("outro"));

        Assert.True(AgentCommandKinds.IsHostTarget(AgentCommandKind.RemoveVolume));
        Assert.Equal(a, AgentCommandKinds.PayloadHash(AgentCommandKind.RemoveVolume, string.Empty, string.Empty, new RemoveVolumeParameters("dados")));
        Assert.NotEqual(a, b);
        Assert.Throws<ArgumentException>(() => new RemoveVolumeParameters("../etc"));
    }

    [Fact]
    public void VolumeRemoval_IsAnAuditedCommandActionInTheVolumesCategory()
    {
        var command = Guid.NewGuid();
        var audit = AuditEvent.Create(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), AuditActorKind.User, "sub-1", "volume.remove", Guid.NewGuid(), AuditOutcome.Succeeded, null, command)
            .WithSubject("dados", new Dictionary<string, string> { ["name"] = "dados", ["justification"] = "Limpeza do lote antigo" });

        Assert.Equal("volumes", AuditCategories.CategoryOf("volume.remove"));
        Assert.Throws<ArgumentException>(() => AuditEvent.Create(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), AuditActorKind.User, "sub-1", "volume.remove", Guid.NewGuid(), AuditOutcome.Succeeded));
        Assert.Equal("dados", audit.SubjectId);
    }
}
