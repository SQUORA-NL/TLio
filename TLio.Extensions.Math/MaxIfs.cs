using TLio.Core.Models;

namespace TLio.Extensions.Math;

/// <summary>
/// =maxifs(max_range, criteria_range1, criteria1 [, criteria_range2, criteria2, ...])
///
/// Maximum value from max_range where ALL criteria match.
/// Returns FAILURE (not 0) when nothing matches — differs from AverageIfs/SumIfs.
/// Requires an odd number of arguments >= 3.
/// </summary>
public class MaxIfs<TNode> : MathFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count < 3 || Arguments.Count % 2 == 0)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: requires max_range + pairs of (criteria_range, criteria).");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        var maxRange = ResolveList(Arguments[0], currentNode, dataContext, context);
        if (maxRange == null)
        {
            context.LogError(FunctionName, $"{FunctionName}: max_range not found.");
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
            var criteriaArg = Arguments[i + 1].GetValue(currentNode, dataContext, context);
            if (!criteriaArg.Success || criteriaArg.Data.Count == 0)
            {
                context.LogError(FunctionName, $"{FunctionName}: criteria {(i - 1) / 2 + 1} not found.");
                return FunctionResult<TNode>.Failed(currentNode);
            }
            pairs.Add((range, ExtractCriteria(criteriaArg.Data.First!, context.NodeAdapter)));
        }

        var matchedValues = new List<double>();
        for (int i = 0; i < maxRange.Count; i++)
        {
            bool allMatch = pairs.All(p =>
            {
                if (i >= p.Range.Count) return false;
                return ConditionEvaluator.EvaluateCondition(ExtractPrimitiveValue(p.Range[i], context.NodeAdapter), p.Criteria);
            });
            if (allMatch)
            {
                var num = context.NodeAdapter.TryGetDouble(maxRange[i]);
                if (num.HasValue) matchedValues.Add(num.Value);
            }
        }

        if (matchedValues.Count == 0)
        {
            context.LogError(FunctionName, $"{FunctionName}: no values matched criteria.");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        return FunctionResult<TNode>.Successful(CreateNumericResult(matchedValues.Max(), context.NodeAdapter));
    }
}
