using TLio.Core.Models;

namespace TLio.Extensions.Math;

/// <summary>
/// =sumifs(sum_range, criteria_range1, criteria1 [, criteria_range2, criteria2, ...])
///
/// Sums values from sum_range where ALL criteria pairs match.
/// Requires an odd number of arguments >= 3.
/// </summary>
public class SumIfs<TNode> : MathFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count < 3 || Arguments.Count % 2 == 0)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: requires sum_range + pairs of (criteria_range, criteria).");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        var sumRange = ResolveList(Arguments[0], currentNode, dataContext, context);
        if (sumRange == null)
        {
            context.LogError(FunctionName, $"{FunctionName}: sum_range not found.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        // Build criteria pairs: [(range, criteria), ...]
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

        double result = 0;
        for (int i = 0; i < sumRange.Count; i++)
        {
            bool allMatch = pairs.All(p =>
            {
                if (i >= p.Range.Count) return false;
                return ConditionEvaluator.EvaluateCondition(ExtractPrimitiveValue(p.Range[i], context.NodeAdapter), p.Criteria);
            });

            if (allMatch)
            {
                var num = context.NodeAdapter.TryGetDouble(sumRange[i]);
                if (num.HasValue) result += num.Value;
            }
        }
        return FunctionResult<TNode>.Successful(CreateNumericResult(result, context.NodeAdapter));
    }
}
