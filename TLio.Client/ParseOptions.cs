using TLio.Commands;
using TLio.Commands.Advanced;
using TLio.Core.Contracts;
using TLio.Functions;
using TLio.Functions.Collections;
using TLio.Functions.Logic;

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
        options.CommandsProvider.Register("rename",  () => new Rename<TNode>());
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
        options.FunctionsProvider.Register("toArray",    () => new ToArray<TNode>());
        options.FunctionsProvider.Register("partial",    () => new Partial<TNode>());
        options.FunctionsProvider.Register("scriptpath", () => new ScriptPath<TNode>());
        options.FunctionsProvider.Register("datetime",   () => new Datetime<TNode>());
        options.FunctionsProvider.Register("newGuid",    () => new NewGuid<TNode>());
        options.FunctionsProvider.Register("path",       () => new ScriptPath<TNode>());

        // Predicates — built in rather than packaged, because ifElse and decisionTable
        // are core commands and a condition needs something that returns a boolean.
        options.FunctionsProvider.Register("equals",         () => new EqualsFunction<TNode>());
        options.FunctionsProvider.Register("notEquals",      () => new NotEqualsFunction<TNode>());
        options.FunctionsProvider.Register("greaterThan",    () => new GreaterThanFunction<TNode>());
        options.FunctionsProvider.Register("greaterOrEqual", () => new GreaterOrEqualFunction<TNode>());
        options.FunctionsProvider.Register("lessThan",       () => new LessThanFunction<TNode>());
        options.FunctionsProvider.Register("lessOrEqual",    () => new LessOrEqualFunction<TNode>());
        options.FunctionsProvider.Register("and",            () => new AndFunction<TNode>());
        options.FunctionsProvider.Register("or",             () => new OrFunction<TNode>());
        options.FunctionsProvider.Register("not",            () => new NotFunction<TNode>());
        options.FunctionsProvider.Register("exists",         () => new ExistsFunction<TNode>());
        options.FunctionsProvider.Register("isNull",         () => new IsNullFunction<TNode>());
        options.FunctionsProvider.Register("isString",       () => new IsStringFunction<TNode>());
        options.FunctionsProvider.Register("isNumber",       () => new IsNumberFunction<TNode>());
        options.FunctionsProvider.Register("isBoolean",      () => new IsBooleanFunction<TNode>());
        options.FunctionsProvider.Register("isArray",        () => new IsArrayFunction<TNode>());
        options.FunctionsProvider.Register("isObject",       () => new IsObjectFunction<TNode>());
        options.FunctionsProvider.Register("in",             () => new InFunction<TNode>());
        options.FunctionsProvider.Register("matches",        () => new MatchesFunction<TNode>());
        options.FunctionsProvider.Register("if",             () => new IfFunction<TNode>());
        options.FunctionsProvider.Register("coalesce",       () => new CoalesceFunction<TNode>());
        options.FunctionsProvider.Register("between",        () => new BetweenFunction<TNode>());

        // Collections — built in for the same reason the predicates are: ordering and
        // de-duplicating a node set is not a format concern, and until these existed neither
        // was reachable at all without a second document to merge against.
        options.FunctionsProvider.Register("distinct",       () => new Distinct<TNode>());
        options.FunctionsProvider.Register("sort",           () => new Sort<TNode>());
        options.FunctionsProvider.Register("sortby",         () => new SortBy<TNode>());
        options.FunctionsProvider.Register("last",           () => new Last<TNode>());

        return options;
    }
}
