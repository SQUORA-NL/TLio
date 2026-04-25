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

    // ── YAML settings ─────────────────────────────────────────────────────────

    /// <summary>
    /// When <see langword="true"/>, dereference YAML anchors and aliases inline
    /// and mark the dereferenced nodes with <c>Metadata["#anchor-flattened"] = "true"</c>.
    /// Default: <see langword="true"/>.
    /// </summary>
    public bool FlattenAnchors { get; init; } = true;
}
