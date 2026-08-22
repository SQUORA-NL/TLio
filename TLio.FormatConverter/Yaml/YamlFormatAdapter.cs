using TLio.FormatConverter.Core;
using TLio.FormatConverter.Core.Exceptions;
using TLio.FormatConverter.Core.Model;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace TLio.FormatConverter.Yaml;

/// <summary>
/// Bidirectional YAML adapter using YamlDotNet (MIT licence).
/// Format ID: <c>"yaml"</c> (case-insensitive).
/// </summary>
/// <remarks>
/// <para>
/// Output is produced by YamlDotNet's own emitter rather than by hand: the IM is projected onto
/// a <see cref="YamlNode"/> tree and serialised. Hand-written indentation is what previously made
/// any array of objects with more than one key emit YAML that YAML could not read back.
/// </para>
/// <para>
/// Keys beginning with <see cref="ConversionSettings.AttributePrefix"/> (default <c>@</c>) are
/// carried as <see cref="NodeMetadata"/> rather than children, mirroring the JSON adapter, so
/// XML attributes survive a trip through YAML.
/// </para>
/// <para>
/// When <see cref="ConversionSettings.FlattenAnchors"/> is <see langword="true"/> (default),
/// alias nodes are dereferenced inline and the result is marked with
/// <c>Metadata["#anchor-flattened"] = "true"</c>.
/// </para>
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
            var document = new YamlDocument(BuildYaml(root, settings, isRoot: true));
            var stream = new YamlStream(document);

            var writer = new StringWriter();
            stream.Save(writer, assignAnchors: false);

            return Normalise(writer.ToString());
        }
        catch (Exception ex) when (ex is not FormatParseException)
        {
            throw new FormatParseException(FormatId, "FromIM", ex.Message, ex);
        }
    }

    /// <summary>
    /// Strip the explicit document-end marker the emitter appends. A converted document is a
    /// value, not a stream, and <c>...</c> on the end is noise every consumer has to tolerate.
    /// </summary>
    private static string Normalise(string yaml)
    {
        var text = yaml.Replace("\r\n", "\n").TrimEnd('\n');
        if (text.EndsWith("\n...", StringComparison.Ordinal))
            text = text[..^4];
        else if (text == "...")
            text = string.Empty;
        return text.Length == 0 ? text : text + "\n";
    }

    // ── ToIM ─────────────────────────────────────────────────────────────────

    private static IntermediateNode ConvertYamlNode(YamlNode node, string? name, ConversionSettings settings) =>
        node switch
        {
            YamlMappingNode mapping => ConvertMapping(mapping, name, settings),
            YamlSequenceNode sequence => ConvertSequence(sequence, name, settings),
            YamlScalarNode scalar => ConvertScalar(scalar, name, settings),
            _ => new ScalarNode(ScalarType.String, node.ToString()) { Name = name },
        };

    private static IntermediateNode ConvertMapping(YamlMappingNode mapping, string? name, ConversionSettings settings)
    {
        var obj = new ObjectNode { Name = name };
        if (settings.FlattenAnchors && !mapping.Anchor.IsEmpty)
            obj.Metadata[NodeMetadata.AnchorFlattenedKey] = "true";

        foreach (var entry in mapping.Children)
        {
            var key = ((YamlScalarNode)entry.Key).Value ?? string.Empty;

            if (MetadataConvention.IsMetadataKey(key, settings) && entry.Value is YamlScalarNode metaScalar)
                obj.Metadata[key] = metaScalar.Value ?? string.Empty;
            else
                obj.Children.Add(ConvertYamlNode(entry.Value, key, settings));
        }

        return Unwrap(obj, settings);
    }

    /// <summary>
    /// Fold a metadata-plus-text mapping back into the scalar with attributes it came from, so an
    /// XML element carrying both survives a YAML round trip.
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
        var tag = scalar.Tag.IsEmpty ? null : scalar.Tag.Value;

        // A quoted scalar is a string in YAML whatever it spells, so inference must not touch it.
        // This is what lets a converter round-trip the string "42" without it becoming a number.
        var type = settings.InferTypes && !IsExplicitString(scalar, tag)
            ? InferType(raw, tag)
            : ScalarType.String;

        var result = new ScalarNode(type, type == ScalarType.Null ? null : raw) { Name = name };
        if (settings.FlattenAnchors && !scalar.Anchor.IsEmpty)
            result.Metadata[NodeMetadata.AnchorFlattenedKey] = "true";
        return result;
    }

    private static bool IsExplicitString(YamlScalarNode scalar, string? tag) =>
        tag == "tag:yaml.org,2002:str" ||
        scalar.Style is ScalarStyle.SingleQuoted or ScalarStyle.DoubleQuoted
                     or ScalarStyle.Literal or ScalarStyle.Folded;

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

    // ── FromIM ───────────────────────────────────────────────────────────────

    private static YamlNode BuildYaml(IntermediateNode node, ConversionSettings settings, bool isRoot)
    {
        // A named root is wrapped as {name: content} so the element name survives the round trip,
        // matching what the JSON adapter does. This holds for a scalar root too: <price>9.99</price>
        // is `price: '9.99'`, not a bare `'9.99'`.
        if (isRoot && node.Name is not null)
        {
            var wrapper = new YamlMappingNode();
            wrapper.Add(Key(node.Name), BuildYaml(node, settings, isRoot: false));
            return wrapper;
        }

        return node switch
        {
            ObjectNode obj => BuildMapping(obj, settings),
            ArrayNode arr => BuildSequence(arr, settings),
            ScalarNode scalar when MetadataConvention.NeedsTextWrapper(scalar, settings) =>
                BuildTextWrapper(scalar, settings),
            ScalarNode scalar => BuildScalar(scalar),
            MixedContentNode mixed => Scalar(CollapseText(mixed)),
            _ => new YamlScalarNode("null") { Style = ScalarStyle.Plain },
        };
    }

    /// <summary>
    /// Attributes have nowhere to live on a bare value, so the value takes a key of its own
    /// beside them: <c>{'@currency': EUR, '#text': '9.99'}</c>.
    /// </summary>
    private static YamlMappingNode BuildTextWrapper(ScalarNode scalar, ConversionSettings settings)
    {
        var mapping = new YamlMappingNode();
        foreach (var (key, value) in MetadataConvention.Emittable(scalar, settings))
            mapping.Add(Key(key), Scalar(value));
        if (MetadataConvention.WrapperCarriesText(scalar))
            mapping.Add(Key(settings.TextProperty), BuildScalar(scalar));
        return mapping;
    }

    private static YamlMappingNode BuildMapping(ObjectNode obj, ConversionSettings settings)
    {
        var mapping = new YamlMappingNode();
        foreach (var (key, value) in MetadataEntries(obj, settings))
            mapping.Add(Key(key), Scalar(value));
        foreach (var child in obj.Children)
            mapping.Add(Key(child.Name ?? "item"), BuildYaml(child, settings, isRoot: false));
        return mapping;
    }

    private static YamlNode BuildSequence(ArrayNode arr, ConversionSettings settings)
    {
        var sequence = new YamlSequenceNode();
        foreach (var item in arr.Items)
            sequence.Add(BuildYaml(item, settings, isRoot: false));

        // Metadata on an array has nowhere to live in a sequence; wrap so it is not dropped.
        var metadata = MetadataEntries(arr, settings).ToList();
        if (metadata.Count == 0)
            return sequence;

        var mapping = new YamlMappingNode();
        foreach (var (key, value) in metadata)
            mapping.Add(Key(key), Scalar(value));
        mapping.Add(Key(arr.Name ?? "item"), sequence);
        return mapping;
    }

    private static IEnumerable<KeyValuePair<string, string>> MetadataEntries(
        IntermediateNode node, ConversionSettings settings) =>
        MetadataConvention.Emittable(node, settings);

    private static YamlNode BuildScalar(ScalarNode scalar) => scalar.Type switch
    {
        ScalarType.Null => new YamlScalarNode("null") { Style = ScalarStyle.Plain },
        ScalarType.Boolean or ScalarType.Integer or ScalarType.Decimal =>
            new YamlScalarNode(scalar.RawValue!) { Style = ScalarStyle.Plain },
        _ => Scalar(scalar.RawValue ?? string.Empty),
    };

    /// <summary>
    /// A string scalar. Anything that would read back as a number, a boolean, null or a
    /// structural token is single-quoted so the value survives <c>inferTypes</c> on the way in.
    /// </summary>
    private static YamlScalarNode Scalar(string value) =>
        new(value) { Style = NeedsQuoting(value) ? ScalarStyle.SingleQuoted : ScalarStyle.Any };

    private static YamlScalarNode Key(string key) =>
        new(key) { Style = NeedsQuoting(key) ? ScalarStyle.SingleQuoted : ScalarStyle.Any };

    private static bool NeedsQuoting(string value)
    {
        if (string.IsNullOrEmpty(value)) return true;
        if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "null", StringComparison.OrdinalIgnoreCase) ||
            value == "~") return true;
        if (long.TryParse(value, out _) || double.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out _)) return true;
        return false;
    }

    private static string CollapseText(MixedContentNode mixed) =>
        string.Concat(mixed.Content.Select(c => c switch
        {
            TextRun tr => tr.Text,
            ChildNode cn => ExtractText(cn.Node),
            _ => string.Empty,
        }));

    private static string ExtractText(IntermediateNode node) => node switch
    {
        ScalarNode s => s.RawValue ?? string.Empty,
        MixedContentNode m => CollapseText(m),
        _ => string.Empty,
    };
}
