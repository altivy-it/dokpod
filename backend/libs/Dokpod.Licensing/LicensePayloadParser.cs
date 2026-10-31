// SPDX-License-Identifier: MIT
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Dokpod.Licensing;

/// <summary>
/// Parser estrito do payload da licença (spec 031 T023, D-003): UTF-8 estrito, profundidade até 8, sem propriedades
/// duplicadas nem desconhecidas, inteiros em <c>int32</c>, strings até 128 caracteres e instantes UTC com <c>Z</c>.
/// Só é chamado depois da assinatura conferida.
/// </summary>
public static partial class LicensePayloadParser
{
    public const string Product = "dokpod-business";
    public const int MaxDepth = 8;
    public const int MaxStringLength = 128;
    public const int MaxPlanLength = 64;
    public const int MaxCapabilities = 64;
    public const int MaxSlots = 2;
    public const int WindowDays = 30;
    public const int GraceDays = 30;

    private static readonly HashSet<string> RootProperties = new(
        ["schemaVersion", "meteringVersion", "licenseId", "organizationId", "revision", "product", "plan", "issuedAt",
         "notBefore", "expiresAt", "capacity", "vcpuBand", "windowDays", "graceDays", "slots", "capabilities"],
        StringComparer.Ordinal);

    public static bool TryParse(ReadOnlySpan<byte> payload, out LicenseDocument? document, out LicenseFailure failure)
    {
        document = null;
        failure = LicenseFailure.Malformed;
        if (!HasNoDuplicateProperties(payload)) return false;

        JsonDocument json;
        try
        {
            json = JsonDocument.Parse(payload.ToArray(), new JsonDocumentOptions { MaxDepth = MaxDepth });
        }
        catch (JsonException)
        {
            return false;
        }

        using (json)
        {
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;

            // Versão desconhecida é perfil não suportado, mesmo que traga campos que esta versão não conhece.
            if (!TryInt(root, "schemaVersion", out var schemaVersion) || !TryInt(root, "meteringVersion", out var meteringVersion)) return false;
            if (schemaVersion != 1 || meteringVersion != 1)
            {
                failure = LicenseFailure.UnsupportedProfile;
                return false;
            }

            if (root.EnumerateObject().Any(property => !RootProperties.Contains(property.Name))) return false;

            if (!TryId(root, "licenseId", out var licenseId)
                || !TryId(root, "organizationId", out var organizationId)
                || !TryInt(root, "revision", out var revision)
                || !TryString(root, "product", MaxStringLength, out var product)
                || !TryString(root, "plan", MaxPlanLength, out var plan)
                || !TryInstant(root, "issuedAt", out var issuedAt)
                || !TryInstant(root, "notBefore", out var notBefore)
                || !TryInstant(root, "expiresAt", out var expiresAt)
                || !TryInt(root, "vcpuBand", out var vcpuBand)
                || !TryInt(root, "windowDays", out var windowDays)
                || !TryInt(root, "graceDays", out var graceDays)
                || !root.TryGetProperty("capacity", out var capacityElement)
                || !root.TryGetProperty("slots", out var slotsElement)
                || !root.TryGetProperty("capabilities", out var capabilitiesElement)
                || revision < 1)
            {
                return false;
            }

            if (windowDays != WindowDays || graceDays != GraceDays)
            {
                failure = LicenseFailure.UnsupportedProfile;
                return false;
            }

            if (!string.Equals(product, Product, StringComparison.Ordinal))
            {
                failure = LicenseFailure.WrongProduct;
                return false;
            }

            if (!(issuedAt <= notBefore && notBefore < expiresAt))
            {
                failure = LicenseFailure.InconsistentDates;
                return false;
            }

            if (!TryCapacity(capacityElement, vcpuBand, out var capacity, out failure)
                || !TrySlots(slotsElement, capacity!, out var slots, out failure)
                || !TryCapabilities(capabilitiesElement, out var capabilities, out failure))
            {
                return false;
            }

            document = new LicenseDocument(
                schemaVersion, meteringVersion, licenseId!, organizationId!, revision, product!, plan!,
                issuedAt, notBefore, expiresAt, capacity!, vcpuBand, windowDays, graceDays, slots!, capabilities!);
            return true;
        }
    }

    private static bool TryCapacity(JsonElement element, int vcpuBand, out LicenseCapacity? capacity, out LicenseFailure failure)
    {
        capacity = null;
        failure = LicenseFailure.Malformed;
        if (element.ValueKind != JsonValueKind.Object
            || element.EnumerateObject().Any(property => property.Name is not ("mode" or "nodeEquivalents"))
            || !TryString(element, "mode", MaxStringLength, out var mode))
        {
            return false;
        }

        failure = LicenseFailure.InvalidCapacity;
        var hasNodes = element.TryGetProperty("nodeEquivalents", out var nodesElement);
        if (vcpuBand <= 0) return false;
        switch (mode)
        {
            case "unlimited" when !hasNodes:
                capacity = new LicenseCapacity(true, null);
                return true;
            case "limited" when hasNodes:
                if (!nodesElement.TryGetInt32(out var nodes))
                {
                    failure = LicenseFailure.Malformed;
                    return false;
                }

                if (nodes <= 0) return false;
                capacity = new LicenseCapacity(false, nodes);
                return true;
            default:
                return false;
        }
    }

