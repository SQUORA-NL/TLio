using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Functions.Collections;

/// <summary>
/// =distinct($.array) — returns a new array holding each distinct element once, in the order
/// they first appear.
///
/// De-duplication has no other expression in the language: <c>merge</c> with
/// <c>uniqueItemsWithoutKeys</c> is the only thing that comes close, and it needs a second
/// document to merge against.
///
/// Equality is <see cref="INodeAdapter{TNode}.DeepEquals"/>, so objects are compared
/// structurally rather than by reference and two identical <c>{"code":"WA"}</c> nodes collapse
/// into one. First occurrence wins, which is what makes the result stable enough to write to a
/// document and diff.
///
/// The input array is never touched — the result is a fresh array of deep clones.
/// </summary>
public class Distinct<TNode> : CollectionFunctionBase<TNode>
{
    public override string FunctionName => "distinct";

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count != 1)
        {
            context.LogError(FunctionName, $"{FunctionName}() requires exactly one array argument.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        if (!TryResolveElements(Arguments[0], out var elements, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);

        var adapter = context.NodeAdapter;
        var unique = new List<TNode>();
        foreach (var element in elements)
            if (!unique.Any(kept => adapter.DeepEquals(kept, element)))
                unique.Add(element);

        return FunctionResult<TNode>.Successful(BuildArray(unique, adapter));
    }
}
