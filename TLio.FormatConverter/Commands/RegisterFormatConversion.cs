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
    /// <summary>Register the conversion commands for a document in <paramref name="documentFormatId"/>.</summary>
    /// <param name="registrar">The registrar to add to.</param>
    /// <param name="converter">The converter holding the registered format adapters.</param>
    /// <param name="documentFormatId">
    /// The format the documents this engine runs against are in — <c>"json"</c> for a
    /// <c>JToken</c> engine, and so on. It decides whether a converted value comes back as
    /// structure or as text.
    /// </param>
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
