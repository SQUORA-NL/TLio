using TLio.Core.Models;

namespace TLio.Extensions.Math;

/// <summary>
/// =clamp(value, low, high) — bounds value to the inclusive range [low, high].
///
/// A low above high fails rather than swapping the bounds or picking one: an inverted band is a
/// broken rate table, and guessing which bound was meant hides the mistake.
/// </summary>
public class Clamp<TNode> : MathFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count < 3)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: three arguments required (value, low, high).");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        if (!TryGetSingleArg(Arguments[0], out double value, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        if (!TryGetSingleArg(Arguments[1], out double low, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        if (!TryGetSingleArg(Arguments[2], out double high, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        if (low > high)
        {
            context.LogError(FunctionName, $"{FunctionName}: low bound must not exceed high bound.");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        var bounded = System.Math.Min(System.Math.Max(value, low), high);
        return FunctionResult<TNode>.Successful(CreateNumericResult(bounded, context.NodeAdapter));
    }
}
