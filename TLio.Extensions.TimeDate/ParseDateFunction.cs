using System.Globalization;

namespace TLio.Extensions.TimeDate;

/// <summary>
/// =parsedate(text, format?) — reads a date written in some other notation and returns it in
/// TLio's canonical ISO form, so a Dutch "31-12-2026" becomes "2026-12-31".
///
/// With a <c>format</c> the text must match it exactly (InvariantCulture; a text carrying no
/// offset is read as UTC); without one the base class's format list is used. Text that does not
/// parse fails the function — an unreadable date has no answer.
///
/// The class is named ParseDateFunction for symmetry with FormatDateFunction, so
/// <see cref="FunctionName"/> is stated rather than derived from the class name.
/// </summary>
public class ParseDateFunction<TNode> : TimeDateFunctionBase<TNode>
{
    public override string FunctionName => "parsedate";

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count is < 1 or > 2)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: one or two arguments required (text, format?).");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        if (!TryGetStringArg(Arguments[0], out var text, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);

        DateTimeOffset parsed;
        if (Arguments.Count == 2)
        {
            if (!TryGetStringArg(Arguments[1], out var format, currentNode, dataContext, context, FunctionName))
                return FunctionResult<TNode>.Failed(currentNode);
            if (!DateTimeOffset.TryParseExact(text, format, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out parsed))
            {
                context.LogError(FunctionName, $"{FunctionName}: '{text}' does not match format '{format}'.");
                return FunctionResult<TNode>.Failed(currentNode);
            }
        }
        else if (!TryParseDate(text, out parsed, FunctionName, context))
        {
            return FunctionResult<TNode>.Failed(currentNode);
        }

        return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateString(FormatDate(parsed)));
    }
}