    private static bool TrySlots(JsonElement element, LicenseCapacity capacity, out IReadOnlyList<LicenseSlot>? slots, out LicenseFailure failure)
    {
        slots = null;
        failure = LicenseFailure.Malformed;
        if (element.ValueKind != JsonValueKind.Array) return false;

        var result = new List<LicenseSlot>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || item.EnumerateObject().Any(property => property.Name is not ("slotId" or "kind" or "enabled" or "nodeEquivalents"))
                || !TryString(item, "slotId", 64, out var slotId)
                || !SlotPattern().IsMatch(slotId!)
                || !TryString(item, "kind", MaxStringLength, out var kind)
                || !item.TryGetProperty("enabled", out var enabledElement)
                || enabledElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                failure = LicenseFailure.Malformed;
                return false;
            }

            int? quota = null;
            if (item.TryGetProperty("nodeEquivalents", out var quotaElement))
            {
                if (!quotaElement.TryGetInt32(out var value))
                {
                    failure = LicenseFailure.Malformed;
                    return false;
                }

                quota = value;
            }

            failure = LicenseFailure.InvalidCapacity;
            if (kind is not ("production" or "staging")) return false;
            if (capacity.IsUnlimited ? quota is not null : quota is null or < 0) return false;
            result.Add(new LicenseSlot(slotId!, kind, enabledElement.GetBoolean(), quota));
        }

        failure = LicenseFailure.InvalidCapacity;
        if (result.Count is 0 or > MaxSlots) return false;
        if (result.Select(slot => slot.SlotId).Distinct(StringComparer.Ordinal).Count() != result.Count) return false;
        if (!capacity.IsUnlimited && result.Where(slot => slot.Enabled).Sum(slot => (long)slot.NodeEquivalents!.Value) > capacity.NodeEquivalents) return false;

        slots = result;
        return true;
    }

    private static bool TryCapabilities(JsonElement element, out IReadOnlyList<string>? capabilities, out LicenseFailure failure)
    {
        capabilities = null;
        failure = LicenseFailure.Malformed;
        if (element.ValueKind != JsonValueKind.Array) return false;

        var result = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) return false;
            var value = item.GetString()!;
            if (value.Length > MaxStringLength) return false;
            result.Add(value);
        }

        failure = LicenseFailure.UnknownCapability;
        if (result.Count > MaxCapabilities || result.Any(value => !LicenseCapabilities.Known.Contains(value))) return false;
        if (result.Distinct(StringComparer.Ordinal).Count() != result.Count)
        {
            failure = LicenseFailure.Malformed;
            return false;
        }

        capabilities = result;
        return true;
    }

    private static bool TryInt(JsonElement element, string name, out int value)
    {
        value = 0;
        return element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out value);
    }

    private static bool TryString(JsonElement element, string name, int maxLength, out string? value)
    {
        value = null;
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String) return false;
        value = property.GetString();
        return value is not null && value.Length is > 0 && value.Length <= maxLength;
    }

    private static bool TryId(JsonElement element, string name, out string? value) =>
        TryString(element, name, MaxStringLength, out value) && IdPattern().IsMatch(value!);

    private static bool TryInstant(JsonElement element, string name, out DateTimeOffset value)
    {
        value = default;
        return TryString(element, name, MaxStringLength, out var text)
            && InstantPattern().IsMatch(text!)
            && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out value);
    }

    /// <summary>Primeira passada: JSON bem formado em UTF-8 estrito, dentro da profundidade, sem chave repetida no mesmo objeto.</summary>
    private static bool HasNoDuplicateProperties(ReadOnlySpan<byte> payload)
    {
        try
        {
            var reader = new Utf8JsonReader(payload, new JsonReaderOptions { MaxDepth = MaxDepth });
            var scopes = new Stack<HashSet<string>?>();
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                        scopes.Push(new HashSet<string>(StringComparer.Ordinal));
                        break;
                    case JsonTokenType.StartArray:
                        scopes.Push(null);
                        break;
                    case JsonTokenType.EndObject:
                    case JsonTokenType.EndArray:
                        scopes.Pop();
                        break;
                    case JsonTokenType.PropertyName:
                        if (!scopes.Peek()!.Add(reader.GetString()!)) return false;
                        break;
                    case JsonTokenType.String:
                        _ = reader.GetString();
                        break;
                }
            }

            return reader.BytesConsumed == payload.Length;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    [GeneratedRegex(@"\A[A-Za-z0-9_-]{1,128}\z")]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"\A[A-Za-z0-9_-]{1,64}\z")]
    private static partial Regex SlotPattern();

    [GeneratedRegex(@"\A\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?Z\z")]
    private static partial Regex InstantPattern();
}
