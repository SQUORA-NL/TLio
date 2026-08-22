using TLio.FormatConverter.Core.Model;

namespace TLio.FormatConverter.Core;

/// <summary>
/// The rules a format without attributes uses to carry the ones XML has.
/// </summary>
/// <remarks>
/// <para>
/// XML puts three things on an element that JSON and YAML have no room for: attributes, namespace
/// declarations, and text that sits alongside either of them. The convention is that all three
/// become ordinary keys — <c>@name</c> for an attribute, <c>xmlns:prefix</c> for a namespace, and
/// <see cref="ConversionSettings.TextProperty"/> for the text — so a document survives the trip
/// out to JSON or YAML and back.
/// </para>
/// <para>
/// It lives here rather than in each adapter because JSON and YAML have to spell it the same way.
/// When they disagreed, an attribute that survived <c>xml → json</c> vanished on <c>xml → yaml</c>.
/// </para>
/// <para>
/// Keys beginning <c>#</c> — <c>#cdata</c>, <c>#mixed</c>, <c>#anchor-flattened</c> — are internal
/// markers rather than document content and are deliberately not carried across.
/// </para>
/// </remarks>
public static class MetadataConvention
{
    /// <summary>
    /// Whether a property name read from JSON or YAML is metadata rather than a child node.
    /// </summary>
    public static bool IsMetadataKey(string key, ConversionSettings settings) =>
        IsAttributeKey(key, settings) || IsNamespaceKey(key, settings);

    /// <summary>Whether a key names an XML attribute (default <c>@name</c>).</summary>
    public static bool IsAttributeKey(string key, ConversionSettings settings)
    {
        var prefix = settings.AttributePrefix;
        // An empty prefix would make every key an attribute; treat it as "attributes are off".
        return prefix.Length > 0
            && key.Length > prefix.Length
            && key.StartsWith(prefix, StringComparison.Ordinal);
    }

    /// <summary>
    /// Whether a key names a namespace declaration — the default declaration <c>xmlns</c>, or a
    /// prefixed one under <see cref="ConversionSettings.NamespacePrefix"/>.
    /// </summary>
    public static bool IsNamespaceKey(string key, ConversionSettings settings)
    {
        if (key == NodeMetadata.DefaultNamespaceKey) return true;

        var prefix = settings.NamespacePrefix;
        return prefix.Length > 0
            && key.Length > prefix.Length
            && key.StartsWith(prefix, StringComparison.Ordinal);
    }

    /// <summary>
    /// The metadata entries a non-XML format writes out, in the node's own order.
    /// </summary>
    public static IEnumerable<KeyValuePair<string, string>> Emittable(
        IntermediateNode node, ConversionSettings settings)
    {
        foreach (var entry in node.Metadata)
        {
            if (IsMetadataKey(entry.Key, settings))
                yield return entry;
        }
    }

    /// <summary>Whether the node carries metadata a non-XML format has to find room for.</summary>
    public static bool HasEmittableMetadata(IntermediateNode node, ConversionSettings settings) =>
        Emittable(node, settings).Any();

    /// <summary>
    /// Whether a scalar has to be written as an object rather than a bare value.
    /// </summary>
    /// <remarks>
    /// <c>&lt;price currency="EUR"&gt;9.99&lt;/price&gt;</c> cannot be <c>"9.99"</c> in JSON — that
    /// throws the attribute away. It becomes
    /// <c>{"@currency": "EUR", "#text": "9.99"}</c> instead.
    /// </remarks>
    public static bool NeedsTextWrapper(ScalarNode scalar, ConversionSettings settings) =>
        HasEmittableMetadata(scalar, settings);

    /// <summary>
    /// Whether the wrapper carries a text key at all. An empty element with attributes —
    /// <c>&lt;item id="1"/&gt;</c> — has attributes to place and no value to place beside them, so
    /// it is <c>{"@id": "1"}</c> and not <c>{"@id": "1", "#text": null}</c>.
    /// </summary>
    public static bool WrapperCarriesText(ScalarNode scalar) => scalar.Type != ScalarType.Null;

    /// <summary>
    /// Recognise the object form of a scalar-with-attributes on the way back in: every key is
    /// metadata except one named <see cref="ConversionSettings.TextProperty"/>.
    /// </summary>
    /// <param name="childNames">Property names of the object, in document order.</param>
    /// <param name="settings">The conversion settings in force.</param>
    public static bool IsTextWrapper(IEnumerable<string> childNames, ConversionSettings settings)
    {
        var names = childNames.ToList();
        return names.Count == 1 && names[0] == settings.TextProperty;
    }
}
