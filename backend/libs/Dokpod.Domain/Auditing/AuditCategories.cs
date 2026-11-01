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
        // Spec 027: destinos de exportação para SIEM; obrigatória em todo destino (threat model L7).
        [AuditExport] =
        [
            "audit_export.destination_created", "audit_export.destination_updated", "audit_export.destination_deleted",
            "audit_export.test_sent", "audit_export.gap",
        ],
    };

    /// <summary>Categoria que todo destino de exportação recebe, para o SOC sempre ver mudanças de destino (spec 027 L7).</summary>
    public const string AuditExport = "audit_export";

    public static string? CategoryOf(string action) =>
        Actions.FirstOrDefault(entry => entry.Value.Contains(action, StringComparer.Ordinal)).Key;
}
