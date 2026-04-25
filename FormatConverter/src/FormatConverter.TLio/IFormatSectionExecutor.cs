namespace FormatConverter.TLio;

/// <summary>
/// Executes a section of a multi-format TLio script in a specific format.
/// Each registered executor handles one format (e.g. JSON, XML, YAML).
/// </summary>
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
    /// <returns>The document after all section commands have been applied, still in this format.</returns>
    string Execute(string sectionScriptJson, string document);
}
