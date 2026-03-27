namespace TLio.Extensions.TimeDate;

/// <summary>=mindate(arg1, arg2, ...) — returns the earliest date from all arguments (supports arrays).</summary>
public class MinDate<TNode> : TimeDateFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count == 0)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: at least one argument required.");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        var dates = new List<DateTimeOffset>();
        foreach (var arg in Arguments)
            if (!TryCollectDates(arg, dates, currentNode, dataContext, context, FunctionName))
                return FunctionResult<TNode>.Failed(currentNode);
        if (dates.Count == 0)
        {
            context.LogError(FunctionName, $"{FunctionName}: no dates found.");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateString(FormatDate(dates.Min())));
    }
}
