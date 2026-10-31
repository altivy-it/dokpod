// SPDX-License-Identifier: MIT
using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Dokpod.Licensing;

/// <summary>Resultado da verificação do relatório (contracts/report-format.md#verificação-pelo-fornecedor).</summary>
public enum ReportVerificationCode
{
    Ok,
    Tampered,
    Discontinuous,
    Mismatch,
    Stale,
}

/// <summary>Licença emitida pelo fornecedor, como no registro de emissões: <c>ContentSha256</c> em hexadecimal minúsculo.</summary>
public sealed record IssuedLicense(string LicenseId, int Revision, string ContentSha256);

/// <summary>O que o fornecedor sabe: desafio do pedido, instalação e último elo aceitos e tudo o que emitiu.</summary>
public sealed record ReportVerificationContext(
    string Challenge,
    Guid? InstallationId,
    long? PreviousHeadSeq,
    string? PreviousHeadHash,
    IReadOnlyList<IssuedLicense> IssuedLicenses,
    IReadOnlyList<int> IssuedRevocationSequences);

/// <summary>
/// <see cref="Gaps"/> e <see cref="ClockRegressions"/> são informativos e não bloqueiam: dias sem resumo e eventos com
/// instante anterior ao do evento anterior (relógio atrasado, que também deixa o estado indeterminado, ou concorrência).
/// A ordem da cadeia é garantida pelos hashes, não pelos instantes.
/// </summary>
public sealed record ReportVerificationResult(
    ReportVerificationCode Code,
    IReadOnlyList<string> Findings,
    IReadOnlyList<DateOnly> Gaps,
    Guid? InstallationId,
    long HeadSeq,
    string? HeadHash)
{
    public IReadOnlyList<long> ClockRegressions { get; init; } = [];

    public bool BlocksIssuance => Code != ReportVerificationCode.Ok;
}

/// <summary>
/// Verificador do relatório de apuração (spec 031 T071, FR-034, FR-035), só BCL e sem I/O. Recalcula a cadeia inteira
/// com uma implementação própria do JSON canônico, confere a continuidade com o último relatório aceito, a instalação,
/// o desafio e o cruzamento com as emissões. A ordem de gravidade é <c>tampered</c>, <c>discontinuous</c>,
/// <c>mismatch</c>, <c>stale</c>; <c>gaps</c> é só informativo e não bloqueia.
/// </summary>
public static partial class LicensingReportVerifier
{
    private static readonly byte[] GenesisPrefix = Encoding.UTF8.GetBytes("dokpod-licensing-chain-v1");

