using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Functions;

/// <summary>
/// =indirect($.pathToPath) — reads the string stored at the given path, then
/// selects nodes using that string as a second path expression.
///
/// Example: if $.ref = "$.person.name", then =indirect($.ref) returns the
/// value of $.person.name.
///
/// Ported from JLio's Indirect function.
/// </summary>
public class Indirect<TNode> : FunctionBase<TNode>
{
    public override string FunctionName => "indirect";

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count == 0)
        {
            context.LogWarning(FunctionName, "indirect() requires one path argument.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        var refResult = Arguments[0].GetValue(currentNode, dataContext, context);
        if (!refResult.Success || refResult.Data.First == null)
            return FunctionResult<TNode>.Failed(currentNode);

        var actualPath = context.NodeAdapter.TryGetString(refResult.Data.First);
        if (string.IsNullOrEmpty(actualPath))
        {
            context.LogWarning(FunctionName, "indirect() path reference did not resolve to a string.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        var nodes = context.ItemsFetcher.SelectNodes(actualPath, dataContext);
        if (nodes.Count == 0)
            return FunctionResult<TNode>.Failed(currentNode);

        return FunctionResult<TNode>.Successful(nodes);
    }
}
