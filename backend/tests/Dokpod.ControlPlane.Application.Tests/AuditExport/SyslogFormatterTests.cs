using System.Text;
using System.Text.Json.Nodes;
using Dokpod.ControlPlane.Application.AuditExport;
using Xunit;

namespace Dokpod.ControlPlane.Application.Tests.AuditExport;

/// <summary>Spec 027 T014: RFC 5424 sobre RFC 5425 conforme contracts/export-record.md e threat model L6.</summary>
public sealed class SyslogFormatterTests
{
    private static readonly DateTimeOffset At = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero).AddTicks(1_234_560);

    [Theory]
    [InlineData("succeeded", "<85>")]
    [InlineData("denied", "<84>")]
    [InlineData("failed", "<84>")]
    [InlineData("indeterminate", "<84>")]
    public void Priority_UsesAuthprivAndTheOutcomeSeverity(string outcome, string pri)
    {
        var message = Text(SyslogFormatter.Message(Record("container.restart") with { Outcome = outcome }));

        Assert.StartsWith(pri + "1 ", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Header_HasMicrosecondsAppNameMsgIdAndNoStructuredData()
    {
        var message = SyslogFormatter.Message(Record("container.restart"), "dokpod-prod");
        var bom = IndexOf(message, [0xEF, 0xBB, 0xBF]);

        Assert.Equal("<85>1 2026-11-01T12:00:00.123456Z dokpod-prod dokpod - container.restart - ", Encoding.ASCII.GetString(message, 0, bom));
        var json = JsonNode.Parse(Encoding.UTF8.GetString(message, bom + 3, message.Length - bom - 3))!;
        Assert.Equal("container.restart", json["action"]!.GetValue<string>());
        Assert.Equal(1, json["formatVersion"]!.GetValue<int>());
    }

    [Fact]
    public void Hostname_DefaultsToNilValue()
    {
        Assert.Contains(" 2026-11-01T12:00:00.123456Z - dokpod - ", Text(SyslogFormatter.Message(Record("container.restart"))), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("container restart", "container_restart")]
    [InlineData("ação.ç", "a__o._")]
    [InlineData("a.very.long.action.name.that.goes.beyond.the.limit", "a.very.long.action.name.that.goe")]
    [InlineData("", "-")]
    public void MsgId_OnlyPrintableAsciiUpTo32(string action, string expected)
    {
        Assert.Equal(expected, SyslogFormatter.Token(action, SyslogFormatter.MaxMsgIdLength));
    }

    [Fact]
    public void Frame_IsOctetCountedEvenWithMultibyteAndNewlines()
    {
        var record = Record("container.restart") with { ActorName = "José\n<13>1 forjado" };
        var frame = SyslogFormatter.Frame(record);
        var space = Array.IndexOf(frame, (byte)' ');
        var length = int.Parse(Encoding.ASCII.GetString(frame, 0, space), System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(frame.Length - space - 1, length);
        Assert.Equal(SyslogFormatter.Message(record), frame[(space + 1)..]);
        // A quebra de linha vai escapada no JSON, nunca crua na mensagem.
        Assert.DoesNotContain((byte)'\n', frame);
    }

    private static AuditExportRecord Record(string action) => new(
        Guid.NewGuid(), At, Guid.NewGuid(), Guid.NewGuid(), null, "user", "user-1", "Maria", action, "containers",
        Guid.NewGuid(), "web-1", "succeeded", null, new Dictionary<string, string>(), Test: false);

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle)) return i;
        }

        return -1;
    }
}
