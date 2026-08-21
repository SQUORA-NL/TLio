namespace TLio.Extensions.TimeDate;

/// <summary>
/// =datediff(from, to, unit?) — whole units elapsed from <c>from</c> to <c>to</c>, truncated
/// toward zero, negative when <c>to</c> precedes <c>from</c>. <c>unit</c> defaults to days.
///
/// <c>years</c> and <c>months</c> are calendar-aware rather than <c>days/365.25</c>: "years"
/// means birthdays passed, which is what every age-based rule in a rating script means.
/// <c>days</c> and below come from the elapsed interval; <c>weeks</c> is that day count
/// divided by 7.
/// </summary>
public class DateDiff<TNode> : TimeDateFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count is < 2 or > 3)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: two or three arguments required (from, to, unit?).");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        if (!TryGetDateArg(Arguments[0], out var from, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        if (!TryGetDateArg(Arguments[1], out var to, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);

        var unit = DateUnit.Days;
        if (Arguments.Count == 3)
        {
            if (!TryGetStringArg(Arguments[2], out var unitText, currentNode, dataContext, context, FunctionName))
                return FunctionResult<TNode>.Failed(currentNode);
            if (!DateUnits.TryParse(unitText, out unit))
            {
                context.LogError(FunctionName,
                    $"{FunctionName}: unknown unit '{unitText}'. Accepted units: {DateUnits.Accepted}.");
                return FunctionResult<TNode>.Failed(currentNode);
            }
        }

        var elapsed = to - from;
        var value = unit switch
        {
            DateUnit.Years   => WholeMonths(from, to) / 12,
            DateUnit.Months  => WholeMonths(from, to),
            DateUnit.Weeks   => (long)elapsed.TotalDays / 7,
            DateUnit.Days    => (long)elapsed.TotalDays,
            DateUnit.Hours   => (long)elapsed.TotalHours,
            DateUnit.Minutes => (long)elapsed.TotalMinutes,
            _                => (long)elapsed.TotalSeconds,
        };

        return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateValue(value));
    }

    /// <summary>
    /// Full calendar months between the two instants, signed. The raw year/month difference is
    /// one too high whenever the day of the month has not come round yet — 1985-03-12 to
    /// 2026-03-11 is 491 months, not 492 — so it is stepped back by one. AddMonths supplies the
    /// month-end clamping, which is what keeps this answer agreeing with =dateadd(...).
    ///
    /// Whole years divide out of this: integer division truncates toward zero in both
    /// directions, so 491 months is 40 years and -491 months is -40.
    /// </summary>
    private static long WholeMonths(DateTimeOffset from, DateTimeOffset to)
    {
        var earlier = from.UtcDateTime;
        var later = to.UtcDateTime;
        var sign = 1L;
        if (later < earlier)
        {
            (earlier, later) = (later, earlier);
            sign = -1L;
        }

        var months = ((long)later.Year - earlier.Year) * 12 + (later.Month - earlier.Month);
        if (months > 0 && earlier.AddMonths((int)months) > later) months--;
        return months * sign;
    }
}
