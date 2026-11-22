using System.Runtime.CompilerServices;
using Dokpod.ControlPlane.Application.Auditing;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Commands;
using Dokpod.Domain.Alerts;
using Dokpod.Domain.Auditing;
using Dokpod.Domain.Commands;
using Dokpod.Domain.Environments;

namespace Dokpod.ControlPlane.Application.ChangeWindows;

public enum ChangeWindowClearanceMode
{
    /// <summary>Host sem estágio ou estágio sem janela.</summary>
    NoWindow,
    Open,
    /// <summary>Fora da janela, com <c>change:override</c> e justificativa; vale só para esta ação.</summary>
    Override,
    /// <summary>O <c>commandId</c> já existe: a fila só devolve duplicado ou conflito, nunca cria (D-001).</summary>
    Replay,
}

/// <summary>Pedido de exceção de emergência que acompanha a ação (spec 026 D-005).</summary>
public sealed record ChangeWindowOverrideRequest(string? Justification);

/// <summary>Resultado do guard: a liberação, ou a recusa com o código do contrato e a próxima abertura.</summary>
public sealed record ChangeWindowDecision(
    ChangeWindowGuard.Clearance? Clearance,
    string? FailureCode,
    DateTimeOffset? NextWindowStartsAt,
    string? TimeZone)
{
    public bool Allowed => FailureCode is null;
}

