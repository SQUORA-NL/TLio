using TLio.Client;
using TLio.Core.Contracts;

namespace TLio.Xml;

/// <summary>
/// Plugs the XML script notation into a <see cref="ScriptEngine{TNode}"/>.
///
/// The engine understands the JSON notation on its own; XML arrives with this package, which
/// the engine cannot reference without a cycle, so it is registered rather than built in.
/// </summary>
public static class XmlScriptEngineExtensions
{
    /// <summary>
    /// Let <paramref name="engine"/> read scripts written in the XML notation. After this call
    /// a script whose text opens with a tag is parsed as XML, and one written in JSON or YAML
    /// keeps working as before.
    ///
    /// The notation is independent of the data being transformed — an XML script can drive a
    /// JSON document, as long as the paths inside it are JSONPath.
    /// </summary>
    public static ScriptEngine<TNode> UseXmlScripts<TNode>(this ScriptEngine<TNode> engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        return engine.UseScriptParser(
            Core.Models.ScriptFormat.Xml,
            adapter => new XmlScriptParser<TNode>(
                engine.CommandsProvider, engine.FunctionsProvider, adapter));
    }

    /// <summary>Build an XML script parser directly, for callers not going through the engine.</summary>
    public static IScriptParser<TNode> CreateXmlScriptParser<TNode>(
        ICommandsProvider<TNode> commandsProvider,
        IFunctionsProvider<TNode> functionsProvider,
        INodeAdapter<TNode> nodeAdapter) =>
        new XmlScriptParser<TNode>(commandsProvider, functionsProvider, nodeAdapter);
}
