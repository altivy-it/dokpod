namespace Dokpod.Domain.Auditing;

public enum AuditActorKind
{
    User,
    Agent,
    System,
}

public enum AuditOutcome
{
    Succeeded,
    Failed,
    Denied,
    Indeterminate,
}

public sealed record AuditEvent
{
    public const int MaxActorIdLength = 128;
    public const int MaxFailureCodeLength = 128;

    private static readonly HashSet<string> SupportedActions =
    [
        "environment.register",
        "environment.approve",
        "environment.suspend",
        "environment.revoke",
        "environment.stage_changed",
        "container.inspected",
        "container.logs.read",
        "container.logs.stream_started",
        "container.logs.stream_ended",
        "container.start",
        "container.stop",
        "container.restart",
        "container.delete",
        "container.pause",
        "container.unpause",
        "container.command.result",
        "image.pull",
        "container.create.requested",
        "container.exec.requested",
        "container.exec.started",
        "container.exec.ended",
        "container.exec.denied",
    ];

    private static readonly HashSet<string> CommandActions =
    [
        "container.start",
        "container.stop",
        "container.restart",
        "container.delete",
        "container.pause",
        "container.unpause",
        "container.command.result",
        "image.pull",
        "container.create.requested",
    ];

    private AuditEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        Guid correlationId,
        AuditActorKind actorKind,
        string actorId,
        string action,
        Guid environmentId,
        AuditOutcome outcome,
        string? failureCode,
        Guid? commandId)
    {
        EventId = eventId;
        OccurredAtUtc = occurredAtUtc;
        CorrelationId = correlationId;
        ActorKind = actorKind;
        ActorId = actorId;
        Action = action;
        EnvironmentId = environmentId;
        Outcome = outcome;
        FailureCode = failureCode;
        CommandId = commandId;
    }

    public Guid EventId { get; }
    public DateTimeOffset OccurredAtUtc { get; }
    public Guid CorrelationId { get; }
    public AuditActorKind ActorKind { get; }
    public string ActorId { get; }
    public string Action { get; }
    public Guid EnvironmentId { get; }
    public AuditOutcome Outcome { get; }
    public string? FailureCode { get; }
    public Guid? CommandId { get; }

    /// <summary>Recurso afetado (ex.: container) quando a ação não tem comando (spec 011 D-006).</summary>
    public string? SubjectId { get; private init; }

    /// <summary>Metadados permitidos por ação: somente números, booleanos e enums, nunca texto livre.</summary>
    public IReadOnlyDictionary<string, string>? Details { get; private init; }

    public const int MaxDetailsBytes = 2560;

    private static readonly HashSet<string> AllowedDetailKeys =
    [
        "kind", "tail", "since", "until", "filtered", "lines", "bytes", "truncated", "sessionId", "reason", "durationSeconds", "previousStage", "stage",
        "command", "image", "name", "engine",
        "justification", "shell", "root", "bytesIn", "bytesOut", "orphanSuspected", "stage",
    ];

    /// <summary>Justificativa do terminal (spec 014 D-009): única exceção de texto livre além do comando equivalente.</summary>
    public const int MaxJustificationDetailLength = 500;

    /// <summary>Comando equivalente redigido (spec 013 FR-008) cabe em um detalhe maior que os demais.</summary>
    public const int MaxCommandDetailLength = 1800;

    public AuditEvent WithSubject(string subjectId, IReadOnlyDictionary<string, string>? details)
    {
        var subject = NormalizeIdentifier(subjectId, nameof(subjectId), 255)
            ?? throw new ArgumentException("Audit subject is required.", nameof(subjectId));
        Dictionary<string, string>? clean = null;
        if (details is not null)
        {
            clean = new Dictionary<string, string>(StringComparer.Ordinal);
            var size = 0;
            foreach (var (key, value) in details)
            {
                if (!AllowedDetailKeys.Contains(key))
                {
                    throw new ArgumentException($"Audit detail '{key}' is not allowed.", nameof(details));
                }

                var normalized = NormalizeIdentifier(value, nameof(details), key switch
                {
                    "command" => MaxCommandDetailLength,
                    "justification" => MaxJustificationDetailLength,
                    _ => 128,
                }) ?? string.Empty;
                size += key.Length + normalized.Length;
                if (size > MaxDetailsBytes)
                {
                    throw new ArgumentException("Audit details exceed the size limit.", nameof(details));
                }

                clean[key] = normalized;
            }
        }

        return new AuditEvent(EventId, OccurredAtUtc, CorrelationId, ActorKind, ActorId, Action, EnvironmentId, Outcome, FailureCode, CommandId)
        {
            SubjectId = subject,
            Details = clean,
        };
    }

    public static AuditEvent Create(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        Guid correlationId,
        AuditActorKind actorKind,
        string actorId,
        string action,
        Guid environmentId,
        AuditOutcome outcome,
        string? failureCode = null,
        Guid? commandId = null)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("Audit event ID is required.", nameof(eventId));
        }

        if (occurredAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Audit timestamp must be UTC.", nameof(occurredAtUtc));
        }

        if (correlationId == Guid.Empty)
        {
            throw new ArgumentException("Correlation ID is required.", nameof(correlationId));
        }

        if (!Enum.IsDefined(actorKind))
        {
            throw new ArgumentOutOfRangeException(nameof(actorKind));
        }

        var normalizedActorId = NormalizeIdentifier(actorId, nameof(actorId), MaxActorIdLength);
        if (normalizedActorId is null)
        {
            throw new ArgumentException("Audit actor ID is required.", nameof(actorId));
        }

        if (!SupportedActions.Contains(action))
        {
            throw new ArgumentException("Unsupported audit action.", nameof(action));
        }

        if (environmentId == Guid.Empty)
        {
            throw new ArgumentException("Environment ID is required.", nameof(environmentId));
        }

        if (!Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }

        if (commandId == Guid.Empty || (CommandActions.Contains(action) && commandId is null))
        {
            throw new ArgumentException("Command audit actions require a command ID.", nameof(commandId));
        }

        var normalizedFailureCode = NormalizeIdentifier(failureCode, nameof(failureCode), MaxFailureCodeLength);
        if (outcome is AuditOutcome.Failed or AuditOutcome.Denied or AuditOutcome.Indeterminate &&
            normalizedFailureCode is null)
        {
            throw new ArgumentException("Unsuccessful audit events require a failure code.", nameof(failureCode));
        }

        return new AuditEvent(
            eventId,
            occurredAtUtc,
            correlationId,
            actorKind,
            normalizedActorId,
            action,
            environmentId,
            outcome,
            normalizedFailureCode,
            commandId);
    }

    private static string? NormalizeIdentifier(string? value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (value.Any(char.IsControl))
        {
            throw new ArgumentException("Audit identifier has an invalid format.", parameterName);
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException("Audit identifier has an invalid format.", parameterName);
        }

        return normalized;
    }
}
