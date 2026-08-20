using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Functions;

/// <summary>
/// =fetch($.path) — evaluates the path argument and returns the first matched node.
///
/// Invocation styles (both are equivalent at the top level of a command value):
///   =fetch($.order.email)          — bare path
///   =fetch('$.order.email')        — quoted path
///
/// Dynamic path computation (evaluate an expression to produce the path at runtime):
///   =fetch('=indirect($.pathField)')   — indirect reads the path string from $.pathField
///   =fetch('=concat($.prefix, $.sfx)') — concat builds the path from field values
///
/// Path-detection rule: if the argument resolves to a string the fetcher recognises as a
/// path expression it is used as a path; otherwise the resolved value is returned as-is.
/// What counts as a path is the format's own syntax — '$'/'@' for JSON and YAML, a leading
/// '/' for XML — see IItemsFetcher.IsPathExpression.
/// This means a non-path result from a nested function is returned directly without
/// an attempted (and failing) SelectNodes call.
///
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

        // If the argument resolved to a string the fetcher reads as a path expression, use it
        // as a path. Any other string or non-string value is returned directly — this handles
        // both the "fetch returns its own computed value" case and prevents number/bool→string
        // coercions from being misinterpreted as path expressions.
        var pathStr = context.NodeAdapter.TryGetString(pathResult.Data.First);
        if (pathStr == null || !context.ItemsFetcher.IsPathExpression(pathStr))
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
