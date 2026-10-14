using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Dokpod.ControlPlane.Infrastructure.Migrations;

[DbContext(typeof(ControlPlaneDbContext))]
[Migration("202610130005_HostResourceSamples")]
public sealed class HostResourceSamples : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(MigrationSqlScriptLoader.Load("202610130005_HostResourceSamples/Up/01-HostResourceSamples.sql"));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(MigrationSqlScriptLoader.Load("202610130005_HostResourceSamples/Down/01-HostResourceSamples.sql"));
    }
}
