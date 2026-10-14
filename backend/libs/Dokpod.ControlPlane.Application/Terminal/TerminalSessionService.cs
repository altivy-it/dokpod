using System.Globalization;
using System.Security.Claims;
using System.Threading.Channels;
using Dokpod.Agent.Contracts.V1;
using Dokpod.ControlPlane.Application.Auditing;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Environments;
using Dokpod.ControlPlane.Application.Inventory;
using Dokpod.ControlPlane.Application.Queries;
using Dokpod.Domain.Auditing;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Inventory;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace Dokpod.ControlPlane.Application.Terminal;

public sealed record TerminalPolicyResult(bool Allowed, string? Reason, bool RequiresJustification, bool RequiresRecentMfa, TerminalOptions Limits);

/// <summary>
/// Terminal interativo (spec 014 T008-T010): política (scope, estágio, MFA, limites, container, capability),
/// pedido ao agente, retransmissão dos frames, ociosidade/duração com aviso de 1 min, reavaliação periódica com o
/// token mais recente e auditoria `requested/started/ended/denied` sem conteúdo.
/// </summary>
public sealed class TerminalSessionService(
    IEnvironmentAuthorizationDecider authorizationDecider,
    IEnvironmentRegistrationStore registrationStore,
    IContainerSearchStore containers,
    AgentQueryDispatcher dispatcher,
    TerminalSessionRegistry registry,
    IAuditEventWriter auditWriter,
    TerminalOptions options,
    TimeProvider timeProvider)
{
    public const int MinJustification = 10;
    public const int MaxJustification = 500;

    public async Task<TerminalPolicyResult> EvaluatePolicyAsync(
        Guid environmentId,
        string containerId,
        AuthenticatedActor actor,
        string accessToken,
        ClaimsPrincipal? user,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var (reason, registration) = await CheckAsync(environmentId, containerId, actor, accessToken, user, correlationId, cancellationToken).ConfigureAwait(false);
        return new TerminalPolicyResult(reason is null, reason, RequiresJustification(registration), options.RequireRecentMfa, options);
    }

    private bool RequiresJustification(EnvironmentRegistration? registration) =>
        registration?.Stage is { } stage && options.RequireJustificationStages.Contains(EnvironmentStages.ToValue(stage), StringComparer.OrdinalIgnoreCase);

    private async Task<(string? Reason, EnvironmentRegistration? Registration)> CheckAsync(
        Guid environmentId,
        string containerId,
        AuthenticatedActor actor,
        string accessToken,
        ClaimsPrincipal? user,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        if (environmentId == Guid.Empty || containerId.Length != 64 || !containerId.All(Uri.IsHexDigit)) return ("invalid_request", null);
        var decision = await DecideAsync(environmentId, actor, accessToken, correlationId, cancellationToken).ConfigureAwait(false);
        if (decision != AuthorizationDecisionOutcome.Allowed) return (decision == AuthorizationDecisionOutcome.Denied ? "scope" : "authorization_unavailable", null);
        var registration = await registrationStore.GetAsync(environmentId, cancellationToken).ConfigureAwait(false);
        if (registration is null || !registration.Enabled) return ("not_found", registration);
        if (options.RequireRecentMfa && !HasRecentMfa(user)) return ("mfa_required", registration);
        if (!dispatcher.IsConnected(environmentId)) return ("agent_disconnected", registration);
        if (!dispatcher.Supports(environmentId, Capability.ContainerExec)) return ("not_supported", registration);
        var container = await containers.GetAsync(environmentId, containerId, cancellationToken).ConfigureAwait(false);
        if (container is null) return ("not_found", registration);
        if (ContainerDisplayStatuses.NormalizeState(container.State) != "running") return ("not_running", registration);
        return (null, registration);
    }

    private bool HasRecentMfa(ClaimsPrincipal? user)
    {
        var acr = user?.FindFirst("acr")?.Value;
        var amr = user?.FindAll("amr").Select(c => c.Value).ToArray() ?? [];
        var mfa = acr is not null && (acr.Contains("mfa", StringComparison.OrdinalIgnoreCase) || acr is "2" or "3") || amr.Any(v => v is "mfa" or "otp" or "hwk");
        if (!mfa) return false;
        return long.TryParse(user?.FindFirst("auth_time")?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var authTime) &&
            timeProvider.GetUtcNow() - DateTimeOffset.FromUnixTimeSeconds(authTime) <= options.RecentMfaMaxAge;
    }

    private async Task<AuthorizationDecisionOutcome> DecideAsync(Guid environmentId, AuthenticatedActor actor, string accessToken, Guid correlationId, CancellationToken cancellationToken)
    {
        try
        {
            var decision = await authorizationDecider.DecideAsync(
                $"urn:dokpod:environment:{environmentId:D}", EnvironmentResourceScopes.ExecContainer, actor, accessToken, correlationId, cancellationToken).ConfigureAwait(false);
            return decision.Outcome;
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

    /// <summary>Executa a sessão até o fim, escrevendo a saída em `output`; nunca registra o conteúdo.</summary>
    public async Task RunAsync(
        TerminalOpenRequest request,
        AuthenticatedActor actor,
        string accessToken,
        DateTimeOffset? tokenExpiresAt,
        ClaimsPrincipal? user,
        bool webSocket,
        Guid correlationId,
        IAsyncEnumerable<TerminalInputFrame> input,
        ChannelWriter<TerminalOutputFrame> output,
        CancellationToken cancellationToken)
    {
        var container = request.ContainerId ?? string.Empty;
        async Task DenyAsync(string reason)
        {
            await AuditAsync("container.exec.denied", request.EnvironmentId, container, actor, correlationId, false, reason, new Dictionary<string, string> { ["reason"] = reason }).ConfigureAwait(false);
            await output.WriteAsync(new TerminalOutputFrame(null, new TerminalStatus("error", reason)), CancellationToken.None).ConfigureAwait(false);
        }

        if (!webSocket)
        {
            await DenyAsync("websocket_required").ConfigureAwait(false);
            return;
        }

        var (reason, registration) = await CheckAsync(request.EnvironmentId, container, actor, accessToken, user, correlationId, cancellationToken).ConfigureAwait(false);
        if (reason is null && request.Cols is < 10 or > 1000 || request.Rows is < 2 or > 500) reason ??= "invalid_request";
        var justification = request.Justification?.Trim();
        if (reason is null && RequiresJustification(registration) && justification is not { Length: >= MinJustification and <= MaxJustification })
        {
            reason = "justification_missing";
        }

        if (reason is not null)
        {
            if (reason is "invalid_request" or "not_found") await output.WriteAsync(new TerminalOutputFrame(null, new TerminalStatus("error", reason)), CancellationToken.None).ConfigureAwait(false);
            else await DenyAsync(reason).ConfigureAwait(false);
            return;
        }

        var startedAt = timeProvider.GetUtcNow();
        var session = new TerminalSession(Guid.NewGuid(), actor, request.EnvironmentId, container, startedAt);
        session.SetAuthorization(accessToken, tokenExpiresAt);
        if (registry.TryReserve(session) is { } limit)
        {
            await DenyAsync(limit).ConfigureAwait(false);
            return;
        }

        var requested = new Dictionary<string, string> { ["sessionId"] = session.SessionId.ToString("D") };
        if (registration?.Stage is { } stage) requested["stage"] = EnvironmentStages.ToValue(stage);
        if (!string.IsNullOrEmpty(justification)) requested["justification"] = JustificationMasker.Mask(justification[..Math.Min(justification.Length, MaxJustification)]);
        long bytesIn = 0, bytesOut = 0;
        var endReason = "error";
        var opened = false;
        var orphan = false;
        try
        {
            await AuditAsync("container.exec.requested", request.EnvironmentId, container, actor, correlationId, true, null, requested).ConfigureAwait(false);
            await output.WriteAsync(new TerminalOutputFrame(null, new TerminalStatus("connecting", SessionId: session.SessionId)), cancellationToken).ConfigureAwait(false);
            var sent = await dispatcher.TrySendAsync(request.EnvironmentId, new ControlPlaneMessage
            {
                ExecOpen = new ExecOpenRequest
                {
                    ExecSessionId = session.SessionId.ToString("D"),
                    ContainerId = container,
                    Cols = (uint)request.Cols,
                    Rows = (uint)request.Rows,
                    ExpiresAt = Timestamp.FromDateTimeOffset(startedAt + options.MaxDuration),
                    MaxOutputBytesPerSecond = (uint)options.MaxOutputBytesPerSecond,
                },
            }, Capability.ContainerExec, cancellationToken).ConfigureAwait(false);
            if (!sent)
            {
                endReason = "agent_disconnected";
                await DenyAsync(endReason).ConfigureAwait(false);
                return;
            }

            using (var attach = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                attach.CancelAfter(options.AgentAttachTimeout);
                try
                {
                    await session.Attached.Task.WaitAsync(attach.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    endReason = "agent_timeout";
                    await DenyAsync(endReason).ConfigureAwait(false);
                    return;
                }
            }

            var first = await ReadStatusAsync(session, cancellationToken).ConfigureAwait(false);
            if (first?.State != ExecState.Opened)
            {
                endReason = string.IsNullOrEmpty(first?.Reason) ? "error" : first.Reason;
                await DenyAsync(endReason).ConfigureAwait(false);
                return;
            }

            opened = true;
            await AuditAsync("container.exec.started", request.EnvironmentId, container, actor, correlationId, true, null, new Dictionary<string, string>
            {
                ["sessionId"] = session.SessionId.ToString("D"),
                ["shell"] = first.Shell is "/bin/bash" or "/bin/sh" ? first.Shell : "unknown",
                ["root"] = first.ContainerUserIsRoot ? "true" : "false",
            }).ConfigureAwait(false);
            await output.WriteAsync(new TerminalOutputFrame(null, new TerminalStatus("connected", null, first.Shell, first.ContainerUserIsRoot, SessionId: session.SessionId)), cancellationToken).ConfigureAwait(false);

            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            long lastInputTicks = timeProvider.GetUtcNow().UtcTicks;
            var inputTask = Task.Run(async () =>
            {
                await foreach (var frame in input.WithCancellation(lifetime.Token).ConfigureAwait(false))
                {
                    if (frame.Data is { Length: > 0 and <= 64 * 1024 } data)
                    {
                        Interlocked.Add(ref bytesIn, data.Length);
                        Interlocked.Exchange(ref lastInputTicks, timeProvider.GetUtcNow().UtcTicks);
                        await session.ToAgent.Writer.WriteAsync(new ExecFrame { Stdin = ByteString.CopyFrom(data) }, lifetime.Token).ConfigureAwait(false);
                    }
                    else if (frame.Cols is { } cols && frame.Rows is { } rows)
                    {
                        await session.ToAgent.Writer.WriteAsync(new ExecFrame { Resize = new ExecResize { Cols = (uint)Math.Clamp(cols, 10, 1000), Rows = (uint)Math.Clamp(rows, 2, 500) } }, lifetime.Token).ConfigureAwait(false);
                    }
                }
            }, CancellationToken.None);

            var nextCheck = timeProvider.GetUtcNow() + options.AuthorizationRecheckInterval;
            var warned = string.Empty;
            while (true)
            {
                if (inputTask.IsCompleted)
                {
                    endReason = "user_closed";
                    break;
                }

                ExecFrame? frame = null;
                using (var wait = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
                {
                    wait.CancelAfter(TimeSpan.FromSeconds(1));
                    try
                    {
                        frame = await session.FromAgent.Reader.ReadAsync(wait.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!lifetime.IsCancellationRequested)
                    {
                    }
                    catch (ChannelClosedException)
                    {
                        endReason = "agent_disconnected";
                        break;
                    }
                }

                if (frame?.FrameCase == ExecFrame.FrameOneofCase.Output)
                {
                    bytesOut += frame.Output.Length;
                    await output.WriteAsync(new TerminalOutputFrame(frame.Output.ToByteArray(), null), lifetime.Token).ConfigureAwait(false);
                }
                else if (frame?.FrameCase == ExecFrame.FrameOneofCase.Status && frame.Status.State is ExecState.Closed or ExecState.Error)
                {
                    endReason = string.IsNullOrEmpty(frame.Status.Reason) ? "exit" : frame.Status.Reason;
                    orphan = frame.Status.OrphanSuspected;
                    break;
                }

                var now = timeProvider.GetUtcNow();
                var idleLeft = new DateTimeOffset(Interlocked.Read(ref lastInputTicks), TimeSpan.Zero) + options.IdleTimeout - now;
                var durationLeft = startedAt + options.MaxDuration - now;
                if (idleLeft <= TimeSpan.Zero)
                {
                    endReason = "idle_timeout";
                    break;
                }

                if (durationLeft <= TimeSpan.Zero)
                {
                    endReason = "max_duration";
                    break;
                }

                var warning = durationLeft <= options.WarningBefore ? "max_duration" : idleLeft <= options.WarningBefore ? "idle_timeout" : string.Empty;
                if (warning.Length > 0 && warning != warned)
                {
                    warned = warning;
                    await output.WriteAsync(new TerminalOutputFrame(null, new TerminalStatus("connected", Warning: warning, SessionId: session.SessionId)), lifetime.Token).ConfigureAwait(false);
                }
                else if (warning.Length == 0)
                {
                    warned = string.Empty;
                }

                if (now >= nextCheck)
                {
                    nextCheck = now + options.AuthorizationRecheckInterval;
                    var (token, expires) = session.Authorization;
                    if (expires is { } exp && now >= exp - TimeSpan.FromSeconds(30))
                    {
                        endReason = "session_refresh_required";
                        break;
                    }

                    var decision = await DecideAsync(request.EnvironmentId, actor, token, correlationId, lifetime.Token).ConfigureAwait(false);
                    if (decision != AuthorizationDecisionOutcome.Allowed)
                    {
                        endReason = decision == AuthorizationDecisionOutcome.Denied ? "permission_revoked" : "authorization_unavailable";
                        break;
                    }
                }
            }

            await lifetime.CancelAsync().ConfigureAwait(false);
            if (endReason != "exit" && endReason != "container_stopped" && endReason != "agent_disconnected")
            {
                // Pede ao agente o término (SIGHUP no shell) e espera a confirmação com o indicador de órfão.
                session.ToAgent.Writer.TryWrite(new ExecFrame { Status = new ExecStatus { State = ExecState.Closed, Reason = endReason } });
                var closed = await ReadStatusAsync(session, CancellationToken.None, TimeSpan.FromSeconds(8)).ConfigureAwait(false);
                orphan = closed?.OrphanSuspected ?? false;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            endReason = "client_disconnected";
            session.ToAgent.Writer.TryWrite(new ExecFrame { Status = new ExecStatus { State = ExecState.Closed, Reason = endReason } });
            var closed = await ReadStatusAsync(session, CancellationToken.None, TimeSpan.FromSeconds(8)).ConfigureAwait(false);
            orphan = closed?.OrphanSuspected ?? false;
        }
        finally
        {
            session.ToAgent.Writer.TryComplete();
            registry.Release(session.SessionId);
            if (opened)
            {
                await AuditAsync("container.exec.ended", request.EnvironmentId, container, actor, correlationId, true, null, new Dictionary<string, string>
                {
                    ["sessionId"] = session.SessionId.ToString("D"),
                    ["reason"] = endReason.Length <= 64 ? endReason : "error",
                    ["durationSeconds"] = ((long)(timeProvider.GetUtcNow() - startedAt).TotalSeconds).ToString(CultureInfo.InvariantCulture),
                    ["bytesIn"] = bytesIn.ToString(CultureInfo.InvariantCulture),
                    ["bytesOut"] = bytesOut.ToString(CultureInfo.InvariantCulture),
                    ["orphanSuspected"] = orphan ? "true" : "false",
                }).ConfigureAwait(false);
                output.TryWrite(new TerminalOutputFrame(null, new TerminalStatus("closed", endReason, SessionId: session.SessionId)));
            }
        }
    }

    /// <summary>Renovação da autorização com token novo vindo do BFF (D-008); só o próprio ator renova.</summary>
    public async Task<bool> RenewAsync(Guid sessionId, AuthenticatedActor actor, string accessToken, DateTimeOffset? expiresAt, Guid correlationId, CancellationToken cancellationToken)
    {
        var session = registry.Find(sessionId);
        if (session is null || session.Actor.Subject != actor.Subject) return false;
        if (await DecideAsync(session.EnvironmentId, actor, accessToken, correlationId, cancellationToken).ConfigureAwait(false) != AuthorizationDecisionOutcome.Allowed) return false;
        session.SetAuthorization(accessToken, expiresAt);
        return true;
    }

    private static async Task<ExecStatus?> ReadStatusAsync(TerminalSession session, CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        wait.CancelAfter(timeout ?? TimeSpan.FromSeconds(15));
        try
        {
            while (true)
            {
                var frame = await session.FromAgent.Reader.ReadAsync(wait.Token).ConfigureAwait(false);
                if (frame.FrameCase == ExecFrame.FrameOneofCase.Status) return frame.Status;
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or ChannelClosedException)
        {
            return null;
        }
    }

    private Task AuditAsync(string action, Guid environmentId, string containerId, AuthenticatedActor actor, Guid correlationId, bool succeeded, string? failureCode, IReadOnlyDictionary<string, string> details)
    {
        var auditEvent = AuditEvent.Create(
            Guid.NewGuid(),
            timeProvider.GetUtcNow(),
            correlationId,
            AuditActorKind.User,
            actor.Subject,
            action,
            environmentId,
            succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            succeeded ? null : failureCode);
        if (containerId.Length is > 0 and <= 255 && containerId.All(Uri.IsHexDigit)) auditEvent = auditEvent.WithSubject(containerId, details);
        return auditWriter.AppendAsync(auditEvent, CancellationToken.None);
    }
}
