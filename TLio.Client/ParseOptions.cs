using TLio.Commands;
using TLio.Commands.Advanced;
using TLio.Core.Contracts;
using TLio.Functions;

namespace TLio.Client;

/// <summary>
/// Convenience class that pre-registers all built-in TLio commands and functions
/// and exposes the configured providers for use with ScriptEngine.
///
/// Parallel to JLio's ParseOptions.
///
/// Usage:
/// <code>
///   var options = ParseOptions&lt;JToken&gt;.CreateDefault();
///   var engine  = new ScriptEngine&lt;JToken&gt;(options.CommandsProvider, options.FunctionsProvider);
///   var result  = engine.Execute(scriptJson, data, context);
/// </code>
/// </summary>
public class ParseOptions<TNode>
{
    public CommandsProvider<TNode> CommandsProvider { get; } = new();
    public FunctionsProvider<TNode> FunctionsProvider { get; } = new();

    /// <summary>Fluently register a parameterless command by name.</summary>
    public ParseOptions<TNode> RegisterCommand<TCommand>(string name)
        where TCommand : ICommand<TNode>, new()
    {
        CommandsProvider.Register(name, () => new TCommand());
        return this;
    }

    /// <summary>Fluently register a parameterless function by name.</summary>
    public ParseOptions<TNode> RegisterFunction<TFunction>(string name)
        where TFunction : IFunction<TNode>, new()
    {
        FunctionsProvider.Register(name, () => new TFunction());
        return this;
    }

    /// <summary>
    /// Create a ParseOptions instance pre-loaded with all built-in commands and functions.
    /// </summary>
    public static ParseOptions<TNode> CreateDefault()
    {
        var options = new ParseOptions<TNode>();

        // Built-in commands
        options.CommandsProvider.Register("set",     () => new Set<TNode>());
        options.CommandsProvider.Register("add",     () => new Add<TNode>());
        options.CommandsProvider.Register("put",     () => new Put<TNode>());
        options.CommandsProvider.Register("remove",  () => new Remove<TNode>());
        options.CommandsProvider.Register("copy",    () => new Copy<TNode>());
        options.CommandsProvider.Register("move",    () => new Move<TNode>());
        options.CommandsProvider.Register("ifElse",  () => new IfElse<TNode>());
        options.CommandsProvider.Register("compare", () => new Compare<TNode>());
        options.CommandsProvider.Register("merge",         () => new Merge<TNode>());
        options.CommandsProvider.Register("decisionTable", () => new DecisionTable<TNode>());

        // Built-in functions
        options.FunctionsProvider.Register("fetch",      () => new Fetch<TNode>());
        options.FunctionsProvider.Register("indirect",   () => new Indirect<TNode>());
        options.FunctionsProvider.Register("promote",    () => new Promote<TNode>());
        options.FunctionsProvider.Register("partial",    () => new Partial<TNode>());
        options.FunctionsProvider.Register("scriptpath", () => new ScriptPath<TNode>());
        options.FunctionsProvider.Register("datetime",   () => new Datetime<TNode>());
        options.FunctionsProvider.Register("newGuid",    () => new NewGuid<TNode>());
        options.FunctionsProvider.Register("path",       () => new ScriptPath<TNode>());

        return options;
    }
}
