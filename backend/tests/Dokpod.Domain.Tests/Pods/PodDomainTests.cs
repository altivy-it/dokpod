using Dokpod.Domain.Auditing;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Pods;
using Xunit;

namespace Dokpod.Domain.Tests.Pods;

/// <summary>Spec 021 T002: ações de pod, scopes de container, hash, pod do agente e auditoria.</summary>
public sealed class PodDomainTests
{
    private static readonly string Id = new('a', 64);

    [Theory]
    [InlineData(PodActionKind.Start, "container:start", false, false)]
    [InlineData(PodActionKind.Stop, "container:stop", true, true)]
    [InlineData(PodActionKind.Restart, "container:restart", false, true)]
    [InlineData(PodActionKind.Pause, "container:pause", true, true)]
    [InlineData(PodActionKind.Unpause, "container:pause", false, false)]
    public void Actions_MapToContainerScopesJustificationAndAgentPodProtection(PodActionKind action, string scope, bool justification, bool refusedForAgent)
    {
        Assert.Equal(scope, PodNames.ScopeFor(action));
        Assert.Equal(justification, PodNames.RequiresJustification(action));
        Assert.Equal(refusedForAgent, PodNames.RefusedForAgentPod(action));
        Assert.True(PodNames.TryParse(PodNames.ToValue(action), out var parsed));
        Assert.Equal(action, parsed);
        Assert.Contains(PodNames.ScopeFor(action), EnvironmentResourceScopes.Supported);
    }

    [Fact]
    public void PodAction_IsAHostCommandHashedByIdAndAction()
    {
        var stop = AgentCommandKinds.PayloadHash(AgentCommandKind.PodAction, string.Empty, string.Empty, new PodActionParameters(Id, "pod-a", PodActionKind.Stop));
        var renamed = AgentCommandKinds.PayloadHash(AgentCommandKind.PodAction, string.Empty, string.Empty, new PodActionParameters(Id, "outro", PodActionKind.Stop));
        var start = AgentCommandKinds.PayloadHash(AgentCommandKind.PodAction, string.Empty, string.Empty, new PodActionParameters(Id, "pod-a", PodActionKind.Start));

        Assert.True(AgentCommandKinds.IsHostTarget(AgentCommandKind.PodAction));
        Assert.Equal(stop, renamed);
        Assert.NotEqual(stop, start);
        Assert.Throws<ArgumentException>(() => new PodActionParameters("abc", "pod-a", PodActionKind.Stop));
        Assert.Throws<ArgumentException>(() => new PodActionParameters(Id, "../x", PodActionKind.Stop));
        Assert.False(PodNames.TryParse("remove", out _));
    }

    [Fact]
    public void PodActions_AreAuditedCommandsInThePodsCategory()
    {
        var audit = AuditEvent.Create(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), AuditActorKind.User, "sub-1", "pod.stop", Guid.NewGuid(), AuditOutcome.Succeeded, null, Guid.NewGuid())
            .WithSubject("pod-a", new Dictionary<string, string> { ["name"] = "pod-a", ["podId"] = Id, ["justification"] = "Janela de manutenção" });

        Assert.Equal("pods", AuditCategories.CategoryOf("pod.restart"));
        Assert.Equal("pod-a", audit.SubjectId);
        Assert.Throws<ArgumentException>(() => AuditEvent.Create(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), AuditActorKind.User, "sub-1", "pod.start", Guid.NewGuid(), AuditOutcome.Succeeded));
    }
}
