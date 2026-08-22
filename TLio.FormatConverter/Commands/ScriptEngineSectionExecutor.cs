using Microsoft.Extensions.Logging;
using TLio.Client;
using TLio.Core;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Core.Models.Logging;

namespace TLio.FormatConverter;

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
/// It is generic so that <c>TLio.FormatConverter</c> needs nothing but <c>TLio.Client</c> — the
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
    public SectionExecutionResult Execute(string sectionScript, string document)
    {
        var context = _contextFactory();
        var data = context.NodeAdapter.Parse(document);

        // A section is a script in the notation the whole script was written in, so an engine
        // that was never given that notation's parser reads it as nothing. The runner only sends
        // sections that hold commands, so nothing parsed means the notation is unreadable here —
        // and passing the document through as a success would apply none of the script while
        // reporting that it worked.
        var script = _engine.Parse(sectionScript, context.NodeAdapter);
        if (script.Count == 0)
        {
            var logs = new LogEntries();
            logs.Add(new LogEntry(LogLevel.Error, CoreConstants.CommandExecution,
                $"section for format '{FormatId}' parsed to no commands — its notation " +
                $"({ScriptFormatDetector.Detect(sectionScript)}) has no parser registered on this " +
                $"engine. Register it with UseXmlScripts() / UseYamlScripts() when building the " +
                $"engine you hand to the section executor.", DateTimeOffset.UtcNow));
            foreach (var warning in script.ParseWarnings)
                logs.Add(new LogEntry(LogLevel.Warning, CoreConstants.CommandExecution, warning, DateTimeOffset.UtcNow));

            return new SectionExecutionResult(document, false, logs);
        }

        var result = _engine.Execute(script, data, context);

        return new SectionExecutionResult(
            context.NodeAdapter.Serialize(result.Data),
            result.Success,
            context.GetLogEntries());
    }
}
