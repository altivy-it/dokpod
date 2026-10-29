using System.Globalization;
using System.Reflection;
using Dokpod.ControlPlane.Api.Commands;
using Dokpod.ControlPlane.Application.Authorization;
using Dokpod.ControlPlane.Application.Hosts;
using Dokpod.ControlPlane.Application.Metrics;
using Dokpod.ControlPlane.Application.Terminal;
using Dokpod.ControlPlane.Infrastructure;
using Dokpod.Domain.Environments;
using Dokpod.Domain.Hosts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using static Dokpod.ControlPlane.Api.Endpoints.ConsoleEndpoints;

namespace Dokpod.ControlPlane.Api.Endpoints;

/// <summary>Parâmetro efetivo (D-004); <see cref="Key"/> nulo para constantes do produto.</summary>
public sealed record SettingsParameter(string Group, string? Key, string Label, string Value, string Source);

/// <summary>
/// Governança → Configurações (spec 022): acesso, instalação (versões, banco, saúde, agentes) e retenções e limites
/// efetivos, só para quem tem `environment:manage` em algum host (D-002). Nenhum valor é lido por chave arbitrária.
/// </summary>
public static class SettingsEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/settings/access", AccessAsync).RequireAuthorization();
        endpoints.MapGet("/api/v1/settings/installation", InstallationAsync).RequireAuthorization();
        endpoints.MapGet("/api/v1/settings/parameters", ParametersAsync).RequireAuthorization();
    }

    public static string ProductVersion { get; } =
        (typeof(SettingsEndpoints).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0").Split('+')[0];

    private static async Task<IResult> AccessAsync(
        HttpContext context,
        AuthorizedEnvironmentsService authorizedEnvironments,
        IEnvironmentGrantReader grantReader,
        CancellationToken cancellationToken)
    {
        var (allowed, _, error) = await AuthorizeAsync(context, authorizedEnvironments, grantReader, cancellationToken);
        return error ?? Results.Ok(new { allowed });
    }

    private static async Task<IResult> InstallationAsync(
        HttpContext context,
        AuthorizedEnvironmentsService authorizedEnvironments,
        IEnvironmentGrantReader grantReader,
        HostQueryService hostQuery,
        HealthCheckService health,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var (allowed, registrations, error) = await AuthorizeAsync(context, authorizedEnvironments, grantReader, cancellationToken);
        if (error is not null) return error;
        if (!allowed) return Forbidden(context);

        var report = await health.CheckHealthAsync(entry => entry.Tags.Contains("ready"), cancellationToken);
        var dbContext = services.GetService<ControlPlaneDbContext>();
        string? lastMigration = null;
        var pending = 0;
        if (dbContext is not null)
        {
            try
            {
                lastMigration = (await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken)).LastOrDefault();
                pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).Count();
            }
            catch (Exception exception) when (exception is InvalidOperationException or Npgsql.NpgsqlException)
            {
                lastMigration = null;
            }
        }

        var hosts = await hostQuery.DescribeAsync(registrations, cancellationToken);
        return Results.Ok(new
        {
            controlPlane = new { version = ProductVersion },
            protocolVersion = "1",
            database = new { configured = dbContext is not null, lastMigration, pendingMigrations = pending },
            // Só nome, situação e descrição curta dos health checks já existentes (sem exceções nem dados de conexão).
            dependencies = report.Entries.OrderBy(e => e.Key, StringComparer.Ordinal).Select(e => new
            {
                name = e.Key,
                status = e.Value.Status switch { HealthStatus.Healthy => "healthy", HealthStatus.Degraded => "degraded", _ => "unhealthy" },
                description = e.Value.Status == HealthStatus.Healthy ? null : e.Value.Description,
                durationMs = (int)e.Value.Duration.TotalMilliseconds,
            }),
            checkedAt = DateTimeOffset.UtcNow,
            agents = new
            {
                total = hosts.Count,
                connected = hosts.Count(h => h.Connected),
                updateRequired = hosts.Count(h => h.Situation == HostSituation.UpdateRequired),
                unknown = hosts.Count(h => h.Descriptor is null),
                byAgentVersion = hosts.Where(h => h.Descriptor is not null).GroupBy(h => h.Descriptor!.AgentVersion)
                    .OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => new { version = g.Key, count = g.Count() }),
                byEngine = hosts.Where(h => h.Descriptor is not null).GroupBy(h => (h.Descriptor!.EngineKind, h.Descriptor.EngineVersion))
                    .OrderBy(g => g.Key.EngineKind, StringComparer.Ordinal).ThenBy(g => g.Key.EngineVersion, StringComparer.Ordinal)
                    .Select(g => new { engine = g.Key.EngineKind, version = g.Key.EngineVersion, count = g.Count() }),
            },
        });
    }

    private static async Task<IResult> ParametersAsync(
        HttpContext context,
        AuthorizedEnvironmentsService authorizedEnvironments,
        IEnvironmentGrantReader grantReader,
        IConfiguration configuration,
        CommandRetentionOptions commands,
        MetricsOptions metrics,
        TerminalOptions terminal,
        CancellationToken cancellationToken)
    {
        var (allowed, _, error) = await AuthorizeAsync(context, authorizedEnvironments, grantReader, cancellationToken);
        if (error is not null) return error;
        if (!allowed) return Forbidden(context);
        return Results.Ok(new { parameters = Parameters(configuration, commands, metrics, terminal) });
    }

    /// <summary>Lista fechada de parâmetros efetivos (D-004); a origem é "instalação" quando a chave existe na configuração.</summary>
    public static IReadOnlyList<SettingsParameter> Parameters(IConfiguration configuration, CommandRetentionOptions commands, MetricsOptions metrics, TerminalOptions terminal)
    {
        var result = new List<SettingsParameter>();
        void Add(string group, string key, string label, string value) => result.Add(new SettingsParameter(
            group, key.Length > 0 ? key : null, label, value, key.Length > 0 && configuration.GetSection(key).Exists() ? "installation" : "default"));
        Add("retention", "Commands:Retention", "Comandos concluídos e seus parâmetros", Duration(commands.Retention));
        Add("retention", "EventRetention:Period", "Eventos operacionais", Duration(configuration.GetValue("EventRetention:Period", TimeSpan.FromDays(30))));
        Add("retention", "Alerts:ResolvedRetention", "Alertas resolvidos", Duration(configuration.GetValue("Alerts:ResolvedRetention", TimeSpan.FromDays(90))));
        Add("retention", "Metrics:HostRetention", "Métricas por host", Duration(metrics.HostRetention));
        Add("retention", "Metrics:LatestExpiry", "Validade da última amostra de métrica", Duration(metrics.LatestExpiry));
        Add("retention", string.Empty, "Auditoria", "sem remoção automática (partições mensais)");
        Add("limits", string.Empty, "Ações e remoções em lote", "50 itens por lote");
        Add("limits", string.Empty, "Exportação da auditoria", "até 10.000 eventos por arquivo");
        Add("limits", string.Empty, "Período de consulta da auditoria", "até 90 dias");
        Add("terminal", "Terminal:IdleTimeout", "Encerramento por ociosidade", Duration(terminal.IdleTimeout));
        Add("terminal", "Terminal:MaxDuration", "Duração máxima da sessão", Duration(terminal.MaxDuration));
        Add("terminal", "Terminal:MaxPerUser", "Sessões simultâneas por usuário", terminal.MaxPerUser.ToString(CultureInfo.InvariantCulture));
        Add("terminal", "Terminal:MaxPerUserPerContainer", "Sessões por usuário em um container", terminal.MaxPerUserPerContainer.ToString(CultureInfo.InvariantCulture));
        Add("terminal", "Terminal:MaxPerAgent", "Sessões simultâneas por host", terminal.MaxPerAgent.ToString(CultureInfo.InvariantCulture));
        Add("terminal", "Terminal:MaxOutputBytesPerSecond", "Saída máxima", $"{terminal.MaxOutputBytesPerSecond / 1024} KiB/s");
        Add("terminal", "Terminal:RequireJustificationStages", "Estágios que exigem justificativa",
            terminal.RequireJustificationStages.Length == 0 ? "nenhum" : string.Join(", ", terminal.RequireJustificationStages.Select(StageName)));
        Add("terminal", "Terminal:RecentMfaMaxAge", "Autenticação recente exigida", Duration(terminal.RecentMfaMaxAge));
        return result;
    }

    private static string StageName(string stage) => stage switch
    {
        "production" => "Produção",
        "staging" => "Homologação",
        "development" => "Desenvolvimento",
        _ => stage,
    };

    public static string Duration(TimeSpan value) =>
        value.TotalDays >= 1 && value.TotalDays == Math.Floor(value.TotalDays) ? $"{value.TotalDays:0} {(value.TotalDays == 1 ? "dia" : "dias")}"
        : value.TotalHours >= 1 && value.TotalHours == Math.Floor(value.TotalHours) ? $"{value.TotalHours:0} {(value.TotalHours == 1 ? "hora" : "horas")}"
        : value.TotalMinutes >= 1 && value.TotalMinutes == Math.Floor(value.TotalMinutes) ? $"{value.TotalMinutes:0} min"
        : $"{value.TotalSeconds:0} s";

    /// <summary>`environment:manage` em algum host legível e habilitado, numa só consulta ao provedor (indisponível conta como negado).</summary>
    private static async Task<(bool Allowed, IReadOnlyList<EnvironmentRegistration> Registrations, IResult? Error)> AuthorizeAsync(
        HttpContext context,
        AuthorizedEnvironmentsService authorizedEnvironments,
        IEnvironmentGrantReader grantReader,
        CancellationToken cancellationToken)
    {
        if (!TryAuthenticate(context, out var actor, out var token, out var unauthorized)) return (false, [], unauthorized);
        var authorized = await LoadAuthorizedAsync(context, authorizedEnvironments, actor!, token!, cancellationToken);
        if (authorized.Error is not null) return (false, [], authorized.Error);
        var managed = await grantReader.WithScopeAsync(authorized.Registrations.Where(r => r.Enabled), EnvironmentResourceScopes.Manage,
            actor!, token!, GetCorrelationId(context), cancellationToken);
        return (managed.Count > 0, authorized.Registrations, null);
    }

    private static IResult Forbidden(HttpContext context) =>
        Problem(context, StatusCodes.Status403Forbidden, "authorization_denied", "Configurações exigem administração de algum host.");
}
