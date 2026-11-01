using Dokpod.ControlPlane.Application.AuditExport;
using Dokpod.ControlPlane.Infrastructure.AuditExport;

namespace Dokpod.ControlPlane.Api.AuditExport;

/// <summary>
/// Composição Business da exportação de auditoria para SIEM (spec 027 T012). Só o <c>ProductEdition</c> da Business a
/// registra; a imagem CE não tem rotas, worker nem leitura de secrets da exportação.
/// </summary>
public static class AuditExportModule
{
    /// <summary>Raiz dos secrets montados (D-006); padrão <c>/run/secrets/dokpod-audit-export</c>.</summary>
    public const string SecretsRootVariable = "DOKPOD_AUDIT_EXPORT_SECRETS_ROOT";

    /// <summary>Sub-redes do plano de controle, em CIDR separado por vírgula, recusadas como destino (D-007).</summary>
    public const string BlockedNetworksVariable = "DOKPOD_AUDIT_EXPORT_BLOCKED_NETWORKS";

    /// <summary>Nome da instalação no <c>HOSTNAME</c> do syslog; sem ele, vai <c>-</c> (contracts/export-record.md).</summary>
    public const string HostnameVariable = "DOKPOD_AUDIT_EXPORT_HOSTNAME";

    public static IServiceCollection AddDokpodAuditExport(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(provider => DestinationAddressPolicy.FromCidrs(
            (provider.GetRequiredService<IConfiguration>()[BlockedNetworksVariable] ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)));
        services.AddSingleton<IDestinationAddressResolver>(provider => new DnsDestinationAddressResolver(provider.GetRequiredService<DestinationAddressPolicy>()));
        services.AddSingleton<IAuditExportSecretReader>(provider => new MountedSecretReader(
            provider.GetRequiredService<IConfiguration>()[SecretsRootVariable] is { Length: > 0 } root ? root : MountedSecretReader.DefaultRoot));
        services.AddScoped<IAuditTrailExportReader, AuditTrailExportReader>();
        services.AddScoped<IAuditExportStore, AuditExportStore>();
        services.AddScoped<IAuditExportInstallation, LicensingInstallationIdentity>();
        services.AddSingleton<IAuditExportTransport>(provider => new SyslogTlsTransport(
            provider.GetRequiredService<IDestinationAddressResolver>(),
            provider.GetRequiredService<IAuditExportSecretReader>(),
            new SyslogTlsTransportOptions { Hostname = provider.GetRequiredService<IConfiguration>()[HostnameVariable] }));
        services.AddSingleton<IAuditExportTestLimiter, AuditExportTestLimiter>();
        services.AddSingleton<AuditExportBackoff>();
        services.AddScoped<AuditExportDestinationService>();
        services.AddScoped<AuditExportSender>();
        services.AddHostedService<AuditExportWorker>();
        return services;
    }

    public static IEndpointRouteBuilder MapAuditExportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        AuditExportEndpoints.Map(endpoints);
        return endpoints;
    }
}
