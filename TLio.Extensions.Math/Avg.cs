using TLio.Core.Models;

namespace TLio.Extensions.Math;

/// <summary>=avg(arg1, arg2, ...) — arithmetic mean. Found-null counts toward denominator as 0.</summary>
public class Avg<TNode> : MathFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count == 0)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: at least one argument required.");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        var values = new List<double>();
        foreach (var arg in Arguments)
            if (!TryCollectArg(arg, values, currentNode, dataContext, context, FunctionName))
                return FunctionResult<TNode>.Failed(currentNode);
        if (values.Count == 0)
            return FunctionResult<TNode>.Successful(CreateNumericResult(0, context.NodeAdapter));
        return FunctionResult<TNode>.Successful(CreateNumericResult(values.Average(), context.NodeAdapter));
    }
}
