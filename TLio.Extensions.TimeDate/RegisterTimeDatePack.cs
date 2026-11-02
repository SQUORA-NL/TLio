namespace TLio.Extensions.TimeDate;

/// <summary>
/// Extension method to register all TLio.Extensions.TimeDate functions into any
/// <see cref="IFunctionsProviderRegistrar{TNode}"/>.
///
/// Usage (with ParseOptions):
/// <code>
///   ParseOptions&lt;JToken&gt;.CreateDefault().FunctionsProvider.RegisterTimeDate&lt;JToken&gt;();
/// </code>
///
/// Registers: datecompare, isdatebetween, mindate, maxdate, avgdate, datediff, dateadd,
/// datepart, formatdate, parsedate, startofmonth, endofmonth, daycountfraction.
///
/// Ported from JLio.Extensions.TimeDate.RegisterTimeDatePack.RegisterTimeDate().
/// </summary>
public static class RegisterTimeDatePack
{
    /// <summary>
    /// Registers every TimeDate function on <paramref name="target"/> and returns it, so calls
    /// chain. Delegates to <see cref="RegisterTimeDate{TNode}"/>, which holds the list.
    /// </summary>
    public static TSelf WithTimeDate<TNode, TSelf>(this IRegistrationTarget<TNode, TSelf> target)
        where TSelf : IRegistrationTarget<TNode, TSelf>
    {
        ArgumentNullException.ThrowIfNull(target);
        target.FunctionsRegistrar.RegisterTimeDate<TNode>();
        return target.Self;
    }

    public static IFunctionsProviderRegistrar<TNode> RegisterTimeDate<TNode>(
        this IFunctionsProviderRegistrar<TNode> registrar)
    {
        registrar.Register("datecompare",   () => new DateCompare<TNode>());
        registrar.Register("isdatebetween", () => new IsDateBetween<TNode>());
        registrar.Register("mindate",       () => new MinDate<TNode>());
        registrar.Register("maxdate",       () => new MaxDate<TNode>());
        registrar.Register("avgdate",       () => new AvgDate<TNode>());
        registrar.Register("datediff",      () => new DateDiff<TNode>());
        registrar.Register("dateadd",       () => new DateAdd<TNode>());
        registrar.Register("datepart",      () => new DatePart<TNode>());
        registrar.Register("formatdate",    () => new FormatDateFunction<TNode>());
        registrar.Register("parsedate",     () => new ParseDateFunction<TNode>());
        registrar.Register("startofmonth",  () => new StartOfMonth<TNode>());
        registrar.Register("endofmonth",    () => new EndOfMonth<TNode>());
        registrar.Register("daycountfraction", () => new DayCountFraction<TNode>());
        return registrar;
    }
}
