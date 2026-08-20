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
///
/// A script may be written in JSON, XML or YAML. JSON is understood out of the box; the other
/// two arrive with their format package and are plugged in once, after which the notation of a
/// script is detected from its text:
/// <code>
///   engine.UseXmlScripts();   // TLio.Xml
///   engine.UseYamlScripts();  // TLio.Yaml
///   engine.Execute(xmlScriptText, data, context);
/// </code>
/// The notation is independent of the data format: an XML script can transform a JSON document.
/// Only the paths inside the script have to speak the target format's path language.
/// </summary>
/// <typeparam name="TNode">Native node type of the target data format.</typeparam>
public class ScriptEngine<TNode>
{
    private readonly Dictionary<ScriptFormat, Func<INodeAdapter<TNode>, IScriptParser<TNode>>> _parserFactories = new();

    public ScriptEngine(
        ICommandsProvider<TNode> commandsProvider,
        IFunctionsProvider<TNode> functionsProvider)
    {
        CommandsProvider = commandsProvider;
        FunctionsProvider = functionsProvider;

        // The JSON notation needs no registration: CommandConverter lives in this assembly,
        // where the XML and YAML parsers — which sit in their own format packages — cannot.
        _parserFactories[ScriptFormat.Json] = adapter =>
            new CommandConverter<TNode>(commandsProvider, functionsProvider, adapter);
    }

    public ICommandsProvider<TNode> CommandsProvider { get; }

    public IFunctionsProvider<TNode> FunctionsProvider { get; }

    /// <summary>The notations this engine can currently parse.</summary>
    public IEnumerable<ScriptFormat> SupportedScriptFormats => _parserFactories.Keys;

    /// <summary>
    /// Teach the engine a script notation. The factory is handed the node adapter in play at
    /// parse time, so one registration serves every context the engine is used with.
    /// Registering a notation twice replaces the earlier factory.
    ///
    /// The format packages wrap this in a named extension — <c>UseXmlScripts</c> in TLio.Xml,
    /// <c>UseYamlScripts</c> in TLio.Yaml — which is what callers normally reach for.
    /// </summary>
    public ScriptEngine<TNode> UseScriptParser(
        ScriptFormat format,
        Func<INodeAdapter<TNode>, IScriptParser<TNode>> parserFactory)
    {
        ArgumentNullException.ThrowIfNull(parserFactory);
        _parserFactories[format] = parserFactory;
        return this;
    }

    /// <summary>
    /// Teach the engine a script notation using one already-built parser, for the case where
    /// the engine only ever runs against a single adapter.
    /// </summary>
    public ScriptEngine<TNode> UseScriptParser(IScriptParser<TNode> parser)
    {
        ArgumentNullException.ThrowIfNull(parser);
        return UseScriptParser(parser.Format, _ => parser);
    }

    /// <summary>
    /// Parse a serialised script and execute it against the given data context.
    /// The notation is detected from the script text — see <see cref="ScriptFormatDetector"/>.
    /// </summary>
    public TLioExecutionResult<TNode> Execute(string scriptText, TNode data, IExecutionContext<TNode> context) =>
        Execute(scriptText, ScriptFormatDetector.Detect(scriptText), data, context);

    /// <summary>
    /// Parse a serialised script written in <paramref name="format"/> and execute it against
    /// the given data context.
    /// </summary>
    public TLioExecutionResult<TNode> Execute(
        string scriptText, ScriptFormat format, TNode data, IExecutionContext<TNode> context)
    {
        var script = Parse(scriptText, format, context.NodeAdapter);
        return script.Execute(data, context);
    }

    /// <summary>Execute a pre-parsed script object directly.</summary>
    public TLioExecutionResult<TNode> Execute(TLioScript<TNode> script, TNode data, IExecutionContext<TNode> context) =>
        script.Execute(data, context);

    /// <summary>
    /// Parse script text into a script, detecting the notation from the text.
    /// </summary>
    public TLioScript<TNode> Parse(string scriptText, INodeAdapter<TNode> adapter) =>
        Parse(scriptText, ScriptFormatDetector.Detect(scriptText), adapter);

    /// <summary>
    /// Parse script text written in <paramref name="format"/> into a script.
    ///
    /// A notation nobody registered yields an empty script carrying a parse warning, matching
    /// what every parser does with text it cannot read: nothing runs, and the reason is on the
    /// script rather than thrown at a caller who was handed the text by someone else.
    /// </summary>
    public TLioScript<TNode> Parse(string scriptText, ScriptFormat format, INodeAdapter<TNode> adapter)
    {
        if (!_parserFactories.TryGetValue(format, out var factory))
        {
            var script = new TLioScript<TNode>();
            script.ParseWarnings.Add(
                $"No parser is registered for {format} script notation. " +
                $"Call UseScriptParser, or the {UseCallFor(format)} extension from its format package.");
            return script;
        }

        return factory(adapter).ParseScript(scriptText);
    }

    /// <summary>
    /// Parse <paramref name="scriptText"/> once and return an immutable <see cref="CompiledScript{TNode}"/>
    /// that can produce per-execution instances cheaply via <see cref="CompiledScript{TNode}.CreateExecutable"/>
    /// or <see cref="CompiledScript{TNode}.Execute"/>.
    ///
    /// The returned handle is thread-safe and intended to be held for the lifetime of the engine.
    /// </summary>
    public CompiledScript<TNode> Compile(string scriptText, INodeAdapter<TNode> adapter) =>
        Compile(scriptText, ScriptFormatDetector.Detect(scriptText), adapter);

    /// <summary>Compile script text written in a known notation.</summary>
    public CompiledScript<TNode> Compile(string scriptText, ScriptFormat format, INodeAdapter<TNode> adapter) =>
        new(Parse(scriptText, format, adapter));

    /// <summary>Convenience overload — uses <paramref name="context"/>.NodeAdapter for parsing.</summary>
    public CompiledScript<TNode> Compile(string scriptText, IExecutionContext<TNode> context) =>
        Compile(scriptText, context.NodeAdapter);

    /// <summary>Convenience overload — uses <paramref name="context"/>.NodeAdapter for parsing.</summary>
    public CompiledScript<TNode> Compile(string scriptText, ScriptFormat format, IExecutionContext<TNode> context) =>
        Compile(scriptText, format, context.NodeAdapter);

    private static string UseCallFor(ScriptFormat format) => format switch
    {
        ScriptFormat.Xml => "UseXmlScripts",
        ScriptFormat.Yaml => "UseYamlScripts",
        _ => "UseScriptParser"
    };
}
