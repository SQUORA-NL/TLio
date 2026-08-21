namespace FormatConverter.Core;

/// <summary>
/// Carries per-conversion-boundary adapter options from a <c>convert</c> command into both
/// <see cref="IFormatAdapter.ToIM"/> and <see cref="IFormatAdapter.FromIM"/>.
/// Settings are scoped to a single boundary step; they do not persist to later <c>convert</c> commands.
/// </summary>
/// <remarks>
/// <para>Adapters MUST NOT throw for settings they do not use — they silently ignore irrelevant fields.</para>
/// <para>Unknown JSON keys in the <c>settings</c> block of a <c>convert</c> command are logged as warnings
/// and do not produce an error.</para>
/// <para>Passing <see cref="Empty"/> to an adapter is equivalent to omitting the <c>settings</c> block —
/// all defaults apply.</para>
/// </remarks>
public sealed class ConversionSettings
{
    /// <summary>Singleton instance with all default values applied.</summary>
    public static ConversionSettings Empty { get; } = new();

    // ── XML settings ──────────────────────────────────────────────────────────

    /// <summary>
    /// Key used for XML text content when serialising to non-XML formats.
    /// Default: <c>"#text"</c>.
    /// </summary>
    public string TextProperty { get; init; } = "#text";

    /// <summary>
    /// Prefix prepended to XML attribute names when serialising to non-XML formats.
    /// Default: <c>"@"</c> (Badgerfish convention).
    /// </summary>
    public string AttributePrefix { get; init; } = "@";

    /// <summary>
    /// Prefix used for XML namespace declaration keys in metadata.
    /// Default: <c>"xmlns:"</c>.
    /// </summary>
    public string NamespacePrefix { get; init; } = "xmlns:";

    /// <summary>
    /// When <see langword="true"/>, coerce untyped string scalars to typed values
    /// (integer, decimal, boolean) based on their content.
    /// Default: <see langword="false"/>.
    /// </summary>
    public bool InferTypes { get; init; } = false;

    /// <summary>
    /// When <see langword="true"/>, treat CDATA sections as plain text and suppress
    /// the <c>#cdata</c> metadata key.
    /// Default: <see langword="false"/>.
    /// </summary>
    public bool CdataAsText { get; init; } = false;

    /// <summary>
    /// The element name an array item takes in XML when it has no name of its own.
    /// Default: <c>"item"</c>, the canonical item name TLio's own XML adapter recognises.
    /// </summary>
    /// <remarks>
    /// An array read from XML remembers the item name it was written with, and keeps it on the
    /// way back out; this names the ones that arrive without one — from JSON, from YAML, or from
    /// an array that was empty.
    /// </remarks>
    public string ArrayItemName { get; init; } = "item";

    /// <summary>
    /// Whether an array becomes a wrapping element with items inside it, or repeated sibling
    /// elements. Default: <see cref="ArrayHandling.Wrapped"/> — the shape TLio can address.
    /// </summary>
    public ArrayHandling ArrayHandling { get; init; } = ArrayHandling.Wrapped;

    /// <summary>
    /// How an absent value is written in XML. Default: <see cref="NullRepresentation.Empty"/>.
    /// </summary>
    public NullRepresentation NullRepresentation { get; init; } = NullRepresentation.Empty;

    /// <summary>
    /// What to do with a property name XML cannot spell.
    /// Default: <see cref="NameSanitization.Sanitize"/>, which is quiet and lossy.
    /// </summary>
    public NameSanitization NameSanitization { get; init; } = NameSanitization.Sanitize;

    // ── YAML settings ─────────────────────────────────────────────────────────

    /// <summary>
    /// When <see langword="true"/>, dereference YAML anchors and aliases inline
    /// and mark the dereferenced nodes with <c>Metadata["#anchor-flattened"] = "true"</c>.
    /// Default: <see langword="true"/>.
    /// </summary>
    public bool FlattenAnchors { get; init; } = true;
}
