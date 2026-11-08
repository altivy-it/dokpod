using Dokpod.ControlPlane.Infrastructure;
using Dokpod.ControlPlane.Infrastructure.SqlFiles;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests.Admin;

/// <summary>
/// Spec 033 T006 (FR-042): <c>schema/expected-schema.json</c> é a impressão do schema depois de todas as migrations, num
/// PostgreSQL real. Depois de criar ou mudar uma migration, rode com <c>DOKPOD_UPDATE_SQL_CATALOG=1</c> para regravar o
/// arquivo e, em seguida, o <c>SqlCatalogTests</c> no mesmo modo para atualizar o hash no manifesto.
/// </summary>
[Trait("Category", "Integration")]
[Collection(AgentCommandsPostgresCollection.Name)]
public sealed class ExpectedSchemaTests
{
    private static readonly string ExpectedPath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "../../../../../libs/Dokpod.ControlPlane.Infrastructure/Sql/schema/expected-schema.json"));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task MigratedDatabase_MatchesTheExpectedSchemaOfThisVersion()
    {
        await using var context = CreateContextOrSkip();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync(Ct));
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync(Ct);
        var actual = await SchemaFingerprint.ReadAsync(connection, Ct);

        if (Environment.GetEnvironmentVariable("DOKPOD_UPDATE_SQL_CATALOG") == "1")
        {
            await File.WriteAllTextAsync(ExpectedPath, actual.ToJson(), Ct);
        }

        var expected = SchemaFingerprint.Parse(await File.ReadAllTextAsync(ExpectedPath, Ct));
        Assert.Contains(actual.Objects, item => item is { Kind: "table", Name: "audit_events" });
        Assert.Contains(actual.Objects, item => item is { Kind: "function", Name: "dokpod_ensure_monthly_partitions(p_until date)" });
        Assert.DoesNotContain(actual.Objects, item => item.Name.StartsWith("audit_events_20", StringComparison.Ordinal));
        var missing = expected.Objects.Except(actual.Objects).Select(item => $"{item.Kind} {item.Name}").ToArray();
        var unexpected = actual.Objects.Except(expected.Objects).Select(item => $"{item.Kind} {item.Name}").ToArray();
        Assert.True(missing.Length == 0 && unexpected.Length == 0,
            $"expected-schema.json desatualizado (rode com DOKPOD_UPDATE_SQL_CATALOG=1). Faltam: {string.Join(", ", missing.Take(10))}. Sobram: {string.Join(", ", unexpected.Take(10))}.");
        Assert.Equal(expected.Sha256, actual.Sha256);
    }

    [Fact]
    public void EmbeddedExpectedSchema_IsTheFileOfThisVersion()
    {
        var embedded = SchemaFingerprint.Expected();

        Assert.Equal(SchemaFingerprint.Parse(File.ReadAllText(ExpectedPath)), embedded, new FingerprintComparer());
        Assert.NotEmpty(embedded.Objects);
        Assert.Equal(SchemaFingerprint.FromObjects(embedded.Objects).Sha256, embedded.Sha256);
    }

    private static ControlPlaneDbContext CreateContextOrSkip()
    {
        var connectionString = Environment.GetEnvironmentVariable("DOKPOD_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) Assert.Skip("DOKPOD_TEST_POSTGRES_CONNECTION is not configured.");
        return new ControlPlaneDbContext(new DbContextOptionsBuilder<ControlPlaneDbContext>().UseNpgsql(connectionString).Options);
    }

    private sealed class FingerprintComparer : IEqualityComparer<SchemaFingerprint>
    {
        public bool Equals(SchemaFingerprint? x, SchemaFingerprint? y) =>
            x is not null && y is not null && x.Sha256 == y.Sha256 && x.Objects.SequenceEqual(y.Objects);

        public int GetHashCode(SchemaFingerprint obj) => obj.Sha256.GetHashCode(StringComparison.Ordinal);
    }
}
