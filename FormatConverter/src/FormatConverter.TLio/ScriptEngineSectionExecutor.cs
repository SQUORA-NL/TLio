using TLio.Client;
using TLio.Core.Contracts;

namespace FormatConverter.TLio;

/// <summary>
/// Runs one section of a multi-format script on a <see cref="ScriptEngine{TNode}"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the half of the pipeline that makes <c>convert</c> usable in the middle of a script
/// rather than only at the end of one. The runner splits the script at each boundary and converts
/// the document; an executor is what actually runs the commands on either side.
/// </para>
/// <para>
/// It is generic so that <c>FormatConverter.TLio</c> needs nothing but <c>TLio.Client</c> — the
/// host supplies the engine and a factory for the execution context, which is where the format
/// packages come in:
/// </para>
/// <code>
/// runner.RegisterExecutor(new ScriptEngineSectionExecutor&lt;JToken&gt;(
///     "json", jsonEngine, JsonExecutionContext.CreateDefault));
/// runner.RegisterExecutor(new ScriptEngineSectionExecutor&lt;XElement&gt;(
///     "xml", xmlEngine, XmlExecutionContext.CreateWithNativeXPath));
/// </code>
/// <para>
/// A fresh context per section keeps the run reentrant, and a section's document is parsed from
/// and serialised back to text because that is the only thing two different node types can hand
/// each other.
/// </para>
/// </remarks>
/// <typeparam name="TNode">Native node type of this section's format.</typeparam>
public sealed class ScriptEngineSectionExecutor<TNode> : IFormatSectionExecutor
{
    private readonly ScriptEngine<TNode> _engine;
    private readonly Func<IExecutionContext<TNode>> _contextFactory;

    /// <summary>Wire an engine and a context factory to one format ID.</summary>
    /// <param name="formatId">The format this executor handles, e.g. <c>"json"</c>.</param>
    /// <param name="engine">The engine that parses and runs the section's commands.</param>
    /// <param name="contextFactory">Builds a fresh execution context for each section.</param>
    public ScriptEngineSectionExecutor(
        string formatId,
        ScriptEngine<TNode> engine,
        Func<IExecutionContext<TNode>> contextFactory)
    {
        if (string.IsNullOrWhiteSpace(formatId))
            throw new ArgumentException("A section executor needs a format ID.", nameof(formatId));

        FormatId = formatId;
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    /// <inheritdoc/>
    public string FormatId { get; }

    /// <inheritdoc/>
    public SectionExecutionResult Execute(string sectionScriptJson, string document)
    {
        var context = _contextFactory();
        var data = context.NodeAdapter.Parse(document);

        var result = _engine.Execute(sectionScriptJson, data, context);

        return new SectionExecutionResult(
            context.NodeAdapter.Serialize(result.Data),
            result.Success,
            context.GetLogEntries());
    }
}
