namespace TLio.FormatConverter;

/// <summary>
/// A section executor that can pre-parse a section's script text once and reuse the result
/// across many <see cref="ExecuteCompiled"/> calls, instead of parsing on every
/// <see cref="IFormatSectionExecutor.Execute"/>.
/// </summary>
/// <remarks>
/// This is what lets <see cref="MultiFormatScriptRunner.Compile"/> avoid re-parsing a
/// multi-megabyte script on every run — the cost that dominates a large script's execution time.
/// <see cref="ScriptEngineSectionExecutor{TNode}"/> implements it; an executor that does not is
/// still used through the plain <see cref="IFormatSectionExecutor"/> path, re-parsing each time.
/// </remarks>
public interface ICompilableFormatSectionExecutor : IFormatSectionExecutor
{
    /// <summary>
    /// Parse <paramref name="sectionScriptJson"/> once into an opaque handle to hand back to
    /// <see cref="ExecuteCompiled"/>. The handle's concrete type is the executor's own business.
    /// </summary>
    object Compile(string sectionScriptJson);

    /// <summary>
    /// Run a section using a handle produced by <see cref="Compile"/> against the same section's
    /// text, instead of parsing <paramref name="document"/>'s script text again.
    /// </summary>
    SectionExecutionResult ExecuteCompiled(object compiledSection, string document);
}
