using Dokpod.ControlPlane.Application.Authorization;

namespace Dokpod.ControlPlane.Application.Realtime;

public enum StreamingAuthorizationState
{
    /// <summary>Autorização confirmada (ou ainda não é hora de reavaliar).</summary>
    Allowed,

    /// <summary>Permissão revogada no Keycloak: encerrar com `authorization_revoked`.</summary>
    Revoked,

    /// <summary>Decisão indisponível: falha fechada com `authorization_unavailable`.</summary>
    Unavailable,

    /// <summary>
    /// Token da conexão perto de expirar: o WebSocket não recebe token novo, então a sessão termina com
    /// `session_refresh_required` e o navegador reabre a conexão (o BFF renova o token na negociação).
    /// </summary>
    RefreshRequired,
}

/// <summary>
/// Reavaliação periódica de autorização de sessões em streaming (logs ao vivo da 011 e terminal da 014,
/// D-008 da 014). A cada intervalo verifica a expiração do token da conexão e refaz a decisão no Keycloak.
/// Uma revogação passa a valer em até um intervalo, ou antes, quando o token expira.
/// </summary>
public sealed class StreamingAuthorizationMonitor(
    TimeProvider timeProvider,
    DateTimeOffset? tokenExpiresAtUtc,
    TimeSpan? interval = null)
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(60);

    /// <summary>Margem antes do `exp` em que o token é tratado como vencido.</summary>
    public static readonly TimeSpan TokenSkew = TimeSpan.FromSeconds(30);

    private readonly TimeSpan period = interval ?? DefaultInterval;
    private DateTimeOffset nextCheckUtc = timeProvider.GetUtcNow() + (interval ?? DefaultInterval);

    public bool IsDue => timeProvider.GetUtcNow() >= nextCheckUtc;

    /// <summary>Reavalia quando devido; fora do intervalo devolve <see cref="StreamingAuthorizationState.Allowed"/> sem consultar.</summary>
    public async Task<StreamingAuthorizationState> CheckAsync(
        Func<CancellationToken, Task<AuthorizationDecisionOutcome>> authorize,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (now < nextCheckUtc) return StreamingAuthorizationState.Allowed;
        nextCheckUtc = now + period;
        if (tokenExpiresAtUtc is { } expires && now >= expires - TokenSkew) return StreamingAuthorizationState.RefreshRequired;

        AuthorizationDecisionOutcome outcome;
        try
        {
            outcome = await authorize(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            outcome = AuthorizationDecisionOutcome.Indeterminate;
        }

        return outcome switch
        {
            AuthorizationDecisionOutcome.Allowed => StreamingAuthorizationState.Allowed,
            AuthorizationDecisionOutcome.Denied => StreamingAuthorizationState.Revoked,
            _ => StreamingAuthorizationState.Unavailable,
        };
    }

    public static string EndReason(StreamingAuthorizationState state) => state switch
    {
        StreamingAuthorizationState.Revoked => "authorization_revoked",
        StreamingAuthorizationState.Unavailable => "authorization_unavailable",
        StreamingAuthorizationState.RefreshRequired => "session_refresh_required",
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };
}