    public static ReportVerificationResult Verify(ReadOnlySpan<byte> report, ReportVerificationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(report.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
        }
        catch (JsonException)
        {
            return Failure(ReportVerificationCode.Tampered, "relatório não é JSON válido");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("reportVersion", out var version) || version.ValueKind != JsonValueKind.Number || version.GetInt32() != 1
                || !TryGuid(root, "installationId", out var installationId)
                || !root.TryGetProperty("events", out var events) || events.ValueKind != JsonValueKind.Array
                || !root.TryGetProperty("chain", out var chain) || chain.ValueKind != JsonValueKind.Object)
            {
                return Failure(ReportVerificationCode.Tampered, "estrutura do relatório inválida");
            }

            var tampered = new List<string>();
            var regressions = new List<long>();
            var previous = SHA256.HashData([.. GenesisPrefix, .. Encoding.UTF8.GetBytes(installationId.ToString("D"))]);
            var hashesBySeq = new Dictionary<long, string>();
            var parsed = new List<(string Action, JsonElement Details, DateTimeOffset At)>();
            long expectedSeq = 1;
            DateTimeOffset? lastAt = null;
            foreach (var item in events.EnumerateArray())
            {
                if (!TryReadEvent(item, out var seq, out var eventId, out var occurredAt, out var at, out var actorRef, out var correlationId, out var action, out var details, out var prevHash, out var recordHash))
                {
                    tampered.Add($"evento {expectedSeq} malformado");
                    break;
                }

                if (seq != expectedSeq) tampered.Add($"chain_seq {seq} fora de ordem (esperado {expectedSeq})");
                if (!prevHash.AsSpan().SequenceEqual(previous)) tampered.Add($"prev_hash do evento {seq} não liga ao anterior");
                if (lastAt is { } before && at < before) regressions.Add(seq);

                var canonical = new StringBuilder();
                canonical.Append("{\"action\":");
                AppendString(canonical, action);
                canonical.Append(",\"actor_ref\":");
                if (actorRef is null) canonical.Append("null");
                else AppendString(canonical, actorRef);
                canonical.Append(",\"chain_seq\":").Append(seq.ToString(CultureInfo.InvariantCulture));
                canonical.Append(",\"correlation_id\":");
                AppendString(canonical, correlationId);
                canonical.Append(",\"details\":");
                if (!TryAppendCanonical(canonical, details))
                {
                    tampered.Add($"details do evento {seq} fora do JSON canônico");
                    break;
                }

                canonical.Append(",\"event_id\":");
                AppendString(canonical, eventId);
                canonical.Append(",\"occurred_at_utc\":");
                AppendString(canonical, occurredAt);
                canonical.Append('}');

                var computed = SHA256.HashData([.. prevHash, .. Encoding.UTF8.GetBytes(canonical.ToString())]);
                if (!computed.AsSpan().SequenceEqual(recordHash)) tampered.Add($"record_hash do evento {seq} não confere");

                hashesBySeq[seq] = Base64Url.EncodeToString(recordHash);
                parsed.Add((action, details.Clone(), at));
                previous = recordHash;
                lastAt = at;
                expectedSeq = seq + 1;
            }

            var headSeq = expectedSeq - 1;
            var headHash = headSeq > 0 ? hashesBySeq.GetValueOrDefault(headSeq) : null;
            if (!chain.TryGetProperty("headSeq", out var declaredSeq) || declaredSeq.ValueKind != JsonValueKind.Number || declaredSeq.GetInt64() != headSeq
                || !chain.TryGetProperty("headHash", out var declaredHash) || (headHash is not null && declaredHash.GetString() != headHash))
            {
                tampered.Add("cabeça declarada não corresponde à cadeia");
            }

            if (tampered.Count > 0) return new ReportVerificationResult(ReportVerificationCode.Tampered, tampered, [], installationId, headSeq, headHash) { ClockRegressions = regressions };

            var gaps = Gaps(parsed);
            var discontinuous = new List<string>();
            if (context.InstallationId is { } expectedInstallation && expectedInstallation != installationId) discontinuous.Add("installationId diferente do registrado");
            if (context.PreviousHeadSeq is { } previousSeq
                && (!hashesBySeq.TryGetValue(previousSeq, out var hashAtPrevious) || !string.Equals(hashAtPrevious, context.PreviousHeadHash, StringComparison.Ordinal)))
            {
                discontinuous.Add($"o evento {previousSeq} não tem o recordHash do último relatório aceito");
            }

            if (discontinuous.Count > 0) return new ReportVerificationResult(ReportVerificationCode.Discontinuous, discontinuous, gaps, installationId, headSeq, headHash) { ClockRegressions = regressions };

            var mismatch = Mismatches(parsed, context);
            if (mismatch.Count > 0) return new ReportVerificationResult(ReportVerificationCode.Mismatch, mismatch, gaps, installationId, headSeq, headHash) { ClockRegressions = regressions };

            var last = parsed.LastOrDefault();
            if (last.Action != "license.report_exported"
                || !last.Details.TryGetProperty("challenge", out var challenge)
                || challenge.GetString() != context.Challenge)
            {
                return new ReportVerificationResult(ReportVerificationCode.Stale, ["desafio ausente ou diferente do pedido"], gaps, installationId, headSeq, headHash) { ClockRegressions = regressions };
            }

            return new ReportVerificationResult(ReportVerificationCode.Ok, [], gaps, installationId, headSeq, headHash) { ClockRegressions = regressions };
        }
    }

    private static List<string> Mismatches(List<(string Action, JsonElement Details, DateTimeOffset At)> events, ReportVerificationContext context)
    {
        var findings = new List<string>();
        var imported = new HashSet<(string, int, string)>();
        foreach (var (action, details, _) in events.Where(e => e.Action == "license.imported"))
        {
            var key = (Text(details, "licenseId"), Integer(details, "revision"), Text(details, "contentSha256"));
            imported.Add(key);
            if (!context.IssuedLicenses.Any(issued => (issued.LicenseId, issued.Revision, issued.ContentSha256) == key))
            {
                findings.Add($"importação sem emissão correspondente: {key.Item1} revisão {key.Item2}");
            }
        }

        foreach (var issued in context.IssuedLicenses.Where(issued => !imported.Contains((issued.LicenseId, issued.Revision, issued.ContentSha256))))
        {
            findings.Add($"emissão sem importação: {issued.LicenseId} revisão {issued.Revision}");
        }

        var revocations = events.Where(e => e.Action == "revocation.imported").Select(e => Integer(e.Details, "sequence")).ToHashSet();
        findings.AddRange(revocations.Where(sequence => !context.IssuedRevocationSequences.Contains(sequence)).Select(sequence => $"revogação {sequence} sem emissão correspondente"));
        findings.AddRange(context.IssuedRevocationSequences.Where(sequence => !revocations.Contains(sequence)).Select(sequence => $"revogação {sequence} emitida e não importada"));
        return findings;
    }

    private static List<DateOnly> Gaps(List<(string Action, JsonElement Details, DateTimeOffset At)> events)
    {
        var summaries = events.Where(e => e.Action == "capacity.daily_summary").ToArray();
        if (summaries.Length == 0) return [];
        var dates = new HashSet<DateOnly>();
        var gaps = new SortedSet<DateOnly>();
        foreach (var (_, details, _) in summaries)
        {
            if (!DateOnly.TryParseExact(Text(details, "date"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) continue;
            dates.Add(date);
            if (details.TryGetProperty("late", out var late) && late.ValueKind == JsonValueKind.True) gaps.Add(date);
        }

        var lastClosedDay = DateOnly.FromDateTime(events[^1].At.UtcDateTime).AddDays(-1);
        for (var day = dates.Min(); day <= lastClosedDay; day = day.AddDays(1))
        {
            if (!dates.Contains(day)) gaps.Add(day);
        }

        return [.. gaps];
    }

    private static bool TryReadEvent(
        JsonElement item,
        out long seq,
        out string eventId,
        out string occurredAt,
        out DateTimeOffset at,
        out string? actorRef,
        out string correlationId,
        out string action,
        out JsonElement details,
        out byte[] prevHash,
        out byte[] recordHash)
    {
        seq = 0;
        eventId = occurredAt = correlationId = action = string.Empty;
        at = default;
        actorRef = null;
        details = default;
        prevHash = recordHash = [];
        if (item.ValueKind != JsonValueKind.Object
            || !item.TryGetProperty("chainSeq", out var seqElement) || !seqElement.TryGetInt64(out seq)
            || !TryText(item, "eventId", out eventId) || !Guid.TryParseExact(eventId, "D", out var eventGuid) || eventGuid.ToString("D") != eventId
            || !TryText(item, "occurredAt", out occurredAt) || !InstantPattern().IsMatch(occurredAt)
            || !DateTimeOffset.TryParse(occurredAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out at)
            || !TryText(item, "correlationId", out correlationId) || !Guid.TryParseExact(correlationId, "D", out var correlationGuid) || correlationGuid.ToString("D") != correlationId
            || !TryText(item, "action", out action)
            || !item.TryGetProperty("details", out details) || details.ValueKind != JsonValueKind.Object
            || !item.TryGetProperty("actorRef", out var actorElement) || actorElement.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)
            || !TryHash(item, "prevHash", out prevHash) || !TryHash(item, "recordHash", out recordHash))
        {
            return false;
        }

        actorRef = actorElement.ValueKind == JsonValueKind.String ? actorElement.GetString() : null;
        return true;
    }

    private static bool TryAppendCanonical(StringBuilder builder, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                builder.Append('{');
                var first = true;
                foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    if (!first) builder.Append(',');
                    first = false;
                    AppendString(builder, property.Name);
                    builder.Append(':');
                    if (!TryAppendCanonical(builder, property.Value)) return false;
                }

                builder.Append('}');
                return true;
            case JsonValueKind.Array:
                builder.Append('[');
                var index = 0;
                foreach (var value in element.EnumerateArray())
                {
                    if (index++ > 0) builder.Append(',');
                    if (!TryAppendCanonical(builder, value)) return false;
                }

                builder.Append(']');
                return true;
            case JsonValueKind.String:
                AppendString(builder, element.GetString()!);
                return true;
            case JsonValueKind.Number when element.TryGetInt64(out var integer):
                builder.Append(integer.ToString(CultureInfo.InvariantCulture));
                return true;
            case JsonValueKind.True:
                builder.Append("true");
                return true;
            case JsonValueKind.False:
                builder.Append("false");
                return true;
            case JsonValueKind.Null:
                builder.Append("null");
                return true;
            default:
                return false;
        }
    }

    private static void AppendString(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (var character in value)
        {
            _ = character switch
            {
                '"' => builder.Append("\\\""),
                '\\' => builder.Append("\\\\"),
                '\b' => builder.Append("\\b"),
                '\f' => builder.Append("\\f"),
                '\n' => builder.Append("\\n"),
                '\r' => builder.Append("\\r"),
                '\t' => builder.Append("\\t"),
                < ' ' => builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture)),
                _ => builder.Append(character),
            };
        }

        builder.Append('"');
    }

    private static bool TryGuid(JsonElement element, string name, out Guid value)
    {
        value = Guid.Empty;
        return TryText(element, name, out var text) && Guid.TryParseExact(text, "D", out value);
    }

    private static bool TryText(JsonElement element, string name, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String) return false;
        value = property.GetString()!;
        return true;
    }

    private static bool TryHash(JsonElement element, string name, out byte[] hash)
    {
        hash = [];
        if (!TryText(element, name, out var text)) return false;
        try
        {
            hash = Base64Url.DecodeFromChars(text);
        }
        catch (FormatException)
        {
            return false;
        }

        return hash.Length == 32;
    }

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : string.Empty;

    private static int Integer(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var integer) ? integer : 0;

    private static ReportVerificationResult Failure(ReportVerificationCode code, string finding) => new(code, [finding], [], null, 0, null);

    [GeneratedRegex(@"\A\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{6}Z\z")]
    private static partial Regex InstantPattern();
}
