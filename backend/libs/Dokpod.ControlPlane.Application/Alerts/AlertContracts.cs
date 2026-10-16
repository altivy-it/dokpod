using Dokpod.Domain.Alerts;
using Dokpod.Domain.Environments;

namespace Dokpod.ControlPlane.Application.Alerts;

public sealed record AlertRecord(
    Guid AlertId,
    Guid RuleId,
    string RuleName,
    Guid EnvironmentId,
    string? ContainerId,
    string? ContainerName,
    AlertSeverity Severity,
    AlertState State,
    string ConditionText,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset LastOccurredAtUtc,
    int Occurrences,
    string? Assignee,
    Guid? SilenceId,
    DateTimeOffset? ResolvedAtUtc,
    string? ResolutionReason)
{
    public bool IsActive => State != AlertState.Resolved;

    /// <summary>Conta para o menu e o sino (FR-006): aberto ou reconhecido, não silenciado.</summary>
    public bool NeedsAttention => State is AlertState.Open or AlertState.Acknowledged;

    public static string TargetKey(Guid environmentId, string? containerId) =>
        containerId is null ? environmentId.ToString("D") : $"{environmentId:D}:{containerId}";
}

public sealed record AlertTimelineEntry(DateTimeOffset OccurredAtUtc, string Kind, string? Actor, string? Comment);

public static class AlertTimelineKinds
{
    public const string Opened = "opened";
    public const string Occurred = "occurred";
    public const string Acknowledged = "acknowledged";
    public const string Silenced = "silenced";
    public const string Unsilenced = "unsilenced";
    public const string Resolved = "resolved";
}

public static class AlertResolutionReasons
{
    public const string ConditionCleared = "condition_cleared";
    public const string Manual = "manual";
    public const string ContainerRemoved = "container_removed";
    public const string RuleRemoved = "rule_removed";
}

/// <summary>Pedido de abertura (ou nova ocorrência, se já existe alerta ativo para a regra e o alvo).</summary>
public sealed record AlertOccurrence(
    AlertRule Rule,
    Guid EnvironmentId,
    string? ContainerId,
    string? ContainerName,
    string ConditionText,
    DateTimeOffset OccurredAtUtc,
    Guid? SilenceId);

public sealed record AlertCursor(DateTimeOffset At, Guid AlertId);

public sealed record AlertQuery(
    IReadOnlyCollection<Guid> EnvironmentIds,
    bool History,
    IReadOnlyCollection<AlertSeverity> Severities,
    IReadOnlyCollection<AlertState> States,
    string? Text,
    Guid? RuleId,
    DateTimeOffset? SinceUtc,
    DateTimeOffset? UntilUtc,
    AlertCursor? After,
    int Limit);

public sealed record AlertPage(IReadOnlyList<AlertRecord> Alerts, AlertCursor? Next);

public interface IAlertStore
{
    Task<IReadOnlyList<AlertRule>> ListRulesAsync(CancellationToken cancellationToken);

    Task<AlertRule?> GetRuleAsync(Guid ruleId, CancellationToken cancellationToken);

    /// <summary>Cria ou atualiza; com `expectedVersion`, recusa se a versão gravada mudou (devolve false).</summary>
    Task<bool> SaveRuleAsync(AlertRule rule, string actor, int? expectedVersion, DateTimeOffset at, CancellationToken cancellationToken);

    Task<bool> DeleteRuleAsync(Guid ruleId, CancellationToken cancellationToken);

    /// <summary>Abre o alerta ou registra nova ocorrência no ativo (índice único por regra e alvo). Devolve se abriu.</summary>
    Task<(AlertRecord Alert, bool Opened)> OpenOrOccurAsync(AlertOccurrence occurrence, CancellationToken cancellationToken);

    Task<IReadOnlyList<AlertRecord>> ListActiveAsync(CancellationToken cancellationToken);

    Task<AlertPage> ListAsync(AlertQuery query, CancellationToken cancellationToken);

    Task<AlertRecord?> GetAsync(Guid alertId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AlertTimelineEntry>> GetTimelineAsync(Guid alertId, CancellationToken cancellationToken);

    /// <summary>Muda o estado de um alerta ativo e registra a linha do tempo; devolve false se ele já não estava ativo.</summary>
    Task<bool> TransitionAsync(
        Guid alertId,
        AlertState state,
        string? assignee,
        Guid? silenceId,
        string? resolutionReason,
        DateTimeOffset at,
        string timelineKind,
        string? actor,
        string? comment,
        CancellationToken cancellationToken);

    /// <summary>Eventos do tipo para o alvo desde o horário (regras com mínimo de ocorrências).</summary>
    Task<int> CountEventsAsync(Guid environmentId, string eventType, string? subjectId, DateTimeOffset sinceUtc, CancellationToken cancellationToken);

    Task<IReadOnlyList<AlertSilence>> ListSilencesAsync(bool activeOnly, DateTimeOffset now, CancellationToken cancellationToken);

    Task<AlertSilence?> GetSilenceAsync(Guid silenceId, CancellationToken cancellationToken);

    Task CreateSilenceAsync(AlertSilence silence, CancellationToken cancellationToken);

    Task<bool> EndSilenceAsync(Guid silenceId, DateTimeOffset at, CancellationToken cancellationToken);

    Task<int> DeleteResolvedBeforeAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken);
}

public sealed record ContainerActionJustification(
    Guid JustificationId,
    Guid CommandId,
    Guid EnvironmentId,
    string ContainerId,
    string? ContainerName,
    JustifiedAction Action,
    string Actor,
    string Reason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? EndedAtUtc);

/// <summary>Política de um estágio com a última alteração (spec 022: Configurações → Políticas).</summary>
public sealed record JustificationPolicyEntry(EnvironmentStage Stage, bool Required, string? UpdatedBy, DateTimeOffset? UpdatedAt);

public sealed record JustificationCursor(DateTimeOffset At, Guid JustificationId);

public interface IActionJustificationStore
{
    Task<IReadOnlyDictionary<EnvironmentStage, bool>> GetPoliciesAsync(CancellationToken cancellationToken);

    Task SetPolicyAsync(EnvironmentStage stage, bool required, string actor, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>Todos os estágios; os nunca alterados vêm sem autor nem horário.</summary>
    Task<IReadOnlyList<JustificationPolicyEntry>> ListPolicyEntriesAsync(CancellationToken cancellationToken);

    /// <summary>Grava uma vez por comando (nova tentativa idempotente não duplica).</summary>
    Task RecordAsync(ContainerActionJustification justification, CancellationToken cancellationToken);

    /// <summary>Ações justificadas vigentes (sem fim) dos containers informados, a mais recente por container.</summary>
    Task<IReadOnlyDictionary<(Guid EnvironmentId, string ContainerId), ContainerActionJustification>> GetCurrentAsync(
        IReadOnlyCollection<(Guid EnvironmentId, string ContainerId)> containers,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ContainerActionJustification>> ListAsync(
        Guid environmentId,
        string? containerId,
        DateTimeOffset? sinceUtc,
        DateTimeOffset? untilUtc,
        JustificationCursor? after,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ContainerActionJustification>> ListOpenAsync(CancellationToken cancellationToken);

    Task EndAsync(Guid justificationId, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>Há ação justificada recente (vigente ou criada após `sinceUtc`) para o container (FR-019).</summary>
    Task<bool> HasRecentAsync(Guid environmentId, string containerId, DateTimeOffset sinceUtc, CancellationToken cancellationToken);
}
