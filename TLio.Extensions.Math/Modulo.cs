using TLio.Core.Models;

namespace TLio.Extensions.Math;

/// <summary>=modulo(dividend, divisor) — remainder after integer division. Fails if divisor is 0.</summary>
public class Modulo<TNode> : MathFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count < 2)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: two arguments required (dividend, divisor).");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        if (!TryGetSingleArg(Arguments[0], out double dividend, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        if (!TryGetSingleArg(Arguments[1], out double divisor, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        if (divisor == 0)
        {
            context.LogError(FunctionName, $"{FunctionName}: divisor must not be zero.");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        return FunctionResult<TNode>.Successful(CreateNumericResult(dividend % divisor, context.NodeAdapter));
    }
}
