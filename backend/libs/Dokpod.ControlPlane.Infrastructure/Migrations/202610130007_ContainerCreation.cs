using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Dokpod.ControlPlane.Infrastructure.Migrations;

[DbContext(typeof(ControlPlaneDbContext))]
[Migration("202610130007_ContainerCreation")]
public sealed class ContainerCreation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(MigrationSqlScriptLoader.Load("202610130007_ContainerCreation/Up/01-ContainerCreation.sql"));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(MigrationSqlScriptLoader.Load("202610130007_ContainerCreation/Down/01-ContainerCreation.sql"));
    }
}
