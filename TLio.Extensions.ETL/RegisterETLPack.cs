using TLio.Extensions.ETL.Commands;

namespace TLio.Extensions.ETL;

/// <summary>
/// Extension method to register all TLio.Extensions.ETL commands into any
/// <see cref="ICommandsProviderRegistrar{TNode}"/>.
///
/// Usage:
/// <code>
///   ParseOptions&lt;JToken&gt;.CreateDefault().CommandsProvider.RegisterETL&lt;JToken&gt;();
/// </code>
///
/// Registers: flatten, restore, resolve, tocsv.
///
/// Ported from JLio.Extensions.ETL.RegisterETLPack.RegisterETL().
/// </summary>
public static class RegisterETLPack
{
    public static ICommandsProviderRegistrar<TNode> RegisterETL<TNode>(
        this ICommandsProviderRegistrar<TNode> registrar)
    {
        registrar.Register("flatten", () => new Flatten<TNode>());
        registrar.Register("restore", () => new Restore<TNode>());
        registrar.Register("resolve", () => new Resolve<TNode>());
        registrar.Register("tocsv",   () => new ToCsv<TNode>());
        return registrar;
    }
}
