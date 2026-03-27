using TLio.Core.Models;

namespace TLio.Extensions.Math;

/// <summary>=round(value) or =round(value, decimals) — rounds to the specified decimal places (default 0, max 15). Uses AwayFromZero mid-point rounding.</summary>
public class Round<TNode> : MathFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count == 0)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: at least one argument required.");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        if (!TryGetSingleArg(Arguments[0], out double value, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);

        int decimals = 0;
        if (Arguments.Count >= 2)
        {
            if (!TryGetSingleArg(Arguments[1], out double dec, currentNode, dataContext, context, FunctionName))
                return FunctionResult<TNode>.Failed(currentNode);
            decimals = System.Math.Max(0, System.Math.Min(15, (int)dec));
        }

        var rounded = System.Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        return FunctionResult<TNode>.Successful(CreateNumericResult(rounded, context.NodeAdapter));
    }
}
