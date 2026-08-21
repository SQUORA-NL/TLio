using TLio.Core.Models;

namespace TLio.Extensions.Math;

/// <summary>
/// =divide(dividend, divisor) — divides the first argument by the second. Both support arrays,
/// which are summed first, exactly as subtract does.
///
/// A divisor resolving to 0 fails the function rather than emitting infinity or NaN: an ∞
/// written into the document travels further than a script that stops.
/// </summary>
public class Divide<TNode> : MathFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count < 2)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: two arguments required (dividend, divisor).");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        double dividend = 0, divisor = 0;
        if (!TryAccumulateArg(Arguments[0], ref dividend, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        if (!TryAccumulateArg(Arguments[1], ref divisor, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        if (divisor == 0)
        {
            context.LogError(FunctionName, $"{FunctionName}: divisor must not be zero.");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        return FunctionResult<TNode>.Successful(CreateNumericResult(dividend / divisor, context.NodeAdapter));
    }
}
