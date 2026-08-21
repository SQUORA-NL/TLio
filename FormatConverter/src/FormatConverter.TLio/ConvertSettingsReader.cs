using System.Text.Json;
using FormatConverter.Core;

namespace FormatConverter.TLio;

/// <summary>
/// Reads the <c>settings</c> block of a <c>convert</c> command.
/// </summary>
/// <remarks>
/// One reader, because both the command and <see cref="MultiFormatScriptRunner"/> need it: the
/// runner intercepts <c>convert</c> before the engine ever builds the command. They used to parse
/// the block separately, which is how a setting could be added to one and forgotten in the other.
/// </remarks>
public static class ConvertSettingsReader
{
    /// <summary>
    /// Read the settings from a <c>convert</c> command element, falling back to the documented
    /// default for anything the script does not mention.
    /// </summary>
    public static ConversionSettings Read(JsonElement command)
    {
        if (!command.TryGetProperty("settings", out var s) || s.ValueKind != JsonValueKind.Object)
            return ConversionSettings.Empty;

        var defaults = ConversionSettings.Empty;
        return new ConversionSettings
        {
            TextProperty = String(s, "textProperty", defaults.TextProperty),
            AttributePrefix = String(s, "attributePrefix", defaults.AttributePrefix),
            NamespacePrefix = String(s, "namespacePrefix", defaults.NamespacePrefix),
            ArrayItemName = String(s, "arrayItemName", defaults.ArrayItemName),
            InferTypes = Bool(s, "inferTypes", defaults.InferTypes),
            CdataAsText = Bool(s, "cdataAsText", defaults.CdataAsText),
            FlattenAnchors = Bool(s, "flattenAnchors", defaults.FlattenAnchors),
            ArrayHandling = Enum(s, "arrayHandling", defaults.ArrayHandling),
            NullRepresentation = Enum(s, "nullRepresentation", defaults.NullRepresentation),
            NameSanitization = Enum(s, "nameSanitization", defaults.NameSanitization),
        };
    }

    private static string String(JsonElement el, string name, string fallback) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? fallback
            : fallback;

    private static bool Bool(JsonElement el, string name, bool fallback) =>
        el.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? v.GetBoolean()
            : fallback;

    /// <summary>
    /// Enum members are written the way the rest of a script is — camelCase, as
    /// <c>"xsiNil"</c> rather than <c>"XsiNil"</c>. An unrecognised value keeps the default
    /// rather than throwing: a conversion setting nobody understands is not worth failing a
    /// script over, and the documented behaviour is what the reader gets.
    /// </summary>
    private static TEnum Enum<TEnum>(JsonElement el, string name, TEnum fallback) where TEnum : struct
    {
        if (!el.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.String)
            return fallback;

        return System.Enum.TryParse<TEnum>(v.GetString(), ignoreCase: true, out var parsed)
            ? parsed
            : fallback;
    }
}
