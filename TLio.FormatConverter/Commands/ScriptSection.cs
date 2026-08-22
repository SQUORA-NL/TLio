using TLio.FormatConverter.Core;

namespace TLio.FormatConverter;

/// <summary>
/// One segment of a multi-format script between conversion boundaries.
/// </summary>
/// <remarks>
/// The section carries its commands as <b>script text in the notation they were written in</b>,
/// not as a parsed command list. A section is a script in its own right, and the executor runs it
/// through an engine that detects the notation from the text — so an XML script yields XML
/// sections and nothing has to be translated across notations to be split.
/// </remarks>
internal sealed class ScriptSection
{
    /// <summary>Format identifier governing this section's document type.</summary>
    public string FormatId { get; }

    /// <summary>
    /// This section's commands as a runnable script, in the original notation and with the
    /// <c>convert</c> commands removed.
    /// </summary>
    public string ScriptText { get; }

    /// <summary>
    /// How many commands the section holds. A section with none is a boundary that converts and
    /// passes the document straight through — it needs no executor, and asking for one that is
    /// not registered would fail a run that has nothing to run.
    /// </summary>
    public int CommandCount { get; }

    /// <summary>
    /// Settings from the preceding <c>convert</c> command.
    /// <see langword="null"/> for the first section.
    /// </summary>
    public ConversionSettings? IncomingSettings { get; }

    internal ScriptSection(string formatId, string scriptText, int commandCount, ConversionSettings? incomingSettings)
    {
        FormatId = formatId;
        ScriptText = scriptText;
        CommandCount = commandCount;
        IncomingSettings = incomingSettings;
    }
}
