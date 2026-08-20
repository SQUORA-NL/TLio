using System.Text.Json;
using FormatConverter.Core;
using FormatConverter.Core.Exceptions;

namespace FormatConverter.TLio;

/// <summary>
/// Orchestrates segmented script execution across multiple formats.
/// </summary>
/// <remarks>
/// <para>
/// The runner pre-processes a flat JSON command array, splits it at <c>convert</c> command boundaries
/// into <see cref="ScriptSection"/> objects, and executes each section using the registered
/// <see cref="IFormatSectionExecutor"/> for that format.  Format conversion at each boundary is
/// performed by the injected <see cref="FormatConverter.Core.FormatConverter"/>.
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
    /// <param name="scriptJson">JSON array of TLio commands, possibly containing <c>convert</c> commands.</param>
    /// <returns>The document after all sections have been executed, in the last section's format.</returns>
    /// <exception cref="FormatNotRegisteredException">When a <c>convert</c> command targets an unregistered format.</exception>
    public string Execute(string initialFormatId, string inputDocument, string scriptJson)
    {
        var sections = SplitIntoSections(initialFormatId, scriptJson);
        var currentDocument = inputDocument;
        var currentFormat = initialFormatId;

        for (var i = 0; i < sections.Count; i++)
        {
            var section = sections[i];

            // Apply incoming format conversion (all sections after the first)
            if (section.IncomingSettings is not null)
            {
                var prevSection = sections[i - 1];
                currentDocument = _converter.Convert(
                    prevSection.FormatId,
                    currentDocument,
                    section.FormatId,
                    section.IncomingSettings);
                currentFormat = section.FormatId;
            }

            // Execute section commands if any
            if (section.Commands.Count > 0)
            {
                if (_executors.TryGetValue(section.FormatId, out var executor))
                {
                    currentDocument = executor.Execute(section.ToScriptJson(), currentDocument);
                }
                // If no executor registered and there are commands, commands are skipped with no error
                // (conversion-only pipelines don't need executors)
            }
        }

        return currentDocument;
    }

    private static List<ScriptSection> SplitIntoSections(string initialFormatId, string scriptJson)
    {
        var sections = new List<ScriptSection>();
        var commands = new List<JsonElement>();
        var currentFormat = initialFormatId;
        ConversionSettings? pendingSettings = null;

        using var doc = JsonDocument.Parse(scriptJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("Script must be a JSON array.", nameof(scriptJson));

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            if (IsConvertCommand(element, out var to, out var settings))
            {
                // Flush current section
                sections.Add(new ScriptSection(currentFormat, commands.Select(CloneElement).ToList(), pendingSettings));
                commands = new List<JsonElement>();
                currentFormat = to;
                pendingSettings = settings;
            }
            else
            {
                commands.Add(CloneElement(element));
            }
        }

        // Flush last section
        sections.Add(new ScriptSection(currentFormat, commands.Select(CloneElement).ToList(), pendingSettings));

        return sections;
    }

    private static bool IsConvertCommand(JsonElement element, out string to, out ConversionSettings settings)
    {
        to = string.Empty;
        settings = ConversionSettings.Empty;

        if (element.ValueKind != JsonValueKind.Object) return false;
        if (!element.TryGetProperty("command", out var cmdProp)) return false;
        if (!string.Equals(cmdProp.GetString(), "convert", StringComparison.OrdinalIgnoreCase)) return false;

        to = element.TryGetProperty("to", out var toProp) ? toProp.GetString() ?? string.Empty : string.Empty;
        settings = ConvertSettingsReader.Read(element);
        return true;
    }

    private static JsonElement CloneElement(JsonElement element)
    {
        // Clone via round-trip through JsonDocument to own the memory
        var bytes = System.Text.Encoding.UTF8.GetBytes(element.GetRawText());
        return JsonDocument.Parse(bytes).RootElement;
    }
}
