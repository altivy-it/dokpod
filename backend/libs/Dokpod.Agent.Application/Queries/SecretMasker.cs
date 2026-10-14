using System.Text.RegularExpressions;

namespace Dokpod.Agent.Application.Queries;

/// <summary>
/// Mascaramento best-effort de padrões de segredo antes de qualquer conteúdo sair do host
/// (spec 011 FR-003/US4-4). A lista é mantida no agente.
/// </summary>
public static partial class SecretMasker
{
    public const string Mask = "<oculto>";

    [GeneratedRegex(@"(?i)(authorization\s*[:=]\s*(?:bearer|basic|token)\s+)[A-Za-z0-9._~+/=-]+")]
    private static partial Regex AuthorizationHeader();

    [GeneratedRegex(@"(?i)\b(bearer\s+)[A-Za-z0-9._~+/=-]{8,}")]
    private static partial Regex Bearer();

    [GeneratedRegex(@"(?i)((?:password|passwd|pwd|secret|token|api[_-]?key|access[_-]?key|private[_-]?key|client[_-]?secret|credential)s?[""']?\s*[:=]\s*[""']?)[^\s""',;&]+")]
    private static partial Regex KeyValue();

    [GeneratedRegex(@"([a-z][a-z0-9+.-]*://[^\s:/@]+:)[^\s@/]+(@)", RegexOptions.IgnoreCase)]
    private static partial Regex UrlCredentials();

    [GeneratedRegex(@"(-u\s+[^\s:]+:)\S+")]
    private static partial Regex CurlUser();

    [GeneratedRegex(@"-----BEGIN [A-Z ]*PRIVATE KEY-----[\s\S]*?(-----END [A-Z ]*PRIVATE KEY-----|$)")]
    private static partial Regex PrivateKey();

    [GeneratedRegex(@"\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{4,}")]
    private static partial Regex Jwt();

    [GeneratedRegex(@"(?i)(password|passwd|secret|token|key|credential|auth)")]
    private static partial Regex SensitiveKey();

    public static string Apply(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value ?? string.Empty;
        var text = PrivateKey().Replace(value, Mask);
        text = AuthorizationHeader().Replace(text, m => m.Groups[1].Value + Mask);
        text = Bearer().Replace(text, m => m.Groups[1].Value + Mask);
        text = Jwt().Replace(text, Mask);
        text = UrlCredentials().Replace(text, m => m.Groups[1].Value + Mask + m.Groups[2].Value);
        text = CurlUser().Replace(text, m => m.Groups[1].Value + Mask);
        text = KeyValue().Replace(text, m => m.Groups[1].Value + Mask);
        return text;
    }

    /// <summary>Chave cujo valor é sempre ocultado (labels, opções de log, campos de inspeção).</summary>
    public static bool IsSensitiveKey(string key) => SensitiveKey().IsMatch(key);
}
