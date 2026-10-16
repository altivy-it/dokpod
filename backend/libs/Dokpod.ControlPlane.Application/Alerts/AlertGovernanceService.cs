using Dokpod.ControlPlane.Application.Auditing;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Realtime;
using Dokpod.ControlPlane.Application.Terminal;
using Dokpod.Domain.Alerts;
using Dokpod.Domain.Auditing;
using Dokpod.Domain.Environments;

namespace Dokpod.ControlPlane.Application.Alerts;

/// <summary>Quem chama: ator, token e os ambientes que ele pode ler (`environment:read`).</summary>
public sealed record AlertCaller(AuthenticatedActor Actor, string AccessToken, Guid CorrelationId, IReadOnlyList<EnvironmentRegistration> Readable);

public enum GovernanceOutcome
{
    Ok,
    NotFound,
    Forbidden,
    Invalid,
    Conflict,
    AuthorizationUnavailable,
}

public sealed record GovernanceResult(GovernanceOutcome Outcome, string? Code = null, Guid? Id = null)
{
    public static GovernanceResult Ok(Guid? id = null) => new(GovernanceOutcome.Ok, null, id);
}

public sealed record AlertSummary(int Open, IReadOnlyDictionary<string, int> BySeverity, IReadOnlyList<AlertRecord> Recent);

public sealed record AlertDetail(AlertRecord Alert, AlertRule? Rule, IReadOnlyList<AlertTimelineEntry> Timeline, bool CanManage);

