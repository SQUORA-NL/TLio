namespace TLio.Core.Models;

/// <summary>
/// The result of a path-expression evaluation — an ordered set of matched nodes.
/// Wraps an IEnumerable so the IItemsFetcher implementations can stay lazy.
/// </summary>
/// <typeparam name="TNode">Native node type of the target data format.</typeparam>
public class SelectedNodes<TNode> : List<TNode>
{
    public SelectedNodes() { }

    public SelectedNodes(IEnumerable<TNode> nodes) : base(nodes) { }

    public SelectedNodes(TNode single) { Add(single); }

    public TNode? First => Count > 0 ? this[0] : default;
}
