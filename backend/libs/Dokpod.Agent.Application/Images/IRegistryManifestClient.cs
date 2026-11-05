using Dokpod.Domain.Images;

namespace Dokpod.Agent.Application.Images;

/// <summary>Resultado do <c>HEAD</c> do manifesto de uma tag: digest remoto e tipo, ou o motivo da falha (spec 025 D-005).</summary>
public sealed record ManifestHead(string? Digest, string? MediaType, string? FailureReason)
{
    public bool IsIndex => MediaType is ManifestMediaTypes.OciIndex or ManifestMediaTypes.DockerList;

    public static ManifestHead Failed(string reason) => new(null, null, reason);
}

/// <summary>Manifestos de um índice por plataforma (<c>os/arquitetura[/variante]</c>), ou o motivo da falha.</summary>
public sealed record ManifestIndex(IReadOnlyList<(string Platform, string Digest)> Manifests, string? FailureReason)
{
    public static ManifestIndex Failed(string reason) => new([], reason);
}

public static class ManifestMediaTypes
{
    public const string OciIndex = "application/vnd.oci.image.index.v1+json";
    public const string DockerList = "application/vnd.docker.distribution.manifest.list.v2+json";
    public const string OciManifest = "application/vnd.oci.image.manifest.v1+json";
    public const string DockerManifest = "application/vnd.docker.distribution.manifest.v2+json";

    public static readonly IReadOnlyList<string> All = [OciIndex, DockerList, OciManifest, DockerManifest];
}

/// <summary>
/// Consulta de manifestos pela API OCI Distribution, direto do agente (spec 025 D-001). Uma instância por pedido: o
/// token de cada repositório vive só durante o pedido (threat model L3). Nunca faz requisição sem TLS.
/// </summary>
public interface IRegistryManifestClient
{
    Task<ManifestHead> HeadAsync(ImageReference reference, CancellationToken cancellationToken);

    /// <summary><c>GET</c> do índice pelo digest; só chamado quando o índice remoto mudou (D-002).</summary>
    Task<ManifestIndex> GetIndexAsync(ImageReference reference, string digest, CancellationToken cancellationToken);
}
