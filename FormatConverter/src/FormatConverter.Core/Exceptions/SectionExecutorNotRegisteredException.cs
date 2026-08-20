namespace FormatConverter.Core.Exceptions;

/// <summary>
/// Thrown when a section of a multi-format script has commands but no executor registered for
/// its format.
/// </summary>
/// <remarks>
/// The alternative — running the conversions and quietly skipping the commands — reports a
/// successful run of a script that did none of what it said.
/// </remarks>
public sealed class SectionExecutorNotRegisteredException : Exception
{
    /// <summary>The format whose section could not be executed.</summary>
    public string FormatId { get; }

    /// <summary>The format IDs that do have an executor.</summary>
    public IReadOnlyList<string> RegisteredFormats { get; }

    /// <summary>Initialises the exception for <paramref name="formatId"/>.</summary>
    public SectionExecutorNotRegisteredException(string formatId, IReadOnlyList<string> registeredFormats)
        : base($"The script has commands to run in '{formatId}', but no section executor is " +
               $"registered for it. Registered: {(registeredFormats.Count == 0 ? "none" : string.Join(", ", registeredFormats))}. " +
               $"Register one with MultiFormatScriptRunner.RegisterExecutor.")
    {
        FormatId = formatId;
        RegisteredFormats = registeredFormats;
    }
}
