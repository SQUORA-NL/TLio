using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Functions;

/// <summary>
/// =toArray() or =toArray($.path) — wraps a node in a new array, with the current value, if
/// any, as the first (and only) element. With no argument, the node wrapped is the current
/// item — the node the command is presently processing, e.g. one match of a wildcard path —
/// the same "current node" <c>scriptpath()</c> falls back to when called bare.
///
/// The direct sibling of <c>promote</c>: promote adds an object layer around a node,
/// toArray adds an array layer.
///
/// A path that matches nothing, or matches an explicit null, has no current value to place —
/// the result is an empty array rather than a failure, since "no value yet" is exactly the
/// case this function normalizes for (e.g. building a value that is sometimes absent,
/// sometimes a scalar, sometimes already an array, always as an array). A node that is
/// already an array is returned as a deep clone, unchanged — toArray does not nest an array
/// inside another array.
/// </summary>
public class ToArray<TNode> : FunctionBase<TNode>
{
    public override string FunctionName => "toArray";

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        var adapter = context.NodeAdapter;

        if (Arguments.Count == 0)
            return Wrap(currentNode, adapter);

        if (Arguments.Count != 1)
        {
            context.LogError(FunctionName, $"{FunctionName}() takes at most one path argument.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        var pathResult = Arguments[0].GetValue(currentNode, dataContext, context);
        if (!pathResult.Success || pathResult.Data.First == null)
            return FunctionResult<TNode>.Failed(currentNode);

        var pathStr = adapter.TryGetString(pathResult.Data.First);
        if (pathStr == null)
            return FunctionResult<TNode>.Failed(currentNode);

        var resolved = context.ItemsFetcher.SelectNodes(pathStr, dataContext);
        if (resolved.Count == 0)
            return FunctionResult<TNode>.Successful(adapter.CreateArray());

        return Wrap(resolved[0], adapter);
    }

    private static FunctionResult<TNode> Wrap(TNode node, INodeAdapter<TNode> adapter)
    {
        if (adapter.IsNull(node))
            return FunctionResult<TNode>.Successful(adapter.CreateArray());

        if (adapter.IsArray(node))
            return FunctionResult<TNode>.Successful(adapter.DeepClone(node));

        var array = adapter.CreateArray();
        adapter.AppendToArray(array, adapter.DeepClone(node));
        return FunctionResult<TNode>.Successful(array);
    }
}
