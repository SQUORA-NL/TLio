using System.Text.Json;
using TLio.FormatConverter.Core;

namespace TLio.FormatConverter;

/// <summary>
/// Reads the <c>settings</c> block of a <c>convert</c> command.
/// </summary>
/// <remarks>
/// <para>
/// One reader, because both the command and <see cref="MultiFormatScriptRunner"/> need it: the
/// runner intercepts <c>convert</c> before the engine ever builds the command. They used to parse
/// the block separately, which is how a setting could be added to one and forgotten in the other.
/// </para>
/// <para>
/// The same reason now keeps the three notations together. A settings block is a flat map of
/// scalars in every notation — JSON objects, XML child elements, YAML mappings — so the field
/// list lives once, in <see cref="Read(Func{string, string})"/>, and each notation only has to
/// say how to look a name up.
/// </para>
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

        return Read(name => s.TryGetProperty(name, out var v) ? ScalarText(v) : null);
    }

    /// <summary>
    /// Read the settings from any notation, given a lookup that returns a setting's value as
    /// text or <see langword="null"/> when the script does not mention it.
    ///
    /// This is the one place the field list is written down. Everything a settings block can
    /// hold is a scalar, so text is enough to carry it: <c>"true"</c> for the flags and the
    /// camelCase member name for the enums, which is how a script spells them anyway.
    /// </summary>
    public static ConversionSettings Read(Func<string, string?> lookup)
    {
        ArgumentNullException.ThrowIfNull(lookup);

        var defaults = ConversionSettings.Empty;
        return new ConversionSettings
        {
            TextProperty = Text(lookup, "textProperty", defaults.TextProperty),
            AttributePrefix = Text(lookup, "attributePrefix", defaults.AttributePrefix),
            NamespacePrefix = Text(lookup, "namespacePrefix", defaults.NamespacePrefix),
            ArrayItemName = Text(lookup, "arrayItemName", defaults.ArrayItemName),
            InferTypes = Bool(lookup, "inferTypes", defaults.InferTypes),
            CdataAsText = Bool(lookup, "cdataAsText", defaults.CdataAsText),
            FlattenAnchors = Bool(lookup, "flattenAnchors", defaults.FlattenAnchors),
            ArrayHandling = Enum(lookup, "arrayHandling", defaults.ArrayHandling),
            NullRepresentation = Enum(lookup, "nullRepresentation", defaults.NullRepresentation),
            NameSanitization = Enum(lookup, "nameSanitization", defaults.NameSanitization),
        };
    }

    /// <summary>
    /// A settings value as text. Objects, arrays, numbers and null are not what any of these
    /// settings hold, so they read as absent and the default stands.
    /// </summary>
    private static string? ScalarText(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => null,
    };

    private static string Text(Func<string, string?> lookup, string name, string fallback) =>
        lookup(name) ?? fallback;

    private static bool Bool(Func<string, string?> lookup, string name, bool fallback) =>
        bool.TryParse(lookup(name), out var parsed) ? parsed : fallback;

    /// <summary>
    /// Enum members are written the way the rest of a script is — camelCase, as
    /// <c>"xsiNil"</c> rather than <c>"XsiNil"</c>. An unrecognised value keeps the default
    /// rather than throwing: a conversion setting nobody understands is not worth failing a
    /// script over, and the documented behaviour is what the reader gets.
    /// </summary>
    private static TEnum Enum<TEnum>(Func<string, string?> lookup, string name, TEnum fallback)
        where TEnum : struct =>
        System.Enum.TryParse<TEnum>(lookup(name), ignoreCase: true, out var parsed)
            ? parsed
            : fallback;
}
