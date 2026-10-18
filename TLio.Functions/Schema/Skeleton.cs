using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Functions.Schema;

/// <summary>
/// =skeleton(schema) — builds the empty document a schema describes: every declared property in
/// declaration order, empty arrays where the schema says array, the schema's own <c>default</c>
/// (or <c>const</c>) wherever it gives one, and null elsewhere.
///
/// It is the schema counterpart of <c>merge</c>, in either direction:
///   - skeleton as the <b>target</b>, <c>fullMerge</c>: the data wins and the schema's property
///     order leads;
///   - skeleton as the <b>source</b>, <c>onlyStructure</c>: the document keeps every value (and its
///     own order) and gains the properties and defaults it lacked.
/// Arrays of objects cannot be shaped that way — a skeleton has no per-item content — which is
/// why <c>orderBySchema</c> is its own function and not a merge.
///
/// A blank document cannot be two shapes at once, so a <c>oneOf</c> / <c>anyOf</c> commits to its
/// <b>first</b> branch; <c>allOf</c> members are all included. A recursive schema is cut where it
/// loops back on itself.
/// </summary>
public class Skeleton<TNode> : SchemaFunctionBase<TNode>
{
    public override string FunctionName => "skeleton";

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count != 1)
        {
            context.LogError(FunctionName, $"{FunctionName}() requires exactly one schema (a path or JSON text).");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        if (!TryReadSchema(Arguments[0], currentNode, dataContext, context, out var document))
            return FunctionResult<TNode>.Failed(currentNode);

        var view = new SchemaView<TNode>(document, new[] { document.Root }, FirstBranchSelector<TNode>.Instance);
        return FunctionResult<TNode>.Successful(SchemaShaper.Skeleton(view, context.NodeAdapter));
    }
}
