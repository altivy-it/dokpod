using System.Reflection;
using System.Xml.Linq;
using Xunit;

namespace Dokpod.Licensing.Tests;

/// <summary>Spec 031 D-002: o verificador depende só da BCL, para servir ao produto e à CLI sob MIT.</summary>
public sealed class LicensingAssemblyTests
{
    [Fact]
    public void Project_HasNoPackageOrProjectReferences()
    {
        var projectPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../libs/Dokpod.Licensing/Dokpod.Licensing.csproj"));
        var document = XDocument.Load(projectPath);

        Assert.Empty(document.Descendants("PackageReference"));
        Assert.Empty(document.Descendants("ProjectReference"));
        Assert.Equal("MIT", document.Descendants("PackageLicenseExpression").Single().Value);
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(projectPath)!, "LICENSE")));
    }

    [Fact]
    public void Assembly_ReferencesOnlyTheBaseClassLibrary()
    {
        var assembly = Assembly.Load("Dokpod.Licensing");

        Assert.All(
            assembly.GetReferencedAssemblies(),
            reference => Assert.True(
                reference.Name is "netstandard" or "mscorlib" || reference.Name!.StartsWith("System.", StringComparison.Ordinal),
                $"Dokpod.Licensing referencia {reference.Name}, fora da BCL."));
    }
}
