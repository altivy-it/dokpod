using Dokpod.Domain.Auditing;
using Xunit;

namespace Dokpod.ControlPlane.Application.Tests.AuditExport;

/// <summary>Spec 027 T005: a categoria dos destinos de exportação existe e cobre todas as ações dela.</summary>
public sealed class AuditExportCategoriesTests
{
    [Theory]
    [InlineData("audit_export.destination_created")]
    [InlineData("audit_export.destination_updated")]
    [InlineData("audit_export.destination_deleted")]
    [InlineData("audit_export.test_sent")]
    [InlineData("audit_export.gap")]
    public void AuditExportActions_BelongToTheMandatoryCategory(string action) =>
        Assert.Equal(AuditCategories.AuditExport, AuditCategories.CategoryOf(action));

    [Fact]
    public void Categories_DoNotShareActions()
    {
        var all = AuditCategories.Actions.SelectMany(entry => entry.Value).ToArray();

        Assert.Equal(all.Length, all.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(5, AuditCategories.Actions[AuditCategories.AuditExport].Count);
    }
}
