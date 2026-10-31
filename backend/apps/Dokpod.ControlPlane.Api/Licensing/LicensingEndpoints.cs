using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Licensing;
using Dokpod.Licensing;
using static Dokpod.ControlPlane.Api.Endpoints.ConsoleEndpoints;

namespace Dokpod.ControlPlane.Api.Licensing;

public sealed record TrustedInstantResetRequest(string? Reason);

/// <summary>Chaves públicas embutidas em <c>Licensing/TrustStore/&lt;kid&gt;.pem</c> (T027); nenhuma chave privada.</summary>
public static class EmbeddedTrustStore
{
    private const string Prefix = "Dokpod.TrustStore.";

    public static TrustStore Load(Assembly? assembly = null)
    {
        assembly ??= typeof(EmbeddedTrustStore).Assembly;
        var keys = new List<TrustedKey>();
        foreach (var name in assembly.GetManifestResourceNames().Where(name => name.StartsWith(Prefix, StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            keys.Add(TrustedKey.FromPublicKeyPem(name[Prefix.Length..], reader.ReadToEnd()));
        }

        return new TrustStore(keys);
    }
}

/// <summary>Até 10 uploads de licença ou revogação por minuto por sujeito (threat model L4), em memória da réplica.</summary>
public sealed class LicensingUploadRateLimiter : IDisposable
{
    public const int PermitsPerMinute = 10;

    private readonly PartitionedRateLimiter<string> limiter = PartitionedRateLimiter.Create<string, string>(subject =>
        RateLimitPartition.GetFixedWindowLimiter(subject, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = PermitsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));

    public bool TryAcquire(string subject)
    {
        using var lease = limiter.AttemptAcquire(subject);
        return lease.IsAcquired;
    }

    public void Dispose() => limiter.Dispose();
}

/// <summary>
/// <c>/api/v1/licensing</c> da composição Business (spec 031 T027). Ordem fixa: autenticar, autorizar no Keycloak
/// (<c>urn:dokpod:installation</c>), avaliar e persistir (FR-022). O conteúdo do arquivo nunca aparece em resposta,
/// log ou problema (FR-026, FR-027).
/// </summary>
public static partial class LicensingEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet("/api/v1/licensing/status", StatusAsync).RequireAuthorization();
        endpoints.MapPut("/api/v1/licensing/license", ImportAsync).RequireAuthorization();
        endpoints.MapPost("/api/v1/licensing/trusted-instant", ResetTrustedInstantAsync).RequireAuthorization();
        endpoints.MapPost("/api/v1/licensing/revocations", ImportRevocationAsync).RequireAuthorization();
        endpoints.MapGet("/api/v1/licensing/report", ExportReportAsync).RequireAuthorization();
    }

    /// <summary>
    /// Relatório de apuração para exportação local (T060): cadeia completa com o desafio do fornecedor no último evento.
    /// Hosts aparecem só pelo ID opaco, sem nome; autores só como <c>actorRef</c> (report-format.md, FR-007, FR-026).
    /// </summary>
    public static async Task<IResult> ExportReportAsync(
        HttpContext context,
        IEnvironmentAuthorizationDecider authorizationDecider,
        LicensingReportService reportService,
        CancellationToken cancellationToken)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return unauthorized!;
        var denied = await AuthorizeAsync(context, authorizationDecider, actor!, token!, LicensingAuthorization.ReadScope, cancellationToken);
        if (denied is not null) return denied;

        var challenge = context.Request.Query["challenge"].ToString();
        if (!LicensingReportService.IsValidChallenge(challenge))
        {
            return Problem(context, StatusCodes.Status400BadRequest, "invalid_challenge", "Informe o desafio recebido do fornecedor (8 a 64 caracteres).");
        }

