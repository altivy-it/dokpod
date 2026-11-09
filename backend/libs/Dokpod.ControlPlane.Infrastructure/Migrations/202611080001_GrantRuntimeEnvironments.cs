using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Dokpod.ControlPlane.Infrastructure.Migrations;

[DbContext(typeof(ControlPlaneDbContext))]
[Migration("202611080001_GrantRuntimeEnvironments")]
public sealed class GrantRuntimeEnvironments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(MigrationSqlScriptLoader.Load("202611080001_GrantRuntimeEnvironments/Up/01-GrantRuntimeEnvironments.sql"));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(MigrationSqlScriptLoader.Load("202611080001_GrantRuntimeEnvironments/Down/01-GrantRuntimeEnvironments.sql"));
    }
}
