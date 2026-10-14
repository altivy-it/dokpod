using System.Runtime.CompilerServices;
using System.Security.Claims;
using Dokpod.ControlPlane.Api.Endpoints;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Queries;
using Dokpod.ControlPlane.Application.Realtime;
using Dokpod.Domain.Environments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Dokpod.ControlPlane.Api.Realtime;

public sealed record InventoryChangedNotification(Guid EnvironmentId, ulong Revision);

[Authorize]
public sealed class ControlPlaneHub(
    IEnvironmentAuthorizationDecider authorizationDecider,
    IServiceScopeFactory scopeFactory,
    AgentQueryDispatcher dispatcher,
    TimeProvider timeProvider) : Hub
{
    public static readonly TimeSpan LiveLogsMaxDuration = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan AuthorizationRecheckInterval = TimeSpan.FromSeconds(60);

    public async Task JoinEnvironmentAsync(string environmentId)
    {
        if (string.IsNullOrWhiteSpace(environmentId)
            || environmentId.Length > 64
            || environmentId.Any(character => !char.IsLetterOrDigit(character) && character != '-'))
        {
            throw new HubException("Identificador de ambiente inválido.");
        }

        var httpContext = Context.GetHttpContext();
        var accessToken = ExtractAccessToken(httpContext);
        var subject = Context.User?.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(subject))
        {
            throw new HubException("Acesso negado.");
        }

        if (!Guid.TryParse(environmentId, out var parsedEnvironmentId))
        {
            throw new HubException("Identificador de ambiente inválido.");
        }

        var decision = await authorizationDecider.DecideAsync(
            $"urn:dokpod:environment:{parsedEnvironmentId:D}",
            EnvironmentResourceScopes.Read,
            AuthenticatedActor.FromSubject(subject),
            accessToken,
            GetCorrelationId(httpContext),
            Context.ConnectionAborted);
        if (!decision.Allowed)
        {
            throw new HubException("Acesso negado.");
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GroupFor(environmentId),
            Context.ConnectionAborted);
    }

    /// <summary>
    /// Logs ao vivo (spec 011 FR-005): autorização `container:logs` no início e a cada 60 s, duração
    /// máxima de 15 min; cancelamento do cliente, logout ou revogação encerram a sessão no agente.
    /// </summary>
    public async IAsyncEnumerable<object> StreamContainerLogs(
        string environmentId,
        string containerId,
        string? contains,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var httpContext = Context.GetHttpContext();
        var accessToken = ExtractAccessToken(httpContext);
        var subject = Context.User?.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(subject) ||
            !Guid.TryParse(environmentId, out var envId) || contains is { Length: > ContainerInsightsService.MaxContainsLength })
        {
            throw new HubException("Requisição inválida.");
        }

        var actor = AuthenticatedActor.FromSubject(subject);
        var correlationId = GetCorrelationId(httpContext);
        using var scope = scopeFactory.CreateScope();
        var insights = scope.ServiceProvider.GetRequiredService<ContainerInsightsService>();
        var access = await insights.AuthorizeAsync(envId, containerId, EnvironmentResourceScopes.ReadContainerLogs, actor, accessToken, correlationId, cancellationToken);
        if (access.Access == InsightAccess.NotFound) throw new HubException("container_not_found");
        if (access.Access != InsightAccess.Allowed) throw new HubException("authorization_denied");

        var startedAt = timeProvider.GetUtcNow();
        var (outcome, handle) = await dispatcher.StartLogStreamAsync(
            envId, containerId, string.IsNullOrEmpty(contains) ? null : contains, subject, startedAt + LiveLogsMaxDuration, cancellationToken);
        if (outcome != LogStreamStartOutcome.Started || handle is null)
        {
            throw new HubException(outcome switch
            {
                LogStreamStartOutcome.AgentDisconnected => "agent_disconnected",
                LogStreamStartOutcome.NotSupported => "not_supported",
                _ => "too_many_requests",
            });
        }

        await insights.AuditAsync("container.logs.stream_started", envId, containerId, actor, correlationId, true, null,
            new Dictionary<string, string> { ["sessionId"] = handle.SessionId, ["filtered"] = string.IsNullOrEmpty(contains) ? "false" : "true" },
            CancellationToken.None);

        var reason = "client_closed";
        long lines = 0;
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lifetime.CancelAfter(LiveLogsMaxDuration);
        var monitor = new StreamingAuthorizationMonitor(timeProvider, TokenExpiresAt(Context.User), AuthorizationRecheckInterval);
        try
        {
            while (true)
            {
                Dokpod.Agent.Contracts.V1.LogChunk? chunk;
                using (var wait = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
                {
                    wait.CancelAfter(TimeSpan.FromSeconds(5));
                    try
                    {
                        chunk = await handle.Chunks.ReadAsync(wait.Token);
                    }
                    catch (OperationCanceledException) when (!lifetime.IsCancellationRequested)
                    {
                        chunk = null;
                    }
                    catch (System.Threading.Channels.ChannelClosedException)
                    {
                        reason = "agent_ended";
                        break;
                    }
                }

                if (monitor.IsDue)
                {
                    var state = await monitor.CheckAsync(
                        async token =>
                        {
                            var recheck = await insights.AuthorizeAsync(envId, containerId, EnvironmentResourceScopes.ReadContainerLogs, actor, accessToken, correlationId, token);
                            return recheck.Access switch
                            {
                                InsightAccess.Allowed => AuthorizationDecisionOutcome.Allowed,
                                InsightAccess.Unavailable => AuthorizationDecisionOutcome.Indeterminate,
                                _ => AuthorizationDecisionOutcome.Denied,
                            };
                        },
                        lifetime.Token);
                    if (state != StreamingAuthorizationState.Allowed)
                    {
                        reason = StreamingAuthorizationMonitor.EndReason(state);
                        yield return new { sessionId = handle.SessionId, lines = Array.Empty<object>(), omittedLines = 0, ended = true, endReason = reason };
                        break;
                    }
                }

                if (chunk is null) continue;
                lines += chunk.Lines.Count;
                yield return new
                {
                    sessionId = handle.SessionId,
                    lines = chunk.Lines.Select(LogResponses.ToView).ToArray(),
                    omittedLines = chunk.OmittedLines,
                    ended = chunk.Ended,
                    endReason = string.IsNullOrEmpty(chunk.EndReason) ? null : chunk.EndReason,
                };
                if (chunk.Ended)
                {
                    reason = chunk.EndReason;
                    break;
                }
            }
        }
        finally
        {
            if (lifetime.IsCancellationRequested && !cancellationToken.IsCancellationRequested) reason = "expired";
            await dispatcher.StopLogStreamAsync(handle.SessionId);
            await insights.AuditAsync("container.logs.stream_ended", envId, containerId, actor, correlationId, true, null,
                new Dictionary<string, string>
                {
                    ["sessionId"] = handle.SessionId,
                    ["reason"] = string.IsNullOrEmpty(reason) ? "ended" : reason,
                    ["lines"] = ContainerInsightsService.Number(lines),
                    ["durationSeconds"] = ContainerInsightsService.Number((long)(timeProvider.GetUtcNow() - startedAt).TotalSeconds),
                },
                CancellationToken.None);
        }
    }

    public static string GroupFor(string environmentId) => $"environment-{environmentId}";

    /// <summary>`exp` do access token da conexão (validado na abertura do hub).</summary>
    internal static DateTimeOffset? TokenExpiresAt(ClaimsPrincipal? user) =>
        long.TryParse(user?.FindFirstValue("exp"), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;

    private static string? ExtractAccessToken(HttpContext? context)
    {
        var header = context?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(header)
            && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return header["Bearer ".Length..].Trim();
        }

        return null;
    }

    private static Guid GetCorrelationId(HttpContext? context) =>
        Guid.TryParse(context?.Request.Headers["X-Correlation-Id"], out var correlationId)
            ? correlationId
            : Guid.NewGuid();
}
