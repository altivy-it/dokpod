namespace Dokpod.Domain.Auditing;

/// <summary>Categorias das ações de auditoria para a consulta (spec 017 D-007).</summary>
public static class AuditCategories
{
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Actions { get; } = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        ["environments"] = ["environment.register", "environment.approve", "environment.suspend", "environment.revoke", "environment.stage_changed"],
        ["containers"] = ["container.start", "container.stop", "container.restart", "container.delete", "container.pause", "container.unpause", "container.create.requested"],
        ["commands"] = ["container.command.result"],
        ["images"] = ["image.pull"],
        ["volumes"] = ["volume.remove"],
        ["networks"] = ["network.remove"],
        ["pods"] = ["pod.start", "pod.stop", "pod.restart", "pod.pause", "pod.unpause"],
        ["inspection"] = ["container.inspected", "container.logs.read", "container.logs.stream_started", "container.logs.stream_ended"],
        ["terminal"] = ["container.exec.requested", "container.exec.started", "container.exec.ended", "container.exec.denied"],
        ["governance"] =
        [
            "alert.rule_created", "alert.rule_updated", "alert.rule_deleted", "alert.acknowledged", "alert.resolved", "alert.silenced",
            "alert.silence_created", "alert.silence_ended", "container.justification_policy_changed", "registry.policy_changed",
        ],
        ["audit"] = ["audit.exported"],
    };

    public static string? CategoryOf(string action) =>
        Actions.FirstOrDefault(entry => entry.Value.Contains(action, StringComparer.Ordinal)).Key;
}
