using Dokpod.Agent.Contracts.V1;

namespace Dokpod.ControlPlane.Application.Images;

/// <summary>
/// Porta neutra do aviso de nova versão (spec 025) para as listas da CE: só a composição Business a registra; sem ela,
/// as respostas não ganham o campo <c>updateCheck</c>. A CE nunca referencia o módulo Business (BusinessGateTests).
/// </summary>
public interface IImageUpdateAnnotations
{
    /// <summary>Resultados dos hosts, ou nulo quando o campo não deve aparecer (sem a capability na licença).</summary>
    Task<IImageUpdateAnnotationSet?> LoadAsync(IEnumerable<Guid> environmentIds, CancellationToken cancellationToken);
}

public interface IImageUpdateAnnotationSet
{
    object? ForImage(Guid environmentId, IEnumerable<string> repoTags, bool stale);

    object? ForContainer(Guid environmentId, string reference, string? imageId, bool stale);
}

/// <summary>Porta neutra do recebimento da mensagem 27; presente só na composição Business, que aceita a capability 23.</summary>
public interface IImageUpdateResultSink
{
    /// <summary>Grava a página; devolve falso quando ela foi descartada ou recusada.</summary>
    Task<bool> ApplyAsync(Guid environmentId, ImageUpdateCheckResultPage page, CancellationToken cancellationToken);
}
