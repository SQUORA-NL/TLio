using TLio.Client;
using TLio.Core.Contracts;

namespace TLio.Yaml;

/// <summary>
/// Plugs the YAML script notation into a <see cref="ScriptEngine{TNode}"/>.
///
/// The engine understands the JSON notation on its own; YAML arrives with this package, which
/// the engine cannot reference without a cycle, so it is registered rather than built in.
/// </summary>
public static class YamlScriptEngineExtensions
{
    /// <summary>
    /// Let <paramref name="engine"/> read scripts written in the YAML notation. After this call
    /// a script that is neither a tag nor a bracket is parsed as YAML, and one written in JSON
    /// or XML keeps working as before.
    ///
    /// A JSON script still goes to the JSON parser even though YAML would accept it, so adding
    /// this changes nothing about scripts already in use.
    /// </summary>
    public static ScriptEngine<TNode> UseYamlScripts<TNode>(this ScriptEngine<TNode> engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        return engine.UseScriptParser(
            Core.Models.ScriptFormat.Yaml,
            adapter => new YamlScriptParser<TNode>(
                engine.CommandsProvider, engine.FunctionsProvider, adapter));
    }

    /// <summary>Build a YAML script parser directly, for callers not going through the engine.</summary>
    public static IScriptParser<TNode> CreateYamlScriptParser<TNode>(
        ICommandsProvider<TNode> commandsProvider,
        IFunctionsProvider<TNode> functionsProvider,
        INodeAdapter<TNode> nodeAdapter) =>
        new YamlScriptParser<TNode>(commandsProvider, functionsProvider, nodeAdapter);
}
