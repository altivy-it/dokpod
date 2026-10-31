using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Dokpod.ControlPlane.Infrastructure.Licensing;

/// <summary>
/// Cálculo da cadeia de licenciamento (contracts/report-format.md, FR-035). O JSON canônico ordena as chaves por
/// ordinal UTF-16, não usa espaços, escapa só aspas, barra invertida e caracteres de controle e aceita apenas
/// inteiros como números, para que o fornecedor recalcule os mesmos hashes a partir do relatório.
/// </summary>
internal static class LicensingChain
{
    private static readonly byte[] GenesisPrefix = Encoding.UTF8.GetBytes("dokpod-licensing-chain-v1");

    internal static byte[] GenesisHash(Guid installationId) =>
        SHA256.HashData([.. GenesisPrefix, .. Encoding.UTF8.GetBytes(installationId.ToString("D"))]);

    internal static string? ActorReference(byte[] hmacKey, string? actorId) =>
        actorId is null ? null : Base64Url.EncodeToString(HMACSHA256.HashData(hmacKey, Encoding.UTF8.GetBytes(actorId)));

    /// <summary>O PostgreSQL guarda microssegundos; o instante é truncado antes de gravar e de calcular o hash.</summary>
    internal static DateTimeOffset ToStoredInstant(DateTimeOffset instant)
    {
        var utc = instant.ToUniversalTime();
        return new DateTimeOffset(utc.Ticks - (utc.Ticks % 10), TimeSpan.Zero);
    }

    internal static byte[] RecordHash(
        byte[] previousHash,
        long chainSeq,
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        string? actorRef,
        Guid correlationId,
        string action,
        JsonObject details)
    {
        var builder = new StringBuilder();
        builder.Append("{\"action\":");
        AppendString(builder, action);
        builder.Append(",\"actor_ref\":");
        if (actorRef is null) builder.Append("null");
        else AppendString(builder, actorRef);
        builder.Append(",\"chain_seq\":").Append(chainSeq.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"correlation_id\":");
        AppendString(builder, correlationId.ToString("D"));
        builder.Append(",\"details\":");
        AppendNode(builder, details);
        builder.Append(",\"event_id\":");
        AppendString(builder, eventId.ToString("D"));
        builder.Append(",\"occurred_at_utc\":");
        AppendString(builder, occurredAtUtc.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture));
        builder.Append('}');

        return SHA256.HashData([.. previousHash, .. Encoding.UTF8.GetBytes(builder.ToString())]);
    }

    internal static string Canonical(JsonNode? node)
    {
        var builder = new StringBuilder();
        AppendNode(builder, node);
        return builder.ToString();
    }

    private static void AppendNode(StringBuilder builder, JsonNode? node)
    {
        switch (node)
        {
            case null:
                builder.Append("null");
                break;
            case JsonObject jsonObject:
                builder.Append('{');
                var first = true;
                foreach (var property in jsonObject.OrderBy(property => property.Key, StringComparer.Ordinal))
                {
                    if (!first) builder.Append(',');
                    first = false;
                    AppendString(builder, property.Key);
                    builder.Append(':');
                    AppendNode(builder, property.Value);
                }

                builder.Append('}');
                break;
            case JsonArray jsonArray:
                builder.Append('[');
                for (var index = 0; index < jsonArray.Count; index++)
                {
                    if (index > 0) builder.Append(',');
                    AppendNode(builder, jsonArray[index]);
                }

                builder.Append(']');
                break;
            case JsonValue jsonValue:
                AppendValue(builder, jsonValue);
                break;
            default:
                throw new ArgumentException("Unsupported JSON node in licensing details.");
        }
    }

    private static void AppendValue(StringBuilder builder, JsonValue value)
    {
        switch (value.GetValueKind())
        {
            case JsonValueKind.String:
                AppendString(builder, value.GetValue<string>());
                break;
            case JsonValueKind.True:
                builder.Append("true");
                break;
            case JsonValueKind.False:
                builder.Append("false");
                break;
            case JsonValueKind.Null:
                builder.Append("null");
                break;
            case JsonValueKind.Number when long.TryParse(value.ToJsonString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var integer):
                builder.Append(integer.ToString(CultureInfo.InvariantCulture));
                break;
            default:
                throw new ArgumentException("Licensing details accept only strings, integers, booleans, null, arrays and objects.");
        }
    }

    private static void AppendString(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (var character in value)
        {
            switch (character)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                case < ' ': builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture)); break;
                default: builder.Append(character); break;
            }
        }

        builder.Append('"');
    }
}
