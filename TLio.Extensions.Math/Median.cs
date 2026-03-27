using TLio.Core.Models;

namespace TLio.Extensions.Math;

/// <summary>=median(arg1, arg2, ...) — middle value; averages two middles for even count.</summary>
public class Median<TNode> : MathFunctionBase<TNode>
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
        values.Sort();
        int n = values.Count;
        double median = n % 2 == 0
            ? (values[n / 2 - 1] + values[n / 2]) / 2.0
            : values[n / 2];
        return FunctionResult<TNode>.Successful(CreateNumericResult(median, context.NodeAdapter));
    }
}
