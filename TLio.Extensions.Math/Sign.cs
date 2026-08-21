using TLio.Core.Models;

namespace TLio.Extensions.Math;

/// <summary>=sign(value) — -1, 0 or 1 as a whole number, according to the sign of value.</summary>
public class Sign<TNode> : MathFunctionBase<TNode>
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
        return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateValue((long)System.Math.Sign(value)));
    }
}
