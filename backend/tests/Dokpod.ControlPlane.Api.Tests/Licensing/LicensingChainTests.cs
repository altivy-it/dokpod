using System.Text.Json.Nodes;
using Dokpod.ControlPlane.Infrastructure.Licensing;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests.Licensing;

/// <summary>
/// Vetor fixo da cadeia (contracts/report-format.md), calculado de forma independente fora do .NET; serve também
/// de referência para o verificador do fornecedor (T071).
/// </summary>
public sealed class LicensingChainTests
{
    private static readonly Guid InstallationId = Guid.Parse("3f0c5a1e-2b4d-4c6e-8f90-a1b2c3d4e5f6");

    [Fact]
    public void GenesisActorReferenceAndRecordHash_MatchReferenceVector()
    {
        var genesis = LicensingChain.GenesisHash(InstallationId);
        var actorRef = LicensingChain.ActorReference([.. Enumerable.Range(0, 32).Select(value => (byte)value)], "user-1");
        var details = new JsonObject
        {
            ["vcpuBand"] = 32,
            ["date"] = "2026-11-14",
            ["unknownHosts"] = 0,
            ["peakNodeEquivalents"] = 7,
            ["late"] = false,
            ["hosts"] = new JsonArray(new JsonObject
            {
                ["source"] = "engine",
                ["maxVcpus"] = 64,
                ["hostId"] = "8b1e0000-0000-4000-8000-000000000001",
            }),
        };

        var record = LicensingChain.RecordHash(
            genesis,
            1,
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            new DateTimeOffset(2026, 11, 14, 13, 0, 0, TimeSpan.Zero).AddTicks(1_234_567),
            actorRef,
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "capacity.daily_summary",
            details);

        Assert.Equal("f099b7b1cef9a9981dec9c1e8e593182fc1c901db2cc274cb5fc97d8f3350f5e", Convert.ToHexStringLower(genesis));
        Assert.Equal("xJ3AF5eKIuGCRF7Cn9SN-AETTFVCCw_1vyGTLz7_VYM", actorRef);
        Assert.Equal("f6409e296295d3d139dc20352f7d7d0704901f24a6684f8b3c4454fd7207176c", Convert.ToHexStringLower(record));
    }

    [Fact]
    public void Canonical_SortsKeysAndEscapesOnlyQuotesBackslashAndControlCharacters()
    {
        var node = JsonNode.Parse("{\"b\":\"a\\\"b\\\\c\\n\\u0001é/<\",\"a\":[1,true,null,{\"z\":-2,\"y\":\"\"}]}");

        Assert.Equal("{\"a\":[1,true,null,{\"y\":\"\",\"z\":-2}],\"b\":\"a\\\"b\\\\c\\n\\u0001é/<\"}", LicensingChain.Canonical(node));
    }

    [Fact]
    public void Canonical_RefusesNonIntegerNumbers()
    {
        Assert.Throws<ArgumentException>(() => LicensingChain.Canonical(JsonNode.Parse("{\"ratio\":1.5}")));
        Assert.Throws<ArgumentException>(() => LicensingChain.Canonical(new JsonObject { ["ratio"] = 0.5 }));
    }

    [Fact]
    public void StoredInstant_IsTruncatedToMicroseconds()
    {
        var instant = new DateTimeOffset(2026, 11, 14, 13, 0, 0, TimeSpan.FromHours(-3)).AddTicks(1_234_567);

        var stored = LicensingChain.ToStoredInstant(instant);

        Assert.Equal(TimeSpan.Zero, stored.Offset);
        Assert.Equal(0, stored.Ticks % 10);
        Assert.Equal(instant.UtcTicks - 7, stored.UtcTicks);
    }
}
