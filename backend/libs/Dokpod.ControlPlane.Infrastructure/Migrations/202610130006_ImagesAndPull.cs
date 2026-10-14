using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Dokpod.ControlPlane.Infrastructure.Migrations;

[DbContext(typeof(ControlPlaneDbContext))]
[Migration("202610130006_ImagesAndPull")]
public sealed class ImagesAndPull : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(MigrationSqlScriptLoader.Load("202610130006_ImagesAndPull/Up/01-ImagesAndPull.sql"));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(MigrationSqlScriptLoader.Load("202610130006_ImagesAndPull/Down/01-ImagesAndPull.sql"));
    }
}
