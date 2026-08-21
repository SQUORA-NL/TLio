namespace TLio.Extensions.Math;

/// <summary>
/// Extension method to register all TLio.Extensions.Math functions into any
/// <see cref="IFunctionsProviderRegistrar{TNode}"/>.
///
/// Usage (with ParseOptions):
/// <code>
///   ParseOptions&lt;JToken&gt;.CreateDefault().FunctionsProvider.RegisterMath&lt;JToken&gt;();
/// </code>
///
/// Registers: sum, avg, count, min, max, median, abs, ceiling, floor, round, sqrt, pow,
///            subtract, multiply, divide, modulo, clamp, sign, calculate, sumif, sumifs,
///            countif, countifs, averageif, averageifs, minifs, maxifs.
///
/// Ported from JLio.Extensions.Math.RegisterMathPack.RegisterMath().
/// </summary>
public static class RegisterMathPack
{
    public static IFunctionsProviderRegistrar<TNode> RegisterMath<TNode>(
        this IFunctionsProviderRegistrar<TNode> registrar)
    {
        registrar.Register("sum",        () => new Sum<TNode>());
        registrar.Register("avg",        () => new Avg<TNode>());
        registrar.Register("count",      () => new Count<TNode>());
        registrar.Register("min",        () => new Min<TNode>());
        registrar.Register("max",        () => new Max<TNode>());
        registrar.Register("median",     () => new Median<TNode>());
        registrar.Register("abs",        () => new Abs<TNode>());
        registrar.Register("ceiling",    () => new Ceiling<TNode>());
        registrar.Register("floor",      () => new Floor<TNode>());
        registrar.Register("round",      () => new Round<TNode>());
        registrar.Register("sqrt",       () => new Sqrt<TNode>());
        registrar.Register("pow",        () => new Pow<TNode>());
        registrar.Register("subtract",   () => new Subtract<TNode>());
        registrar.Register("multiply",   () => new Multiply<TNode>());
        registrar.Register("divide",     () => new Divide<TNode>());
        registrar.Register("modulo",     () => new Modulo<TNode>());
        registrar.Register("clamp",      () => new Clamp<TNode>());
        registrar.Register("sign",       () => new Sign<TNode>());
        registrar.Register("calculate",  () => new Calculate<TNode>());
        registrar.Register("sumif",      () => new SumIf<TNode>());
        registrar.Register("sumifs",     () => new SumIfs<TNode>());
        registrar.Register("countif",    () => new CountIf<TNode>());
        registrar.Register("countifs",   () => new CountIfs<TNode>());
        registrar.Register("averageif",  () => new AverageIf<TNode>());
        registrar.Register("averageifs", () => new AverageIfs<TNode>());
        registrar.Register("minifs",     () => new MinIfs<TNode>());
        registrar.Register("maxifs",     () => new MaxIfs<TNode>());
        return registrar;
    }
}
