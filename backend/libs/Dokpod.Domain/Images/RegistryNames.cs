using System.Text.RegularExpressions;

namespace Dokpod.Domain.Images;

/// <summary>Endereço de registro (host e porta opcional) na gramática e na normalização da spec 012 (spec 020).</summary>
public static partial class RegistryNames
{
    public const int MaxLength = 255;

    /// <summary>Forma canônica: minúscula, sem esquema, caminho ou barra final; aliases do Docker Hub viram `docker.io`.</summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Trim();
        var scheme = text.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0) text = text[(scheme + 3)..];
        var slash = text.IndexOf('/');
        if (slash >= 0) text = text[..slash];
        text = text.ToLowerInvariant();
        if (text is "index.docker.io" or "registry-1.docker.io") text = ImageReference.DefaultRegistry;
        return IsValid(text) ? text : null;
    }

    /// <summary>Registro já canônico: só host e porta, sem esquema, caminho, credencial ou maiúsculas.</summary>
    public static bool IsValid(string? registry) =>
        registry is { Length: > 0 and <= MaxLength } && registry == registry.ToLowerInvariant() && Pattern().IsMatch(registry);

    /// <summary>Registro de origem da imagem pela primeira tag e, sem tag, pelo primeiro digest.</summary>
    public static string? OfImage(IEnumerable<string> repoTags, IEnumerable<string> repoDigests)
    {
        foreach (var name in repoTags.Concat(repoDigests))
        {
            if (ImageReference.TryParse(name, out var reference)) return reference.Registry;
        }

        return null;
    }

    [GeneratedRegex(@"^(?:[a-z0-9]|[a-z0-9][a-z0-9-]*[a-z0-9])(?:\.(?:[a-z0-9]|[a-z0-9][a-z0-9-]*[a-z0-9]))*(?::[0-9]{1,5})?$")]
    private static partial Regex Pattern();
}
