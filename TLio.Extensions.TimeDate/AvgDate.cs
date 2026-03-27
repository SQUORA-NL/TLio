namespace TLio.Extensions.TimeDate;

/// <summary>
/// =avgdate(arg1, arg2, ...) — returns the average (midpoint) date from all arguments (supports arrays).
/// Average is computed as the mean of UTC ticks, then converted back to a date string.
/// </summary>
public class AvgDate<TNode> : TimeDateFunctionBase<TNode>
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
        var avgTicks = (long)(dates.Average(d => (double)d.UtcTicks));
        var avgDate = new DateTimeOffset(avgTicks, TimeSpan.Zero);
        return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateString(FormatDate(avgDate)));
    }
}
