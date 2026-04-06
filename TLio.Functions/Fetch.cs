using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Functions;

/// <summary>
/// =fetch($.path) — evaluates the path argument and returns the first matched node.
/// Returns a failed result when the path matches nothing.
/// Ported from JLio's Fetch function.
/// </summary>
public class Fetch<TNode> : FunctionBase<TNode>
{
    public override string FunctionName => "fetch";

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count == 0)
        {
            context.LogWarning(FunctionName, "fetch() requires one path argument.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        var pathResult = Arguments[0].GetValue(currentNode, dataContext, context);
        if (!pathResult.Success || pathResult.Data.First == null)
            return FunctionResult<TNode>.Failed(currentNode);

        // If the argument resolved to a string, treat it as a path expression
        var pathStr = context.NodeAdapter.TryGetString(pathResult.Data.First);
        if (pathStr == null)
            return FunctionResult<TNode>.Successful(pathResult.Data.First);

        var nodes = context.ItemsFetcher.SelectNodes(pathStr, dataContext);
        if (nodes.Count == 0)
        {
            // Optional second argument: return as default value when path resolves to nothing
            if (Arguments.Count >= 2)
            {
                var def = Arguments[1].GetValue(currentNode, dataContext, context);
                if (def.Success) return def;
            }
            return FunctionResult<TNode>.Failed(currentNode);
        }

        return FunctionResult<TNode>.Successful(nodes);
    }
}
