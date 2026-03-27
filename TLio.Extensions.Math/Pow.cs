using TLio.Core.Models;

namespace TLio.Extensions.Math;

/// <summary>=pow(base, exponent) — raises base to the power of exponent. Null-found-but-null maps to 0.</summary>
public class Pow<TNode> : MathFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count < 2)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: two arguments required (base, exponent).");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        if (!TryGetSingleArg(Arguments[0], out double @base, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        if (!TryGetSingleArg(Arguments[1], out double exponent, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        var result = System.Math.Pow(@base, exponent);
        if (double.IsNaN(result) || double.IsInfinity(result))
        {
            context.LogError(FunctionName, $"{FunctionName}: result is not a finite number.");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        return FunctionResult<TNode>.Successful(CreateNumericResult(result, context.NodeAdapter));
    }
}
