using TLio.Core.Models;

namespace TLio.Extensions.Math;

/// <summary>=countif(range, criteria) — counts elements in range matching criteria.</summary>
public class CountIf<TNode> : MathFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count < 2)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: two arguments required (range, criteria).");
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
            context.LogError(FunctionName, $"{FunctionName}: criteria not found.");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        var criteria = ExtractCriteria(criteriaArg.Data.First!, context.NodeAdapter);

        long count = range.Count(item =>
            ConditionEvaluator.EvaluateCondition(ExtractPrimitiveValue(item, context.NodeAdapter), criteria));

        return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateValue(count));
    }
}
