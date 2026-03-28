using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Functions;

/// <summary>
/// =scriptpath() — returns the current node's absolute path as a string node.
/// When called with a relative path argument (@.&lt;-- etc.) that path is resolved
/// first and the resulting node's path is returned.
///
/// Ported from JLio's ScriptPath function.
/// </summary>
public class ScriptPath<TNode> : FunctionBase<TNode>
{
    public override string FunctionName => "scriptpath";

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        TNode targetNode;

        if (Arguments.Count > 0)
        {
            var argResult = Arguments[0].GetValue(currentNode, dataContext, context);
            if (argResult.Success && argResult.Data.First != null)
            {
                // If the argument is a relative path string (@.<-- etc.), resolve it
                // and navigate to that node before getting its path.
                var argStr = context.NodeAdapter.TryGetString(argResult.Data.First);
                if (argStr != null && argStr.StartsWith("@"))
                {
                    var resolvedPath = context.ItemsFetcher.ResolveRelativePath(argStr, currentNode, dataContext);
                    var nodes = context.ItemsFetcher.SelectNodes(resolvedPath, dataContext);
                    targetNode = nodes.Count > 0 ? nodes[0] : currentNode;
                }
                else
                {
                    targetNode = argResult.Data.First;
                }
            }
            else
            {
                targetNode = currentNode;
            }
        }
        else
        {
            targetNode = currentNode;
        }

        var path = context.ItemsFetcher.GetPath(targetNode);
        return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateString(path));
    }
}
