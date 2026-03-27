namespace TLio.Extensions.TimeDate;

/// <summary>
/// =isdatebetween(date, startDate, endDate) — returns true if date is between startDate and endDate (inclusive).
/// </summary>
public class IsDateBetween<TNode> : TimeDateFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count < 3)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: three arguments required (date, startDate, endDate).");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        if (!TryGetDateArg(Arguments[0], out var date, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        if (!TryGetDateArg(Arguments[1], out var start, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        if (!TryGetDateArg(Arguments[2], out var end, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);

        bool between = date >= start && date <= end;
        return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateBoolean(between));
    }
}
