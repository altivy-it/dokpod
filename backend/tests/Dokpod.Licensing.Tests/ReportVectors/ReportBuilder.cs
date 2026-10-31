using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Dokpod.Licensing.Tests.ReportVectors;

/// <summary>
/// Monta relatórios sintéticos com hashes calculados aqui (report-format.md), sem usar o verificador. Os detalhes usados
/// já vêm com chaves ordenadas e só ASCII, então a forma canônica é a do próprio <see cref="JsonNode.ToJsonString"/>;
/// o primeiro evento reproduz o vetor de referência calculado fora do .NET na Fundação.
/// </summary>
public sealed class ReportBuilder(Guid installationId)
{
    private readonly List<JsonObject> events = [];
    private byte[] previous = SHA256.HashData([.. "dokpod-licensing-chain-v1"u8.ToArray(), .. Encoding.UTF8.GetBytes(installationId.ToString("D"))]);

    public Guid InstallationId => installationId;

    public ReportBuilder Add(string action, DateTimeOffset at, string detailsJson, string? actorRef = null, Guid? eventId = null, Guid? correlationId = null)
    {
        var seq = events.Count + 1;
        var id = (eventId ?? Guid.NewGuid()).ToString("D");
        var correlation = (correlationId ?? Guid.NewGuid()).ToString("D");
        var occurredAt = at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture);
        var actor = actorRef is null ? "null" : $"\"{actorRef}\"";
        var canonical = $"{{\"action\":\"{action}\",\"actor_ref\":{actor},\"chain_seq\":{seq},\"correlation_id\":\"{correlation}\",\"details\":{detailsJson},\"event_id\":\"{id}\",\"occurred_at_utc\":\"{occurredAt}\"}}";
        var record = SHA256.HashData([.. previous, .. Encoding.UTF8.GetBytes(canonical)]);
        events.Add(new JsonObject
        {
            ["chainSeq"] = seq,
            ["eventId"] = id,
            ["occurredAt"] = occurredAt,
            ["actorRef"] = actorRef,
            ["correlationId"] = correlation,
            ["action"] = action,
            ["details"] = JsonNode.Parse(detailsJson),
            ["prevHash"] = Base64Url.EncodeToString(previous),
            ["recordHash"] = Base64Url.EncodeToString(record),
        });
        previous = record;
        return this;
    }

    public string HeadHash => Base64Url.EncodeToString(previous);

    public long HeadSeq => events.Count;

    public JsonObject Build() => new()
    {
        ["reportVersion"] = 1,
        ["installationId"] = installationId.ToString("D"),
        ["slotId"] = "prd",
        ["generatedAt"] = "2027-10-02T13:00:00Z",
        ["challenge"] = "rnw-2027-org_51d2e0-a91f",
        ["status"] = new JsonObject(),
        ["hosts"] = new JsonArray(),
        ["chain"] = new JsonObject { ["headSeq"] = events.Count, ["headHash"] = events.Count == 0 ? string.Empty : HeadHash },
        ["events"] = new JsonArray([.. events.Select(e => (JsonNode?)e.DeepClone())]),
    };

    public static byte[] Bytes(JsonObject report) => Encoding.UTF8.GetBytes(report.ToJsonString());
}
