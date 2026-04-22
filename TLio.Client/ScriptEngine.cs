using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Client;

/// <summary>
/// Entry point for executing TLio scripts.
/// The engine is generic over TNode so it can drive any registered data-format adapter.
///
/// Typical usage (JSON):
/// <code>
///   var engine  = new ScriptEngine&lt;JToken&gt;(commandsProvider, functionsProvider);
///   var context = JsonExecutionContext.CreateDefault();
///   var result  = engine.Execute(scriptJson, data, context);
/// </code>
/// </summary>
/// <typeparam name="TNode">Native node type of the target data format.</typeparam>
public class ScriptEngine<TNode>
{
    private readonly ICommandsProvider<TNode> _commandsProvider;
    private readonly IFunctionsProvider<TNode> _functionsProvider;

    public ScriptEngine(
        ICommandsProvider<TNode> commandsProvider,
        IFunctionsProvider<TNode> functionsProvider)
    {
        _commandsProvider = commandsProvider;
        _functionsProvider = functionsProvider;
    }

    /// <summary>
    /// Parse a serialised script and execute it against the given data context.
    /// The serialisation format of the script itself is implementation-specific —
    /// it may be JSON (for compatibility with JLio), YAML, or another format.
    /// </summary>
    public TLioExecutionResult<TNode> Execute(string scriptText, TNode data, IExecutionContext<TNode> context)
    {
        var converter = new CommandConverter<TNode>(_commandsProvider, _functionsProvider, context.NodeAdapter);
        var script = converter.ParseScript(scriptText);
        return script.Execute(data, context);
    }

    /// <summary>Execute a pre-parsed script object directly.</summary>
    public TLioExecutionResult<TNode> Execute(TLioScript<TNode> script, TNode data, IExecutionContext<TNode> context) =>
        script.Execute(data, context);

    /// <summary>
    /// Parse <paramref name="scriptText"/> once and return an immutable <see cref="CompiledScript{TNode}"/>
    /// that can produce per-execution instances cheaply via <see cref="CompiledScript{TNode}.CreateExecutable"/>
    /// or <see cref="CompiledScript{TNode}.Execute"/>.
    ///
    /// The returned handle is thread-safe and intended to be held for the lifetime of the engine.
    /// </summary>
    public CompiledScript<TNode> Compile(string scriptText, INodeAdapter<TNode> adapter)
    {
        var converter = new CommandConverter<TNode>(_commandsProvider, _functionsProvider, adapter);
        var template  = converter.ParseScript(scriptText);
        return new CompiledScript<TNode>(template);
    }

    /// <summary>Convenience overload — uses <paramref name="context"/>.NodeAdapter for parsing.</summary>
    public CompiledScript<TNode> Compile(string scriptText, IExecutionContext<TNode> context) =>
        Compile(scriptText, context.NodeAdapter);
}
