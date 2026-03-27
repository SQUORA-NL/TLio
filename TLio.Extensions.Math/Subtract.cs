using TLio.Core.Models;

namespace TLio.Extensions.Math;

/// <summary>=subtract(base, subtract) — subtracts second argument from first. Both support arrays (summed first).</summary>
public class Subtract<TNode> : MathFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count < 2)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: two arguments required.");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        double baseVal = 0, subtractVal = 0;
        if (!TryAccumulateArg(Arguments[0], ref baseVal, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        if (!TryAccumulateArg(Arguments[1], ref subtractVal, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        return FunctionResult<TNode>.Successful(CreateNumericResult(baseVal - subtractVal, context.NodeAdapter));
    }
}
