using TLio.Core.Models.Logging;

namespace TLio.FormatConverter;

/// <summary>
/// Executes a section of a multi-format TLio script in a specific format.
/// Each registered executor handles one format (e.g. JSON, XML, YAML).
/// </summary>
/// <remarks>
/// A script cannot change format inside the engine — <c>ICommand&lt;TNode&gt;</c> takes a node type
/// in and returns the same one out — so a multi-format script is split at each <c>convert</c> and
/// each piece is re-hosted on an engine of its own. This is that re-hosting seam.
/// <see cref="ScriptEngineSectionExecutor{TNode}"/> is the implementation callers normally want.
/// </remarks>
public interface IFormatSectionExecutor
{
    /// <summary>The format ID this executor handles (case-insensitive).</summary>
    string FormatId { get; }

    /// <summary>
    /// Execute the commands in <paramref name="sectionScriptJson"/> against <paramref name="document"/>
    /// and return the result serialised back to the same format string.
    /// </summary>
    /// <param name="sectionScriptJson">JSON array of TLio commands for this section (no <c>convert</c> commands).</param>
    /// <param name="document">The working document in this section's native format string.</param>
    SectionExecutionResult Execute(string sectionScriptJson, string document);
}

/// <summary>The outcome of running one section of a multi-format script.</summary>
/// <param name="Document">The document after the section's commands, in the section's own format.</param>
/// <param name="Success">Whether every command in the section succeeded.</param>
/// <param name="Logs">
/// What the engine logged while running the section. A multi-format run spans several engines, so
/// these are collected as the pipeline goes and handed back together.
/// </param>
public sealed record SectionExecutionResult(string Document, bool Success, LogEntries Logs)
{
    /// <summary>A section that ran cleanly and logged nothing worth keeping.</summary>
    public static SectionExecutionResult Ok(string document) => new(document, true, new LogEntries());
}
