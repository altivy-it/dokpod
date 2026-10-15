using Dokpod.Domain.Auditing;
using Xunit;

namespace Dokpod.Domain.Tests.Auditing;

/// <summary>Spec 017 T001: nome do ator, exportação e categorias.</summary>
public sealed class AuditConsoleTests
{
    private static AuditEvent Event(AuditActorKind kind = AuditActorKind.User, string action = "environment.stage_changed") =>
        AuditEvent.Create(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), kind, "sub-1", action, Guid.NewGuid(), AuditOutcome.Succeeded);

    [Fact]
    public void ActorName_IsKeptForUsersAndSurvivesSubjectAndDetails()
    {
        var named = Event().WithActorName("  ana  ").WithSubject("abc", new Dictionary<string, string> { ["stage"] = "production" });

        Assert.Equal("ana", named.ActorName);
        Assert.Equal("abc", named.SubjectId);
        Assert.Equal("ana", Event().WithSubject("abc", null).WithActorName("ana").ActorName);
    }

    [Theory]
    [InlineData(AuditActorKind.Agent, "ana")]
    [InlineData(AuditActorKind.User, "   ")]
    [InlineData(AuditActorKind.User, "an\u0007a")]
    public void ActorName_IsIgnoredForNonUsersBlankOrControlCharacters(AuditActorKind kind, string name) =>
        Assert.Null(Event(kind).WithActorName(name).ActorName);

    [Fact]
    public void ActorName_LongerThanTheColumnIsIgnored() =>
        Assert.Null(Event().WithActorName(new string('a', AuditEvent.MaxActorNameLength + 1)).ActorName);

    [Fact]
    public void Export_IsAnAuditActionWithCount()
    {
        var exported = Event(action: "audit.exported").WithSubject("audit", new Dictionary<string, string> { ["since"] = "2026-10-01T00:00:00Z", ["until"] = "2026-10-15T00:00:00Z", ["count"] = "42" });

        Assert.Equal("42", exported.Details!["count"]);
    }

    [Fact]
    public void EveryCategorizedActionIsSupportedAndEverySupportedActionHasACategory()
    {
        var categorized = AuditCategories.Actions.Values.SelectMany(a => a).ToHashSet();
        Assert.Subset(AuditEvent.Actions.ToHashSet(), categorized);
        Assert.Subset(categorized, AuditEvent.Actions.ToHashSet());
        Assert.Equal("terminal", AuditCategories.CategoryOf("container.exec.started"));
        Assert.Null(AuditCategories.CategoryOf("unknown"));
    }
}
