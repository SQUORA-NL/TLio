using System.Text;
using FormatConverter.Core;
using FormatConverter.Core.Exceptions;
using FormatConverter.Core.Model;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace FormatConverter.Yaml;

/// <summary>
/// Bidirectional YAML adapter using YamlDotNet (MIT licence).
/// Format ID: <c>"yaml"</c> (case-insensitive).
/// </summary>
/// <remarks>
/// When <see cref="ConversionSettings.FlattenAnchors"/> is <see langword="true"/> (default),
/// alias nodes are dereferenced inline and the result is marked with
/// <c>Metadata["#anchor-flattened"] = "true"</c>.
/// </remarks>
public sealed class YamlFormatAdapter : IFormatAdapter
{
    /// <inheritdoc/>
    public string FormatId => "yaml";

    /// <inheritdoc/>
    public IntermediateNode ToIM(string source, ConversionSettings settings)
    {
        try
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(source));

            if (stream.Documents.Count == 0)
                return new ObjectNode { Name = null };

            var root = stream.Documents[0].RootNode;
            return ConvertYamlNode(root, null, settings);
        }
        catch (YamlException ex)
        {
            throw new FormatParseException(FormatId, "ToIM", ex.Message, ex);
        }
    }

    /// <inheritdoc/>
    public string FromIM(IntermediateNode root, ConversionSettings settings)
    {
        try
        {
            var sb = new StringBuilder();
            using var writer = new StringWriter(sb);
            WriteNode(writer, root, 0, isRoot: true);
            return sb.ToString();
        }
        catch (Exception ex) when (ex is not FormatParseException)
        {
            throw new FormatParseException(FormatId, "FromIM", ex.Message, ex);
        }
    }

    private static IntermediateNode ConvertYamlNode(YamlNode node, string? name, ConversionSettings settings) =>
        node switch
        {
            YamlMappingNode mapping => ConvertMapping(mapping, name, settings),
            YamlSequenceNode sequence => ConvertSequence(sequence, name, settings),
            YamlScalarNode scalar => ConvertScalar(scalar, name, settings),
            _ => new ScalarNode(ScalarType.String, node.ToString()) { Name = name },
        };

    private static ObjectNode ConvertMapping(YamlMappingNode mapping, string? name, ConversionSettings settings)
    {
        var obj = new ObjectNode { Name = name };
        if (settings.FlattenAnchors && !mapping.Anchor.IsEmpty)
            obj.Metadata[NodeMetadata.AnchorFlattenedKey] = "true";

        foreach (var entry in mapping.Children)
        {
            var key = ((YamlScalarNode)entry.Key).Value ?? string.Empty;
            obj.Children.Add(ConvertYamlNode(entry.Value, key, settings));
        }
        return obj;
    }

    private static ArrayNode ConvertSequence(YamlSequenceNode sequence, string? name, ConversionSettings settings)
    {
        var arr = new ArrayNode { Name = name };
        if (settings.FlattenAnchors && !sequence.Anchor.IsEmpty)
            arr.Metadata[NodeMetadata.AnchorFlattenedKey] = "true";

        foreach (var item in sequence.Children)
            arr.Items.Add(ConvertYamlNode(item, null, settings));
        return arr;
    }

    private static ScalarNode ConvertScalar(YamlScalarNode scalar, string? name, ConversionSettings settings)
    {
        var raw = scalar.Value ?? string.Empty;
        ScalarType type;

        if (settings.InferTypes)
            type = InferType(raw, scalar.Tag.IsEmpty ? null : scalar.Tag.Value);
        else
            type = ScalarType.String;

        var result = new ScalarNode(type, type == ScalarType.Null ? null : raw) { Name = name };
        if (settings.FlattenAnchors && !scalar.Anchor.IsEmpty)
            result.Metadata[NodeMetadata.AnchorFlattenedKey] = "true";
        return result;
    }

    private static ScalarType InferType(string value, string? tag)
    {
        if (tag == "tag:yaml.org,2002:int" || long.TryParse(value, out _)) return ScalarType.Integer;
        if (tag == "tag:yaml.org,2002:float" || double.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out _)) return ScalarType.Decimal;
        if (tag == "tag:yaml.org,2002:bool" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)) return ScalarType.Boolean;
        if (tag == "tag:yaml.org,2002:null" || value is "~" or ""
            || string.Equals(value, "null", StringComparison.OrdinalIgnoreCase))
            return ScalarType.Null;
        return ScalarType.String;
    }

    // ── FromIM: simple YAML text writer ─────────────────────────────────────

    private static void WriteNode(TextWriter writer, IntermediateNode node, int indent, bool isRoot)
    {
        var pad = new string(' ', indent * 2);

        switch (node)
        {
            case ObjectNode obj when isRoot && obj.Name is not null:
                // Wrap named root so the element name survives a YAML round-trip
                writer.WriteLine($"{YamlKey(obj.Name)}:");
                foreach (var child in obj.Children)
                {
                    writer.Write($"  {YamlKey(child.Name ?? "item")}: ");
                    WriteNode(writer, child, 1, isRoot: false);
                }
                break;

            case ObjectNode obj:
                if (!isRoot && obj.Children.Count > 0)
                    writer.WriteLine();
                foreach (var child in obj.Children)
                {
                    writer.Write($"{pad}{YamlKey(child.Name ?? "item")}: ");
                    WriteNode(writer, child, indent + 1, isRoot: false);
                }
                break;

            case ArrayNode arr:
                if (!isRoot)
                    writer.WriteLine();
                foreach (var item in arr.Items)
                {
                    writer.Write($"{pad}- ");
                    WriteArrayItem(writer, item, indent + 1);
                }
                break;

            case ScalarNode scalar:
                writer.WriteLine(FormatScalar(scalar));
                break;

            case MixedContentNode mixed:
                var text = string.Concat(mixed.Content.Select(c => c switch
                {
                    TextRun tr => tr.Text,
                    ChildNode cn => ExtractText(cn.Node),
                    _ => string.Empty,
                }));
                writer.WriteLine(QuoteYamlString(text));
                break;
        }
    }

    private static void WriteArrayItem(TextWriter writer, IntermediateNode item, int indent)
    {
        var pad = new string(' ', indent * 2);
        switch (item)
        {
            case ObjectNode obj:
                var first = true;
                foreach (var child in obj.Children)
                {
                    if (first) { first = false; writer.Write($"{YamlKey(child.Name ?? "item")}: "); WriteNode(writer, child, indent + 1, isRoot: false); }
                    else { writer.Write($"{pad}  {YamlKey(child.Name ?? "item")}: "); WriteNode(writer, child, indent + 1, isRoot: false); }
                }
                if (obj.Children.Count == 0) writer.WriteLine("{}");
                break;

            case ScalarNode scalar:
                writer.WriteLine(FormatScalar(scalar));
                break;

            default:
                WriteNode(writer, item, indent, isRoot: false);
                break;
        }
    }

    private static string FormatScalar(ScalarNode scalar) => scalar.Type switch
    {
        ScalarType.Null => "null",
        ScalarType.Boolean => scalar.RawValue!,
        ScalarType.Integer => scalar.RawValue!,
        ScalarType.Decimal => scalar.RawValue!,
        _ => QuoteYamlString(scalar.RawValue ?? string.Empty),
    };

    private static string QuoteYamlString(string value)
    {
        if (NeedsQuoting(value))
            return $"'{value.Replace("'", "''")}'";
        return value;
    }

    private static bool NeedsQuoting(string value)
    {
        if (string.IsNullOrEmpty(value)) return true;
        if (value.Contains(':') || value.Contains('#') || value.Contains('\'') ||
            value.Contains('"') || value.Contains('\n') || value.Contains('\r') ||
            value.StartsWith(' ') || value.EndsWith(' ')) return true;
        if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "null", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "~")) return true;
        if (long.TryParse(value, out _) || double.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out _)) return true;
        return false;
    }

    private static string YamlKey(string key) => NeedsQuoting(key) ? $"'{key}'" : key;

    private static string ExtractText(IntermediateNode node) => node switch
    {
        ScalarNode s => s.RawValue ?? string.Empty,
        MixedContentNode m => string.Concat(m.Content.Select(c => c switch
        {
            TextRun tr => tr.Text,
            ChildNode cn => ExtractText(cn.Node),
            _ => string.Empty,
        })),
        _ => string.Empty,
    };
}