/// <summary>
/// Ponto único da janela de mudança (spec 026 D-001, CE). Toda ação que enfileira comando precisa de uma
/// <see cref="Clearance"/>, que só este guard emite; o terminal, que não usa a fila, chama
/// <see cref="EvaluateTerminalAsync"/>. A ordem do serviço é autorização da ação, ambiente, janela e justificativa da
/// 016: a busca do <c>commandId</c> fica aqui dentro, depois das duas primeiras (threat model L6).
/// </summary>
public sealed class ChangeWindowGuard(
    IChangeWindowSchedule schedule,
    IAgentCommandStore commands,
    IEnvironmentAuthorizationDecider authorizationDecider,
    IAuditEventWriter auditWriter,
    TimeProvider timeProvider)
{
    public const string WindowClosed = "change_window_closed";
    public const string JustificationRequired = "justification_required";
    public const string OverrideDenied = "change_override_denied";
    public const string AuthorizationUnavailable = "authorization_unavailable";

    /// <summary>Folga do prazo do comando depois do fechamento da janela (threat model L1).</summary>
    public static readonly TimeSpan DeadlineGrace = TimeSpan.FromMinutes(5);

    private static Func<Guid, AgentCommandKind, ChangeWindowClearanceMode, DateTimeOffset?, AuditEvent?, AgentCommandStatusSnapshot?, Clearance>? issue;

    /// <summary>
    /// Liberação para enfileirar um comando. O construtor é privado e só este guard a emite: um construtor
    /// <c>internal</c> não bastaria, porque a fila e os serviços estão no mesmo assembly.
    /// </summary>
    public sealed class Clearance
    {
        static Clearance() =>
            issue = (environmentId, kind, mode, maxDeadline, overrideEvent, existing) =>
                new Clearance(environmentId, kind, mode, maxDeadline, overrideEvent, existing);

        private Clearance(
            Guid environmentId,
            AgentCommandKind kind,
            ChangeWindowClearanceMode mode,
            DateTimeOffset? maxDeadlineUtc,
            AuditEvent? overrideAuditEvent,
            AgentCommandStatusSnapshot? existingCommand)
        {
            EnvironmentId = environmentId;
            Kind = kind;
            Mode = mode;
            MaxDeadlineUtc = maxDeadlineUtc;
            OverrideAuditEvent = overrideAuditEvent;
            ExistingCommand = existingCommand;
        }

        public Guid EnvironmentId { get; }

        public AgentCommandKind Kind { get; }

        public ChangeWindowClearanceMode Mode { get; }

        /// <summary>Com a janela aberta, o fechamento mais a folga; o serviço encurta o prazo pedido para ele.</summary>
        public DateTimeOffset? MaxDeadlineUtc { get; }

        /// <summary>O <c>change_window.override</c>, gravado na mesma transação do comando.</summary>
        public AuditEvent? OverrideAuditEvent { get; }

        /// <summary>O comando já aceito, no modo <see cref="ChangeWindowClearanceMode.Replay"/>.</summary>
        public AgentCommandStatusSnapshot? ExistingCommand { get; }

        public DateTimeOffset LimitDeadline(DateTimeOffset requestedUtc) =>
            MaxDeadlineUtc is { } limit && requestedUtc > limit ? limit : requestedUtc;
    }

    /// <summary>
    /// Decide uma ação que enfileira comando. <paramref name="auditAction"/> e <paramref name="subjectId"/> descrevem a
    /// ação nos eventos <c>change_window.*</c>.
    /// </summary>
    public async Task<ChangeWindowDecision> EvaluateCommandAsync(
        EnvironmentRegistration registration,
        AgentCommandKind kind,
        Guid commandId,
        string auditAction,
        string? subjectId,
        AuthenticatedActor actor,
        string accessToken,
        Guid correlationId,
        ChangeWindowOverrideRequest? overrideRequest,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(actor);
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (commandId == Guid.Empty) throw new ArgumentException("Command ID is required.", nameof(commandId));

        var window = registration.Stage is { } stage
            ? await schedule.GetAsync(stage, cancellationToken).ConfigureAwait(false)
            : null;
        if (window is null) return Allow(registration.EnvironmentId, kind, ChangeWindowClearanceMode.NoWindow, null, null, null);

        // Repetição idempotente (princípio IV): o comando aceito recebe o mesmo resultado depois que a janela fecha.
        var existing = await commands.GetAsync(registration.EnvironmentId, commandId, cancellationToken).ConfigureAwait(false);
        if (existing is not null) return Allow(registration.EnvironmentId, kind, ChangeWindowClearanceMode.Replay, null, null, existing);

        var now = timeProvider.GetUtcNow();
        var status = ChangeWindowCalendar.Evaluate(window, now);
        if (status.State != ChangeWindowState.Closed)
        {
            return status.State == ChangeWindowState.Open
                ? Allow(registration.EnvironmentId, kind, ChangeWindowClearanceMode.Open, status.ClosesAt!.Value + DeadlineGrace, null, null)
                : Allow(registration.EnvironmentId, kind, ChangeWindowClearanceMode.NoWindow, null, null, null);
        }

        var refusal = await EvaluateOverrideAsync(registration, status, auditAction, subjectId, commandId, actor, accessToken, correlationId, overrideRequest, cancellationToken)
            .ConfigureAwait(false);
        return refusal.Override is { } overrideEvent
            ? Allow(registration.EnvironmentId, kind, ChangeWindowClearanceMode.Override, null, overrideEvent, null)
            : refusal.Decision!;
    }

    /// <summary>
    /// Decide a abertura de um terminal (spec 014), que não passa pela fila. A exceção, quando aceita, grava o
    /// <c>change_window.override</c> antes de a sessão abrir.
    /// </summary>
    public async Task<ChangeWindowDecision> EvaluateTerminalAsync(
        EnvironmentRegistration registration,
        string containerId,
        AuthenticatedActor actor,
        string accessToken,
        Guid correlationId,
        ChangeWindowOverrideRequest? overrideRequest,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(actor);
        var window = registration.Stage is { } stage
            ? await schedule.GetAsync(stage, cancellationToken).ConfigureAwait(false)
            : null;
        if (window is null) return new ChangeWindowDecision(null, null, null, null);
        var status = ChangeWindowCalendar.Evaluate(window, timeProvider.GetUtcNow());
        if (status.State != ChangeWindowState.Closed) return new ChangeWindowDecision(null, null, null, null);

        var refusal = await EvaluateOverrideAsync(registration, status, "container.exec.requested", containerId, null, actor, accessToken, correlationId, overrideRequest, cancellationToken)
            .ConfigureAwait(false);
        if (refusal.Override is not { } overrideEvent) return refusal.Decision!;
        await auditWriter.AppendAsync(overrideEvent, cancellationToken).ConfigureAwait(false);
        return new ChangeWindowDecision(null, null, null, null);
    }

    private async Task<(AuditEvent? Override, ChangeWindowDecision? Decision)> EvaluateOverrideAsync(
        EnvironmentRegistration registration,
        ChangeWindowStatus status,
        string auditAction,
        string? subjectId,
        Guid? commandId,
        AuthenticatedActor actor,
        string accessToken,
        Guid correlationId,
        ChangeWindowOverrideRequest? overrideRequest,
        CancellationToken cancellationToken)
    {
        if (overrideRequest is null)
        {
            await AuditDeniedAsync(registration, status, auditAction, subjectId, commandId, actor, correlationId, WindowClosed, cancellationToken).ConfigureAwait(false);
            return (null, Refuse(WindowClosed, status));
        }

        var justification = overrideRequest.Justification?.Trim();
        if (!ActionJustifications.IsValidLength(justification)) return (null, Refuse(JustificationRequired, status));

        var decision = await DecideOverrideAsync(registration, actor, accessToken, correlationId, cancellationToken).ConfigureAwait(false);
        if (decision == AuthorizationDecisionOutcome.Indeterminate) return (null, Refuse(AuthorizationUnavailable, status));
        if (decision == AuthorizationDecisionOutcome.Denied)
        {
            await AuditDeniedAsync(registration, status, auditAction, subjectId, commandId, actor, correlationId, OverrideDenied, cancellationToken).ConfigureAwait(false);
            return (null, Refuse(OverrideDenied, status));
        }

        var details = Details(status, auditAction);
        details["justification"] = Terminal.JustificationMasker.Mask(justification!);
        var overrideEvent = AuditEvent.Create(
            Guid.NewGuid(), timeProvider.GetUtcNow(), correlationId, AuditActorKind.User, actor.Subject,
            "change_window.override", registration.EnvironmentId, AuditOutcome.Succeeded, commandId: commandId);
        return (overrideEvent.WithSubject(subjectId ?? registration.EnvironmentId.ToString("D"), details), null);
    }

    private async Task<AuthorizationDecisionOutcome> DecideOverrideAsync(
        EnvironmentRegistration registration,
        AuthenticatedActor actor,
        string accessToken,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        // O cadastro também precisa declarar o scope, como nas demais ações (EnvironmentAccessDecision).
        if (!registration.Scopes.Contains(EnvironmentResourceScopes.ChangeOverride)) return AuthorizationDecisionOutcome.Denied;
        try
        {
            var decision = await authorizationDecider.DecideAsync(
                $"urn:dokpod:environment:{registration.EnvironmentId:D}",
                EnvironmentResourceScopes.ChangeOverride,
                actor,
                accessToken,
                correlationId,
                cancellationToken).ConfigureAwait(false);
            // Falha fechada: negação sem código é tratada como decisão indisponível, como nos serviços de ação.
            return decision.Allowed || !string.IsNullOrWhiteSpace(decision.FailureCode)
                ? decision.Outcome
                : AuthorizationDecisionOutcome.Indeterminate;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return AuthorizationDecisionOutcome.Indeterminate;
        }
    }

    private async Task AuditDeniedAsync(
        EnvironmentRegistration registration,
        ChangeWindowStatus status,
        string auditAction,
        string? subjectId,
        Guid? commandId,
        AuthenticatedActor actor,
        Guid correlationId,
        string failureCode,
        CancellationToken cancellationToken)
    {
        var details = Details(status, auditAction);
        var denied = AuditEvent.Create(
            Guid.NewGuid(), timeProvider.GetUtcNow(), correlationId, AuditActorKind.User, actor.Subject,
            "change_window.denied", registration.EnvironmentId, AuditOutcome.Denied, failureCode, commandId)
            .WithSubject(subjectId ?? registration.EnvironmentId.ToString("D"), details);
        try
        {
            await auditWriter.AppendAsync(denied, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // A recusa vale mesmo sem a trilha: a ação nunca chega à fila (falha fechada).
        }
    }

    private static Dictionary<string, string> Details(ChangeWindowStatus status, string auditAction)
    {
        var details = new Dictionary<string, string>(StringComparer.Ordinal) { ["action"] = auditAction };
        if (status.NextOpensAt is { } next) details["nextWindowStartsAt"] = next.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        if (status.TimeZone is { } zone) details["timeZone"] = zone;
        return details;
    }

    private static ChangeWindowDecision Refuse(string failureCode, ChangeWindowStatus status) =>
        new(null, failureCode, status.NextOpensAt, status.TimeZone);

    private static ChangeWindowDecision Allow(
        Guid environmentId,
        AgentCommandKind kind,
        ChangeWindowClearanceMode mode,
        DateTimeOffset? maxDeadlineUtc,
        AuditEvent? overrideEvent,
        AgentCommandStatusSnapshot? existing)
    {
        RuntimeHelpers.RunClassConstructor(typeof(Clearance).TypeHandle);
        return new ChangeWindowDecision(issue!(environmentId, kind, mode, maxDeadlineUtc, overrideEvent, existing), null, null, null);
    }
}
