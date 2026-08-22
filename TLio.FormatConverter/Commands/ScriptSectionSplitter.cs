using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using TLio.FormatConverter.Core;
using TLio.Core.Models;
using YamlDotNet.RepresentationModel;

namespace TLio.FormatConverter;

/// <summary>
/// Splits a script into the sections between its <c>convert</c> boundaries.
/// </summary>
/// <remarks>
/// <para>
/// A script can be written in JSON, XML or YAML, and <c>convert</c> is a command like any other
/// in all three. The splitter therefore reads the script in whatever notation it is written in
/// and hands each section back <b>in that same notation</b> — nothing is translated. That works
/// because the executor runs a section through <c>ScriptEngine.Execute</c>, which detects the
/// notation from the text; a section is a script in its own right.
/// </para>
/// <para>
/// Until this existed the runner parsed every script as JSON, so an XML or YAML script
/// containing <c>convert</c> was never recognised as crossing a boundary. It went to a plain
/// engine instead, where the command can only report that it converted nothing.
/// </para>
/// </remarks>
internal static class ScriptSectionSplitter
{
    private const string ConvertCommandName = "convert";

    /// <summary>
    /// Whether the script contains a <c>convert</c> command, and so needs the runner rather
    /// than a plain engine.
    /// </summary>
    /// <remarks>
    /// Text that does not parse as a script in its detected notation is not a multi-format
    /// script, and says so by returning false rather than throwing; whatever reads it next will
    /// report the real problem.
    /// </remarks>
    public static bool ContainsConvert(string? scriptText, ScriptFormat? notation = null)
    {
        if (string.IsNullOrWhiteSpace(scriptText)) return false;

        try
        {
            return (notation ?? ScriptFormatDetector.Detect(scriptText)) switch
            {
                ScriptFormat.Xml => XmlCommands(scriptText).Any(IsConvert),
                ScriptFormat.Yaml => YamlCommands(scriptText).Any(IsConvert),
                _ => JsonCommands(scriptText).Any(IsConvert),
            };
        }
        catch (Exception e) when (
            e is JsonException or System.Xml.XmlException or YamlDotNet.Core.YamlException ||
            e is ArgumentException)
        {
            // Unparsable text, and text that parses but is not a list of commands — a bare
            // scalar, an object, an XML document that is not a script — are both "not a
            // multi-format script". Saying so by returning false leaves the real complaint to
            // whatever reads the script next, which is the thing that knows what it wanted.
            return false;
        }
    }

    /// <summary>
    /// Split <paramref name="scriptText"/> at each <c>convert</c>, in the notation it is
    /// written in — or in <paramref name="notation"/> when the caller already knows it, which is
    /// how an explicitly declared notation beats detection. The first section runs in <paramref name="initialFormatId"/>; each later one
    /// runs in the format the preceding <c>convert</c> named, with that command's settings
    /// applied at the boundary.
    /// </summary>
    public static List<ScriptSection> Split(
        string initialFormatId, string scriptText, ScriptFormat? notation = null) =>
        (notation ?? ScriptFormatDetector.Detect(scriptText)) switch
        {
            ScriptFormat.Xml => Split(initialFormatId, XmlCommands(scriptText), XmlSectionWriter(scriptText)),
            ScriptFormat.Yaml => Split(initialFormatId, YamlCommands(scriptText), YamlSectionWriter),
            _ => Split(initialFormatId, JsonCommands(scriptText), JsonSectionWriter),
        };

    /// <summary>
    /// One command as the splitter needs to see it: is it a <c>convert</c>, where does it point,
    /// and the node itself so a section can be written back out unchanged.
    /// </summary>
    private sealed record Command(bool IsConvert, string To, ConversionSettings Settings, object Node);

    private static bool IsConvert(Command c) => c.IsConvert;

    private static List<ScriptSection> Split(
        string initialFormatId,
        IEnumerable<Command> commands,
        Func<IReadOnlyList<object>, string> writeSection)
    {
        var sections = new List<ScriptSection>();
        var buffer = new List<object>();
        var currentFormat = initialFormatId;
        ConversionSettings? pendingSettings = null;

        foreach (var command in commands)
        {
            if (command.IsConvert)
            {
                sections.Add(new ScriptSection(currentFormat, writeSection(buffer), buffer.Count, pendingSettings));
                buffer = new List<object>();
                currentFormat = command.To;
                pendingSettings = command.Settings;
            }
            else
            {
                buffer.Add(command.Node);
            }
        }

        sections.Add(new ScriptSection(currentFormat, writeSection(buffer), buffer.Count, pendingSettings));
        return sections;
    }

    // ── JSON ─────────────────────────────────────────────────────────────────

    private static IEnumerable<Command> JsonCommands(string scriptText)
    {
        using var doc = JsonDocument.Parse(scriptText);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("Script must be a JSON array.", nameof(scriptText));

        // Cloned because the JsonDocument is disposed with this method's scope.
        return doc.RootElement.EnumerateArray().Select(CloneElement).Select(JsonCommand).ToList();
    }

    private static Command JsonCommand(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("command", out var name) ||
            !string.Equals(name.GetString(), ConvertCommandName, StringComparison.OrdinalIgnoreCase))
            return new Command(false, string.Empty, ConversionSettings.Empty, element);

