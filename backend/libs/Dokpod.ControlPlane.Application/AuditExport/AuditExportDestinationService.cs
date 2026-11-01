using System.Globalization;
using Dokpod.ControlPlane.Application.Auditing;
using Dokpod.ControlPlane.Application.Licensing;
using Dokpod.Domain.Auditing;

namespace Dokpod.ControlPlane.Application.AuditExport;

/// <summary>Destino como a tela o mostra: configuração, cursor, presença dos arquivos do secret, fila e situação.</summary>
public sealed record AuditExportDestinationView(
    AuditExportDestination Destination,
    AuditExportCursor Cursor,
    AuditExportSecretFiles SecretFiles,
    AuditExportPending Pending,
    string State);

public enum AuditExportServiceOutcome
{
    Ok,
    Created,
    Replayed,
    Deleted,
    InvalidDestination,
    InvalidSecretRef,
    AddressBlocked,
    LicenseDenied,
    NotFound,
    VersionConflict,
    NameTaken,
    LimitReached,
    RateLimited,
}

public sealed record AuditExportServiceResult(
    AuditExportServiceOutcome Outcome,
    AuditExportDestinationView? View = null,
    string? DenialCode = null,
    AuditExportSendResult? TestResult = null);

/// <summary>Quem age, já autorizado pelo Keycloak no recurso da instalação.</summary>
public sealed record AuditExportCaller(string ActorId, string? ActorName, Guid CorrelationId);

