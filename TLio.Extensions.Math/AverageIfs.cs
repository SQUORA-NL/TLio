using TLio.Core.Models;

namespace TLio.Extensions.Math;

/// <summary>
/// =averageifs(average_range, criteria_range1, criteria1 [, criteria_range2, criteria2, ...])
///
/// Averages values from average_range where ALL criteria pairs match.
/// Returns 0 (not failure) when nothing matches.
/// Requires an odd number of arguments >= 3.
/// </summary>
public class AverageIfs<TNode> : MathFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count < 3 || Arguments.Count % 2 == 0)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: requires average_range + pairs of (criteria_range, criteria).");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        var avgRange = ResolveList(Arguments[0], currentNode, dataContext, context);
        if (avgRange == null)
        {
            context.LogError(FunctionName, $"{FunctionName}: average_range not found.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        var pairs = new List<(List<TNode> Range, string Criteria)>();
        for (int i = 1; i < Arguments.Count - 1; i += 2)
        {
            var range = ResolveList(Arguments[i], currentNode, dataContext, context);
            if (range == null)
            {
                context.LogError(FunctionName, $"{FunctionName}: criteria_range {(i - 1) / 2 + 1} not found.");
                return FunctionResult<TNode>.Failed(currentNode);
            }
            var criteriaArg = ResolveArg(Arguments[i + 1], currentNode, dataContext, context);
            if (!criteriaArg.Success || criteriaArg.Data.Count == 0)
            {
                context.LogError(FunctionName, $"{FunctionName}: criteria {(i - 1) / 2 + 1} not found.");
                return FunctionResult<TNode>.Failed(currentNode);
            }
            pairs.Add((range, ExtractCriteria(criteriaArg.Data.First!, context.NodeAdapter)));
        }

        double total = 0;
        long count = 0;
        for (int i = 0; i < avgRange.Count; i++)
        {
            bool allMatch = pairs.All(p =>
            {
                if (i >= p.Range.Count) return false;
                return ConditionEvaluator.EvaluateCondition(ExtractPrimitiveValue(p.Range[i], context.NodeAdapter), p.Criteria);
            });
            if (allMatch)
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
