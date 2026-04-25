namespace FormatConverter.Core.Model;

/// <summary>
/// Represents a named container with ordered children.
/// Maps to: JSON object, XML element, YAML mapping, EDI segment.
/// </summary>
public sealed class ObjectNode : IntermediateNode
{
    /// <summary>Ordered child nodes. Repeated names are allowed (e.g. repeating XML elements).</summary>
    public IList<IntermediateNode> Children { get; init; } = new List<IntermediateNode>();
}
