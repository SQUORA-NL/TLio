using TLio.Core.Models;

namespace TLio.Extensions.Math;

/// <summary>
/// =averageif(range, criteria) or =averageif(range, criteria, average_range)
///
/// Averages elements matching criteria. Returns 0 (not failure) when nothing matches.
/// </summary>
public class AverageIf<TNode> : MathFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count < 2)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: at least two arguments required (range, criteria).");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        var range = ResolveList(Arguments[0], currentNode, dataContext, context);
        if (range == null)
        {
            context.LogError(FunctionName, $"{FunctionName}: range not found.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        var criteriaArg = Arguments[1].GetValue(currentNode, dataContext, context);
        if (!criteriaArg.Success || criteriaArg.Data.Count == 0)
        {
            context.LogError(FunctionName, $"{FunctionName}: criteria not found.");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        var criteria = ExtractCriteria(criteriaArg.Data.First!, context.NodeAdapter);

        var avgRange = Arguments.Count >= 3
            ? ResolveList(Arguments[2], currentNode, dataContext, context)
            : range;
        if (avgRange == null)
        {
            context.LogError(FunctionName, $"{FunctionName}: average_range not found.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        double total = 0;
        long count = 0;
        for (int i = 0; i < System.Math.Min(range.Count, avgRange.Count); i++)
        {
            if (ConditionEvaluator.EvaluateCondition(ExtractPrimitiveValue(range[i], context.NodeAdapter), criteria))
            {
                var num = context.NodeAdapter.TryGetDouble(avgRange[i]);
                if (num.HasValue) { total += num.Value; count++; }
            }
        }

        if (count == 0)
            return FunctionResult<TNode>.Successful(CreateNumericResult(0, context.NodeAdapter));
        return FunctionResult<TNode>.Successful(CreateNumericResult(total / count, context.NodeAdapter));
    }
}