/// <summary>Governança de alertas (spec 016): consultas por `alert:read`, ações por `alert:manage`, auditoria.</summary>
public sealed class AlertGovernanceService(
    IAlertStore alerts,
    IActionJustificationStore justifications,
    IEnvironmentAuthorizationDecider decider,
    IAuditEventWriter auditWriter,
    IControlPlaneNotifier notifier,
    TimeProvider timeProvider)
{
    public const int MinComment = ActionJustifications.MinLength;
    public const int MaxComment = ActionJustifications.MaxLength;

    /// <summary>Ambientes legíveis em que o chamador tem o scope (decisões em paralelo; indisponível conta como negado).</summary>
    public async Task<IReadOnlyList<EnvironmentRegistration>> FilterAsync(AlertCaller caller, string scope, IEnumerable<EnvironmentRegistration>? subset, CancellationToken cancellationToken)
    {
        var candidates = (subset ?? caller.Readable).Where(r => r.Enabled).ToArray();
        var decisions = await Task.WhenAll(candidates.Select(async registration =>
        {
            try
            {
                var decision = await decider.DecideAsync($"urn:dokpod:environment:{registration.EnvironmentId:D}", scope, caller.Actor, caller.AccessToken, caller.CorrelationId, cancellationToken).ConfigureAwait(false);
                return decision.Allowed;
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                return false;
            }
        })).ConfigureAwait(false);
        return candidates.Where((_, index) => decisions[index]).ToArray();
    }

    public async Task<AlertSummary> SummarizeAsync(AlertCaller caller, EnvironmentStage? stage, CancellationToken cancellationToken)
    {
        var readable = await FilterAsync(caller, EnvironmentResourceScopes.ReadAlerts, caller.Readable.Where(r => stage is null || r.Stage == stage), cancellationToken).ConfigureAwait(false);
        if (readable.Count == 0) return new AlertSummary(0, Severities(), []);
        var ids = readable.Select(r => r.EnvironmentId).ToArray();
        var page = await alerts.ListAsync(new AlertQuery(ids, false, [], [AlertState.Open, AlertState.Acknowledged], null, null, null, null, null, 500), cancellationToken).ConfigureAwait(false);
        var bySeverity = Severities();
        foreach (var alert in page.Alerts) bySeverity[AlertValues.Severity(alert.Severity)]++;
        return new AlertSummary(page.Alerts.Count, bySeverity, page.Alerts.Take(10).ToArray());
    }

    public async Task<(AlertPage Page, IReadOnlySet<Guid> Manageable)> ListAsync(AlertCaller caller, EnvironmentStage? stage, AlertQuery query, CancellationToken cancellationToken)
    {
        var candidates = caller.Readable.Where(r => (stage is null || r.Stage == stage) && (query.EnvironmentIds.Count == 0 || query.EnvironmentIds.Contains(r.EnvironmentId)));
        var readable = await FilterAsync(caller, EnvironmentResourceScopes.ReadAlerts, candidates, cancellationToken).ConfigureAwait(false);
        if (readable.Count == 0) return (new AlertPage([], null), new HashSet<Guid>());
        var page = await alerts.ListAsync(query with { EnvironmentIds = readable.Select(r => r.EnvironmentId).ToArray() }, cancellationToken).ConfigureAwait(false);
        var manageable = await FilterAsync(caller, EnvironmentResourceScopes.ManageAlerts, readable.Where(r => page.Alerts.Any(a => a.EnvironmentId == r.EnvironmentId)), cancellationToken).ConfigureAwait(false);
        return (page, manageable.Select(r => r.EnvironmentId).ToHashSet());
    }

    public async Task<AlertDetail?> GetAsync(AlertCaller caller, Guid alertId, CancellationToken cancellationToken)
    {
        var alert = await alerts.GetAsync(alertId, cancellationToken).ConfigureAwait(false);
        if (alert is null) return null;
        var registration = caller.Readable.FirstOrDefault(r => r.EnvironmentId == alert.EnvironmentId);
        // Sem leitura: indistinguível de inexistente (FR-023).
        if (registration is null || (await FilterAsync(caller, EnvironmentResourceScopes.ReadAlerts, [registration], cancellationToken).ConfigureAwait(false)).Count == 0) return null;
        var canManage = (await FilterAsync(caller, EnvironmentResourceScopes.ManageAlerts, [registration], cancellationToken).ConfigureAwait(false)).Count == 1;
        return new AlertDetail(
            alert,
            await alerts.GetRuleAsync(alert.RuleId, cancellationToken).ConfigureAwait(false),
            await alerts.GetTimelineAsync(alertId, cancellationToken).ConfigureAwait(false),
            canManage);
    }

    public Task<GovernanceResult> AcknowledgeAsync(AlertCaller caller, Guid alertId, string? comment, CancellationToken cancellationToken) =>
        ActAsync(caller, alertId, comment, "alert.acknowledged", async (alert, masked, now) =>
            await alerts.TransitionAsync(alert.AlertId, alert.State == AlertState.Silenced ? AlertState.Silenced : AlertState.Acknowledged, caller.Actor.Label, alert.SilenceId, null, now, AlertTimelineKinds.Acknowledged, caller.Actor.Label, masked, cancellationToken).ConfigureAwait(false),
            cancellationToken);

    public Task<GovernanceResult> ResolveAsync(AlertCaller caller, Guid alertId, string? comment, CancellationToken cancellationToken) =>
        ActAsync(caller, alertId, comment, "alert.resolved", async (alert, masked, now) =>
            await alerts.TransitionAsync(alert.AlertId, AlertState.Resolved, null, null, AlertResolutionReasons.Manual, now, AlertTimelineKinds.Resolved, caller.Actor.Label, masked, cancellationToken).ConfigureAwait(false),
            cancellationToken);

    public async Task<GovernanceResult> SilenceAlertAsync(AlertCaller caller, Guid alertId, DateTimeOffset until, string? justification, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (AlertSilence.ValidateWindow(now, until) is not null) return new GovernanceResult(GovernanceOutcome.Invalid, "until");
        Guid? silenceId = null;
        var result = await ActAsync(caller, alertId, justification, "alert.silenced", async (alert, masked, at) =>
        {
            var silence = new AlertSilence(Guid.NewGuid(), AlertScope.Everything, alert.AlertId, at, until, null, masked, caller.Actor.Label);
            await alerts.CreateSilenceAsync(silence, cancellationToken).ConfigureAwait(false);
            silenceId = silence.SilenceId;
            return await alerts.TransitionAsync(alert.AlertId, AlertState.Silenced, null, silence.SilenceId, null, at, AlertTimelineKinds.Silenced, caller.Actor.Label, masked, cancellationToken).ConfigureAwait(false);
        }, cancellationToken, new Dictionary<string, string> { ["until"] = until.ToString("O") }).ConfigureAwait(false);
        return result.Outcome == GovernanceOutcome.Ok ? GovernanceResult.Ok(silenceId) : result;
    }

    private async Task<GovernanceResult> ActAsync(
        AlertCaller caller,
        Guid alertId,
        string? comment,
        string auditAction,
        Func<AlertRecord, string, DateTimeOffset, Task<bool>> apply,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? extraDetails = null)
    {
        if (!ActionJustifications.IsValidLength(comment)) return new GovernanceResult(GovernanceOutcome.Invalid, "comment");
        var alert = await alerts.GetAsync(alertId, cancellationToken).ConfigureAwait(false);
        var registration = alert is null ? null : caller.Readable.FirstOrDefault(r => r.EnvironmentId == alert.EnvironmentId);
        if (alert is null || registration is null || (await FilterAsync(caller, EnvironmentResourceScopes.ReadAlerts, [registration], cancellationToken).ConfigureAwait(false)).Count == 0)
        {
            return new GovernanceResult(GovernanceOutcome.NotFound);
        }

        if ((await FilterAsync(caller, EnvironmentResourceScopes.ManageAlerts, [registration], cancellationToken).ConfigureAwait(false)).Count == 0)
        {
            return new GovernanceResult(GovernanceOutcome.Forbidden, "authorization_denied");
        }

        if (!alert.IsActive) return new GovernanceResult(GovernanceOutcome.Conflict, "alert_resolved");
        var now = timeProvider.GetUtcNow();
        if (!await apply(alert, JustificationMasker.Mask(comment!.Trim()), now).ConfigureAwait(false)) return new GovernanceResult(GovernanceOutcome.Conflict, "alert_resolved");
        var details = new Dictionary<string, string> { ["alertId"] = alert.AlertId.ToString("D"), ["ruleId"] = alert.RuleId.ToString("D"), ["severity"] = AlertValues.Severity(alert.Severity) };
        foreach (var (key, value) in extraDetails ?? new Dictionary<string, string>()) details[key] = value;
        await AuditAsync(caller, auditAction, alert.EnvironmentId, details, cancellationToken).ConfigureAwait(false);
        await notifier.NotifyAsync(alert.EnvironmentId, InvalidationKinds.Alerts, cancellationToken).ConfigureAwait(false);
        return GovernanceResult.Ok(alert.AlertId);
    }

    /// <summary>Regras visíveis: globais ou de hosts legíveis (FR-023).</summary>
    public async Task<(IReadOnlyList<AlertRule> Rules, bool CanManage)> ListRulesAsync(AlertCaller caller, CancellationToken cancellationToken)
    {
        var readable = await FilterAsync(caller, EnvironmentResourceScopes.ReadAlerts, null, cancellationToken).ConfigureAwait(false);
        if (readable.Count == 0) return ([], false);
        var ids = readable.Select(r => r.EnvironmentId).ToHashSet();
        var rules = (await alerts.ListRulesAsync(cancellationToken).ConfigureAwait(false)).Where(r => r.Scope.EnvironmentId is null || ids.Contains(r.Scope.EnvironmentId.Value)).ToArray();
        var canManage = (await FilterAsync(caller, EnvironmentResourceScopes.ManageAlerts, readable, cancellationToken).ConfigureAwait(false)).Count == readable.Count;
        return (rules, canManage);
    }

    public async Task<GovernanceResult> SaveRuleAsync(AlertCaller caller, AlertRule rule, int? expectedVersion, CancellationToken cancellationToken)
    {
        var errors = rule.Validate();
        if (errors.Count > 0) return new GovernanceResult(GovernanceOutcome.Invalid, errors[0]);
        AlertRule? previous = null;
        if (expectedVersion is not null)
        {
            previous = await alerts.GetRuleAsync(rule.RuleId, cancellationToken).ConfigureAwait(false);
            if (previous is null) return new GovernanceResult(GovernanceOutcome.NotFound);
            // Regra padrão mantém condição e alvo; o administrador ajusta limite, severidade, abrangência e ativação.
            if (previous.BuiltIn) rule = rule with { ConditionKind = previous.ConditionKind, EventType = previous.EventType, Metric = previous.Metric, BuiltIn = true };
        }

        var covered = await CoveredHostsAsync(caller, rule.Scope, previous?.Scope, cancellationToken).ConfigureAwait(false);
        if (covered is null) return new GovernanceResult(GovernanceOutcome.Forbidden, "authorization_denied");
        if (!await alerts.SaveRuleAsync(rule with { BuiltIn = previous?.BuiltIn ?? false }, caller.Actor.Label, expectedVersion, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false))
        {
            return new GovernanceResult(GovernanceOutcome.Conflict, expectedVersion is null ? "rule_exists" : "version_conflict");
        }

        var details = new Dictionary<string, string>
        {
            ["ruleId"] = rule.RuleId.ToString("D"),
            ["severity"] = AlertValues.Severity(rule.Severity),
            ["enabled"] = rule.Enabled ? "true" : "false",
            ["version"] = ((expectedVersion ?? 0) + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["durationSeconds"] = rule.DurationSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        if (rule.ThresholdPercent is { } threshold) details["thresholdPercent"] = threshold.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        foreach (var host in covered) await AuditAsync(caller, expectedVersion is null ? "alert.rule_created" : "alert.rule_updated", host, details, cancellationToken).ConfigureAwait(false);
        return GovernanceResult.Ok(rule.RuleId);
    }

    public async Task<GovernanceResult> DeleteRuleAsync(AlertCaller caller, Guid ruleId, CancellationToken cancellationToken)
    {
        var rule = await alerts.GetRuleAsync(ruleId, cancellationToken).ConfigureAwait(false);
        if (rule is null) return new GovernanceResult(GovernanceOutcome.NotFound);
        if (rule.BuiltIn) return new GovernanceResult(GovernanceOutcome.Conflict, "built_in_rule");
        var covered = await CoveredHostsAsync(caller, rule.Scope, null, cancellationToken).ConfigureAwait(false);
        if (covered is null) return new GovernanceResult(GovernanceOutcome.Forbidden, "authorization_denied");
        var now = timeProvider.GetUtcNow();
        foreach (var alert in (await alerts.ListActiveAsync(cancellationToken).ConfigureAwait(false)).Where(a => a.RuleId == ruleId))
        {
            await alerts.TransitionAsync(alert.AlertId, AlertState.Resolved, null, null, AlertResolutionReasons.RuleRemoved, now, AlertTimelineKinds.Resolved, caller.Actor.Label, null, cancellationToken).ConfigureAwait(false);
        }

        await alerts.DeleteRuleAsync(ruleId, cancellationToken).ConfigureAwait(false);
        foreach (var host in covered) await AuditAsync(caller, "alert.rule_deleted", host, new Dictionary<string, string> { ["ruleId"] = ruleId.ToString("D") }, cancellationToken).ConfigureAwait(false);
        return GovernanceResult.Ok(ruleId);
    }

    /// <summary>Silenciamentos visíveis e se o chamador pode criá-los (alert:manage em todos os hosts com alert:read).</summary>
    public async Task<(IReadOnlyList<AlertSilence> Silences, bool CanManage)> ListSilencesAsync(AlertCaller caller, bool activeOnly, CancellationToken cancellationToken)
    {
        var readable = await FilterAsync(caller, EnvironmentResourceScopes.ReadAlerts, null, cancellationToken).ConfigureAwait(false);
        var canManage = readable.Count > 0 && (await FilterAsync(caller, EnvironmentResourceScopes.ManageAlerts, readable, cancellationToken).ConfigureAwait(false)).Count == readable.Count;
        var ids = readable.Select(r => r.EnvironmentId).ToHashSet();
        var visibleAlerts = new HashSet<Guid>();
        var silences = await alerts.ListSilencesAsync(activeOnly, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        foreach (var silence in silences.Where(s => s.AlertId is not null))
        {
            if (await alerts.GetAsync(silence.AlertId!.Value, cancellationToken).ConfigureAwait(false) is { } alert && ids.Contains(alert.EnvironmentId)) visibleAlerts.Add(silence.SilenceId);
        }

        return (silences.Where(s => s.AlertId is not null ? visibleAlerts.Contains(s.SilenceId) : readable.Count > 0 && (s.Scope.EnvironmentId is null || ids.Contains(s.Scope.EnvironmentId.Value))).ToArray(), canManage);
    }

    public async Task<GovernanceResult> CreateSilenceAsync(AlertCaller caller, AlertScope scope, DateTimeOffset startsAt, DateTimeOffset endsAt, string? justification, CancellationToken cancellationToken)
    {
        if (!ActionJustifications.IsValidLength(justification)) return new GovernanceResult(GovernanceOutcome.Invalid, "justification");
        if (AlertSilence.ValidateWindow(startsAt, endsAt) is { } error) return new GovernanceResult(GovernanceOutcome.Invalid, error);
        if (scope.ContainerPattern is { Length: > AlertScope.MaxPatternLength }) return new GovernanceResult(GovernanceOutcome.Invalid, "scope.containerPattern");
        var covered = await CoveredHostsAsync(caller, scope, null, cancellationToken).ConfigureAwait(false);
        if (covered is null) return new GovernanceResult(GovernanceOutcome.Forbidden, "authorization_denied");
        var silence = new AlertSilence(Guid.NewGuid(), scope, null, startsAt, endsAt, null, JustificationMasker.Mask(justification!.Trim()), caller.Actor.Label);
        await alerts.CreateSilenceAsync(silence, cancellationToken).ConfigureAwait(false);
        foreach (var host in covered)
        {
            await AuditAsync(caller, "alert.silence_created", host, new Dictionary<string, string> { ["silenceId"] = silence.SilenceId.ToString("D"), ["until"] = endsAt.ToString("O") }, cancellationToken).ConfigureAwait(false);
            await notifier.NotifyAsync(host, InvalidationKinds.Alerts, cancellationToken).ConfigureAwait(false);
        }

        return GovernanceResult.Ok(silence.SilenceId);
    }

    public async Task<GovernanceResult> EndSilenceAsync(AlertCaller caller, Guid silenceId, CancellationToken cancellationToken)
    {
        var silence = await alerts.GetSilenceAsync(silenceId, cancellationToken).ConfigureAwait(false);
        if (silence is null) return new GovernanceResult(GovernanceOutcome.NotFound);
        IReadOnlyList<Guid>? covered;
        if (silence.AlertId is { } alertId)
        {
            var alert = await alerts.GetAsync(alertId, cancellationToken).ConfigureAwait(false);
            var registration = alert is null ? null : caller.Readable.FirstOrDefault(r => r.EnvironmentId == alert.EnvironmentId);
            covered = registration is not null && (await FilterAsync(caller, EnvironmentResourceScopes.ManageAlerts, [registration], cancellationToken).ConfigureAwait(false)).Count == 1
                ? [registration.EnvironmentId]
                : null;
        }
        else
        {
            covered = await CoveredHostsAsync(caller, silence.Scope, null, cancellationToken).ConfigureAwait(false);
        }

        if (covered is null) return new GovernanceResult(GovernanceOutcome.Forbidden, "authorization_denied");
        if (!await alerts.EndSilenceAsync(silenceId, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false)) return new GovernanceResult(GovernanceOutcome.Conflict, "silence_ended");
        foreach (var host in covered)
        {
            await AuditAsync(caller, "alert.silence_ended", host, new Dictionary<string, string> { ["silenceId"] = silenceId.ToString("D") }, cancellationToken).ConfigureAwait(false);
            await notifier.NotifyAsync(host, InvalidationKinds.Alerts, cancellationToken).ConfigureAwait(false);
        }

        return GovernanceResult.Ok(silenceId);
    }


    /// <summary>Políticas com a última alteração e se o chamador pode editar cada estágio (spec 022 D-005).</summary>
    public async Task<IReadOnlyList<(JustificationPolicyEntry Policy, bool CanEdit)>> ListPoliciesAsync(AlertCaller caller, CancellationToken cancellationToken)
    {
        var entries = await justifications.ListPolicyEntriesAsync(cancellationToken).ConfigureAwait(false);
        var managed = (await FilterAsync(caller, EnvironmentResourceScopes.Manage, null, cancellationToken).ConfigureAwait(false))
            .Select(r => r.EnvironmentId).ToHashSet();
        return entries.Select(entry =>
        {
            var candidates = PolicyCandidates(caller, entry.Stage);
            return (entry, candidates.Length > 0 && candidates.All(r => managed.Contains(r.EnvironmentId)));
        }).ToArray();
    }

    /// <summary>Hosts que decidem a política: os legíveis do estágio ou, sem nenhum, todos os legíveis habilitados.</summary>
    private static EnvironmentRegistration[] PolicyCandidates(AlertCaller caller, EnvironmentStage stage)
    {
        var inStage = caller.Readable.Where(r => r.Enabled && r.Stage == stage).ToArray();
        return inStage.Length > 0 ? inStage : caller.Readable.Where(r => r.Enabled).ToArray();
    }

    /// <summary>Política de justificativa (D-007): `environment:manage` em todos os hosts legíveis do estágio.</summary>
    public async Task<GovernanceResult> SetPolicyAsync(AlertCaller caller, EnvironmentStage stage, bool required, CancellationToken cancellationToken)
    {
        var candidates = PolicyCandidates(caller, stage);
        if (candidates.Length == 0) return new GovernanceResult(GovernanceOutcome.Forbidden, "authorization_denied");
        var allowed = await FilterAsync(caller, EnvironmentResourceScopes.Manage, candidates, cancellationToken).ConfigureAwait(false);
        if (allowed.Count != candidates.Length) return new GovernanceResult(GovernanceOutcome.Forbidden, "authorization_denied");
        await justifications.SetPolicyAsync(stage, required, caller.Actor.Label, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        foreach (var host in candidates)
        {
            await AuditAsync(caller, "container.justification_policy_changed", host.EnvironmentId,
                new Dictionary<string, string> { ["stage"] = EnvironmentStages.ToValue(stage), ["required"] = required ? "true" : "false" }, cancellationToken).ConfigureAwait(false);
        }

        return GovernanceResult.Ok();
    }

    /// <summary>Hosts legíveis abrangidos; nulo se faltar `alert:manage` em algum ou se nenhum for abrangido.</summary>
    private async Task<IReadOnlyList<Guid>?> CoveredHostsAsync(AlertCaller caller, AlertScope scope, AlertScope? previous, CancellationToken cancellationToken)
    {
        bool Covers(AlertScope s, EnvironmentRegistration r) => (s.Stage is null || s.Stage == r.Stage) && (s.EnvironmentId is null || s.EnvironmentId == r.EnvironmentId);
        var covered = caller.Readable.Where(r => r.Enabled && (Covers(scope, r) || (previous is not null && Covers(previous, r)))).ToArray();
        if (scope.EnvironmentId is { } host && covered.All(r => r.EnvironmentId != host)) return null;
        if (covered.Length == 0) return null;
        var allowed = await FilterAsync(caller, EnvironmentResourceScopes.ManageAlerts, covered, cancellationToken).ConfigureAwait(false);
        return allowed.Count == covered.Length ? covered.Select(r => r.EnvironmentId).ToArray() : null;
    }

    private Task AuditAsync(AlertCaller caller, string action, Guid environmentId, IReadOnlyDictionary<string, string> details, CancellationToken cancellationToken)
    {
        var auditEvent = AuditEvent.Create(Guid.NewGuid(), timeProvider.GetUtcNow(), caller.CorrelationId, AuditActorKind.User, caller.Actor.Subject, action, environmentId, AuditOutcome.Succeeded, null);
        var subject = details.TryGetValue("alertId", out var alertId) ? alertId : details.TryGetValue("ruleId", out var ruleId) ? ruleId : details.TryGetValue("silenceId", out var silenceId) ? silenceId : environmentId.ToString("D");
        return auditWriter.AppendAsync(auditEvent.WithSubject(subject, details), cancellationToken);
    }

    private static Dictionary<string, int> Severities() =>
        Enum.GetValues<AlertSeverity>().ToDictionary(AlertValues.Severity, _ => 0);
}
