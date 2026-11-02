namespace TLio.Extensions.Text;

/// <summary>
/// Extension method to register all TLio.Extensions.Text functions into any
/// <see cref="IFunctionsProviderRegistrar{TNode}"/>.
///
/// Usage (with ParseOptions):
/// <code>
///   ParseOptions&lt;JToken&gt;.CreateDefault().FunctionsProvider.RegisterText&lt;JToken&gt;();
/// </code>
///
/// Registers: concat, length, substring, toupper, tolower, trim, trimstart, trimend,
///            startswith, endswith, contains, replace, split, join, indexof,
///            format, parse, padleft, padright, newguid, isempty, toString,
///            toFixed, right, regexreplace, regexextract.
///
/// Function lookup is case-insensitive (see FunctionsProvider), so each name also matches
/// its camelCase spelling — =toUpper(...) and =toupper(...) resolve to the same function.
/// No separate alias registrations are needed.
///
/// Ported from JLio.Extensions.Text.RegisterTextPack.RegisterText().
/// </summary>
public static class TextRegistrar
{
    /// <summary>
    /// Registers every Text function on <paramref name="target"/> and returns it, so calls chain:
    /// <c>ParseOptions&lt;JToken&gt;.CreateDefault().WithText().WithMath()</c>. Delegates to
    /// <see cref="RegisterText{TNode}"/>, which holds the list of what the pack provides.
    /// </summary>
    public static TSelf WithText<TNode, TSelf>(this IRegistrationTarget<TNode, TSelf> target)
        where TSelf : IRegistrationTarget<TNode, TSelf>
    {
        ArgumentNullException.ThrowIfNull(target);
        target.FunctionsRegistrar.RegisterText<TNode>();
        return target.Self;
    }

    /// <summary>
    /// Alias for <see cref="RegisterText{TNode}"/> — JLio-compatible method name.
    /// </summary>
    [Obsolete("Use WithText() (or RegisterText). Text was the only pack with an alias; it is removed in the next major version.")]
    public static IFunctionsProviderRegistrar<TNode> RegisterTextPack<TNode>(
        this IFunctionsProviderRegistrar<TNode> registrar)
        => registrar.RegisterText<TNode>();

    public static IFunctionsProviderRegistrar<TNode> RegisterText<TNode>(
        this IFunctionsProviderRegistrar<TNode> registrar)
    {
        registrar.Register("concat",     () => new Concat<TNode>());
        registrar.Register("length",     () => new Length<TNode>());
        registrar.Register("substring",  () => new Substring<TNode>());
        registrar.Register("toupper",    () => new ToUpper<TNode>());
        registrar.Register("tolower",    () => new ToLower<TNode>());
        registrar.Register("trim",       () => new Trim<TNode>());
        registrar.Register("trimstart",  () => new TrimStart<TNode>());
        registrar.Register("trimend",    () => new TrimEnd<TNode>());
        registrar.Register("startswith", () => new StartsWith<TNode>());
        registrar.Register("endswith",   () => new EndsWith<TNode>());
        registrar.Register("contains",   () => new Contains<TNode>());
        registrar.Register("replace",    () => new Replace<TNode>());
        registrar.Register("split",      () => new Split<TNode>());
        registrar.Register("join",       () => new Join<TNode>());
        registrar.Register("indexof",    () => new IndexOf<TNode>());
        registrar.Register("format",     () => new Format<TNode>());
        registrar.Register("parse",      () => new Parse<TNode>());
        registrar.Register("padleft",    () => new PadLeft<TNode>());
        registrar.Register("padright",   () => new PadRight<TNode>());
        registrar.Register("newguid",    () => new NewGuid<TNode>());
        registrar.Register("isempty",    () => new IsEmpty<TNode>());
        // New in 008: toString function
        registrar.Register("toString",   () => new ToStringFunction<TNode>());
        registrar.Register("toFixed",    () => new ToFixed<TNode>());
        registrar.Register("right",        () => new Right<TNode>());
        registrar.Register("regexreplace", () => new RegexReplace<TNode>());
        registrar.Register("regexextract", () => new RegexExtract<TNode>());
        return registrar;
    }
}
