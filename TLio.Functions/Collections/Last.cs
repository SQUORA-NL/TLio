using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Functions.Collections;

/// <summary>
/// =last($.path[*]) — returns the last node matched by the path argument.
///
/// The direct sibling of <c>partial</c>, which counts from the front only: reaching the end of a
/// collection with <c>partial</c> means knowing its length first, and the length of a collection
/// is not something a script can compute into an argument position.
///
/// Like <c>partial</c>, this works on the <b>matched node set</b>, not on an array value — so
/// <c>=last($.items[*])</c> is the last item, while <c>=last($.items)</c> matches one node (the
/// array) and returns the array itself.
///
/// A path that matches nothing fails, which is what <c>partial</c> does with an index that
/// addresses nothing.
/// </summary>
public class Last<TNode> : FunctionBase<TNode>
{
    public override string FunctionName => "last";

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count != 1)
        {
            context.LogError(FunctionName, $"{FunctionName}() requires exactly one path argument.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        var pathResult = Arguments[0].GetValue(currentNode, dataContext, context);
        if (!pathResult.Success || pathResult.Data.Count == 0)
            return FunctionResult<TNode>.Failed(currentNode);

        var pathStr = context.NodeAdapter.TryGetString(pathResult.Data[0]);
        if (pathStr == null)
            return FunctionResult<TNode>.Failed(currentNode);

        var nodes = context.ItemsFetcher.SelectNodes(pathStr, dataContext);
        if (nodes.Count == 0)
        {
            context.LogError(FunctionName, $"{FunctionName}(): path '{pathStr}' matched no nodes.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        return FunctionResult<TNode>.Successful(nodes[^1]);
    }
}
