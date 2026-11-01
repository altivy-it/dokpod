using System.Text.RegularExpressions;

namespace Dokpod.Domain.Images;

/// <summary>Estados da verificação de nova versão (spec 025, data-model.md); <c>container_behind</c> é só derivado.</summary>
public static class ImageUpdateStates
{
    public const string UpToDate = "up_to_date";
    public const string UpdateAvailable = "update_available";
    public const string NotCheckable = "not_checkable";
    public const string Failed = "failed";

    /// <summary>A referência está em dia, mas o container ainda usa a imagem anterior (D-008).</summary>
    public const string ContainerBehind = "container_behind";

    public static readonly IReadOnlySet<string> Stored = new HashSet<string>(StringComparer.Ordinal) { UpToDate, UpdateAvailable, NotCheckable, Failed };
}

/// <summary>Motivos de <c>not_checkable</c> e <c>failed</c>; nunca carregam mensagem do registro.</summary>
public static class ImageUpdateReasons
{
    public const string DigestReference = "digest_reference";
    public const string LocalImage = "local_image";
    public const string RegistryNotAllowed = "registry_not_allowed";
    public const string InsecureRegistry = "insecure_registry";
    public const string AgentUnsupported = "agent_unsupported";

    public const string RegistryUnavailable = "registry_unavailable";
    public const string RateLimited = "rate_limited";
    public const string Unauthorized = "unauthorized";
    public const string ReferenceNotFound = "reference_not_found";
    public const string Timeout = "timeout";
    public const string InvalidResponse = "invalid_response";

    public static readonly IReadOnlySet<string> NotCheckable = new HashSet<string>(StringComparer.Ordinal)
    {
        DigestReference, LocalImage, RegistryNotAllowed, InsecureRegistry, AgentUnsupported,
    };

    public static readonly IReadOnlySet<string> Failed = new HashSet<string>(StringComparer.Ordinal)
    {
        RegistryUnavailable, RateLimited, Unauthorized, ReferenceNotFound, Timeout, InvalidResponse,
    };

    /// <summary>Motivo válido para o estado: nulo em <c>up_to_date</c>/<c>update_available</c>, da lista nos demais.</summary>
    public static bool IsValidFor(string state, string? reason) => state switch
    {
        ImageUpdateStates.UpToDate or ImageUpdateStates.UpdateAvailable => reason is null,
        ImageUpdateStates.NotCheckable => reason is not null && NotCheckable.Contains(reason),
        ImageUpdateStates.Failed => reason is not null && Failed.Contains(reason),
        _ => false,
    };
}

/// <summary>Plataforma da imagem local, <c>os/arquitetura[/variante]</c>, até 64 caracteres.</summary>
public static partial class ImagePlatform
{
    public const int MaxLength = 64;

    [GeneratedRegex(@"^[a-z0-9]+/[a-z0-9_]+(?:/[a-z0-9]+)?$")]
    private static partial Regex Pattern();

    public static bool IsValid(string? platform) => platform is { Length: > 0 and <= MaxLength } && Pattern().IsMatch(platform);

    public static string Of(string os, string architecture, string? variant = null) =>
        string.IsNullOrEmpty(variant) ? $"{os}/{architecture}" : $"{os}/{architecture}/{variant}";
}

/// <summary>Digest de conteúdo aceito do agente: <c>sha256:</c> e 64 hexadecimais minúsculos.</summary>
public static partial class ContentDigest
{
    [GeneratedRegex(@"^sha256:[a-f0-9]{64}$")]
    private static partial Regex Pattern();

    public static bool IsValid(string? digest) => digest is not null && Pattern().IsMatch(digest);
}

/// <summary>Último resultado da verificação de uma referência num host (uma linha de <c>image_update_checks</c>).</summary>
public sealed record ImageUpdateCheck(
    Guid EnvironmentId,
    string Reference,
    string Platform,
    string? LocalImageId,
    string State,
    string? Reason,
    string? RemoteDigest,
    DateTimeOffset CheckedAtUtc,
    DateTimeOffset? LastSuccessAtUtc,
    DateTimeOffset? OutdatedSinceUtc)
{
    /// <summary>
    /// Estado do container que usa a referência (D-008): em dia, mas com outra imagem local, é
    /// <c>container_behind</c>; nos demais casos, o da referência.
    /// </summary>
    public string StateForContainer(string? containerImageId) =>
        State == ImageUpdateStates.UpToDate && LocalImageId is not null && !string.Equals(containerImageId, LocalImageId, StringComparison.Ordinal)
            ? ImageUpdateStates.ContainerBehind
            : State;
}
