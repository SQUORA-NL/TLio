using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Functions.Logic;

/// <summary>
/// Base class for the boolean-returning functions used as <c>ifElse</c> conditions.
///
/// These differ from the value functions in one important way: a path that matches nothing
/// is an ANSWER, not a failure. <c>=exists($.missing)</c> must return false and
/// <c>=isNull($.missing)</c> must return true, so argument resolution here reports
/// "not found" instead of failing the whole expression.
/// </summary>
public abstract class PredicateFunctionBase<TNode> : FunctionBase<TNode>
{
    /// <summary>
    /// Resolve an argument to a single node.
    /// Returns false when the argument is a path that matched nothing — <paramref name="node"/>
    /// is then default and the caller decides what that means.
    /// </summary>
    protected static bool TryResolveNode(
        IFunctionSupportedValue<TNode> arg,
        TNode currentNode, TNode dataContext,
        IExecutionContext<TNode> context,
        out TNode node)
    {
        node = default!;

        var result = arg.GetValue(currentNode, dataContext, context);
        if (!result.Success || result.Data.Count == 0) return false;

        var first = result.Data.First!;

        // Paths inside an argument list arrive as plain strings; resolve them here rather
        // than through ResolveArg, which cannot distinguish "no match" from "failed".
        var str = context.NodeAdapter.TryGetString(first);
        if (result.Data.Count == 1 && str != null && context.ItemsFetcher.IsPathExpression(str))
        {
            var nodes = context.ItemsFetcher.SelectNodes(str, dataContext);
            if (nodes.Count == 0) return false;
            node = nodes.First!;
            return true;
        }

        node = first;
        return true;
    }

    /// <summary>Resolve an argument and report its truthiness; a missing path is false.</summary>
    protected static bool ResolveTruthy(
        IFunctionSupportedValue<TNode> arg,
        TNode currentNode, TNode dataContext,
        IExecutionContext<TNode> context)
        => TryResolveNode(arg, currentNode, dataContext, context, out var node) &&
           NodeComparison.IsTruthy(node, context.NodeAdapter);

    protected FunctionResult<TNode> Boolean(bool value, IExecutionContext<TNode> context) =>
        FunctionResult<TNode>.Successful(context.NodeAdapter.CreateBoolean(value));

    protected bool RequireArguments(int count, TNode currentNode, IExecutionContext<TNode> context,
        string what, out FunctionResult<TNode> failure)
    {
        if (Arguments.Count >= count)
        {
            failure = default!;
            return true;
        }

        context.LogWarning(FunctionName, $"{FunctionName}: {what}.");
        failure = FunctionResult<TNode>.Failed(currentNode);
        return false;
    }
}
