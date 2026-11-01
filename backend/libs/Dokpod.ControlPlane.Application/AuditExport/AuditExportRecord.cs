using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dokpod.ControlPlane.Application.Auditing;
using Dokpod.Domain.Auditing;

namespace Dokpod.ControlPlane.Application.AuditExport;

/// <summary>
/// Registro exportado ao SIEM (spec 027 D-010, contracts/export-record.md, <c>formatVersion</c> 1). Vem da mesma
/// linha da trilha que o console e o CSV mostram, com os valores que o domínio já restringe (detalhes só por
/// allowlist, sem texto livre além da justificativa e do comando redigido); nada é acrescentado além de categoria,
/// instalação e marca de teste.
/// </summary>
public sealed record AuditExportRecord(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid InstallationId,
    Guid CorrelationId,
    Guid? CommandId,
    string ActorKind,
    string ActorId,
    string? ActorName,
    string Action,
    string? Category,
    Guid EnvironmentId,
    string? SubjectId,
    string Outcome,
    string? FailureCode,
    IReadOnlyDictionary<string, string> Details,
    bool Test)
{
    public const int FormatVersion = 1;
    public const string TestAction = "audit_export.test";

    private static readonly JsonSerializerOptions JsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Sucesso vai como aviso informativo; negado, falha e indeterminado vão como alerta (syslog e OTLP).</summary>
    public bool IsWarning => Outcome != "succeeded";

    public static AuditExportRecord FromEvent(AuditEventRecord e, Guid installationId) => new(
        e.EventId,
        e.OccurredAtUtc,
        installationId,
        e.CorrelationId,
        e.CommandId,
        e.ActorKind switch { AuditActorKind.User => "user", AuditActorKind.Agent => "agent", _ => "system" },
        e.ActorId,
        e.ActorName,
        e.Action,
        AuditCategories.CategoryOf(e.Action),
        // D-012: eventos da instalação saem sem ambiente.
        e.EnvironmentId == AuditEvent.InstallationScopeId ? Guid.Empty : e.EnvironmentId,
        e.SubjectId,
        e.Outcome switch { AuditOutcome.Succeeded => "succeeded", AuditOutcome.Denied => "denied", AuditOutcome.Failed => "failed", _ => "indeterminate" },
        e.FailureCode,
        e.Details ?? new Dictionary<string, string>(),
        Test: false);

    /// <summary>Evento de teste: identificador novo, não gravado na trilha (só <c>audit_export.test_sent</c> é).</summary>
    public static AuditExportRecord TestEvent(Guid installationId, Guid correlationId, string actorId, string? actorName, DateTimeOffset nowUtc) => new(
        Guid.NewGuid(),
        nowUtc,
        installationId,
        correlationId,
        null,
        "user",
        actorId,
        actorName,
        TestAction,
        AuditCategories.AuditExport,
        Guid.Empty,
        null,
        "succeeded",
        null,
        new Dictionary<string, string>(),
        Test: true);

    /// <summary>Instante com microssegundos, igual no JSON, no syslog e no atributo OTLP.</summary>
    public string OccurredAt => OccurredAtUtc.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture);

    /// <summary>Detalhes em ordem de chave; o atributo OTLP <c>dokpod.details</c> é este JSON.</summary>
    public string DetailsJson() => SortedDetails().ToJsonString(JsonOptions);

    /// <summary>JSON do contrato, com microssegundos em <c>occurredAt</c> e detalhes em ordem de chave.</summary>
    public string ToJson()
    {
        return new JsonObject
        {
            ["formatVersion"] = FormatVersion,
            ["eventId"] = EventId.ToString("D"),
            ["occurredAt"] = OccurredAt,
            ["installationId"] = InstallationId.ToString("D"),
            ["correlationId"] = CorrelationId.ToString("D"),
            ["commandId"] = CommandId?.ToString("D"),
            ["actor"] = new JsonObject { ["kind"] = ActorKind, ["id"] = ActorId, ["name"] = ActorName },
            ["action"] = Action,
            ["category"] = Category,
            ["environmentId"] = EnvironmentId == Guid.Empty ? null : EnvironmentId.ToString("D"),
            ["subjectId"] = SubjectId,
            ["outcome"] = Outcome,
            ["failureCode"] = FailureCode,
            ["details"] = SortedDetails(),
            ["test"] = Test,
        }.ToJsonString(JsonOptions);
    }

    private JsonObject SortedDetails()
    {
        var details = new JsonObject();
        foreach (var (key, value) in Details.OrderBy(pair => pair.Key, StringComparer.Ordinal)) details[key] = value;
        return details;
    }
}
