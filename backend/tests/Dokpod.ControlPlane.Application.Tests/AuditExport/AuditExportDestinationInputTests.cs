using Dokpod.ControlPlane.Application.AuditExport;
using Xunit;

namespace Dokpod.ControlPlane.Application.Tests.AuditExport;

/// <summary>Spec 027 T006: a validação da aplicação segue as restrições da migration (data-model.md).</summary>
public sealed class AuditExportDestinationInputTests
{
    private static AuditExportDestinationInput Valid(string protocol = "syslog") =>
        new(" siem-soc ", protocol, "siem.exemplo.interno", 6514, null, "siem-soc", ["containers"], true);

    [Fact]
    public void Valid_IsNormalizedWithMandatoryCategoryAndOtlpDefaultPath()
    {
        var (syslog, syslogError) = Valid().Validate();
        var (otlp, otlpError) = Valid("otlp").Validate();

        Assert.Null(syslogError);
        Assert.Equal("siem-soc", syslog!.Name);
        Assert.Equal(["audit_export", "containers"], syslog.Categories);
        Assert.Null(syslog.Path);
        Assert.Null(otlpError);
        Assert.Equal("/v1/logs", otlp!.Path);
    }

    [Theory]
    [InlineData("name-empty")]
    [InlineData("name-long")]
    [InlineData("protocol")]
    [InlineData("host-empty")]
    [InlineData("host-invalid")]
    [InlineData("port-zero")]
    [InlineData("port-high")]
    [InlineData("syslog-with-path")]
    [InlineData("otlp-relative-path")]
    [InlineData("otlp-path-with-query")]
    [InlineData("unknown-category")]
    public void Invalid_IsRefused(string scenario)
    {
        var input = scenario switch
        {
            "name-empty" => Valid() with { Name = "  " },
            "name-long" => Valid() with { Name = new string('a', 65) },
            "protocol" => Valid() with { Protocol = "udp" },
            "host-empty" => Valid() with { Host = "" },
            "host-invalid" => Valid() with { Host = "siem soc" },
            "port-zero" => Valid() with { Port = 0 },
            "port-high" => Valid() with { Port = 65536 },
            "syslog-with-path" => Valid() with { Path = "/v1/logs" },
            "otlp-relative-path" => Valid("otlp") with { Path = "v1/logs" },
            "otlp-path-with-query" => Valid("otlp") with { Path = "/v1/logs?x=1" },
            _ => Valid() with { Categories = ["containers", "nope"] },
        };

        var (normalized, error) = input.Validate();

        Assert.Null(normalized);
        Assert.Equal(AuditExportDestinationInput.InvalidDestination, error);
    }

    [Theory]
    [InlineData("../etc")]
    [InlineData("Siem")]
    [InlineData("")]
    public void InvalidSecretRef_HasItsOwnCode(string secretRef) =>
        Assert.Equal(AuditExportDestinationInput.InvalidSecretRef, (Valid() with { SecretRef = secretRef }).Validate().ErrorCode);

    [Fact]
    public void Position_OrdersByTransactionThenSequence()
    {
        Assert.True(new AuditExportPosition(5, 100).CompareTo(new AuditExportPosition(6, 1)) < 0);
        Assert.True(new AuditExportPosition(6, 2).CompareTo(new AuditExportPosition(6, 1)) > 0);
    }
}
