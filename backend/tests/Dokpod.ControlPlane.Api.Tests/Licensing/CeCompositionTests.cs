using Dokpod.ControlPlane.Api.Licensing;
using Dokpod.ControlPlane.Application.Licensing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests.Licensing;

/// <summary>
/// Spec 031 T013 (História 1, FR-001, D-009): a composição CE, montada como o <c>Program</c> monta, não registra
/// serviço, rota nem tarefa de fundo do licenciamento. Sem rota, <c>GET /api/v1/licensing/status</c> responde 404 a
/// quem está autenticado e 401, como qualquer rota desconhecida, a quem não está (política de fallback da API).
/// </summary>
public sealed class CeCompositionTests
{
    [Fact]
    public void CommunityBuild_IsTheCommunityEdition()
    {
        Assert.Equal(ProductEdition.Community, ProductEdition.Current);
    }

    [Fact]
    public void CommunityComposition_RegistersNoLicensingServiceRouteOrHostedService()
    {
        using var serverCertificate = TestCertificateFactory.CreateServerCertificate();
        // Com banco configurado, para incluir as stores e tarefas de fundo da infraestrutura (sem abrir conexão).
        var builder = ApiHost.CreateBuilder(
            ["--ConnectionStrings:ControlPlane=Host=localhost;Database=dokpod;Username=dokpod"],
            new ApiHostOptions(
                GrpcPort: 7443,
                HealthPort: 8080,
                ServerCertificate: serverCertificate,
                ClientCertificateValidation: null,
                LoopbackOnly: true));
        Assert.Contains(builder.Services, descriptor => descriptor.ServiceType.Name == "ControlPlaneDbContext");

        ProductEdition.AddServices(builder.Services);
        var licensingServices = builder.Services
            .Where(descriptor => IsLicensing(descriptor.ServiceType) || IsLicensing(descriptor.ImplementationType))
            .Select(descriptor => descriptor.ServiceType.FullName)
            .ToArray();
        var hostedServices = builder.Services
            .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
            .Select(descriptor => descriptor.ImplementationType)
            .Where(IsLicensing)
            .ToArray();

        using var app = builder.Build();
        ApiHost.MapEndpoints(app);
        ProductEdition.MapEndpoints(app);
        var licensingRoutes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .Where(route => route.StartsWith("/api/v1/licensing", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Empty(licensingServices);
        Assert.Empty(hostedServices);
        Assert.Empty(licensingRoutes);
        Assert.Null(app.Services.GetService<ILicenseStateReader>());
        Assert.Null(app.Services.GetService<IBusinessCapabilityGate>());
        Assert.Null(app.Services.GetService<ILicensingAuditWriter>());
    }

    private static bool IsLicensing(Type? type) =>
        type?.Namespace is { } name && name.Split('.').Contains("Licensing", StringComparer.Ordinal);
}
