using System.Globalization;

namespace TLio.Extensions.TimeDate;

/// <summary>
/// =datepart(date, part) — extracts one numbered component of a date as a long.
///
/// Two parts deliberately do not follow their .NET namesakes: <c>dayofweek</c> is ISO
/// (1 = Monday … 7 = Sunday) rather than .NET's 0 = Sunday, and <c>weekofyear</c> is the ISO
/// 8601 week number, so the first days of January can belong to week 52 or 53 of the previous
/// year. Both are the answers a business rule means.
/// </summary>
public class DatePart<TNode> : TimeDateFunctionBase<TNode>
{
    private const string AcceptedParts =
        "year, month, day, hour, minute, second, quarter, dayofweek, dayofyear, weekofyear, daysinmonth";

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count != 2)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: two arguments required (date, part).");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        if (!TryGetDateArg(Arguments[0], out var date, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        if (!TryGetStringArg(Arguments[1], out var partText, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);

        var utc = date.UtcDateTime;
        long value;
        switch (partText.Trim().ToLowerInvariant())
        {
            case "year":        value = utc.Year; break;
            case "month":       value = utc.Month; break;
            case "day":         value = utc.Day; break;
            case "hour":        value = utc.Hour; break;
            case "minute":      value = utc.Minute; break;
            case "second":      value = utc.Second; break;
            case "quarter":     value = (utc.Month - 1) / 3 + 1; break;
            case "dayofweek":   value = ((int)utc.DayOfWeek + 6) % 7 + 1; break;
            case "dayofyear":   value = utc.DayOfYear; break;
            case "weekofyear":  value = ISOWeek.GetWeekOfYear(utc); break;
            case "daysinmonth": value = DateTime.DaysInMonth(utc.Year, utc.Month); break;
            default:
                context.LogError(FunctionName,
                    $"{FunctionName}: unknown part '{partText}'. Accepted parts: {AcceptedParts}.");
                return FunctionResult<TNode>.Failed(currentNode);
        }

        return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateValue(value));
    }
}
