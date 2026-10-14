using System.Text.RegularExpressions;

namespace Dokpod.Domain.Images;

/// <summary>
/// Referência de imagem OCI validada pela gramática da distribution (spec 012 T004, D-009):
/// <c>[domínio[:porta]/]caminho[:tag][@algoritmo:hex]</c>. Rejeita credenciais, query, fragmento, espaços e
/// maiúsculas no caminho. Normaliza para a forma canônica (<c>nginx</c> → <c>docker.io/library/nginx:latest</c>).
/// </summary>
public sealed partial record ImageReference(string Registry, string Repository, string? Tag, string? Digest)
{
    public const int MaxLength = 255;
    public const string DefaultRegistry = "docker.io";

    [GeneratedRegex(@"^(?:[a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9-]*[a-zA-Z0-9])(?:\.(?:[a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9-]*[a-zA-Z0-9]))*(?::[0-9]{1,5})?$")]
    private static partial Regex DomainPattern();

    [GeneratedRegex(@"^[a-z0-9]+(?:(?:\.|_|__|-+)[a-z0-9]+)*$")]
    private static partial Regex PathComponentPattern();

    [GeneratedRegex(@"^[\w][\w.-]{0,127}$")]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"^sha256:[a-f0-9]{64}$|^sha512:[a-f0-9]{128}$")]
    private static partial Regex DigestPattern();

    /// <summary>Tag sem digest: o conteúdo pode mudar entre pulls.</summary>
    public bool IsMutableTag => Digest is null;

    /// <summary>Forma canônica usada no hash do comando, na comparação com tags e no engine.</summary>
    public string Canonical => $"{Registry}/{Repository}{(Tag is null ? string.Empty : ":" + Tag)}{(Digest is null ? string.Empty : "@" + Digest)}";

    /// <summary>Nome sem tag/digest (`fromImage` do engine).</summary>
    public string Name => $"{Registry}/{Repository}";

    public static bool TryParse(string? value, out ImageReference reference)
    {
        reference = null!;
        if (string.IsNullOrEmpty(value) || value.Length > MaxLength) return false;
        if (value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)) || value.IndexOfAny(['?', '#', '\\']) >= 0) return false;

        var remainder = value;
        string? digest = null;
        var at = remainder.IndexOf('@');
        if (at >= 0)
        {
            digest = remainder[(at + 1)..];
            remainder = remainder[..at];
            if (!DigestPattern().IsMatch(digest)) return false; // também recusa `usuario:senha@host`
        }

        string? tag = null;
        var lastSlash = remainder.LastIndexOf('/');
        var colon = remainder.LastIndexOf(':');
        if (colon > lastSlash)
        {
            tag = remainder[(colon + 1)..];
            remainder = remainder[..colon];
            if (!TagPattern().IsMatch(tag)) return false;
        }

        if (remainder.Length == 0) return false;
        var parts = remainder.Split('/');
        string registry;
        IEnumerable<string> path;
        var first = parts[0];
        if (parts.Length > 1 && (first.Contains('.') || first.Contains(':') || first == "localhost"))
        {
            if (!DomainPattern().IsMatch(first)) return false;
            registry = first.ToLowerInvariant();
            path = parts.Skip(1);
        }
        else
        {
            registry = DefaultRegistry;
            path = parts;
        }

        var components = path.ToArray();
        if (components.Length == 0 || components.Any(c => !PathComponentPattern().IsMatch(c))) return false;
        if (registry is DefaultRegistry or "index.docker.io" or "registry-1.docker.io")
        {
            registry = DefaultRegistry;
            if (components.Length == 1) components = ["library", components[0]];
        }

        if (tag is null && digest is null) tag = "latest";
        reference = new ImageReference(registry, string.Join('/', components), tag, digest);
        return reference.Canonical.Length <= MaxLength;
    }

    /// <summary>Verdadeiro quando a tag/digest informados pelo engine apontam para esta referência.</summary>
    public bool Matches(IEnumerable<string> repoTags, IEnumerable<string> repoDigests)
    {
        if (Digest is not null)
        {
            return repoDigests.Any(d => TryParse(d, out var parsed) && parsed.Name == Name && parsed.Digest == Digest);
        }

        return repoTags.Any(t => TryParse(t, out var parsed) && parsed.Canonical == Canonical);
    }

    public override string ToString() => Canonical;
}
