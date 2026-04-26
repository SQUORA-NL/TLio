namespace FormatConverter.Core.Model;

/// <summary>
/// Format-specific key-value annotations attached to every <see cref="IntermediateNode"/>.
/// Keys follow documented prefix conventions; adapters must not throw for keys they do not recognise.
/// </summary>
public sealed class NodeMetadata : Dictionary<string, string>
{
    /// <summary>Prefix used for XML attribute keys (e.g. <c>@id</c>).</summary>
    public const string AttributePrefix = "@";

    /// <summary>Prefix used for XML namespace declaration keys (e.g. <c>xmlns:ns</c>).</summary>
    public const string NamespacePrefix = "xmlns:";

    /// <summary>Key for the XML default namespace declaration.</summary>
    public const string DefaultNamespaceKey = "xmlns";

    /// <summary>Key for plain-text XML content within an element.</summary>
    public const string TextProperty = "#text";

    /// <summary>Key set to <c>"true"</c> when the source was a CDATA section.</summary>
    public const string CdataKey = "#cdata";

    /// <summary>Key set to <c>"true"</c> when mixed XML content was collapsed to a string.</summary>
    public const string MixedKey = "#mixed";

    /// <summary>Key set to <c>"true"</c> when a YAML anchor/alias was dereferenced inline.</summary>
    public const string AnchorFlattenedKey = "#anchor-flattened";

    /// <summary>Prefix for processing-instruction data (e.g. <c>#pi:xml-stylesheet</c>).</summary>
    public const string PiPrefix = "#pi:";
}
