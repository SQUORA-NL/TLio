using TLio.Core.Models;

namespace TLio.Extensions.Math;

/// <summary>=floor(value) — largest integer less than or equal to value.</summary>
public class Floor<TNode> : MathFunctionBase<TNode>
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
        return FunctionResult<TNode>.Successful(CreateNumericResult(System.Math.Floor(value), context.NodeAdapter));
    }
}
