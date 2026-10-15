namespace Dokpod.ControlPlane.Application.Authorization;

public sealed record AuthenticatedActor(string Subject)
{
    /// <summary>Nome de usuário do token (`preferred_username`), para exibir autor (spec 016); a auditoria usa o `sub`.</summary>
    public string? DisplayName { get; init; }

    public string Label => string.IsNullOrWhiteSpace(DisplayName) ? Subject : DisplayName;

    public static AuthenticatedActor FromSubject(string subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new ArgumentException("Authenticated actor subject is required.", nameof(subject));
        }

        return new AuthenticatedActor(subject.Trim());
    }
}

public enum AuthorizationDecisionOutcome
{
    Allowed,
    Denied,
    Indeterminate,
}

public sealed record EnvironmentAuthorizationDecision(
    AuthorizationDecisionOutcome Outcome,
    string? FailureCode)
{
    public bool Allowed => Outcome is AuthorizationDecisionOutcome.Allowed;
}

public interface IEnvironmentAuthorizationDecider
{
    Task<EnvironmentAuthorizationDecision> DecideAsync(
        string resource,
        string scope,
        AuthenticatedActor actor,
        string accessToken,
        Guid correlationId,
        CancellationToken cancellationToken);
}