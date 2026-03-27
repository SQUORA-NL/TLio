using TLio.Core.Models;

namespace TLio.Extensions.Math;

/// <summary>=sum(arg1, arg2, ...) — sums all numeric arguments. Arrays are flattened.</summary>
public class Sum<TNode> : MathFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count == 0)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: at least one argument required.");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        double total = 0;
        foreach (var arg in Arguments)
            if (!TryAccumulateArg(arg, ref total, currentNode, dataContext, context, FunctionName))
                return FunctionResult<TNode>.Failed(currentNode);
        return FunctionResult<TNode>.Successful(CreateNumericResult(total, context.NodeAdapter));
    }
}
