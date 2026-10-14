using System.Security.Claims;

namespace Dokpod.Bff.Endpoints;

public sealed record BffSessionResponse(bool Authenticated, string? Subject, string? Name, string? DisplayGroup = null)
{
    private const int MaxDisplayGroupLength = 80;

    /// <param name="displayGroupClaim">Claim configurada para o grupo de exibição; nunca expõe a lista completa de grupos.</param>
    public static BffSessionResponse FromPrincipal(ClaimsPrincipal principal, string? displayGroupClaim = null)
    {
        var authenticated = principal.Identity?.IsAuthenticated == true;
        if (!authenticated)
        {
            return new(false, null, null);
        }

        var group = string.IsNullOrWhiteSpace(displayGroupClaim) ? null : principal.FindFirstValue(displayGroupClaim)?.Trim();
        if (string.IsNullOrEmpty(group))
        {
            group = null;
        }
        else if (group.Length > MaxDisplayGroupLength)
        {
            group = group[..MaxDisplayGroupLength];
        }

        return new(true, principal.FindFirstValue("sub"), principal.Identity?.Name, group);
    }
}
