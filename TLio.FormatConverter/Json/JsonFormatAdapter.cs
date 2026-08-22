using System.Text;
using System.Text.Json;
using TLio.FormatConverter.Core;
using TLio.FormatConverter.Core.Exceptions;
using TLio.FormatConverter.Core.Model;

namespace TLio.FormatConverter.Json;

/// <summary>
/// Bidirectional JSON adapter using <c>System.Text.Json</c> only — no Newtonsoft dependency.
/// Format ID: <c>"json"</c> (case-insensitive).
/// </summary>
/// <remarks>
/// Conventions:
/// <list type="bullet">
///   <item>JSON object → <see cref="ObjectNode"/></item>
///   <item>JSON array → <see cref="ArrayNode"/></item>
///   <item>JSON string → <see cref="ScalarNode"/>(<see cref="ScalarType.String"/>)</item>
///   <item>JSON number (integer) → <see cref="ScalarNode"/>(<see cref="ScalarType.Integer"/>)</item>
///   <item>JSON number (decimal) → <see cref="ScalarNode"/>(<see cref="ScalarType.Decimal"/>)</item>
///   <item>JSON true/false → <see cref="ScalarNode"/>(<see cref="ScalarType.Boolean"/>)</item>
///   <item>JSON null → <see cref="ScalarNode"/>(<see cref="ScalarType.Null"/>)</item>
///   <item>Properties prefixed with <see cref="ConversionSettings.AttributePrefix"/> (default <c>@</c>) are
///   stored as <see cref="NodeMetadata"/> entries rather than child nodes.</item>
/// </list>
/// When <see cref="FromIM"/> receives a named root node, it wraps the output in
/// <c>{"Name":{content}}</c> so that the element name survives a JSON round-trip.
/// </remarks>
public sealed class JsonFormatAdapter : IFormatAdapter
{
    /// <inheritdoc/>
    public string FormatId => "json";

    /// <inheritdoc/>
    public IntermediateNode ToIM(string source, ConversionSettings settings)
    {
        try
        {
            using var doc = JsonDocument.Parse(source);
            return ConvertElement(doc.RootElement, null, settings);
        }
        catch (JsonException ex)
        {
            throw new FormatParseException(FormatId, "ToIM", ex.Message, ex);
        }
    }

