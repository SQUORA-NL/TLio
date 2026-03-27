using TLio.Core.Models;

namespace TLio.Extensions.Math;

/// <summary>
/// =countifs(criteria_range1, criteria1 [, criteria_range2, criteria2, ...])
///
/// Counts rows where ALL criteria pairs match.
/// Requires an even number of arguments >= 2.
/// </summary>
public class CountIfs<TNode> : MathFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count < 2 || Arguments.Count % 2 != 0)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: requires pairs of (criteria_range, criteria).");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        var pairs = new List<(List<TNode> Range, string Criteria)>();
        for (int i = 0; i < Arguments.Count - 1; i += 2)
        {
            var range = ResolveList(Arguments[i], currentNode, dataContext, context);
            if (range == null)
            {
                context.LogError(FunctionName, $"{FunctionName}: range {i / 2 + 1} not found.");
                return FunctionResult<TNode>.Failed(currentNode);
            }
            var criteriaArg = Arguments[i + 1].GetValue(currentNode, dataContext, context);
            if (!criteriaArg.Success || criteriaArg.Data.Count == 0)
            {
                context.LogError(FunctionName, $"{FunctionName}: criteria {i / 2 + 1} not found.");
                return FunctionResult<TNode>.Failed(currentNode);
            }
            pairs.Add((range, ExtractCriteria(criteriaArg.Data.First!, context.NodeAdapter)));
        }

        int rowCount = pairs.Max(p => p.Range.Count);
        long count = 0;
        for (int i = 0; i < rowCount; i++)
        {
            bool allMatch = pairs.All(p =>
            {
                if (i >= p.Range.Count) return false;
                return ConditionEvaluator.EvaluateCondition(ExtractPrimitiveValue(p.Range[i], context.NodeAdapter), p.Criteria);
            });
            if (allMatch) count++;
        }
        return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateValue(count));
    }
}
