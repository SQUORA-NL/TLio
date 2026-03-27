using TLio.Core.Models;

namespace TLio.Extensions.Math;

/// <summary>=max(arg1, arg2, ...) — largest numeric value across all arguments.</summary>
public class Max<TNode> : MathFunctionBase<TNode>
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
        {
            context.LogError(FunctionName, $"{FunctionName}: no numeric values found.");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        return FunctionResult<TNode>.Successful(CreateNumericResult(values.Max(), context.NodeAdapter));
    }
}
