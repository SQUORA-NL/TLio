namespace TLio.Extensions.TimeDate;

/// <summary>
/// =daycountfraction(from, to, convention) — the fraction of a year between two dates under a
/// named day-count convention. Generic date-market math (bonds, loans, swaps generally), not
/// specific to any one contract standard, which is why it lives here alongside
/// <see cref="DateDiff{TNode}"/> rather than in a dedicated package.
///
/// v1 covers the three conventions common to a fixed-rate loan or bond:
///   "A360"   — actual days elapsed / 360 (money-market convention)
///   "A365"   — actual days elapsed / 365
///   "30E360" — 30/360 European: each month treated as 30 days, capping any day-of-month at 30
///
/// More exist (ISDA actual/actual, plain 30/360, business-day/EOM-adjusted variants) and are
/// deliberately out of scope for now — this list grows the same incremental way the rest of the
/// pack has (008 → 023).
/// </summary>
public class DayCountFraction<TNode> : TimeDateFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count != 3)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: three arguments required (from, to, convention).");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        if (!TryGetDateArg(Arguments[0], out var from, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        if (!TryGetDateArg(Arguments[1], out var to, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        if (!TryGetStringArg(Arguments[2], out var conventionText, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);

        double fraction;
        switch (conventionText.Trim().ToUpperInvariant())
        {
            case "A360":
                fraction = (to - from).TotalDays / 360d;
                break;
            case "A365":
                fraction = (to - from).TotalDays / 365d;
                break;
            case "30E360":
                fraction = Thirty360European(from, to) / 360d;
                break;
            default:
                context.LogError(FunctionName,
                    $"{FunctionName}: unknown convention '{conventionText}'. Accepted conventions: A360, A365, 30E360.");
                return FunctionResult<TNode>.Failed(currentNode);
        }

        return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateNumber(fraction));
    }

    /// <summary>
    /// 30E/360: day-of-month capped at 30 on both ends, then
    /// 360*(Y2-Y1) + 30*(M2-M1) + (D2-D1) days.
    /// </summary>
    private static double Thirty360European(DateTimeOffset from, DateTimeOffset to)
    {
        var d1 = Math.Min(from.Day, 30);
        var d2 = Math.Min(to.Day, 30);
        return 360d * (to.Year - from.Year) + 30d * (to.Month - from.Month) + (d2 - d1);
    }
}