/// <summary>
/// Gestão dos destinos de exportação (spec 027 T019). Criar, editar, reativar e testar passam pelo gate da capability
/// <c>audit-siem-export</c>; desativar (só <c>active: false</c>) e excluir nunca, para o administrador poder parar o
/// envio com a licença restrita (FR-008). Cada mudança grava <c>audit_export.*</c> na mesma transação, no ambiente
/// reservado da instalação (D-012) e sem segredos: do secret, só se há <c>secretRef</c>.
/// </summary>
[BusinessCapability(AuditExportAuthorization.Capability)]
public sealed class AuditExportDestinationService(
    IAuditExportStore store,
    IAuditTrailExportReader trail,
    IBusinessCapabilityGate gate,
    IAuditEventWriter audit,
    IDestinationAddressResolver resolver,
    IAuditExportSecretReader secrets,
    IEnumerable<IAuditExportTransport> transports,
    IAuditExportInstallation installation,
    IAuditExportTestLimiter testLimiter,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<AuditExportDestinationView>> ListAsync(CancellationToken cancellationToken)
    {
        var views = new List<AuditExportDestinationView>();
        foreach (var state in await store.ListAsync(cancellationToken).ConfigureAwait(false))
        {
            views.Add(await ViewAsync(state, cancellationToken).ConfigureAwait(false));
        }

        return views;
    }

    public async Task<AuditExportServiceResult> CreateAsync(
        AuditExportDestinationInput input, string idempotencyKey, AuditExportCaller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var (normalized, error) = input.Validate();
        if (normalized is null) return Invalid(error);
        if (await DeniedAsync(cancellationToken).ConfigureAwait(false) is { } denied) return denied;
        if (await AddressBlockedAsync(normalized.Host, cancellationToken).ConfigureAwait(false)) return new(AuditExportServiceOutcome.AddressBlocked);

        var now = timeProvider.GetUtcNow();
        var result = await store.CreateAsync(normalized, idempotencyKey, now, cancellationToken, (destination, ct) =>
            AppendAsync("audit_export.destination_created", destination, caller, now, Describe(destination), ct)).ConfigureAwait(false);
        return result.Outcome switch
        {
            AuditExportWriteOutcome.Created => await ResultAsync(AuditExportServiceOutcome.Created, result.Destination!, cancellationToken).ConfigureAwait(false),
            AuditExportWriteOutcome.Replayed => await ResultAsync(AuditExportServiceOutcome.Replayed, result.Destination!, cancellationToken).ConfigureAwait(false),
            _ => new(Map(result.Outcome)),
        };
    }

    public async Task<AuditExportServiceResult> UpdateAsync(
        Guid destinationId, AuditExportDestinationInput input, long expectedVersion, AuditExportCaller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var (normalized, error) = input.Validate();
        if (normalized is null) return Invalid(error);
        var existing = await store.GetAsync(destinationId, cancellationToken).ConfigureAwait(false);
        if (existing is null) return new(AuditExportServiceOutcome.NotFound);
        if (existing.Destination.Version != expectedVersion) return new(AuditExportServiceOutcome.VersionConflict);

        var changed = Changes(existing.Destination, normalized);
        // Desativar sem outra mudança não passa pelo gate nem pela resolução do host (FR-008).
        if (normalized.Active || changed.Any(field => field != "active"))
        {
            if (await DeniedAsync(cancellationToken).ConfigureAwait(false) is { } denied) return denied;
            if (await AddressBlockedAsync(normalized.Host, cancellationToken).ConfigureAwait(false)) return new(AuditExportServiceOutcome.AddressBlocked);
        }

        var now = timeProvider.GetUtcNow();
        var result = await store.UpdateAsync(destinationId, normalized, expectedVersion, now, cancellationToken, (destination, ct) =>
        {
            var details = Describe(destination);
            details["changed"] = changed.Count == 0 ? "-" : string.Join(',', changed);
            return AppendAsync("audit_export.destination_updated", destination, caller, now, details, ct);
        }).ConfigureAwait(false);
        return result.Outcome == AuditExportWriteOutcome.Updated
            ? await ResultAsync(AuditExportServiceOutcome.Ok, result.Destination!, cancellationToken).ConfigureAwait(false)
            : new(Map(result.Outcome));
    }

    public async Task<AuditExportServiceResult> DeleteAsync(Guid destinationId, long expectedVersion, AuditExportCaller caller, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var outcome = await store.DeleteAsync(destinationId, expectedVersion, cancellationToken, (destination, ct) =>
            AppendAsync("audit_export.destination_deleted", destination, caller, now, new Dictionary<string, string>
            {
                ["destinationId"] = destination.DestinationId.ToString("D"),
                ["name"] = destination.Name,
            }, ct)).ConfigureAwait(false);
        return new(outcome == AuditExportWriteOutcome.Deleted ? AuditExportServiceOutcome.Deleted : Map(outcome));
    }

    /// <summary>
    /// Envia um evento de teste (<c>audit_export.test</c>, <c>test: true</c>), que não entra na trilha; só
    /// <c>audit_export.test_sent</c> é gravado, com o resultado e o código de erro (threat model L8).
    /// </summary>
    public async Task<AuditExportServiceResult> TestAsync(Guid destinationId, AuditExportCaller caller, CancellationToken cancellationToken)
    {
        var existing = await store.GetAsync(destinationId, cancellationToken).ConfigureAwait(false);
        if (existing is null) return new(AuditExportServiceOutcome.NotFound);
        if (await DeniedAsync(cancellationToken).ConfigureAwait(false) is { } denied) return denied;
        if (!testLimiter.TryAcquire()) return new(AuditExportServiceOutcome.RateLimited);

        var destination = existing.Destination;
        var now = timeProvider.GetUtcNow();
        var record = AuditExportRecord.TestEvent(
            await installation.GetIdAsync(cancellationToken).ConfigureAwait(false), caller.CorrelationId, caller.ActorId, caller.ActorName, now);
        var transport = transports.FirstOrDefault(candidate => candidate.Protocol == destination.Protocol);
        var sent = transport is null
            ? AuditExportSendResult.Failed(AuditExportErrorCodes.Rejected)
            : await transport.SendAsync(destination, [record], cancellationToken).ConfigureAwait(false);

        var details = new Dictionary<string, string>
        {
            ["destinationId"] = destination.DestinationId.ToString("D"),
            ["delivered"] = sent.Delivered ? "true" : "false",
        };
        if (sent.ErrorCode is not null) details["errorCode"] = sent.ErrorCode;
        await AppendAsync("audit_export.test_sent", destination, caller, now, details, cancellationToken, sent.ErrorCode).ConfigureAwait(false);
        return new(AuditExportServiceOutcome.Ok, TestResult: sent);
    }

    /// <summary>Situação de data-model.md: desativado, lacuna recente, falha, atraso de mais de um lote ou em dia.</summary>
    public static string StateOf(AuditExportDestination destination, AuditExportCursor cursor, AuditExportPending pending, DateTimeOffset nowUtc) =>
        !destination.Active ? AuditExportStates.Disabled
        : cursor.LastGapAtUtc is { } gap && nowUtc - gap < AuditExportLimits.GapNoticeWindow ? AuditExportStates.Gap
        : cursor.LastErrorCode is not null ? AuditExportStates.Failing
        : pending.Count > AuditExportLimits.MaxBatch ? AuditExportStates.CatchingUp
        : AuditExportStates.Ok;

    private async Task<AuditExportDestinationView> ViewAsync(AuditExportDestinationState state, CancellationToken cancellationToken)
    {
        var pending = await trail.PendingAsync(state.Cursor.Position, state.Destination.Actions, AuditExportLimits.MaxPendingCount, cancellationToken).ConfigureAwait(false);
        return new AuditExportDestinationView(
            state.Destination,
            state.Cursor,
            secrets.Describe(state.Destination.SecretRef),
            pending,
            StateOf(state.Destination, state.Cursor, pending, timeProvider.GetUtcNow()));
    }

    private async Task<AuditExportServiceResult> ResultAsync(AuditExportServiceOutcome outcome, AuditExportDestination destination, CancellationToken cancellationToken)
    {
        var state = await store.GetAsync(destination.DestinationId, cancellationToken).ConfigureAwait(false);
        return state is null ? new(AuditExportServiceOutcome.NotFound) : new(outcome, await ViewAsync(state, cancellationToken).ConfigureAwait(false));
    }

    private async Task<AuditExportServiceResult?> DeniedAsync(CancellationToken cancellationToken)
    {
        var decision = await gate.EnsureAsync(AuditExportAuthorization.Capability, cancellationToken).ConfigureAwait(false);
        return decision.Allowed ? null : new(AuditExportServiceOutcome.LicenseDenied, DenialCode: decision.DenialCode);
    }

    /// <summary>
    /// Recusa só o que resolve para endereço proibido. Um nome que ainda não resolve é aceito: a política vale de novo
    /// a cada conexão (D-007).
    /// </summary>
    private async Task<bool> AddressBlockedAsync(string host, CancellationToken cancellationToken) =>
        (await resolver.ResolveAsync(host, cancellationToken).ConfigureAwait(false)).ErrorCode == AuditExportErrorCodes.AddressBlocked;

    private Task AppendAsync(
        string action, AuditExportDestination destination, AuditExportCaller caller, DateTimeOffset now,
        Dictionary<string, string> details, CancellationToken cancellationToken, string? failureCode = null)
    {
        var auditEvent = AuditEvent.Create(
                Guid.NewGuid(), now, caller.CorrelationId, AuditActorKind.User, caller.ActorId, action, AuditEvent.InstallationScopeId,
                failureCode is null ? AuditOutcome.Succeeded : AuditOutcome.Failed, failureCode)
            .WithSubject(destination.DestinationId.ToString("D"), details)
            .WithActorName(caller.ActorName);
        return audit.AppendAsync(auditEvent, cancellationToken);
    }

    private static Dictionary<string, string> Describe(AuditExportDestination destination) => new()
    {
        ["destinationId"] = destination.DestinationId.ToString("D"),
        ["name"] = destination.Name,
        ["protocol"] = destination.Protocol,
        ["host"] = destination.Host,
        ["port"] = destination.Port.ToString(CultureInfo.InvariantCulture),
        ["categories"] = string.Join(',', destination.Categories),
        ["hasSecretRef"] = destination.SecretRef is null ? "false" : "true",
        ["active"] = destination.Active ? "true" : "false",
    };

    private static List<string> Changes(AuditExportDestination before, AuditExportDestinationInput after)
    {
        var changed = new List<string>();
        if (before.Name != after.Name) changed.Add("name");
        if (before.Protocol != after.Protocol) changed.Add("protocol");
        if (before.Host != after.Host) changed.Add("host");
        if (before.Port != after.Port) changed.Add("port");
        if (before.Path != after.Path) changed.Add("path");
        if (before.SecretRef != after.SecretRef) changed.Add("secretRef");
        if (!before.Categories.SequenceEqual(after.Categories, StringComparer.Ordinal)) changed.Add("categories");
        if (before.Active != after.Active) changed.Add("active");
        return changed;
    }

    private static AuditExportServiceResult Invalid(string? error) =>
        new(error == AuditExportDestinationInput.InvalidSecretRef ? AuditExportServiceOutcome.InvalidSecretRef : AuditExportServiceOutcome.InvalidDestination);

    private static AuditExportServiceOutcome Map(AuditExportWriteOutcome outcome) => outcome switch
    {
        AuditExportWriteOutcome.NotFound => AuditExportServiceOutcome.NotFound,
        AuditExportWriteOutcome.VersionConflict => AuditExportServiceOutcome.VersionConflict,
        AuditExportWriteOutcome.NameTaken => AuditExportServiceOutcome.NameTaken,
        AuditExportWriteOutcome.LimitReached => AuditExportServiceOutcome.LimitReached,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
    };
}
