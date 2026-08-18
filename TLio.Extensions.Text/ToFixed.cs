using System.Globalization;
using TLio.Core.Models;

namespace TLio.Extensions.Text;

/// <summary>
/// =toFixed(value, decimals[, decimalSeparator]) — formats a number as text with exactly
/// <c>decimals</c> digits after the separator, rounding half away from zero.
///
/// The result is a STRING, because trailing zeros cannot survive in a numeric node:
/// <c>=toFixed(14.5, 2)</c> → <c>"14.50"</c>. Use =round(...) from the Math pack when you
/// want a rounded number instead.
///
/// The separator defaults to <c>.</c> (invariant), independent of the machine's locale.
/// Pass a third argument for a locale-style separator: <c>=toFixed($.price, 2, ',')</c> → <c>"14,50"</c>.
/// </summary>
public class ToFixed<TNode> : TextFunctionBase<TNode>
{
    public override string FunctionName => "toFixed";

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count < 2)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: two arguments required (value, decimals).");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        var valueResult = ResolveArg(Arguments[0], currentNode, dataContext, context);
        if (!valueResult.Success || valueResult.Data.Count == 0)
        {
            context.LogError(FunctionName, $"{FunctionName}: argument path not found.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        var number = context.NodeAdapter.TryGetDouble(valueResult.Data.First!);
        if (number == null)
        {
            context.LogError(FunctionName, $"{FunctionName}: value is not numeric.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        if (!TryGetIntArg(Arguments[1], out var decimals, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);

        if (decimals < 0 || decimals > 15)
        {
            context.LogError(FunctionName, $"{FunctionName}: decimals must be between 0 and 15 (was {decimals}).");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        // ToString("F") rounds half away from zero, which is what invoice-style rounding expects.
        var text = NodeComparison.FormatNumber(number.Value, decimals);

        if (Arguments.Count >= 3)
        {
            if (!TryGetStringArg(Arguments[2], out var separator, currentNode, dataContext, context, FunctionName))
                return FunctionResult<TNode>.Failed(currentNode);
            text = text.Replace(NumberFormatInfo.InvariantInfo.NumberDecimalSeparator, separator);
        }

        return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateString(text));
    }
}
