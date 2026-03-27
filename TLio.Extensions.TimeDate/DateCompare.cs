namespace TLio.Extensions.TimeDate;

/// <summary>
/// =datecompare(date1, date2) — compares two date values.
/// Returns -1 if date1 &lt; date2, 0 if equal, 1 if date1 &gt; date2.
/// </summary>
public class DateCompare<TNode> : TimeDateFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count < 2)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: two arguments required (date1, date2).");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        if (!TryGetDateArg(Arguments[0], out var d1, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        if (!TryGetDateArg(Arguments[1], out var d2, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);

        long cmp = d1 < d2 ? -1L : d1 > d2 ? 1L : 0L;
        return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateValue(cmp));
    }
}
