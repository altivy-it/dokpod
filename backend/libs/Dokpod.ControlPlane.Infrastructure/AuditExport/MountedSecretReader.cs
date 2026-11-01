using System.Text;
using System.Text.RegularExpressions;
using Dokpod.ControlPlane.Application.AuditExport;

namespace Dokpod.ControlPlane.Infrastructure.AuditExport;

/// <summary>
/// Lê <c>ca.pem</c>, <c>tls.crt</c>, <c>tls.key</c> e <c>token</c> de <c>&lt;raiz&gt;/&lt;secretRef&gt;/</c> (spec 027 D-006).
/// O caminho é montado só a partir do <c>secretRef</c> validado, normalizado e conferido dentro da raiz antes de
/// qualquer acesso; symlinks são aceitos só quando resolvem dentro da raiz (como os de secrets do Kubernetes).
/// Cada arquivo tem no máximo 64 KiB (L4), e o token só aceita ASCII visível (L5).
/// </summary>
public sealed partial class MountedSecretReader(string root) : IAuditExportSecretReader
{
    public const string DefaultRoot = "/run/secrets/dokpod-audit-export";
    public const int MaxFileBytes = 64 * 1024;

    private readonly string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

    public AuditExportSecretFiles Describe(string? secretRef)
    {
        if (secretRef is null || Directory(secretRef) is not { } directory) return new AuditExportSecretFiles(false, false, false);
        return new AuditExportSecretFiles(
            Exists(directory, "ca.pem"),
            Exists(directory, "tls.crt") && Exists(directory, "tls.key"),
            Exists(directory, "token"));
    }

    public AuditExportSecret Read(string? secretRef)
    {
        if (secretRef is null) return AuditExportSecret.None;
        if (Directory(secretRef) is not { } directory) return AuditExportSecret.Missing("secret_not_found");

        var (ca, caError) = ReadFile(directory, "ca.pem");
        var (certificate, certificateError) = ReadFile(directory, "tls.crt");
        var (key, keyError) = ReadFile(directory, "tls.key");
        var (token, tokenError) = ReadFile(directory, "token");
        var error = caError ?? certificateError ?? keyError ?? tokenError;
        if (error is not null) return AuditExportSecret.Missing(error);

        if (ca is not null && !ca.Contains("-----BEGIN CERTIFICATE-----", StringComparison.Ordinal)) return AuditExportSecret.Missing("ca_not_pem");
        if ((certificate is null) != (key is null)) return AuditExportSecret.Missing("client_certificate_incomplete");
        if (certificate is not null && !certificate.Contains("-----BEGIN CERTIFICATE-----", StringComparison.Ordinal)) return AuditExportSecret.Missing("client_certificate_not_pem");
        if (key is not null && !key.Contains("PRIVATE KEY-----", StringComparison.Ordinal)) return AuditExportSecret.Missing("client_key_not_pem");
        if (token is not null)
        {
            token = token.TrimEnd('\n', '\r');
            if (!TokenPattern().IsMatch(token)) return AuditExportSecret.Missing("token_invalid");
        }

        return new AuditExportSecret(ca, certificate, key, token, null, null);
    }

    private string? Directory(string secretRef)
    {
        if (!SecretRefPattern().IsMatch(secretRef)) return null;
        var directory = Path.GetFullPath(Path.Combine(root, secretRef));
        if (!IsInside(directory) || !System.IO.Directory.Exists(directory)) return null;
        var info = new DirectoryInfo(directory);
        var resolved = info.LinkTarget is null ? directory : info.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? directory;
        return IsInside(Path.GetFullPath(resolved)) ? directory : null;
    }

    private bool Exists(string directory, string name) => Resolve(directory, name) is { } path && File.Exists(path);

    private (string? Content, string? Error) ReadFile(string directory, string name)
    {
        var candidate = Path.Combine(directory, name);
        if (!File.Exists(candidate) && new FileInfo(candidate).LinkTarget is null) return (null, null);
        if (Resolve(directory, name) is not { } path || !File.Exists(path)) return (null, $"{name}_outside_secret");
        var info = new FileInfo(path);
        if (info.Length > MaxFileBytes) return (null, $"{name}_too_large");
        return (File.ReadAllText(path, Encoding.ASCII), null);
    }

    private string? Resolve(string directory, string name)
    {
        var path = Path.Combine(directory, name);
        var info = new FileInfo(path);
        var target = info.LinkTarget is null ? path : info.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? path;
        var full = Path.GetFullPath(target);
        return IsInside(full) ? full : null;
    }

    private bool IsInside(string path) => path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    [GeneratedRegex(@"\A[a-z0-9-]{1,64}\z")]
    private static partial Regex SecretRefPattern();

    [GeneratedRegex(@"\A[\x21-\x7E]{1,4096}\z")]
    private static partial Regex TokenPattern();
}
