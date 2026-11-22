using Dokpod.ControlPlane.Application.Alerts;
using Dokpod.ControlPlane.Application.Auditing;
using Dokpod.ControlPlane.Application.Licensing;
using Dokpod.Domain.Auditing;
using Dokpod.Domain.ChangeWindows;
using Dokpod.Domain.Environments;

namespace Dokpod.ControlPlane.Application.ChangeWindows;

public enum ChangeWindowSettingsOutcome
{
    Ok,
    Forbidden,
    LicenseDenied,
    Invalid,
    PreconditionFailed,
    NotFound,
}

/// <summary>Janela de um estágio na vista Políticas: a gravada (ou nenhuma), se o chamador edita e o estado agora.</summary>
public sealed record ChangeWindowView(EnvironmentStage Stage, ChangeWindow? Window, bool Editable, ChangeWindowStatus Status);

public sealed record ChangeWindowSettingsResult(
    ChangeWindowSettingsOutcome Outcome,
    ChangeWindowView? View = null,
    string? DenialCode = null,
    IReadOnlyList<ChangeWindowFieldError>? Errors = null);

/// <summary>
/// Configuração das janelas por estágio (spec 026 T017, só Business). Editar e remover exigem `environment:manage` em
/// todos os hosts legíveis do estágio, a regra da política de justificativa (016 D-007). Só gravar passa pelo gate da
/// licença (D-007): com a licença restrita, a listagem e a remoção continuam, e a janela continua valendo.
/// </summary>
[BusinessCapability(Capability)]
public sealed class ChangeWindowSettingsService(
    IChangeWindowStore store,
    IChangeWindowSchedule schedule,
    AlertGovernanceService governance,
    IBusinessCapabilityGate gate,
    IAuditEventWriter auditWriter,
    TimeProvider timeProvider)
{
    public const string Capability = "change-windows";

    private static readonly EnvironmentStage[] Stages = [EnvironmentStage.Production, EnvironmentStage.Staging, EnvironmentStage.Development];

    /// <summary>Os três estágios; nulo quando o chamador não tem `environment:manage` em nenhum host.</summary>
    public async Task<IReadOnlyList<ChangeWindowView>?> ListAsync(AlertCaller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);
        var managed = await governance.ReadableWithAsync(caller, EnvironmentResourceScopes.Manage, null, cancellationToken).ConfigureAwait(false);
        if (managed.Count == 0) return null;
        var editable = await governance.StageEditorAsync(caller, cancellationToken).ConfigureAwait(false);
        var windows = (await schedule.ListAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(window => window.Stage);
        var now = timeProvider.GetUtcNow();
        return Stages
            .Select(stage => new ChangeWindowView(stage, windows.GetValueOrDefault(stage), editable(stage), ChangeWindowCalendar.Evaluate(windows.GetValueOrDefault(stage), now)))
            .ToArray();
    }

    /// <summary>Cria (<paramref name="expectedVersion"/> nulo) ou edita a janela na versão esperada.</summary>
    public async Task<ChangeWindowSettingsResult> SaveAsync(
        AlertCaller caller,
        EnvironmentStage stage,
        string? timeZone,
        IReadOnlyList<ChangeWindowRange>? ranges,
        int? expectedVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);
        var hosts = await governance.AuthorizeStageAsync(caller, stage, cancellationToken).ConfigureAwait(false);
        if (hosts is null) return new ChangeWindowSettingsResult(ChangeWindowSettingsOutcome.Forbidden);
        var license = await gate.EnsureAsync(Capability, cancellationToken).ConfigureAwait(false);
        if (!license.Allowed) return new ChangeWindowSettingsResult(ChangeWindowSettingsOutcome.LicenseDenied, DenialCode: license.DenialCode ?? "license_invalid");
        var errors = ChangeWindow.Validate(timeZone, ranges);
        if (errors.Count > 0) return new ChangeWindowSettingsResult(ChangeWindowSettingsOutcome.Invalid, Errors: errors);

        var now = timeProvider.GetUtcNow();
        var written = await store.SaveAsync(new ChangeWindow(stage, timeZone!.Trim(), ranges!, 0, caller.Actor.Label, now), expectedVersion, cancellationToken)
            .ConfigureAwait(false);
        if (written.Outcome != ChangeWindowWriteOutcome.Saved) return Failure(written.Outcome);

        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["stage"] = EnvironmentStages.ToValue(stage),
            ["timeZone"] = written.Window!.TimeZone,
            ["ranges"] = Describe(written.Window.Ranges),
            ["version"] = written.Window.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        if (written.Previous is { } previous)
        {
            details["previousTimeZone"] = previous.TimeZone;
            details["previousRanges"] = Describe(previous.Ranges);
        }

        await AuditAsync(caller, "change_window.updated", hosts, details, cancellationToken).ConfigureAwait(false);
        var editable = await governance.StageEditorAsync(caller, cancellationToken).ConfigureAwait(false);
        return new ChangeWindowSettingsResult(
            ChangeWindowSettingsOutcome.Ok,
            new ChangeWindowView(stage, written.Window, editable(stage), ChangeWindowCalendar.Evaluate(written.Window, now)));
    }

    /// <summary>Remove a janela na versão esperada; não passa pelo gate da licença.</summary>
    public async Task<ChangeWindowSettingsResult> DeleteAsync(AlertCaller caller, EnvironmentStage stage, int expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);
        var hosts = await governance.AuthorizeStageAsync(caller, stage, cancellationToken).ConfigureAwait(false);
        if (hosts is null) return new ChangeWindowSettingsResult(ChangeWindowSettingsOutcome.Forbidden);
        var removed = await store.DeleteAsync(stage, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (removed.Outcome != ChangeWindowWriteOutcome.Removed) return Failure(removed.Outcome);

        await AuditAsync(
            caller,
            "change_window.removed",
            hosts,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["stage"] = EnvironmentStages.ToValue(stage),
                ["previousTimeZone"] = removed.Previous!.TimeZone,
                ["previousRanges"] = Describe(removed.Previous.Ranges),
            },
            cancellationToken).ConfigureAwait(false);
        return new ChangeWindowSettingsResult(ChangeWindowSettingsOutcome.Ok);
    }

    /// <summary>Faixas legíveis na auditoria: <c>mon,tue 22:00-06:00; sat 09:30-11:00</c>.</summary>
    internal static string Describe(IReadOnlyList<ChangeWindowRange> ranges) =>
        string.Join("; ", ranges.Select(range =>
            $"{string.Join(',', range.Days.OrderBy(day => ((int)day + 6) % 7).Select(ChangeWindowFormat.DayToValue))} " +
            $"{ChangeWindowFormat.TimeToValue(range.Start)}-{ChangeWindowFormat.TimeToValue(range.End)}"));

    private static ChangeWindowSettingsResult Failure(ChangeWindowWriteOutcome outcome) => new(
        outcome == ChangeWindowWriteOutcome.NotFound ? ChangeWindowSettingsOutcome.NotFound : ChangeWindowSettingsOutcome.PreconditionFailed);

    private async Task AuditAsync(
        AlertCaller caller,
        string action,
        IReadOnlyList<EnvironmentRegistration> hosts,
        IReadOnlyDictionary<string, string> details,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var host in hosts)
        {
            var auditEvent = AuditEvent.Create(Guid.NewGuid(), now, caller.CorrelationId, AuditActorKind.User, caller.Actor.Subject, action, host.EnvironmentId, AuditOutcome.Succeeded);
            // O estágio é o alvo do evento, para a auditoria filtrar por janela.
            await auditWriter.AppendAsync(auditEvent.WithSubject(details["stage"], details), cancellationToken).ConfigureAwait(false);
        }
    }
}
