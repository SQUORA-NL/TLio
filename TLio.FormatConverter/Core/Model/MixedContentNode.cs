namespace TLio.FormatConverter.Core.Model;

/// <summary>
/// Represents XML mixed content where text runs are interleaved with child elements
/// (e.g. <c>&lt;p&gt;text &lt;b&gt;bold&lt;/b&gt; more&lt;/p&gt;</c>).
/// Non-XML adapters collapse this to a concatenated string and set
/// <c>Metadata[NodeMetadata.MixedKey] = "true"</c>.
/// </summary>
public sealed class MixedContentNode : IntermediateNode
{
    /// <summary>Ordered content items: alternating <see cref="TextRun"/> and <see cref="ChildNode"/> entries.</summary>
    public IList<MixedContentItem> Content { get; init; } = new List<MixedContentItem>();
}

/// <summary>A single item within <see cref="MixedContentNode.Content"/>.</summary>
public abstract class MixedContentItem { }

/// <summary>A run of plain text within mixed content.</summary>
public sealed class TextRun : MixedContentItem
{
    /// <summary>The text content.</summary>
    public string Text { get; }

    /// <summary>Initialises a <see cref="TextRun"/> with the given <paramref name="text"/>.</summary>
    public TextRun(string text) => Text = text;
}

/// <summary>A child element embedded within mixed content.</summary>
public sealed class ChildNode : MixedContentItem
{
    /// <summary>The embedded child node.</summary>
    public IntermediateNode Node { get; }

    /// <summary>Initialises a <see cref="ChildNode"/> wrapping the given <paramref name="node"/>.</summary>
    public ChildNode(IntermediateNode node) => Node = node;
}
