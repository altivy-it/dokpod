using System.Collections.Concurrent;
using System.Security.Claims;
using Dokpod.ControlPlane.Application.Auditing;

namespace Dokpod.ControlPlane.Api.Auditing;

/// <summary>
/// Últimos nomes vistos por identificador (`sub` → `preferred_username`), para eventos gravados fora de uma
/// requisição do usuário, como o fim de uma sessão de terminal. Limitado; só serve à exibição do ator.
/// </summary>
public sealed class AuditActorNameCache
{
    public const int MaxEntries = 10_000;
    private readonly ConcurrentDictionary<string, string> names = new(StringComparer.Ordinal);

    public void Remember(ClaimsPrincipal? user)
    {
        var subject = user?.FindFirstValue("sub");
        var name = user?.FindFirstValue("preferred_username");
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(name)) return;
        if (names.Count >= MaxEntries && !names.ContainsKey(subject)) names.Clear();
        names[subject] = name;
    }

    public string? Find(string actorId) => names.GetValueOrDefault(actorId);
}

/// <summary>Nome do usuário da requisição (HTTP ou hub) ou o último visto para o identificador (spec 017 D-001).</summary>
public sealed class HttpAuditActorNameProvider(IHttpContextAccessor accessor, AuditActorNameCache cache) : IAuditActorNameProvider
{
    public string? NameFor(string actorId)
    {
        var user = accessor.HttpContext?.User;
        if (user is not null && string.Equals(user.FindFirstValue("sub"), actorId, StringComparison.Ordinal))
        {
            var name = user.FindFirstValue("preferred_username");
            if (!string.IsNullOrWhiteSpace(name)) return name;
        }

        return cache.Find(actorId);
    }
}