        var report = await reportService.ExportAsync(challenge, actor!.Subject, GetCorrelationId(context), cancellationToken);
        context.Response.Headers.ContentDisposition = $"attachment; filename=\"relatorio-licenciamento-{report.GeneratedAtUtc:yyyyMMdd'T'HHmmss'Z'}.json\"";
        return Results.Json(new
        {
            reportVersion = 1,
            installationId = report.InstallationId,
            slotId = report.SlotId,
            generatedAt = report.GeneratedAtUtc,
            challenge = report.Challenge,
            status = ToResponse(report.Status),
            hosts = report.Hosts.Select(host => new
            {
                hostId = host.HostId,
                vcpus = host.Vcpus,
                source = host.Source,
                nodeEquivalents = host.NodeEquivalents,
                removed = host.Removed,
            }),
            chain = new
            {
                headSeq = report.Events.Count == 0 ? 0 : report.Events[^1].ChainSeq,
                headHash = report.Events.Count == 0 ? string.Empty : System.Buffers.Text.Base64Url.EncodeToString(report.Events[^1].RecordHash),
            },
            events = report.Events.Select(item => new
            {
                chainSeq = item.ChainSeq,
                eventId = item.EventId.ToString("D"),
                occurredAt = item.OccurredAtUtc.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture),
                actorRef = item.ActorRef,
                correlationId = item.CorrelationId.ToString("D"),
                action = item.Action,
                details = System.Text.Json.Nodes.JsonNode.Parse(item.Details),
                prevHash = System.Buffers.Text.Base64Url.EncodeToString(item.PrevHash),
                recordHash = System.Buffers.Text.Base64Url.EncodeToString(item.RecordHash),
            }),
        });
    }

    /// <summary>
    /// Pacote de revogação assinado (T052, D-011): mesma ordem e limites da importação de licença; sem <c>If-Match</c>,
    /// porque a sequência estritamente crescente serializa a aplicação.
    /// </summary>
    public static async Task<IResult> ImportRevocationAsync(
        HttpContext context,
        IEnvironmentAuthorizationDecider authorizationDecider,
        RevocationImportService importService,
        LicensingUploadRateLimiter rateLimiter,
        CancellationToken cancellationToken)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return unauthorized!;
        var denied = await AuthorizeAsync(context, authorizationDecider, actor!, token!, LicensingAuthorization.ManageScope, cancellationToken);
        if (denied is not null) return denied;

        if (!rateLimiter.TryAcquire(actor!.Subject))
        {
            return LicensingProblems.Create(context, LicensingProblems.RateLimited, "Limite de importações por minuto atingido. Tente de novo em instantes.");
        }

        if (context.Request.ContentLength > JwsCompactReader.MaxDocumentBytes || await ReadBodyAsync(context.Request, cancellationToken) is not { } content)
        {
            return LicensingProblems.Create(context, LicensingProblems.PayloadTooLarge, "O pacote de revogação tem mais de 64 KiB.");
        }

        var result = await importService.ImportAsync(content, actor.Subject, GetCorrelationId(context), cancellationToken);
        return result.Outcome == LicenseImportOutcome.Accepted
            ? Ok(context, result.Status!)
            : LicensingProblems.Create(context, result.ProblemCode!, result.ProblemCode == LicenseImportCodes.Obsolete
                ? "A sequência do pacote não é maior que a já importada."
                : "O pacote de revogação foi recusado na verificação ou não é da organização da licença ativa.");
    }

    /// <summary>
    /// Saída de <c>indeterminate</c> por relógio (T069, threat model L2): regrava o instante confiável com o relógio
    /// atual, nunca cria nem estende tolerância e grava <c>license.trusted_instant_reset</c> com a justificativa.
    /// </summary>
    public static async Task<IResult> ResetTrustedInstantAsync(
        TrustedInstantResetRequest? request,
        HttpContext context,
        IEnvironmentAuthorizationDecider authorizationDecider,
        LicenseStatusService statusService,
        ILicenseStateReader stateReader,
        ILicensingEvaluationStore evaluationStore,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return unauthorized!;
        var denied = await AuthorizeAsync(context, authorizationDecider, actor!, token!, LicensingAuthorization.ManageScope, cancellationToken);
        if (denied is not null) return denied;

        var reason = request?.Reason?.Trim();
        if (reason is null || reason.Length is < 10 or > 500 || reason.Any(char.IsControl))
        {
            return Problem(context, StatusCodes.Status400BadRequest, "invalid_reason", "Informe a justificativa (10 a 500 caracteres, sem dados pessoais).");
        }

        if (!TryReadIfMatch(context, out var expectedVersion))
        {
            return Problem(context, StatusCodes.Status400BadRequest, "if_match_required", "Informe o If-Match com a versão lida do estado.");
        }

        var status = await statusService.GetAsync(cancellationToken);
        var state = await stateReader.ReadAsync(cancellationToken);
        if (status.Entitlement != "indeterminate" || state is null)
        {
            return LicensingProblems.Create(context, LicensingProblems.LicenseNotIndeterminate, "O estado não está indeterminado por relógio; nada a recuperar.");
        }

        var now = timeProvider.GetUtcNow();
        var reset = new LicensingEvent(Guid.NewGuid(), now, actor!.Subject, GetCorrelationId(context), LicensingEventAction.LicenseTrustedInstantReset, new System.Text.Json.Nodes.JsonObject
        {
            ["previousInstant"] = LicenseStatusService.Iso(state.LastTrustedInstantUtc),
            ["newInstant"] = LicenseStatusService.Iso(now),
            ["reason"] = reason,
        });
        if (!await evaluationStore.ResetTrustedInstantAsync(now, expectedVersion, reset, cancellationToken))
        {
            return LicensingProblems.Create(context, LicensingProblems.LicenseRevisionConflict, "O estado da licença mudou desde a leitura. Atualize e tente de novo.");
        }

        return Ok(context, await statusService.GetAsync(cancellationToken));
    }

    public static async Task<IResult> StatusAsync(
        HttpContext context,
        IEnvironmentAuthorizationDecider authorizationDecider,
        LicenseStatusService statusService,
        CancellationToken cancellationToken)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return unauthorized!;
        var denied = await AuthorizeAsync(context, authorizationDecider, actor!, token!, LicensingAuthorization.ReadScope, cancellationToken);
        if (denied is not null) return denied;

        return Ok(context, await statusService.GetAsync(cancellationToken));
    }

    public static async Task<IResult> ImportAsync(
        HttpContext context,
        IEnvironmentAuthorizationDecider authorizationDecider,
        LicenseImportService importService,
        LicensingUploadRateLimiter rateLimiter,
        CancellationToken cancellationToken)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return unauthorized!;
        var denied = await AuthorizeAsync(context, authorizationDecider, actor!, token!, LicensingAuthorization.ManageScope, cancellationToken);
        if (denied is not null) return denied;

        if (!rateLimiter.TryAcquire(actor!.Subject))
        {
            return LicensingProblems.Create(context, LicensingProblems.RateLimited, "Limite de importações por minuto atingido. Tente de novo em instantes.");
        }

        var slotId = context.Request.Query["slotId"].ToString();
        if (!SlotPattern().IsMatch(slotId))
        {
            return Problem(context, StatusCodes.Status400BadRequest, "invalid_slot", "Informe o slot da instalação (slotId).");
        }

        if (!TryReadIfMatch(context, out var expectedVersion))
        {
            return Problem(context, StatusCodes.Status400BadRequest, "if_match_required", "Informe o If-Match com a versão lida do estado.");
        }

        if (context.Request.ContentLength > JwsCompactReader.MaxDocumentBytes)
        {
            return LicensingProblems.Create(context, LicensingProblems.PayloadTooLarge, "O arquivo de licença tem mais de 64 KiB.");
        }

        var content = await ReadBodyAsync(context.Request, cancellationToken);
        if (content is null)
        {
            return LicensingProblems.Create(context, LicensingProblems.PayloadTooLarge, "O arquivo de licença tem mais de 64 KiB.");
        }

        var result = await importService.ImportAsync(content, slotId, expectedVersion, actor.Subject, GetCorrelationId(context), cancellationToken);
        return result.Outcome switch
        {
            LicenseImportOutcome.Accepted or LicenseImportOutcome.Unchanged => Ok(context, result.Status!),
            LicenseImportOutcome.Conflict => LicensingProblems.Create(context, LicensingProblems.LicenseRevisionConflict, "O estado da licença mudou desde a leitura. Atualize e tente de novo."),
            _ => LicensingProblems.Create(context, result.ProblemCode!, RejectionDetail(result.ProblemCode!)),
        };
    }

    internal static object ToResponse(LicenseStatus status) => new
    {
        entitlement = status.Entitlement,
        authenticity = status.Authenticity,
        validity = status.Validity,
        metering = status.Metering,
        reasons = status.Reasons,
        license = status.License is not { } license ? null : new
        {
            licenseId = license.LicenseId,
            organizationId = license.OrganizationId,
            revision = license.Revision,
            plan = license.Plan,
            notBefore = license.NotBefore,
            expiresAt = license.ExpiresAt,
            capabilities = license.Capabilities,
            capacity = new { mode = license.UnlimitedCapacity ? "unlimited" : "limited", nodeEquivalents = license.NodeEquivalents },
        },
        installation = new
        {
            slotId = status.Installation.SlotId,
            kind = status.Installation.Kind,
            quota = status.Installation.Quota,
        },
        consumption = new
        {
            vcpuBand = status.VcpuBand,
            current = status.Consumption?.Current,
            windowPeak = status.Consumption?.WindowPeak,
            windowPeakAt = status.Consumption?.WindowPeakAt,
            unknownHosts = status.Consumption?.UnknownHosts ?? 0,
        },
        grace = status.Grace is not { } grace ? null : new { cause = grace.Cause, startedAt = grace.StartedAt, until = grace.Until },
    };

    private static IResult Ok(HttpContext context, LicenseStatus status)
    {
        context.Response.Headers.ETag = $"\"{status.Version.ToString(CultureInfo.InvariantCulture)}\"";
        return Results.Ok(ToResponse(status));
    }

    internal static async Task<IResult?> AuthorizeAsync(
        HttpContext context,
        IEnvironmentAuthorizationDecider authorizationDecider,
        AuthenticatedActor actor,
        string token,
        string scope,
        CancellationToken cancellationToken)
    {
        EnvironmentAuthorizationDecision decision;
        try
        {
            decision = await authorizationDecider.DecideAsync(LicensingAuthorization.Resource, scope, actor, token, GetCorrelationId(context), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            decision = new EnvironmentAuthorizationDecision(AuthorizationDecisionOutcome.Indeterminate, "authorization_unavailable");
        }

        return decision.Outcome switch
        {
            AuthorizationDecisionOutcome.Allowed => null,
            AuthorizationDecisionOutcome.Denied => LicensingProblems.Create(context, LicensingProblems.AuthorizationDenied, "A gestão do licenciamento exige a permissão correspondente no Keycloak."),
            _ => Problem(context, StatusCodes.Status503ServiceUnavailable, "authorization_unavailable", "A decisão de autorização está indisponível."),
        };
    }

    private static bool TryReadIfMatch(HttpContext context, out long version)
    {
        version = 0;
        var value = context.Request.Headers.IfMatch.ToString().Trim();
        return value.Length >= 3 && value[0] == '"' && value[^1] == '"'
            && long.TryParse(value.AsSpan(1, value.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out version);
    }

    /// <summary>Lê no máximo 64 KiB; null se o corpo passar disso, mesmo sem <c>Content-Length</c>.</summary>
    private static async Task<byte[]?> ReadBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var buffer = new byte[JwsCompactReader.MaxDocumentBytes + 1];
        var total = 0;
        int read;
        while (total < buffer.Length && (read = await request.Body.ReadAsync(buffer.AsMemory(total), cancellationToken)) > 0)
        {
            total += read;
        }

        return total > JwsCompactReader.MaxDocumentBytes ? null : buffer[..total];
    }

    private static string RejectionDetail(string code) => code switch
    {
        LicenseImportCodes.Obsolete => "A licença tem revisão anterior à ativa. A licença atual continua em vigor.",
        LicenseImportCodes.SlotUnknown => "O slot informado não existe nesta licença. A licença atual continua em vigor.",
        LicenseImportCodes.SlotDisabled => "O slot informado está desabilitado nesta licença. A licença atual continua em vigor.",
        _ => "O arquivo de licença foi recusado na verificação. A licença atual continua em vigor.",
    };

    [GeneratedRegex(@"\A[A-Za-z0-9_-]{1,64}\z")]
    private static partial Regex SlotPattern();
}
