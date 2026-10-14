using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Dokpod.ControlPlane.Infrastructure.Migrations;

[DbContext(typeof(ControlPlaneDbContext))]
[Migration("202610130008_FixRetentionPartitionDelete")]
public sealed class FixRetentionPartitionDelete : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(MigrationSqlScriptLoader.Load("202610130008_FixRetentionPartitionDelete/Up/01-FixRetentionPartitionDelete.sql"));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(MigrationSqlScriptLoader.Load("202610130008_FixRetentionPartitionDelete/Down/01-FixRetentionPartitionDelete.sql"));
    }
}
