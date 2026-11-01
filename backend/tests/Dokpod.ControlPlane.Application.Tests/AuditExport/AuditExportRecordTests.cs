using System.Text.Json.Nodes;
using Dokpod.ControlPlane.Application.AuditExport;
using Dokpod.ControlPlane.Application.Auditing;
using Dokpod.Domain.Auditing;
using Xunit;

namespace Dokpod.ControlPlane.Application.Tests.AuditExport;

/// <summary>Spec 027 T007: o registro exportado tem os campos da linha da trilha mostrada no console e no CSV.</summary>
public sealed class AuditExportRecordTests
{
    private static readonly Guid Installation = Guid.Parse("3f0c5a1e-2b4d-4c6e-8f90-a1b2c3d4e5f6");

    [Fact]
    public void FromEvent_KeepsTheTrailFieldsAndFormat()
    {
        var e = new AuditEventRecord(
            Guid.NewGuid(),
            new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero).AddTicks(1_234_560),
            Guid.NewGuid(),
            AuditActorKind.User,
            "user-1",
            "Maria",
            "container.restart",
            Guid.NewGuid(),
            AuditOutcome.Denied,
            "authorization_denied",
            "web-1",
            Guid.NewGuid(),
            new Dictionary<string, string> { ["reason"] = "x", ["kind"] = "y" });

        var record = AuditExportRecord.FromEvent(e, Installation);
        var json = JsonNode.Parse(record.ToJson())!;

        Assert.Equal(1, json["formatVersion"]!.GetValue<int>());
        Assert.Equal(e.EventId.ToString("D"), json["eventId"]!.GetValue<string>());
        Assert.Equal("2026-11-01T12:00:00.123456Z", json["occurredAt"]!.GetValue<string>());
        Assert.Equal(Installation.ToString("D"), json["installationId"]!.GetValue<string>());
        Assert.Equal(("user", "user-1", "Maria"), (json["actor"]!["kind"]!.GetValue<string>(), json["actor"]!["id"]!.GetValue<string>(), json["actor"]!["name"]!.GetValue<string>()));
        Assert.Equal("containers", json["category"]!.GetValue<string>());
        Assert.Equal("denied", json["outcome"]!.GetValue<string>());
        Assert.Equal(["kind", "reason"], json["details"]!.AsObject().Select(pair => pair.Key));
        Assert.False(json["test"]!.GetValue<bool>());
        Assert.True(record.IsWarning);
        Assert.Equal(15, json.AsObject().Count);
    }

    [Fact]
    public void TestEvent_IsMarkedAndBelongsToTheMandatoryCategory()
    {
        var record = AuditExportRecord.TestEvent(Installation, Guid.NewGuid(), "user-1", "Maria", DateTimeOffset.UtcNow);
        var json = JsonNode.Parse(record.ToJson())!;

        Assert.True(json["test"]!.GetValue<bool>());
        Assert.Equal("audit_export.test", json["action"]!.GetValue<string>());
        Assert.Equal("audit_export", json["category"]!.GetValue<string>());
        Assert.Null(json["environmentId"]);
        Assert.False(record.IsWarning);
    }

    [Fact]
    public void InstallationEvents_LeaveWithoutEnvironment()
    {
        var e = new AuditEventRecord(
            Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), AuditActorKind.User, "user-1", null,
            "audit_export.destination_created", AuditEvent.InstallationScopeId, AuditOutcome.Succeeded, null, "d-1", null, null);

        var json = JsonNode.Parse(AuditExportRecord.FromEvent(e, Installation).ToJson())!;

        Assert.Null(json["environmentId"]);
        Assert.Equal("audit_export", json["category"]!.GetValue<string>());
    }
}
