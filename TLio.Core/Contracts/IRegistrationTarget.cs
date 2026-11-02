namespace TLio.Core.Contracts;

/// <summary>
/// Something a pack can be registered on: it holds both a commands registrar and a functions
/// registrar, so a pack's single <c>With&lt;Pack&gt;()</c> extension method can register everything
/// the pack provides without the caller knowing which of the two it uses.
/// </summary>
/// <remarks>
/// <typeparamref name="TSelf"/> is the implementing type itself (<c>ParseOptions&lt;TNode&gt;</c>),
/// so the <c>With*</c> methods return the concrete type and calls chain:
/// <code>
/// var options = ParseOptions&lt;JToken&gt;.CreateDefault().WithMath().WithText().WithLooping();
/// </code>
/// The members are implemented explicitly by <c>ParseOptions</c>; they exist for the extension
/// methods, not for hosts.
/// </remarks>
/// <typeparam name="TNode">Native node type of the target data format.</typeparam>
/// <typeparam name="TSelf">The implementing type; returned by the <c>With*</c> methods.</typeparam>
public interface IRegistrationTarget<TNode, out TSelf>
    where TSelf : IRegistrationTarget<TNode, TSelf>
{
    /// <summary>Where a pack's commands are registered.</summary>
    ICommandsProviderRegistrar<TNode> CommandsRegistrar { get; }

    /// <summary>Where a pack's functions are registered.</summary>
    IFunctionsProviderRegistrar<TNode> FunctionsRegistrar { get; }

    /// <summary>The target itself, as its concrete type.</summary>
    TSelf Self { get; }
}
