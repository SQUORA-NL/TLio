namespace TLio.FormatConverter.Core.Model;

/// <summary>
/// Abstract base for all nodes in the Intermediate Model (IM).
/// The IM is an internal transport between format adapters — script authors never interact with it directly.
/// </summary>
public abstract class IntermediateNode
{
    /// <summary>
    /// Property or element name. <see langword="null"/> for anonymous array items and the document root.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>Format-specific annotations. Always initialised; never <see langword="null"/>.</summary>
    public NodeMetadata Metadata { get; init; } = new();
}
