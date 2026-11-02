using TLio.Core.Contracts;

namespace TLio.FormatConverter;

/// <summary>
/// Registers the format-conversion commands into any
/// <see cref="ICommandsProviderRegistrar{TNode}"/>.
/// </summary>
/// <remarks>
/// <code>
/// var options = ParseOptions&lt;JToken&gt;.CreateDefault();
/// options.CommandsProvider.RegisterFormatConversion&lt;JToken&gt;(converter, "json");
/// </code>
/// <para>
/// Registers <c>convertValue</c>, which converts a value in place, and <c>convert</c>, which
/// marks a format boundary. <c>convert</c> only does anything under
/// <see cref="MultiFormatScriptRunner"/> — the runner intercepts it before the engine is
/// involved. It is registered anyway so a script that uses it outside a pipeline gets a warning
/// naming the reason rather than "unknown command".
/// </para>
/// </remarks>
public static class RegisterFormatConversionPack
{
    /// <summary>
    /// Registers the conversion commands on <paramref name="target"/> and returns it, so calls
    /// chain. Both arguments are required — without them <c>convertValue</c> cannot know whether
    /// to graft its result in as structure or as text, so there is no overload that omits them.
    /// Delegates to <see cref="RegisterFormatConversion{TNode}"/>.
    /// </summary>
    /// <param name="target">The target to register on, usually <c>ParseOptions&lt;TNode&gt;</c>.</param>
    /// <param name="converter">The converter holding the registered format adapters.</param>
    /// <param name="documentFormatId">
    /// The format the documents this engine runs against are in — <c>"json"</c> for a
    /// <c>JToken</c> engine, <c>"xml"</c> for <c>XElement</c>, <c>"yaml"</c> for <c>YamlNode</c>.
    /// </param>
    public static TSelf WithFormatConversion<TNode, TSelf>(
        this IRegistrationTarget<TNode, TSelf> target,
        Core.FormatConverter converter,
        string documentFormatId)
        where TSelf : IRegistrationTarget<TNode, TSelf>
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentFormatId);
        target.CommandsRegistrar.RegisterFormatConversion<TNode>(converter, documentFormatId);
        return target.Self;
    }

    /// <summary>Register the conversion commands for a document in <paramref name="documentFormatId"/>.</summary>
    /// <param name="registrar">The registrar to add to.</param>
    /// <param name="converter">The converter holding the registered format adapters.</param>
    /// <param name="documentFormatId">The format of the documents this engine runs against.</param>
    public static ICommandsProviderRegistrar<TNode> RegisterFormatConversion<TNode>(
        this ICommandsProviderRegistrar<TNode> registrar,
        Core.FormatConverter converter,
        string documentFormatId)
    {
        ArgumentNullException.ThrowIfNull(registrar);
        ArgumentNullException.ThrowIfNull(converter);

        registrar.Register("convertValue", () => new ConvertValue<TNode>(converter, documentFormatId));
        registrar.Register("convert", () => new ConvertCommand<TNode>());
        return registrar;
    }
}
