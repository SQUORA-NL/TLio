using TLio.FormatConverter.Core;
using TLio.FormatConverter.Core.Exceptions;
using TLio.Core.Models;
using TLio.Core.Models.Logging;

namespace TLio.FormatConverter;

/// <summary>
/// Orchestrates segmented script execution across multiple formats.
/// </summary>
/// <remarks>
/// <para>
/// The runner splits a script at its <c>convert</c> command boundaries into
/// <see cref="ScriptSection"/> objects and executes each section using the registered
/// <see cref="IFormatSectionExecutor"/> for that format.  Format conversion at each boundary is
/// performed by the injected <see cref="TLio.FormatConverter.Core.FormatConverter"/>.
/// </para>
/// <para>
/// If a section contains no non-<c>convert</c> commands, execution is skipped for that section
/// and the document passes through unchanged.
/// </para>
/// </remarks>
public sealed class MultiFormatScriptRunner
{
    private readonly Core.FormatConverter _converter;
    private readonly Dictionary<string, IFormatSectionExecutor> _executors =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initialises a runner with the given format converter.
    /// Register <see cref="IFormatSectionExecutor"/> instances via <see cref="RegisterExecutor"/>
    /// for each format that contains non-<c>convert</c> commands.
    /// </summary>
    public MultiFormatScriptRunner(Core.FormatConverter converter)
    {
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
    }

    /// <summary>
    /// Whether a script crosses a format boundary and therefore needs this runner rather than a
    /// plain <c>ScriptEngine</c>.
    /// </summary>
    /// <remarks>
    /// Callers that accept scripts from elsewhere — an API endpoint, the MCP server — use this to
    /// decide which of the two paths to take. The script is read in whichever of the three
    /// notations it is written in, so <c>convert</c> is found in an XML or YAML script as
    /// readily as in a JSON one. Text that is not a script at all is not a multi-format script,
    /// and says so by returning false rather than throwing; whatever reads it next will report
    /// the real problem.
    /// </remarks>
    public static bool CrossesAFormatBoundary(string script, ScriptFormat? notation = null) =>
        ScriptSectionSplitter.ContainsConvert(script, notation);

    /// <summary>Register a format-specific section executor.</summary>
    public void RegisterExecutor(IFormatSectionExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(executor);
        _executors[executor.FormatId] = executor;
    }

    /// <summary>
    /// Execute a multi-format script against an input document and return the final output.
    /// </summary>
    /// <param name="initialFormatId">Format of <paramref name="inputDocument"/>.</param>
    /// <param name="inputDocument">The starting document in <paramref name="initialFormatId"/> format.</param>
    /// <param name="script">TLio script in any notation, possibly containing <c>convert</c> commands.</param>
    /// <returns>The document after all sections have been executed, in the last section's format.</returns>
    /// <exception cref="FormatNotRegisteredException">When a <c>convert</c> command targets an unregistered format.</exception>
    /// <exception cref="SectionExecutorNotRegisteredException">When a section has commands and no executor to run them.</exception>
    public string Execute(string initialFormatId, string inputDocument, string script) =>
        Run(initialFormatId, inputDocument, script).Document;

    /// <summary>
    /// Execute a multi-format script and return the output together with what every section
    /// logged along the way.
    /// </summary>
    /// <remarks>
    /// A multi-format run spans one engine per section, so there is no single execution context to
    /// ask afterwards. The logs are collected as the pipeline goes.
    /// </remarks>
    /// <inheritdoc cref="Execute(string,string,string)" path="/exception"/>
    public MultiFormatScriptResult Run(
        string initialFormatId, string inputDocument, string script, ScriptFormat? notation = null)
    {
        var sections = ScriptSectionSplitter.Split(initialFormatId, script, notation);
        return RunSections(sections, initialFormatId, inputDocument,
            (executor, section, _, document) => executor.Execute(section.ScriptText, document));
    }

    /// <summary>
    /// Parse <paramref name="script"/> once and return a handle that can run it against many
    /// input documents via <see cref="CompiledMultiFormatScript.Run"/>, without re-parsing.
    /// </summary>
    /// <remarks>
    /// For a large script, parsing text into commands is the dominant per-run cost — this is the
    /// multi-format equivalent of <c>ScriptEngine{TNode}.Compile</c>. Only
    /// sections whose registered executor implements <see cref="ICompilableFormatSectionExecutor"/>
    /// are pre-parsed; the rest still parse on every <see cref="CompiledMultiFormatScript.Run"/>
    /// call, so mixing a compilable and a non-compilable executor degrades gracefully rather than
    /// failing.
    /// </remarks>
    /// <exception cref="FormatNotRegisteredException">When a <c>convert</c> command targets an unregistered format.</exception>
    /// <exception cref="SectionExecutorNotRegisteredException">When a section has commands and no executor to run them.</exception>
    public CompiledMultiFormatScript Compile(
        string initialFormatId, string script, ScriptFormat? notation = null)
    {
        var sections = ScriptSectionSplitter.Split(initialFormatId, script, notation);
        var compiledSections = new object?[sections.Count];

        for (var i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            if (section.CommandCount == 0)
                continue;

            var executor = ResolveExecutor(section.FormatId);
            if (executor is ICompilableFormatSectionExecutor compilable)
                compiledSections[i] = compilable.Compile(section.ScriptText);
        }

        return new CompiledMultiFormatScript(this, initialFormatId, sections, compiledSections);
    }

    internal MultiFormatScriptResult RunCompiled(
        string initialFormatId, string inputDocument, List<ScriptSection> sections, object?[] compiledSections) =>
        RunSections(sections, initialFormatId, inputDocument, (executor, section, index, document) =>
            compiledSections[index] is { } handle && executor is ICompilableFormatSectionExecutor compilable
                ? compilable.ExecuteCompiled(handle, document)
                : executor.Execute(section.ScriptText, document));

    private MultiFormatScriptResult RunSections(
        List<ScriptSection> sections,
        string initialFormatId,
        string inputDocument,
        Func<IFormatSectionExecutor, ScriptSection, int, string, SectionExecutionResult> runSection)
    {
        var currentDocument = inputDocument;
        var currentFormat = initialFormatId;
        var logs = new LogEntries();
        var success = true;

        for (var i = 0; i < sections.Count; i++)
        {
            var section = sections[i];

            // Apply incoming format conversion (all sections after the first)
            if (section.IncomingSettings is not null)
            {
                currentDocument = _converter.Convert(
                    sections[i - 1].FormatId,
                    currentDocument,
                    section.FormatId,
                    section.IncomingSettings);
                currentFormat = section.FormatId;
            }

            if (section.CommandCount == 0)
                continue;

            var executor = ResolveExecutor(section.FormatId);
            var result = runSection(executor, section, i, currentDocument);
            currentDocument = result.Document;
            logs.AddRange(result.Logs);
            success &= result.Success;
        }

        return new MultiFormatScriptResult(currentDocument, currentFormat, success, logs);
    }

    private IFormatSectionExecutor ResolveExecutor(string formatId)
    {
        if (!_executors.TryGetValue(formatId, out var executor))
        {
            // Silently dropping the commands is the one thing this must not do: the script
            // would report success having applied none of it.
            throw new SectionExecutorNotRegisteredException(formatId, _executors.Keys.ToList());
        }

        return executor;
    }
}
