#if DOKPOD_BUSINESS
using Dokpod.ControlPlane.Api.AuditExport;
using Dokpod.ControlPlane.Api.ImageUpdates;
using Dokpod.ControlPlane.Api.Licensing;
#endif

namespace Dokpod.ControlPlane.Api;

/// <summary>
/// Edição fixada no build (spec 031 D-009): <c>-p:DokpodEdition=Business</c> define <c>DOKPOD_BUSINESS</c>; sem a
/// propriedade, a API é a CE. O <c>Program</c> e os testes de composição usam os mesmos dois pontos de entrada.
/// </summary>
public static class ProductEdition
{
    public const string Community = "community";
    public const string Business = "business";

#if DOKPOD_BUSINESS
    public const string Current = Business;
#else
    public const string Current = Community;
#endif

    /// <summary>
    /// Diagnóstico para a inspeção de imagens (T062): edição e <c>kid</c> do trust store embarcado, sem iniciar o host.
    /// Não expõe nada além do que o próprio binário contém; chaves privadas nunca existem nele.
    /// </summary>
    public static string Describe()
    {
#if DOKPOD_BUSINESS
        using var trustStore = EmbeddedTrustStore.Load();
        var keyIds = trustStore.KeyIds.Order(StringComparer.Ordinal).ToArray();
#else
        var keyIds = Array.Empty<string>();
#endif
        return System.Text.Json.JsonSerializer.Serialize(new { edition = Current, trustStoreKeyIds = keyIds });
    }

    /// <summary>
    /// Spec 033 (FR-031): <c>admin licensing import</c>. Compila nas duas edições, como o resto do licenciamento, e o
    /// comando recusa na CE (<see cref="Admin.AdminContext.Edition"/>).
    /// </summary>
    internal static Task<int> RunLicensingAdminAsync(
        string command,
        Admin.AdminArguments arguments,
        Dokpod.ControlPlane.Infrastructure.ControlPlaneDbContext database,
        Admin.AdminContext context,
        CancellationToken cancellationToken) =>
        Licensing.LicensingAdminCommands.RunAsync(command, arguments, database, context, cancellationToken);

    public static void AddServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
#if DOKPOD_BUSINESS
        services.AddDokpodBusinessLicensing();
        services.AddDokpodAuditExport();
        services.AddDokpodImageUpdates();
#endif
    }

    public static void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
#if DOKPOD_BUSINESS
        endpoints.MapLicensingEndpoints();
        endpoints.MapAuditExportEndpoints();
        endpoints.MapImageUpdateEndpoints();
#endif
    }
}
