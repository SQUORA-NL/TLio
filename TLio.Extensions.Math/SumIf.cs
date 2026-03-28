using TLio.Core.Models;

namespace TLio.Extensions.Math;

/// <summary>
/// =sumif(range, criteria) or =sumif(range, criteria, sum_range)
///
/// For each element in range that satisfies criteria, adds the corresponding
/// element from sum_range (or range itself when sum_range is omitted).
/// Non-numeric sum-range items are silently skipped.
/// </summary>
public class SumIf<TNode> : MathFunctionBase<TNode>
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

        var criteriaArg = ResolveArg(Arguments[1], currentNode, dataContext, context);
        if (!criteriaArg.Success || criteriaArg.Data.Count == 0)
        {
            context.LogError(FunctionName, $"{FunctionName}: criteria argument not found.");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        var criteria = ExtractCriteria(criteriaArg.Data.First!, context.NodeAdapter);

        var sumRange = Arguments.Count >= 3
            ? ResolveList(Arguments[2], currentNode, dataContext, context)
            : range;
        if (sumRange == null)
        {
            context.LogError(FunctionName, $"{FunctionName}: sum_range not found.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        double result = 0;
        for (int i = 0; i < System.Math.Min(range.Count, sumRange.Count); i++)
        {
            var testValue = ExtractPrimitiveValue(range[i], context.NodeAdapter);
            if (ConditionEvaluator.EvaluateCondition(testValue, criteria))
            {
                var num = context.NodeAdapter.TryGetDouble(sumRange[i]);
                if (num.HasValue) result += num.Value;
            }
        }
        return FunctionResult<TNode>.Successful(CreateNumericResult(result, context.NodeAdapter));
    }
}
