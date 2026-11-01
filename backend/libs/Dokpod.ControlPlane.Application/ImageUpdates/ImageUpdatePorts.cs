using Dokpod.Domain.Images;

namespace Dokpod.ControlPlane.Application.ImageUpdates;

/// <summary>Limites da verificação de nova versão (spec 025, D-005 e D-006).</summary>
public static class ImageUpdateLimits
{
    public const int MaxItems = 500;

    /// <summary>Cobre o envio do pedido e a marcação da agenda; o resultado chega depois (D-006).</summary>
    public static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);

    public static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    /// <summary>Variação só para menos: o próximo vencimento fica entre 5 h 24 min e 6 h (SC-001).</summary>
    public static readonly TimeSpan MaxJitter = TimeSpan.FromMinutes(36);

    public static readonly TimeSpan ManualCooldown = TimeSpan.FromMinutes(1);

    /// <summary>Páginas de um pedido chegadas depois disso são descartadas (contracts/agent-protocol.md).</summary>
    public static readonly TimeSpan ResultDeadline = TimeSpan.FromMinutes(6);

    /// <summary>Próximo vencimento a partir de agora, com a variação para menos em <paramref name="jitter"/> (0 a 1).</summary>
    public static DateTimeOffset NextDue(DateTimeOffset nowUtc, double jitter) =>
        nowUtc + Interval - TimeSpan.FromTicks((long)(MaxJitter.Ticks * Math.Clamp(jitter, 0, 1)));
}

/// <summary>Um resultado já validado, pronto para gravar (o instante é o do recebimento no plano de controle).</summary>
public sealed record ImageUpdateResult(string Reference, string Platform, string State, string? Reason, string? RemoteDigest, string? LocalImageId);

public enum ImageUpdateManualOutcome
{
    Accepted,
    RateLimited,
}

public sealed record ImageUpdateManualRequest(ImageUpdateManualOutcome Outcome, DateTimeOffset NextDueAtUtc, DateTimeOffset? RetryAtUtc);

public enum ImageUpdateApplyOutcome
{
    Applied,

    /// <summary><c>request_id</c> não é o pendente do host ou chegou depois do prazo; nada gravado.</summary>
    Discarded,
}

/// <summary>Agenda por host e último resultado por referência (data-model.md).</summary>
public interface IImageUpdateStore
{
    /// <summary>Cria a agenda, vencida agora, dos hosts que ainda não têm uma.</summary>
    Task EnsureScheduledAsync(IReadOnlyCollection<Guid> environmentIds, DateTimeOffset nowUtc, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> ListDueAsync(DateTimeOffset nowUtc, int limit, CancellationToken cancellationToken);

    /// <summary>Lease do host vencido; falso se outra réplica o tem ou se ele não está mais vencido.</summary>
    Task<bool> TryAcquireAsync(Guid environmentId, string owner, DateTimeOffset nowUtc, CancellationToken cancellationToken);

    /// <summary>Registra o pedido enviado, marca o próximo vencimento e solta o lease.</summary>
    Task<bool> MarkRequestedAsync(Guid environmentId, string owner, Guid requestId, DateTimeOffset nowUtc, DateTimeOffset nextDueAtUtc, CancellationToken cancellationToken);

    /// <summary>Solta o lease sem pedido (host desconectado): o host continua vencido.</summary>
    Task ReleaseAsync(Guid environmentId, string owner, CancellationToken cancellationToken);

    /// <summary>
    /// Adianta o vencimento para agora. Manual ("Verificar agora"): no máximo 1 por <see cref="ImageUpdateLimits.ManualCooldown"/>;
    /// automático (depois de um pull): sem limite.
    /// </summary>
    Task<ImageUpdateManualRequest> RequestNowAsync(Guid environmentId, DateTimeOffset nowUtc, bool manual, CancellationToken cancellationToken);

    /// <summary>
    /// Grava uma página do pedido pendente. Na última página, as referências do host não renovadas por este pedido saem.
    /// Resultados decididos no plano de controle (fora da política, agente sem a capability) usam <see cref="UpsertLocalAsync"/>.
    /// </summary>
    Task<ImageUpdateApplyOutcome> ApplyPageAsync(
        Guid environmentId, Guid requestId, IReadOnlyList<ImageUpdateResult> results, bool isLastPage, DateTimeOffset nowUtc, CancellationToken cancellationToken);

    Task UpsertLocalAsync(Guid environmentId, IReadOnlyList<ImageUpdateResult> results, DateTimeOffset nowUtc, CancellationToken cancellationToken);

    Task<IReadOnlyList<ImageUpdateCheck>> ListAsync(IReadOnlyCollection<Guid> environmentIds, CancellationToken cancellationToken);

    Task<ImageUpdateScheduleState?> GetScheduleAsync(Guid environmentId, CancellationToken cancellationToken);
}

public sealed record ImageUpdateScheduleState(
    Guid EnvironmentId,
    DateTimeOffset NextDueAtUtc,
    DateTimeOffset? LastRequestedAtUtc,
    DateTimeOffset? LastManualAtUtc,
    Guid? PendingRequestId,
    string? LeaseOwner,
    DateTimeOffset? LeaseUntilUtc);
