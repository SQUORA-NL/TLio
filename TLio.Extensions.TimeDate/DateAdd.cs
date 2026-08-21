namespace TLio.Extensions.TimeDate;

/// <summary>
/// =dateadd(date, amount, unit?) — shifts a date by a whole number of units. <c>unit</c>
/// defaults to days and <c>amount</c> may be negative, so this subtracts as well as adds.
///
/// Month-end clamps the way .NET's AddMonths/AddYears do: 2024-01-31 + 1 month is 2024-02-29,
/// not 2024-03-02. The result goes through the shared date formatter, so a date-only input
/// yields a date-only result.
/// </summary>
public class DateAdd<TNode> : TimeDateFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count is < 2 or > 3)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: two or three arguments required (date, amount, unit?).");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        if (!TryGetDateArg(Arguments[0], out var date, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        if (!TryGetLongArg(Arguments[1], out var amount, currentNode, dataContext, context, FunctionName))
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

        DateTimeOffset shifted;
        try
        {
            shifted = unit switch
            {
                DateUnit.Years   => date.AddYears(checked((int)amount)),
                DateUnit.Months  => date.AddMonths(checked((int)amount)),
                DateUnit.Weeks   => date.AddDays(amount * 7d),
                DateUnit.Days    => date.AddDays(amount),
                DateUnit.Hours   => date.AddHours(amount),
                DateUnit.Minutes => date.AddMinutes(amount),
                _                => date.AddSeconds(amount),
            };
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or OverflowException)
        {
            context.LogError(FunctionName,
                $"{FunctionName}: adding {amount} {unit.ToString().ToLowerInvariant()} moves the date out of range.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateString(FormatDate(shifted)));
    }
}
