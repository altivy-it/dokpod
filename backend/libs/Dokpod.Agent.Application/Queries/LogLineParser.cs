using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Dokpod.Agent.Application.Queries;

public sealed record ParsedLogLine(DateTimeOffset? Timestamp, string Text, string Level);

/// <summary>
/// Converte uma linha bruta do engine em texto seguro: separa o timestamp, remove sequências ANSI e
/// caracteres de controle, trunca em 16 KiB, mascara segredos e infere o nível (spec 011 FR-007).
/// </summary>
public static partial class LogLineParser
{
    public const int MaxLineBytes = 16 * 1024;

    [GeneratedRegex(@"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~]|\][^\x07\x1B]*(?:\x07|\x1B\\))")]
    private static partial Regex Ansi();

    [GeneratedRegex(@"\b(TRACE|DEBUG|DBG|INFO|INF|NOTICE|WARN|WARNING|WRN|ERROR|ERR|FATAL|CRITICAL|CRIT|PANIC)\b")]
    private static partial Regex LevelWord();

    [GeneratedRegex(@"""(?:level|lvl|severity)""\s*:\s*""([A-Za-z]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex JsonLevel();

    public static ParsedLogLine Parse(string raw, bool hasTimestamp)
    {
        DateTimeOffset? timestamp = null;
        var text = raw;
        if (hasTimestamp)
        {
            var space = raw.IndexOf(' ');
            if (space > 0 && DateTimeOffset.TryParse(raw[..space], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            {
                timestamp = parsed.ToUniversalTime();
                text = raw[(space + 1)..];
            }
        }

        text = Sanitize(text);
        return new ParsedLogLine(timestamp, SecretMasker.Apply(text), InferLevel(text));
    }

    public static string Sanitize(string text)
    {
        var withoutAnsi = Ansi().Replace(text, string.Empty);
        var builder = new StringBuilder(withoutAnsi.Length);
        foreach (var c in withoutAnsi)
        {
            if (c == '\t') builder.Append("    ");
            else if (!char.IsControl(c)) builder.Append(c);
        }

        var result = builder.ToString().TrimEnd();
        if (Encoding.UTF8.GetByteCount(result) <= MaxLineBytes) return result;
        var cut = Math.Min(result.Length, MaxLineBytes);
        while (cut > 0 && Encoding.UTF8.GetByteCount(result.AsSpan(0, cut)) > MaxLineBytes - 3) cut--;
        return result[..cut] + "…";
    }

    public static string InferLevel(string text)
    {
        var json = JsonLevel().Match(text);
        var word = json.Success ? json.Groups[1].Value : LevelWord().Match(text.Length > 200 ? text[..200] : text).Value;
        return word.ToUpperInvariant() switch
        {
            "TRACE" => "TRACE",
            "DEBUG" or "DBG" => "DEBUG",
            "INFO" or "INF" or "NOTICE" or "INFORMATION" => "INFO",
            "WARN" or "WARNING" or "WRN" => "WARN",
            "ERROR" or "ERR" or "FATAL" or "CRITICAL" or "CRIT" or "PANIC" => "ERROR",
            _ => string.Empty,
        };
    }
}
