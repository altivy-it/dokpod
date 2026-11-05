using System.Runtime.InteropServices;
using Dokpod.Agent.Application.Engines;
using Dokpod.Domain.Images;

namespace Dokpod.Agent.Application.Images;

/// <summary>Uma referência em uso no host e a imagem local que o container usa (mensagem 17, spec 025).</summary>
public sealed record ImageUpdateCheckInput(string Reference, string ImageId);

/// <summary>Resultado de uma referência; <see cref="Reason"/> vazio em <c>up_to_date</c> e <c>update_available</c>.</summary>
public sealed record ImageUpdateCheckOutcome(
    string Reference, string Platform, string State, string? Reason, string? RemoteDigest, string? MatchedLocalDigest);

/// <summary>
/// Compara a tag remota com a imagem local (spec 025 T010, D-002 e D-003):
/// <list type="bullet">
/// <item>digest remoto entre os <c>RepoDigests</c> locais do mesmo repositório → <c>up_to_date</c>;</item>
/// <item>índice remoto mudou, mas o manifesto da plataforma do host não → <c>up_to_date</c>;</item>
/// <item>senão → <c>update_available</c>.</item>
/// </list>
/// Referência só por digest, imagem sem <c>RepoDigests</c> (construída ou importada no host) e registro inseguro são
/// <c>not_checkable</c> sem nenhuma chamada HTTP. Nenhuma falha resulta em <c>up_to_date</c> (SC-002).
/// </summary>
public sealed class ImageUpdateChecker(IContainerEngine engine, IRegistryManifestClient registry, IReadOnlySet<string> insecureRegistries)
{
    public async Task<ImageUpdateCheckOutcome> CheckAsync(ImageUpdateCheckInput input, CancellationToken cancellationToken)
    {
        var local = string.IsNullOrEmpty(input.ImageId) ? null : await engine.InspectImagePlatformAsync(input.ImageId, cancellationToken);
        var platform = local is { Architecture.Length: > 0 } && ImagePlatform.IsValid(ImagePlatform.Of(local.Os, local.Architecture, local.Variant))
            ? ImagePlatform.Of(local.Os, local.Architecture, local.Variant)
            : HostPlatform();
        ImageUpdateCheckOutcome Outcome(string state, string? reason = null, string? remote = null, string? matched = null) =>
            new(input.Reference, platform, state, reason, remote, matched);

        if (!ImageReference.TryParse(input.Reference, out var reference)) return Outcome(ImageUpdateStates.Failed, ImageUpdateReasons.InvalidResponse);
        if (reference.Digest is not null) return Outcome(ImageUpdateStates.NotCheckable, ImageUpdateReasons.DigestReference);
        var localDigests = (local?.RepoDigests ?? [])
            .Select(entry => ImageReference.TryParse(entry, out var parsed) && parsed.Name == reference.Name ? parsed.Digest : null)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        if (localDigests.Count == 0) return Outcome(ImageUpdateStates.NotCheckable, ImageUpdateReasons.LocalImage);
        if (insecureRegistries.Contains(reference.Registry)) return Outcome(ImageUpdateStates.NotCheckable, ImageUpdateReasons.InsecureRegistry);

        var head = await registry.HeadAsync(reference, cancellationToken);
        if (head.FailureReason is not null || head.Digest is null) return Outcome(ImageUpdateStates.Failed, head.FailureReason ?? ImageUpdateReasons.InvalidResponse);
        if (localDigests.Contains(head.Digest)) return Outcome(ImageUpdateStates.UpToDate, remote: head.Digest, matched: head.Digest);
        if (!head.IsIndex) return Outcome(ImageUpdateStates.UpdateAvailable, remote: head.Digest);

        // O índice mudou: só há versão nova se o manifesto da plataforma do host mudou (caso de borda da spec).
        var index = await registry.GetIndexAsync(reference, head.Digest, cancellationToken);
        if (index.FailureReason is not null) return Outcome(ImageUpdateStates.Failed, index.FailureReason);
        var entry = index.Manifests.FirstOrDefault(m => m.Platform == platform);
        if (entry.Digest is null && local?.Variant is null)
        {
            entry = index.Manifests.FirstOrDefault(m => m.Platform.StartsWith(platform + "/", StringComparison.Ordinal));
        }

        return entry.Digest is not null && localDigests.Contains(entry.Digest)
            ? Outcome(ImageUpdateStates.UpToDate, remote: head.Digest, matched: entry.Digest)
            : Outcome(ImageUpdateStates.UpdateAvailable, remote: head.Digest);
    }

    /// <summary>Plataforma do próprio host, quando a imagem local não informa a sua (chave da tabela, data-model.md).</summary>
    public static string HostPlatform() => RuntimeInformation.OSArchitecture switch
    {
        Architecture.Arm64 => "linux/arm64",
        Architecture.Arm => "linux/arm/v7",
        _ => OperatingSystem.IsWindows() ? "windows/amd64" : "linux/amd64",
    };
}
