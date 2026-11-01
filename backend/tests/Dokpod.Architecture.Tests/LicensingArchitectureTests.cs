using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Dokpod.Architecture.Tests;

/// <summary>Spec 031 T005: separação de edições (D-009) e ausência de rede e relógio implícito (FR-002).</summary>
public sealed partial class LicensingArchitectureTests
{
    private static readonly string BackendRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    private static readonly string[] LicensingSourceDirectories =
    [
        "libs/Dokpod.Licensing",
        "libs/Dokpod.ControlPlane.Application/Licensing",
        "libs/Dokpod.ControlPlane.Infrastructure/Licensing",
        "apps/Dokpod.ControlPlane.Api/Licensing",
    ];

    [Fact]
    public void LicensingLibrary_ReferencesNoDokpodProjectOrPackage()
    {
        var document = XDocument.Load(Path.Combine(BackendRoot, "libs/Dokpod.Licensing/Dokpod.Licensing.csproj"));

        Assert.Empty(document.Descendants("ProjectReference"));
        Assert.Empty(document.Descendants("PackageReference"));
        Assert.Empty(document.Descendants("Reference"));
    }

    [Fact]
    public void CommunityUseCases_DoNotDependOnBusinessCapabilityGate()
    {
        var applicationRoot = Path.Combine(BackendRoot, "libs/Dokpod.ControlPlane.Application");
        var licensingRoot = Path.Combine(applicationRoot, "Licensing") + Path.DirectorySeparatorChar;
        // Spec 027: a exportação ao SIEM é exclusiva Business e só entra na composição Business (BusinessGateTests).
        var auditExportRoot = Path.Combine(applicationRoot, "AuditExport") + Path.DirectorySeparatorChar;

        var offenders = SourceFiles(applicationRoot)
            .Where(file => !file.StartsWith(licensingRoot, StringComparison.Ordinal) && !file.StartsWith(auditExportRoot, StringComparison.Ordinal))
            .Where(file => File.ReadAllText(file).Contains("IBusinessCapabilityGate", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void LicensingTypes_DoNotUseImplicitClockOrNetwork()
    {
        var offenders = LicensingSourceDirectories
            .Select(directory => Path.Combine(BackendRoot, directory))
            .Where(Directory.Exists)
            .SelectMany(SourceFiles)
            .SelectMany(file => ForbiddenApi().Matches(StripComments(File.ReadAllText(file)))
                .Select(match => $"{Path.GetRelativePath(BackendRoot, file)}: {match.Value}"))
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void ForbiddenApiPattern_DetectsEachRestrictedApi()
    {
        foreach (var sample in new[] { "DateTime.UtcNow", "DateTimeOffset.UtcNow", "DateTime.Now", "new HttpClient()", "Dns.GetHostEntry", "new Socket(", "TcpClient", "UdpClient" })
        {
            Assert.Matches(ForbiddenApi(), sample);
        }

        Assert.DoesNotMatch(ForbiddenApi(), "timeProvider.GetUtcNow()");
    }

    private static IEnumerable<string> SourceFiles(string root) =>
        Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string StripComments(string source) => Comments().Replace(source, string.Empty);

    [GeneratedRegex(@"\bDateTime(Offset)?\.(Utc)?Now\b|\bHttpClient\b|\bDns\.|\bSocket\b|\bTcpClient\b|\bUdpClient\b")]
    private static partial Regex ForbiddenApi();

    [GeneratedRegex(@"//[^\n]*|/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comments();
}
