using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Dokpod.ControlPlane.Infrastructure.Migrations;

[DbContext(typeof(ControlPlaneDbContext))]
[Migration("202610130002_PauseCommands")]
public sealed class PauseCommands : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(MigrationSqlScriptLoader.Load("202610130002_PauseCommands/Up/01-PauseCommands.sql"));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(MigrationSqlScriptLoader.Load("202610130002_PauseCommands/Down/01-PauseCommands.sql"));
    }
}
