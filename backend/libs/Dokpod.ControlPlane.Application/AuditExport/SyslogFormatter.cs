using System.Globalization;
using System.Text;

namespace Dokpod.ControlPlane.Application.AuditExport;

/// <summary>
/// Mensagem RFC 5424 com enquadramento por contagem de octetos da RFC 5425 (spec 027 D-004, contracts/export-record.md):
/// <c>LEN SP &lt;PRI&gt;1 TIMESTAMP HOSTNAME dokpod - MSGID - BOM JSON</c>. A contagem de octetos impede que o conteúdo
/// quebre o quadro, e o <c>MSGID</c> só leva ASCII imprimível (threat model L6).
/// </summary>
public static class SyslogFormatter
{
    /// <summary>Facility <c>security/authorization</c> (10, <i>authpriv</i>).</summary>
    public const int Facility = 10;
    public const int SeverityNotice = 5;
    public const int SeverityWarning = 4;
    public const int MaxMsgIdLength = 32;
    public const int MaxHostnameLength = 255;

    private static readonly byte[] Bom = [0xEF, 0xBB, 0xBF];

    /// <summary>Quadro pronto para o fio: <c>LEN SP MSG</c>, com <c>LEN</c> em octetos.</summary>
    public static byte[] Frame(AuditExportRecord record, string? hostname = null)
    {
        var message = Message(record, hostname);
        var prefix = Encoding.ASCII.GetBytes(message.Length.ToString(CultureInfo.InvariantCulture) + " ");
        return [.. prefix, .. message];
    }

    public static byte[] Message(AuditExportRecord record, string? hostname = null)
    {
        ArgumentNullException.ThrowIfNull(record);
        var pri = Facility * 8 + (record.IsWarning ? SeverityWarning : SeverityNotice);
        var timestamp = record.OccurredAtUtc.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture);
        var header = $"<{pri.ToString(CultureInfo.InvariantCulture)}>1 {timestamp} {Token(hostname, MaxHostnameLength)} dokpod - {Token(record.Action, MaxMsgIdLength)} - ";
        return [.. Encoding.ASCII.GetBytes(header), .. Bom, .. Encoding.UTF8.GetBytes(record.ToJson())];
    }

    /// <summary>Campo de cabeçalho: ASCII 33–126, até <paramref name="maxLength"/>; o resto vira <c>_</c>; vazio vira <c>-</c>.</summary>
    public static string Token(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return "-";
        var builder = new StringBuilder(Math.Min(value.Length, maxLength));
        foreach (var character in value)
        {
            if (builder.Length == maxLength) break;
            builder.Append(character is >= '!' and <= '~' ? character : '_');
        }

        return builder.ToString();
    }
}