        var to = element.TryGetProperty("to", out var toProp) ? toProp.GetString() ?? string.Empty : string.Empty;
        return new Command(true, to, ConvertSettingsReader.Read(element), element);
    }

    private static string JsonSectionWriter(IReadOnlyList<object> commands)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartArray();
        foreach (JsonElement command in commands)
            command.WriteTo(writer);
        writer.WriteEndArray();
        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static JsonElement CloneElement(JsonElement element) =>
        JsonDocument.Parse(Encoding.UTF8.GetBytes(element.GetRawText())).RootElement;

    // ── XML ──────────────────────────────────────────────────────────────────

    private static IEnumerable<Command> XmlCommands(string scriptText)
    {
        var root = XDocument.Parse(scriptText).Root
                   ?? throw new ArgumentException("Script has no root element.", nameof(scriptText));

        return root.Elements().Select(XmlCommand).ToList();
    }

    private static Command XmlCommand(XElement element)
    {
        if (!string.Equals(element.Name.LocalName, ConvertCommandName, StringComparison.OrdinalIgnoreCase))
            return new Command(false, string.Empty, ConversionSettings.Empty, element);

        // The XML notation writes a scalar property as an attribute; a child element is the
        // form for what an attribute cannot hold. `to` is a scalar, so the attribute is the
        // spelling — the child is accepted because it costs nothing to read both.
        var to = Attribute(element, "to")
                 ?? element.Elements().FirstOrDefault(e => NameIs(e, "to"))?.Value
                 ?? string.Empty;

        var settings = element.Elements().FirstOrDefault(e => NameIs(e, "settings"));
        return new Command(true, to, ReadXmlSettings(settings), element);
    }

    /// <summary>Attribute lookup that ignores case, as the XML notation's property binding does.</summary>
    private static string? Attribute(XElement element, string name) =>
        element.Attributes()
            .FirstOrDefault(a => string.Equals(a.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))
            ?.Value;

    private static bool NameIs(XElement element, string name) =>
        string.Equals(element.Name.LocalName, name, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// <c>&lt;settings&gt;&lt;inferTypes&gt;true&lt;/inferTypes&gt;&lt;/settings&gt;</c> — a
    /// flat block of scalars, which is all a settings block ever is.
    /// </summary>
    private static ConversionSettings ReadXmlSettings(XElement? settings)
    {
        if (settings is null) return ConversionSettings.Empty;

        return ConvertSettingsReader.Read(name =>
            settings.Elements().FirstOrDefault(e => NameIs(e, name))?.Value);
    }

    /// <summary>
    /// Writes a section back as a script in the same container element the original used, so a
    /// section is the script it would have been if written on its own.
    /// </summary>
    private static Func<IReadOnlyList<object>, string> XmlSectionWriter(string scriptText)
    {
        var rootName = XDocument.Parse(scriptText).Root!.Name;

        return commands =>
            new XElement(rootName, commands.Cast<XElement>().Select(e => new XElement(e))).ToString();
    }

    // ── YAML ─────────────────────────────────────────────────────────────────

    private static IEnumerable<Command> YamlCommands(string scriptText)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(scriptText));

        if (stream.Documents.Count == 0) return Array.Empty<Command>();
        if (stream.Documents[0].RootNode is not YamlSequenceNode sequence)
            throw new ArgumentException("Script must be a YAML sequence of commands.", nameof(scriptText));

        return sequence.Children.Select(YamlCommand).ToList();
    }

    private static Command YamlCommand(YamlNode node)
    {
        if (node is not YamlMappingNode mapping ||
            YamlScalar(mapping, "command") is not { } name ||
            !string.Equals(name, ConvertCommandName, StringComparison.OrdinalIgnoreCase))
            return new Command(false, string.Empty, ConversionSettings.Empty, node);

        var settings = YamlChild(mapping, "settings") as YamlMappingNode;
        return new Command(true, YamlScalar(mapping, "to") ?? string.Empty, ReadYamlSettings(settings), node);
    }

    private static ConversionSettings ReadYamlSettings(YamlMappingNode? settings) =>
        settings is null
            ? ConversionSettings.Empty
            : ConvertSettingsReader.Read(name => YamlScalar(settings, name));

    private static YamlNode? YamlChild(YamlMappingNode mapping, string key) =>
        mapping.Children.FirstOrDefault(p =>
            p.Key is YamlScalarNode k &&
            string.Equals(k.Value, key, StringComparison.OrdinalIgnoreCase)).Value;

    private static string? YamlScalar(YamlMappingNode mapping, string key) =>
        YamlChild(mapping, key) is YamlScalarNode scalar ? scalar.Value : null;

    private static string YamlSectionWriter(IReadOnlyList<object> commands)
    {
        var sequence = new YamlSequenceNode(commands.Cast<YamlNode>());
        var stream = new YamlStream(new YamlDocument(sequence));

        using var writer = new StringWriter();
        // assignAnchors: false — a section is script text a human may read, and the
        // anchors YamlDotNet would invent for repeated nodes are noise in one.
        stream.Save(writer, assignAnchors: false);
        return writer.ToString();
    }
}
