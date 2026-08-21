namespace TLio.Extensions.TimeDate;

/// <summary>
/// =startofmonth(date) — the first day of that date's month, as a date-only ISO string.
/// Any time component is dropped: the answer is a day, not an instant.
/// </summary>
public class StartOfMonth<TNode> : TimeDateFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count != 1)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: one argument required (date).");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        if (!TryGetDateArg(Arguments[0], out var date, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);

        var utc = date.UtcDateTime;
        var first = new DateTimeOffset(utc.Year, utc.Month, 1, 0, 0, 0, TimeSpan.Zero);
        return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateString(FormatDate(first)));
    }
}
