using System.Security.Claims;
using Dokpod.ControlPlane.Application.Auditing;

namespace Dokpod.ControlPlane.Api.Auditing;

/// <summary>Nome do usuário da requisição (HTTP ou hub) para eventos de auditoria em que ele é o ator (spec 017 D-001).</summary>
public sealed class HttpAuditActorNameProvider(IHttpContextAccessor accessor) : IAuditActorNameProvider
{
    public string? NameFor(string actorId)
    {
        var user = accessor.HttpContext?.User;
        return user is not null && string.Equals(user.FindFirstValue("sub"), actorId, StringComparison.Ordinal)
            ? user.FindFirstValue("preferred_username")
            : null;
    }
}
