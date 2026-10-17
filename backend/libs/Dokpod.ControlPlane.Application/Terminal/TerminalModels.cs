using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Dokpod.Agent.Contracts.V1;
using Dokpod.ControlPlane.Application.Authorization;

namespace Dokpod.ControlPlane.Application.Terminal;

/// <summary>Política do terminal (spec 014, `Terminal:*`); valores são padrões configuráveis.</summary>
public sealed class TerminalOptions
{
    public const string SectionName = "Terminal";

    public TimeSpan IdleTimeout { get; init; } = TimeSpan.FromMinutes(10);
    public TimeSpan MaxDuration { get; init; } = TimeSpan.FromMinutes(30);
    public int MaxPerUser { get; init; } = 2;
    public int MaxPerUserPerContainer { get; init; } = 1;
    public int MaxPerAgent { get; init; } = 4;
    public int MaxOutputBytesPerSecond { get; init; } = 256 * 1024;
    public string[] RequireJustificationStages { get; init; } = ["production"];
    public bool RequireRecentMfa { get; init; }
    public TimeSpan RecentMfaMaxAge { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan AuthorizationRecheckInterval { get; init; } = TimeSpan.FromSeconds(60);
    public TimeSpan WarningBefore { get; init; } = TimeSpan.FromMinutes(1);
    public TimeSpan AgentAttachTimeout { get; init; } = TimeSpan.FromSeconds(10);

    public bool IsValid() =>
        IdleTimeout >= TimeSpan.FromMinutes(1) && MaxDuration >= IdleTimeout && MaxDuration <= TimeSpan.FromHours(8) &&
        MaxPerUser is >= 1 and <= 20 && MaxPerUserPerContainer is >= 1 and <= 5 && MaxPerAgent is >= 1 and <= 20 &&
        MaxOutputBytesPerSecond is >= 4096 and <= 4 * 1024 * 1024 && AuthorizationRecheckInterval >= TimeSpan.FromSeconds(10);
}

public sealed record TerminalOpenRequest(Guid EnvironmentId, string ContainerId, int Cols, int Rows, string? Justification);

/// <summary>Entrada do navegador: dados (texto já decodificado) ou redimensionamento.</summary>
public sealed record TerminalInputFrame(byte[]? Data, int? Cols, int? Rows);

public sealed record TerminalStatus(string State, string? Reason = null, string? Shell = null, bool ContainerUserIsRoot = false, string? Warning = null, Guid? SessionId = null);

/// <summary>Saída para o navegador: bytes do TTY ou mudança de estado (conectando, conectado, aviso, encerrado).</summary>
public sealed record TerminalOutputFrame(byte[]? Data, TerminalStatus? Status);

/// <summary>Sessão em memória da API (réplica única, data-model da 014).</summary>
public sealed class TerminalSession(Guid sessionId, AuthenticatedActor actor, Guid environmentId, string containerId, DateTimeOffset startedAt)
{
    public Guid SessionId { get; } = sessionId;
    public AuthenticatedActor Actor { get; } = actor;
    public Guid EnvironmentId { get; } = environmentId;
    public string ContainerId { get; } = containerId;
    public DateTimeOffset StartedAt { get; } = startedAt;
    public Channel<ExecFrame> ToAgent { get; } = Channel.CreateBounded<ExecFrame>(new BoundedChannelOptions(256) { FullMode = BoundedChannelFullMode.Wait });
    public Channel<ExecFrame> FromAgent { get; } = Channel.CreateBounded<ExecFrame>(new BoundedChannelOptions(256) { FullMode = BoundedChannelFullMode.Wait });
    public TaskCompletionSource Attached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public DateTimeOffset? AgentAttachedAt { get; set; }

    private readonly Lock gate = new();
    private string accessToken = string.Empty;
    private DateTimeOffset? tokenExpiresAt;

    /// <summary>Token mais recente (renovado pelo BFF via `renew`), usado na reavaliação periódica (D-008).</summary>
    public (string Token, DateTimeOffset? ExpiresAt) Authorization
    {
        get
        {
            lock (gate) return (accessToken, tokenExpiresAt);
        }
    }

    public void SetAuthorization(string token, DateTimeOffset? expiresAt)
    {
        lock (gate)
        {
            accessToken = token;
            tokenExpiresAt = expiresAt;
        }
    }
}

/// <summary>Sessões de terminal ativas e limites por usuário, por container e por agente (US3).</summary>
public sealed class TerminalSessionRegistry(TerminalOptions options)
{
    private readonly ConcurrentDictionary<Guid, TerminalSession> sessions = new();
    private readonly Lock reserveGate = new();

    public int Count => sessions.Count;

    public string? TryReserve(TerminalSession session)
    {
        lock (reserveGate)
        {
            var mine = sessions.Values.Where(s => s.Actor.Subject == session.Actor.Subject).ToArray();
            if (mine.Length >= options.MaxPerUser) return "limit";
            if (mine.Count(s => s.EnvironmentId == session.EnvironmentId && s.ContainerId == session.ContainerId) >= options.MaxPerUserPerContainer) return "limit";
            if (sessions.Values.Count(s => s.EnvironmentId == session.EnvironmentId) >= options.MaxPerAgent) return "limit";
            sessions[session.SessionId] = session;
            return null;
        }
    }

    public void Release(Guid sessionId) => sessions.TryRemove(sessionId, out _);

    public TerminalSession? Find(Guid sessionId) => sessions.GetValueOrDefault(sessionId);

    /// <summary>Vincula o RPC `Exec` do agente à sessão pendente (uma única vez).</summary>
    public TerminalSession? Attach(Guid execSessionId, Guid environmentId)
    {
        if (!sessions.TryGetValue(execSessionId, out var session) || session.EnvironmentId != environmentId || session.AgentAttachedAt is not null) return null;
        lock (reserveGate)
        {
            if (session.AgentAttachedAt is not null) return null;
            session.AgentAttachedAt = DateTimeOffset.UtcNow;
        }

        session.Attached.TrySetResult();
        return session;
    }
}

/// <summary>
/// Mascaramento da justificativa antes da auditoria (FR-004) e do comando de saúde no comando equivalente (spec 023): mesmos
/// padrões do `SecretMasker` do agente.
/// </summary>
public static partial class JustificationMasker
{
    [GeneratedRegex(@"(?i)\b(bearer)\s+[A-Za-z0-9._~+/=-]{8,}")]
    private static partial Regex Bearer();

    [GeneratedRegex(@"(?i)\b(password|passwd|pwd|secret|token|api[_-]?key|access[_-]?key)\s*[=:]\s*\S+")]
    private static partial Regex KeyValue();

    [GeneratedRegex(@"(?i)([a-z][a-z0-9+.-]*://)[^\s:/@]+:[^\s@/]+@")]
    private static partial Regex UrlCredential();

    [GeneratedRegex(@"\beyJ[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]{5,}\b")]
    private static partial Regex Jwt();

    public static string Mask(string text)
    {
        var masked = Bearer().Replace(text, "$1 ***");
        masked = KeyValue().Replace(masked, m => m.Value[..(m.Value.IndexOfAny(['=', ':']) + 1)] + "***");
        masked = UrlCredential().Replace(masked, "$1***@");
        masked = Jwt().Replace(masked, "***");
        return new string(masked.Where(c => !char.IsControl(c)).ToArray());
    }
}
