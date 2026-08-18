using System.Text.RegularExpressions;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Functions.Logic;

/// <summary>
/// =in(value, option1, option2, ...) — true when the value equals any of the options.
/// When a single option argument resolves to an array, its elements are the options:
/// <c>=in($.status, $.allowedStatuses)</c>.
/// Equality follows the same cross-type rules as =equals.
/// </summary>
public class InFunction<TNode> : PredicateFunctionBase<TNode>
{
    public override string FunctionName => "in";

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (!RequireArguments(2, currentNode, context, "at least two arguments required (value, option, ...)", out var failure))
            return failure;

        if (!TryResolveNode(Arguments[0], currentNode, dataContext, context, out var value))
            return Boolean(false, context);

        var adapter = context.NodeAdapter;

        for (int i = 1; i < Arguments.Count; i++)
        {
            if (!TryResolveNode(Arguments[i], currentNode, dataContext, context, out var option))
                continue;

            if (adapter.IsArray(option))
            {
                if (adapter.GetArrayElements(option).Any(el => NodeComparison.AreEqual(value, el, adapter)))
                    return Boolean(true, context);
                continue;
            }

            if (NodeComparison.AreEqual(value, option, adapter))
                return Boolean(true, context);
        }

        return Boolean(false, context);
    }
}

/// <summary>
/// =matches(value, pattern) — true when the value's text matches the .NET regular expression.
/// The match is unanchored; anchor the pattern yourself with ^ and $ when you need a full match.
/// </summary>
public class MatchesFunction<TNode> : PredicateFunctionBase<TNode>
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    public override string FunctionName => "matches";

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (!RequireArguments(2, currentNode, context, "two arguments required (value, pattern)", out var failure))
            return failure;

        if (!TryResolveNode(Arguments[0], currentNode, dataContext, context, out var valueNode))
            return Boolean(false, context);
        if (!TryResolveNode(Arguments[1], currentNode, dataContext, context, out var patternNode))
        {
            context.LogWarning(FunctionName, $"{FunctionName}: pattern argument resolved to nothing.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        var value = context.NodeAdapter.TryGetString(valueNode) ?? string.Empty;
        var pattern = context.NodeAdapter.TryGetString(patternNode);
        if (pattern == null)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: pattern argument is not a string.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        try
        {
            // A timeout keeps a pathological pattern from hanging the whole transformation.
            return Boolean(Regex.IsMatch(value, pattern, RegexOptions.None, MatchTimeout), context);
        }
        catch (ArgumentException ex)
        {
            context.LogError(FunctionName, $"{FunctionName}: invalid pattern '{pattern}': {ex.Message}");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        catch (RegexMatchTimeoutException)
        {
            context.LogError(FunctionName, $"{FunctionName}: pattern '{pattern}' timed out.");
            return FunctionResult<TNode>.Failed(currentNode);
        }
    }
}
