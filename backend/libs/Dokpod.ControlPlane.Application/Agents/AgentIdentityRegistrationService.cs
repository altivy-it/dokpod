using System.Text.RegularExpressions;
using Dokpod.ControlPlane.Application.Auditing;
using Dokpod.ControlPlane.Application.Environments;
using Dokpod.Domain.Auditing;
using Dokpod.Domain.Environments;

namespace Dokpod.ControlPlane.Application.Agents;

/// <summary>Gravação de identidade de agente fora do console (spec 033, D-004); só o modo administrativo a usa.</summary>
public interface IAgentIdentityEnrollment
{
    Task<AgentIdentityEnrollmentOutcome> RegisterAsync(Guid environmentId, string certificateFingerprint, CancellationToken cancellationToken);
}

public enum AgentIdentityEnrollmentOutcome
{
    Created,
    Unchanged,
    BoundToAnotherEnvironment,
    Revoked,
}

/// <summary>Quem executou o instalador, registrado no evento (ator técnico <c>installer</c>), e a justificativa informada.</summary>
public sealed record InstallerOperator(string Name, string Reason);

public sealed record InstallerCommandResult(bool Changed, string? FailureCode)
{
    public bool Succeeded => FailureCode is null;
}

/// <summary>
/// Spec 033 T013 (FR-024, FR-025, D-004): cadastro de ambiente, registro e revogação da identidade do agente pelo modo
/// administrativo da API, sem decisão do Keycloak (não há usuário) e sem SQL manual. Cada mudança é auditada com o ator
/// técnico <c>installer</c>, o nome do operador e a justificativa. Repetir com os mesmos dados não muda nada nem audita;
/// dados divergentes são recusados e auditados como falha.
/// </summary>
public sealed partial class AgentIdentityRegistrationService(
    IEnvironmentRegistrationStore environments,
    IAgentIdentityEnrollment enrollment,
    IAgentIdentityRegistry identities,
    IAuditEventWriter audit,
    TimeProvider? timeProvider = null)
{
    public const string ActorId = "installer";
    public const string RegisterAction = "environment.agent_identity_register";

    private TimeProvider Clock => timeProvider ?? TimeProvider.System;

    public async Task<InstallerCommandResult> RegisterEnvironmentAsync(
        EnvironmentRegistration registration,
        InstallerOperator actor,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(actor);
        var existing = await environments.GetAsync(registration.EnvironmentId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            if (SameEnvironment(existing, registration))
            {
                return new InstallerCommandResult(false, null);
            }

            await AppendAsync("environment.register", registration.EnvironmentId, registration.EnvironmentId.ToString("D"), AuditOutcome.Failed,
                "environment_conflict", actor, correlationId, cancellationToken).ConfigureAwait(false);
            return new InstallerCommandResult(false, "environment_conflict");
        }

        var created = await environments.CreateAsync(registration, cancellationToken).ConfigureAwait(false);
        if (created is not EnvironmentRegistrationStoreResult.Created)
        {
            return new InstallerCommandResult(false, "environment_conflict");
        }

        await AppendAsync("environment.register", registration.EnvironmentId, registration.EnvironmentId.ToString("D"), AuditOutcome.Succeeded,
            null, actor, correlationId, cancellationToken).ConfigureAwait(false);
        return new InstallerCommandResult(true, null);
    }

    public async Task<InstallerCommandResult> RegisterIdentityAsync(
        Guid environmentId,
        string certificateFingerprint,
        InstallerOperator actor,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var fingerprint = NormalizeFingerprint(certificateFingerprint);
        if (await environments.GetAsync(environmentId, cancellationToken).ConfigureAwait(false) is null)
        {
            return new InstallerCommandResult(false, "environment_not_found");
        }

        var outcome = await enrollment.RegisterAsync(environmentId, fingerprint, cancellationToken).ConfigureAwait(false);
        var failure = outcome switch
        {
            AgentIdentityEnrollmentOutcome.Created or AgentIdentityEnrollmentOutcome.Unchanged => null,
            AgentIdentityEnrollmentOutcome.BoundToAnotherEnvironment => "agent_identity_bound_to_another_environment",
            AgentIdentityEnrollmentOutcome.Revoked => "agent_identity_revoked",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        };
        if (outcome is AgentIdentityEnrollmentOutcome.Unchanged)
        {
            return new InstallerCommandResult(false, null);
        }

        await AppendAsync(RegisterAction, environmentId, fingerprint, failure is null ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            failure, actor, correlationId, cancellationToken).ConfigureAwait(false);
        return new InstallerCommandResult(failure is null, failure);
    }

    /// <summary>Mesmo efeito e mesma ação de auditoria (<c>environment.revoke</c>) do endpoint de revogação.</summary>
    public async Task<InstallerCommandResult> RevokeIdentityAsync(
        Guid environmentId,
        InstallerOperator actor,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (environmentId == Guid.Empty)
        {
            throw new ArgumentException("Environment ID is required.", nameof(environmentId));
        }

        var revoked = await identities.RevokeEnvironmentAsync(environmentId, Clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        if (!revoked)
        {
            return new InstallerCommandResult(false, null);
        }

        await AppendAsync("environment.revoke", environmentId, environmentId.ToString("D"), AuditOutcome.Succeeded, null, actor, correlationId,
            cancellationToken).ConfigureAwait(false);
        return new InstallerCommandResult(true, null);
    }

    /// <summary>Fingerprint SHA-256 do certificado de cliente, em hexadecimal, como o canal mTLS compara.</summary>
    public static string NormalizeFingerprint(string certificateFingerprint)
    {
        var normalized = (certificateFingerprint ?? string.Empty).Trim().Replace(":", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        return Sha256Hex().IsMatch(normalized)
            ? normalized
            : throw new ArgumentException("O fingerprint precisa ser o SHA-256 do certificado, em hexadecimal.", nameof(certificateFingerprint));
    }

    private static bool SameEnvironment(EnvironmentRegistration existing, EnvironmentRegistration requested) =>
        existing.Name == requested.Name
        && existing.Host == requested.Host
        && existing.Stage == requested.Stage
        && existing.Scopes.SetEquals(requested.Scopes);

    private Task AppendAsync(
        string action,
        Guid environmentId,
        string subject,
        AuditOutcome outcome,
        string? failureCode,
        InstallerOperator actor,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var details = new Dictionary<string, string>(StringComparer.Ordinal);
        var reason = Terminal.JustificationMasker.Mask(actor.Reason.Trim());
        if (!string.IsNullOrWhiteSpace(reason))
        {
            details["justification"] = reason.Length > AuditEvent.MaxJustificationDetailLength ? reason[..AuditEvent.MaxJustificationDetailLength] : reason;
        }

        var auditEvent = AuditEvent.Create(Guid.NewGuid(), Clock.GetUtcNow(), correlationId, AuditActorKind.System, ActorId, action, environmentId,
                outcome, failureCode)
            .WithSubject(subject, details.Count == 0 ? null : details)
            .WithActorName(actor.Name);
        return audit.AppendAsync(auditEvent, cancellationToken);
    }

    [GeneratedRegex("^[0-9A-F]{64}$")]
    private static partial Regex Sha256Hex();
}