    /// <inheritdoc/>
    public string FromIM(IntermediateNode root, ConversionSettings settings)
    {
        try
        {
            var buffer = new MemoryStream();
            using var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false });
            WriteNode(writer, root, settings, isRoot: true);
            writer.Flush();
            return Encoding.UTF8.GetString(buffer.ToArray());
        }
        catch (Exception ex) when (ex is not FormatParseException)
        {
            throw new FormatParseException(FormatId, "FromIM", ex.Message, ex);
        }
    }

    private static IntermediateNode ConvertElement(JsonElement element, string? name, ConversionSettings settings)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => ConvertObject(element, name, settings),
            JsonValueKind.Array => ConvertArray(element, name, settings),
            JsonValueKind.String => new ScalarNode(ScalarType.String, element.GetString()!) { Name = name },
            JsonValueKind.Number => ConvertNumber(element, name),
            JsonValueKind.True => new ScalarNode(ScalarType.Boolean, "true") { Name = name },
            JsonValueKind.False => new ScalarNode(ScalarType.Boolean, "false") { Name = name },
            JsonValueKind.Null => new ScalarNode(ScalarType.Null, null) { Name = name },
            _ => new ScalarNode(ScalarType.String, element.GetRawText()) { Name = name },
        };
    }

    private static IntermediateNode ConvertObject(JsonElement element, string? name, ConversionSettings settings)
    {
        var node = new ObjectNode { Name = name };
        foreach (var prop in element.EnumerateObject())
        {
            if (MetadataConvention.IsMetadataKey(prop.Name, settings) && prop.Value.ValueKind == JsonValueKind.String)
                node.Metadata[prop.Name] = prop.Value.GetString()!;
            else
                node.Children.Add(ConvertElement(prop.Value, prop.Name, settings));
        }

        return Unwrap(node, settings);
    }

    /// <summary>
    /// Fold <c>{"@currency": "EUR", "#text": "9.99"}</c> back into the scalar with attributes that
    /// it was on the way out, so an XML element with both survives a JSON round trip.
    /// </summary>
    private static IntermediateNode Unwrap(ObjectNode node, ConversionSettings settings)
    {
        if (!MetadataConvention.IsTextWrapper(node.Children.Select(c => c.Name ?? string.Empty), settings))
            return node;
        if (node.Children[0] is not ScalarNode text) return node;

        var scalar = new ScalarNode(text.Type, text.RawValue) { Name = node.Name };
        foreach (var entry in node.Metadata)
            scalar.Metadata[entry.Key] = entry.Value;
        return scalar;
    }

    private static ArrayNode ConvertArray(JsonElement element, string? name, ConversionSettings settings)
    {
        var node = new ArrayNode { Name = name };
        foreach (var item in element.EnumerateArray())
            node.Items.Add(ConvertElement(item, null, settings));
        return node;
    }

    private static ScalarNode ConvertNumber(JsonElement element, string? name)
    {
        var raw = element.GetRawText();
        if (raw.Contains('.') || raw.Contains('e') || raw.Contains('E'))
            return new ScalarNode(ScalarType.Decimal, raw) { Name = name };
        return new ScalarNode(ScalarType.Integer, raw) { Name = name };
    }

    private static void WriteNode(Utf8JsonWriter writer, IntermediateNode node, ConversionSettings settings, bool isRoot = false)
    {
        // A named root is wrapped as {"name": …} so the XML element name survives the round trip.
        // This holds for a scalar root too: <price>9.99</price> is {"price":"9.99"}, not "9.99".
        if (isRoot && node.Name is not null)
        {
            writer.WriteStartObject();
            writer.WritePropertyName(node.Name);
            WriteNode(writer, node, settings);
            writer.WriteEndObject();
            return;
        }

        switch (node)
        {
            case ObjectNode obj:
                WriteObjectContent(writer, obj, settings);
                break;

            case ArrayNode arr:
                writer.WriteStartArray();
                foreach (var item in arr.Items)
                    WriteNode(writer, item, settings);
                writer.WriteEndArray();
                break;

            case ScalarNode scalar when MetadataConvention.NeedsTextWrapper(scalar, settings):
                // Attributes have nowhere to live on a bare value, so the value takes a key of
                // its own beside them.
                writer.WriteStartObject();
                foreach (var entry in MetadataConvention.Emittable(scalar, settings))
                    writer.WriteString(entry.Key, entry.Value);
                if (MetadataConvention.WrapperCarriesText(scalar))
                {
                    writer.WritePropertyName(settings.TextProperty);
                    WriteScalar(writer, scalar);
                }
                writer.WriteEndObject();
                break;

            case ScalarNode scalar:
                WriteScalar(writer, scalar);
                break;

            case MixedContentNode mixed:
                var text = string.Concat(mixed.Content.Select(c => c switch
                {
                    TextRun tr => tr.Text,
                    ChildNode cn => ExtractText(cn.Node),
                    _ => string.Empty,
                }));
                writer.WriteStringValue(text);
                break;

            default:
                writer.WriteNullValue();
                break;
        }
    }

    private static void WriteObjectContent(Utf8JsonWriter writer, ObjectNode obj, ConversionSettings settings)
    {
        writer.WriteStartObject();
        foreach (var entry in MetadataConvention.Emittable(obj, settings))
            writer.WriteString(entry.Key, entry.Value);
        foreach (var child in obj.Children)
        {
            writer.WritePropertyName(child.Name ?? string.Empty);
            WriteNode(writer, child, settings);
        }
        writer.WriteEndObject();
    }

    private static void WriteScalar(Utf8JsonWriter writer, ScalarNode scalar)
    {
        switch (scalar.Type)
        {
            case ScalarType.Null:
                writer.WriteNullValue();
                break;
            case ScalarType.Boolean:
                writer.WriteBooleanValue(scalar.RawValue == "true");
                break;
            case ScalarType.Integer:
                if (long.TryParse(scalar.RawValue, out var lng))
                    writer.WriteNumberValue(lng);
                else
                    writer.WriteStringValue(scalar.RawValue);
                break;
            case ScalarType.Decimal:
                if (double.TryParse(scalar.RawValue, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var dbl))
                    writer.WriteNumberValue(dbl);
                else
                    writer.WriteStringValue(scalar.RawValue);
                break;
            default:
                writer.WriteStringValue(scalar.RawValue);
                break;
        }
    }

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
