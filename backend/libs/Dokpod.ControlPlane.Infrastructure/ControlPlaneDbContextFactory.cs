using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Dokpod.ControlPlane.Infrastructure;

public sealed class ControlPlaneDbContextFactory : IDesignTimeDbContextFactory<ControlPlaneDbContext>
{
    public ControlPlaneDbContext CreateDbContext(string[] args) => Create(GetConnectionString(args));

    /// <summary>Contexto com o histórico de migrations no schema do Dokpod; usado pelo EF e pelo modo administrativo (spec 033).</summary>
    public static ControlPlaneDbContext Create(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseNpgsql(
                ControlPlaneConnectionString.Normalize(connectionString),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ControlPlaneSchema.Name))
            .Options;

        return new ControlPlaneDbContext(options);
    }

    private static string GetConnectionString(string[] args)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], "--connection", StringComparison.OrdinalIgnoreCase))
            {
                var value = args[index + 1];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        var environmentValue = Environment.GetEnvironmentVariable("DOKPOD_CONTROLPLANE_CONNECTION");
        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            return environmentValue;
        }

        throw new InvalidOperationException(
            "A PostgreSQL connection string is required through --connection or DOKPOD_CONTROLPLANE_CONNECTION for EF design-time operations.");
    }
}