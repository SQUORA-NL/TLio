using System.Globalization;

namespace TLio.Extensions.TimeDate;

/// <summary>
/// =formatdate(date, format) — renders a date the document already holds with a .NET format
/// string, InvariantCulture, against the UTC value. Where =datetime(format) can only ever
/// format <em>now</em>, this formats a stored date.
///
/// The class is named FormatDateFunction because a plain FormatDate would shadow the inherited
/// helper of that name, so <see cref="FunctionName"/> is stated rather than derived from it.
/// </summary>
public class FormatDateFunction<TNode> : TimeDateFunctionBase<TNode>
{
    public override string FunctionName => "formatdate";

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count != 2)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: two arguments required (date, format).");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        if (!TryGetDateArg(Arguments[0], out var date, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        if (!TryGetStringArg(Arguments[1], out var format, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);

        if (format.Length == 0)
        {
            context.LogError(FunctionName, $"{FunctionName}: format string is empty.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        string formatted;
        try
        {
            formatted = date.UtcDateTime.ToString(format, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            context.LogError(FunctionName, $"{FunctionName}: '{format}' is not a valid date format string.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateString(formatted));
    }
}
