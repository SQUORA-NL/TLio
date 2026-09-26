using TLio.Core.Contracts;

namespace TLio.Extensions.Looping;

/// <summary>
/// Extension method to register all TLio.Extensions.Looping commands into any
/// <see cref="ICommandsProviderRegistrar{TNode}"/>.
///
/// Usage (with ParseOptions):
/// <code>
///   ParseOptions&lt;JToken&gt;.CreateDefault().CommandsProvider.RegisterLooping&lt;JToken&gt;();
/// </code>
///
/// Registers: forEach, while.
/// </summary>
public static class RegisterLoopingPack
{
    public static ICommandsProviderRegistrar<TNode> RegisterLooping<TNode>(
        this ICommandsProviderRegistrar<TNode> registrar)
    {
        registrar.Register("forEach", () => new ForEach<TNode>());
        registrar.Register("while", () => new While<TNode>());
        return registrar;
    }
}
