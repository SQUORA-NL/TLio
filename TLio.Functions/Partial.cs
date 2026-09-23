using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Functions;

/// <summary>
/// =partial($.path, n) — returns the nth element (0-based) from the nodes matched
/// by the first argument, or the first element when n is omitted.
///
/// Ported from JLio's Partial function.
/// </summary>
public class Partial<TNode> : FunctionBase<TNode>
{
    public override string FunctionName => "partial";

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count == 0)
        {
            context.LogWarning(FunctionName, "partial() requires at least one path argument.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        // Treat the first argument as a path expression and select all matching nodes
        var pathResult = Arguments[0].GetValue(currentNode, dataContext, context);
        if (!pathResult.Success || pathResult.Data.First == null)
            return FunctionResult<TNode>.Failed(currentNode);

        var pathStr = context.NodeAdapter.TryGetString(pathResult.Data.First);
        if (pathStr == null)
            return FunctionResult<TNode>.Failed(currentNode);

        // A relative path ("@.field") is anchored on currentNode, not the document root.
        var nodes = RelativePathResolution.SelectRelative(pathStr, currentNode, dataContext, context);
        if (nodes.Count == 0)
            return FunctionResult<TNode>.Failed(currentNode);

        var index = 0;
        if (Arguments.Count >= 2)
        {
            var indexResult = Arguments[1].GetValue(currentNode, dataContext, context);
            if (indexResult.Success && indexResult.Data.First != null)
            {
                var num = context.NodeAdapter.TryGetDouble(indexResult.Data.First);
                if (num.HasValue) index = (int)num.Value;
            }
        }

        if (index < 0 || index >= nodes.Count)
        {
            context.LogWarning(FunctionName, $"partial() index {index} out of range (count={nodes.Count}).");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        return FunctionResult<TNode>.Successful(nodes[index]);
    }
}
