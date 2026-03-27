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
///            format, parse, padleft, padright, newguid, isempty.
///
/// Ported from JLio.Extensions.Text.RegisterTextPack.RegisterText().
/// </summary>
public static class RegisterTextPack
{
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
        return registrar;
    }
}
