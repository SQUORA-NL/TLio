namespace TLio.Functions.Schema;

/// <summary>Which composition keyword the alternatives came from.</summary>
public enum BranchKind
{
    OneOf,
    AnyOf,
    /// <summary><c>then</c> / <c>else</c> of an <c>if</c>.</summary>
    Conditional
}

/// <summary>
/// Decides which branches of a <c>oneOf</c> / <c>anyOf</c> / <c>if-then-else</c> contribute to the
/// shape at a given place in the document.
///
/// Phase 1 ships two answers that need no data: <see cref="UnionBranchSelector{TNode}"/> (every
/// branch — the safe default, because with no validation we cannot know which branch the data
/// matches and picking one would silently drop data) and <see cref="FirstBranchSelector{TNode}"/>
/// (what <c>skeleton</c> uses, since a blank document has to commit to one shape).
///
/// The <c>data</c> parameter exists for phase 2: a discriminator selector looks at a
/// <c>const</c>/<c>enum</c> property of the data node and keeps only the branch whose
/// <see cref="SchemaNode{TNode}.AllowedValues"/> match. Nothing in phase 1 reads it, but every
/// call site already passes it, so phase 2 is a new selector rather than a new traversal.
/// </summary>
public interface IBranchSelector<TNode>
{
    IReadOnlyList<SchemaNode<TNode>> Select(
        BranchKind kind,
        IReadOnlyList<SchemaNode<TNode>> alternatives,
        TNode? data);
}

/// <summary>Every branch contributes. Order is branch order.</summary>
public sealed class UnionBranchSelector<TNode> : IBranchSelector<TNode>
{
    public static readonly UnionBranchSelector<TNode> Instance = new();

    public IReadOnlyList<SchemaNode<TNode>> Select(
        BranchKind kind, IReadOnlyList<SchemaNode<TNode>> alternatives, TNode? data) => alternatives;
}

/// <summary>Only the first branch of each group contributes.</summary>
public sealed class FirstBranchSelector<TNode> : IBranchSelector<TNode>
{
    public static readonly FirstBranchSelector<TNode> Instance = new();

    public IReadOnlyList<SchemaNode<TNode>> Select(
        BranchKind kind, IReadOnlyList<SchemaNode<TNode>> alternatives, TNode? data) =>
        alternatives.Count <= 1 ? alternatives : new[] { alternatives[0] };
}
