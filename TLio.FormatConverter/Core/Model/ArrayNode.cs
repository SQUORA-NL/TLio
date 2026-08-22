namespace TLio.FormatConverter.Core.Model;

/// <summary>
/// Represents an ordered sequence of (possibly unnamed) items.
/// Maps to: JSON array, YAML sequence, CSV file, EDI element group.
/// </summary>
public sealed class ArrayNode : IntermediateNode
{
    /// <summary>Ordered items. Items may be unnamed (null <see cref="IntermediateNode.Name"/>).</summary>
    public IList<IntermediateNode> Items { get; init; } = new List<IntermediateNode>();
}
