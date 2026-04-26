using System.Text.Json;
using FormatConverter.Core;

namespace FormatConverter.TLio;

/// <summary>
/// One segment of a multi-format script between conversion boundaries.
/// </summary>
internal sealed class ScriptSection
{
    /// <summary>Format identifier governing this section's document type.</summary>
    public string FormatId { get; }

    /// <summary>Ordered JSON command objects for this section (no <c>convert</c> commands).</summary>
    public IList<JsonElement> Commands { get; }

    /// <summary>
    /// Settings from the preceding <c>convert</c> command.
    /// <see langword="null"/> for the first section.
    /// </summary>
    public ConversionSettings? IncomingSettings { get; }

    internal ScriptSection(string formatId, IList<JsonElement> commands, ConversionSettings? incomingSettings)
    {
        FormatId = formatId;
        Commands = commands;
        IncomingSettings = incomingSettings;
    }

    /// <summary>Serialise the section's commands back to a JSON array string.</summary>
    public string ToScriptJson()
    {
        using var stream = new System.IO.MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartArray();
        foreach (var cmd in Commands)
            cmd.WriteTo(writer);
        writer.WriteEndArray();
        writer.Flush();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}
