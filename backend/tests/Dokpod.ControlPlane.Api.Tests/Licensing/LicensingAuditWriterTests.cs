using System.Text.Json.Nodes;
using Dokpod.ControlPlane.Application.Licensing;
using Dokpod.ControlPlane.Infrastructure.Licensing;
using Npgsql;
using Xunit;
using static Dokpod.ControlPlane.Api.Tests.Licensing.LicensingPostgres;

namespace Dokpod.ControlPlane.Api.Tests.Licensing;

/// <summary>Spec 031 T009: cadeia sem lacunas, recalculável a partir do que foi gravado e segura sob concorrência.</summary>
[Trait("Category", "Integration")]
[Collection(LicensingPostgresCollection.Name)]
public sealed class LicensingAuditWriterTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Append_LinksEventsAndChainRecomputesFromStoredRows()
    {
        var connectionString = ConnectionStringOrSkip();
        var headBefore = await ReadHeadSeqAsync(connectionString);
        var occurredAt = new DateTimeOffset(2026, 11, 14, 13, 0, 0, TimeSpan.Zero).AddTicks(1_234_567);
        var events = new[]
        {
            Event(LicensingEventAction.HostLinked, "user-sub-1", occurredAt, new JsonObject
            {
                ["hostId"] = Guid.NewGuid().ToString("D"),
                ["environmentId"] = Guid.NewGuid().ToString("D"),
            }),
            Event(LicensingEventAction.LicenseRejected, "user-sub-1", occurredAt.AddSeconds(1), new JsonObject
            {
                ["documentType"] = "license",
                ["failureCode"] = "license_invalid",
                ["contentSha256"] = new string('a', 64),
            }),
            Event(LicensingEventAction.CapacityDailySummary, null, occurredAt.AddSeconds(2), new JsonObject
            {
                ["date"] = "2026-11-14",
                ["vcpuBand"] = 32,
                ["peakNodeEquivalents"] = 7,
                ["unknownHosts"] = 0,
                ["hosts"] = new JsonArray(new JsonObject { ["hostId"] = Guid.NewGuid().ToString("D"), ["maxVcpus"] = 64, ["source"] = "engine" }),
                ["late"] = false,
            }),
        };

        var links = new List<LicensingChainLink>();
        foreach (var licensingEvent in events)
        {
            await using var context = CreateContext(connectionString);
            links.Add(await new LicensingAuditWriter(context).AppendAsync(licensingEvent, Ct));
        }

        Assert.Equal([headBefore + 1, headBefore + 2, headBefore + 3], links.Select(link => link.ChainSeq));

        await using var connection = await OpenAsync(connectionString, Ct);
        var (installationId, hmacKey, headSeq, headHash) = await ReadStateAsync(connection);
        Assert.Equal(headBefore + 3, headSeq);
        Assert.Equal(links[^1].RecordHash, headHash);

        var previous = headBefore == 0
            ? LicensingChain.GenesisHash(installationId)
            : await ScalarAsync<byte[]>(connection, null, "SELECT record_hash FROM dokpod.licensing_events WHERE chain_seq = @seq", Ct, new NpgsqlParameter("seq", headBefore));
        await using var command = new NpgsqlCommand(
            """
            SELECT chain_seq, event_id, occurred_at_utc, actor_id, actor_ref, correlation_id, action, details::text, prev_hash, record_hash
            FROM dokpod.licensing_events WHERE chain_seq > @seq ORDER BY chain_seq
            """,
            connection);
        command.Parameters.AddWithValue("seq", headBefore);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var index = 0;
        while (await reader.ReadAsync(Ct))
        {
            var actorId = reader.IsDBNull(3) ? null : reader.GetString(3);
            var actorRef = reader.IsDBNull(4) ? null : reader.GetString(4);
            var storedPrevious = (byte[])reader.GetValue(8);
            var storedHash = (byte[])reader.GetValue(9);

            Assert.Equal(events[index].ActorId, actorId);
            Assert.Equal(LicensingChain.ActorReference(hmacKey, actorId), actorRef);
            if (actorId is not null)
            {
                Assert.NotEqual(actorId, actorRef);
            }

            Assert.Equal(previous, storedPrevious);
            Assert.Equal(
                storedHash,
                LicensingChain.RecordHash(
                    storedPrevious,
                    reader.GetInt64(0),
                    reader.GetGuid(1),
                    new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(2), DateTimeKind.Utc)),
                    actorRef,
                    reader.GetGuid(5),
                    reader.GetString(6),
                    JsonNode.Parse(reader.GetString(7))!.AsObject()));

            previous = storedHash;
            index++;
        }

        Assert.Equal(events.Length, index);
    }

    [Fact]
    public async Task ConcurrentAppends_NeverRepeatOrSkipChainSeq()
    {
        var connectionString = ConnectionStringOrSkip();
        var headBefore = await ReadHeadSeqAsync(connectionString);
        const int writers = 16;

        var links = await Task.WhenAll(Enumerable.Range(0, writers).Select(async _ =>
        {
            await using var context = CreateContext(connectionString);
            return await new LicensingAuditWriter(context).AppendAsync(
                Event(LicensingEventAction.HostRemoved, null, DateTimeOffset.UnixEpoch.AddYears(56), new JsonObject { ["hostId"] = Guid.NewGuid().ToString("D") }),
                Ct);
        }));

        Assert.Equal(
            Enumerable.Range(1, writers).Select(offset => headBefore + offset),
            links.Select(link => link.ChainSeq).Order());
        await using var connection = await OpenAsync(connectionString, Ct);
        Assert.Equal((long)writers, await ScalarAsync<long>(
            connection, null, "SELECT count(DISTINCT chain_seq) FROM dokpod.licensing_events WHERE chain_seq > @seq", Ct, new NpgsqlParameter("seq", headBefore)));
    }

    [Fact]
    public async Task DetailsOutsideAllowlistOrWithFractions_AreRefusedWithoutWriting()
    {
        var connectionString = ConnectionStringOrSkip();
        var headBefore = await ReadHeadSeqAsync(connectionString);
        await using var context = CreateContext(connectionString);
        var writer = new LicensingAuditWriter(context);

        await Assert.ThrowsAsync<ArgumentException>(() => writer.AppendAsync(
            Event(LicensingEventAction.LicenseImported, "user-sub-1", DateTimeOffset.UnixEpoch.AddYears(56), new JsonObject { ["content"] = "eyJhbGciOiJFUzI1NiJ9" }),
            Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => writer.AppendAsync(
            Event(LicensingEventAction.CapacityDailySummary, null, DateTimeOffset.UnixEpoch.AddYears(56), new JsonObject { ["peakNodeEquivalents"] = 1.5 }),
            Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => writer.AppendAsync(
            Event("license.unknown", null, DateTimeOffset.UnixEpoch.AddYears(56), []),
            Ct));

        Assert.Equal(headBefore, await ReadHeadSeqAsync(connectionString));
    }

    private static LicensingEvent Event(string action, string? actorId, DateTimeOffset occurredAt, JsonObject details) =>
        new(Guid.NewGuid(), occurredAt, actorId, Guid.NewGuid(), action, details);

    private static async Task<long> ReadHeadSeqAsync(string connectionString)
    {
        await using var connection = await OpenAsync(connectionString, Ct);
        return await ScalarAsync<long>(connection, null, "SELECT COALESCE((SELECT chain_head_seq FROM dokpod.licensing_state), 0)", Ct);
    }

    private static async Task<(Guid InstallationId, byte[] HmacKey, long HeadSeq, byte[] HeadHash)> ReadStateAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            "SELECT installation_id, actor_hmac_key, chain_head_seq, chain_head_hash FROM dokpod.licensing_state",
            connection);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct));
        return (reader.GetGuid(0), (byte[])reader.GetValue(1), reader.GetInt64(2), (byte[])reader.GetValue(3));
    }
}
