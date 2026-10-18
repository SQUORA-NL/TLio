using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Functions.Schema;

/// <summary>
/// =orderBySchema(schema, data) — returns a copy of the data with the properties of every object
/// in the order the schema declares them.
///
/// Ported from JLio's <c>orderBySchema</c>, without the schema library: only <c>properties</c>
/// (declaration order), <c>items</c> / <c>prefixItems</c>, local <c>$ref</c> and the composition
/// keywords are read. Properties the schema does not mention are kept and go last, in their
/// original order. Nothing is added or removed, and no value changes.
///
/// The data is named explicitly, like <c>sortby</c> names its array. JLio's implicit form —
/// "shape the node being set" — is not carried over: in TLio's legacy <c>set</c> the node a
/// function is evaluated against is the target's <i>parent</i>, so the implicit form would
/// silently shape the wrong object. Inside <c>forEach</c>, <c>@</c> names the current item.
///
/// Composition is a union: <c>allOf</c> members, and every branch of <c>oneOf</c> / <c>anyOf</c> /
/// <c>then</c> / <c>else</c>, all contribute properties, in branch order. Without validation there
/// is no knowing which branch the data matches, and a union never loses a property. The optional
/// third argument <c>'discriminate'</c> narrows a <c>oneOf</c>/<c>anyOf</c> to the branch whose
/// <c>const</c>/<c>enum</c> discriminator matches the data, where exactly one does
/// (<see cref="DiscriminatorBranchSelector{TNode}"/>); everywhere else it is still the union.
/// </summary>
public class OrderBySchema<TNode> : SchemaFunctionBase<TNode>
{
    public override string FunctionName => "orderBySchema";

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count is < 2 or > 3)
        {
            context.LogError(FunctionName,
                $"{FunctionName}() requires a schema (a path or JSON text), the data path to order and an optional '{DiscriminateFlag}'.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        var discriminate = false;
        if (Arguments.Count == 3)
        {
            if (!IsFlag(Arguments[2], DiscriminateFlag, currentNode, dataContext, context))
            {
                context.LogError(FunctionName, $"{FunctionName}(): the third argument must be '{DiscriminateFlag}'.");
                return FunctionResult<TNode>.Failed(currentNode);
            }
            discriminate = true;
        }

        if (!TryReadSchema(Arguments[0], currentNode, dataContext, context, out var document))
            return FunctionResult<TNode>.Failed(currentNode);

        if (!TryReadData(Arguments[1], currentNode, dataContext, context, out var data))
            return FunctionResult<TNode>.Failed(currentNode);

        var adapter = context.NodeAdapter;
        var result = adapter.DeepClone(data);
        var view = new SchemaView<TNode>(document, new[] { document.Root }, CreateSelector(discriminate, document, adapter));
        SchemaShaper.Order(result, view, adapter);

        return FunctionResult<TNode>.Successful(result);
    }
}
