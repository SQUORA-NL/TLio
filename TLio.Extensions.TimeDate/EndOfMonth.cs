namespace TLio.Extensions.TimeDate;

/// <summary>
/// =endofmonth(date) — the last day of that date's month, as a date-only ISO string.
/// The day count comes from DateTime.DaysInMonth, so February is leap-year correct:
/// 2024-02-10 gives 2024-02-29 and 2023-02-10 gives 2023-02-28.
/// </summary>
public class EndOfMonth<TNode> : TimeDateFunctionBase<TNode>
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
        var lastDay = DateTime.DaysInMonth(utc.Year, utc.Month);
        var last = new DateTimeOffset(utc.Year, utc.Month, lastDay, 0, 0, 0, TimeSpan.Zero);
        return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateString(FormatDate(last)));
    }
}
