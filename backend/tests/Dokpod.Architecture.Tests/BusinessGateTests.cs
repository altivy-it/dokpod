using System.Text.RegularExpressions;
using Xunit;

namespace Dokpod.Architecture.Tests;

/// <summary>
/// Spec 031 T074 (threat model L7): todo caso de uso marcado <c>[BusinessCapability]</c> depende de
/// <c>IBusinessCapabilityGate</c>, e nenhum código da CE referencia a composição Business, o verificador ou as stores
/// de licenciamento. O único ponto de composição é <c>ProductEdition</c>; portas de <c>Application.Licensing</c> que a CE
/// recebe como opcionais (por exemplo, o observador de capacidade) não são o módulo.
/// </summary>
public sealed partial class BusinessGateTests
{
    private static readonly string BackendRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    private static readonly string[] ProductProjects =
    [
        "apps/Dokpod.ControlPlane.Api",
        "libs/Dokpod.ControlPlane.Application",
        "libs/Dokpod.ControlPlane.Infrastructure",
    ];

    [Fact]
    public void BusinessCapabilityUseCases_DependOnTheGate()
    {
        var offenders = ProductSources()
            .Where(file => BusinessCapabilityMarker().IsMatch(File.ReadAllText(file)))
            .Where(file => !File.ReadAllText(file).Contains("IBusinessCapabilityGate", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(BackendRoot, file))
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void CommunityCode_DoesNotReferenceTheLicensingModule()
    {
        var offenders = ProductSources()
            .Where(file => !IsLicensingSource(file) && Path.GetFileName(file) is not "ProductEdition.cs")
            .SelectMany(file => LicensingModuleReference().Matches(File.ReadAllText(file))
                .Select(match => $"{Path.GetRelativePath(BackendRoot, file)}: {match.Value}"))
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Patterns_DetectTheMarkerAndTheModuleReferences()
    {
        Assert.Matches(BusinessCapabilityMarker(), "[BusinessCapability(\"change-windows\")]");
        Assert.Matches(LicensingModuleReference(), "using Dokpod.ControlPlane.Infrastructure.Licensing;");
        Assert.Matches(LicensingModuleReference(), "Dokpod.ControlPlane.Api.Licensing.LicensingModule");
        Assert.Matches(LicensingModuleReference(), "using Dokpod.Licensing;");
        Assert.DoesNotMatch(LicensingModuleReference(), "Dokpod.ControlPlane.Application.Licensing.IAgentCapacityObserver");
        // Spec 027 T012: a exportação de auditoria é módulo Business, nas três camadas.
        Assert.Matches(LicensingModuleReference(), "using Dokpod.ControlPlane.Infrastructure.AuditExport;");
        Assert.Matches(LicensingModuleReference(), "Dokpod.ControlPlane.Api.AuditExport.AuditExportModule");
        Assert.Matches(LicensingModuleReference(), "using Dokpod.ControlPlane.Application.AuditExport;");
    }

    private static bool IsLicensingSource(string file) =>
        file.Contains($"{Path.DirectorySeparatorChar}Licensing{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || file.Contains($"{Path.DirectorySeparatorChar}AuditExport{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static IEnumerable<string> ProductSources() =>
        ProductProjects
            .Select(project => Path.Combine(BackendRoot, project))
            .SelectMany(root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    [GeneratedRegex(@"\[BusinessCapability(Attribute)?\(")]
    private static partial Regex BusinessCapabilityMarker();

    [GeneratedRegex(@"Dokpod\.ControlPlane\.(Infrastructure|Api)\.(Licensing|AuditExport)\b|Dokpod\.ControlPlane\.Application\.AuditExport\b|\busing\s+Dokpod\.Licensing\s*;|\bDokpod\.Licensing\.[A-Z]")]
    private static partial Regex LicensingModuleReference();
}
