using TLio.Core.Models;

namespace TLio.Extensions.Math;

/// <summary>=sqrt(value) — square root. Fails if result is NaN or Infinity.</summary>
public class Sqrt<TNode> : MathFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count == 0)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: one argument required.");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        if (!TryGetSingleArg(Arguments[0], out double value, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        var result = System.Math.Sqrt(value);
        if (double.IsNaN(result) || double.IsInfinity(result))
        {
            context.LogError(FunctionName, $"{FunctionName}: result is not a finite number (input={value}).");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        return FunctionResult<TNode>.Successful(CreateNumericResult(result, context.NodeAdapter));
    }
}
