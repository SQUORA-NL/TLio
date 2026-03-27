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
/// Registers: datecompare, isdatebetween, mindate, maxdate, avgdate.
///
/// Ported from JLio.Extensions.TimeDate.RegisterTimeDatePack.RegisterTimeDate().
/// </summary>
public static class RegisterTimeDatePack
{
    public static IFunctionsProviderRegistrar<TNode> RegisterTimeDate<TNode>(
        this IFunctionsProviderRegistrar<TNode> registrar)
    {
        registrar.Register("datecompare",   () => new DateCompare<TNode>());
        registrar.Register("isdatebetween", () => new IsDateBetween<TNode>());
        registrar.Register("mindate",       () => new MinDate<TNode>());
        registrar.Register("maxdate",       () => new MaxDate<TNode>());
        registrar.Register("avgdate",       () => new AvgDate<TNode>());
        return registrar;
    }
}
