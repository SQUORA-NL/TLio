using System.Xml;
using FormatConverter.Core;
using FormatConverter.Core.Exceptions;
using FormatConverter.Core.Model;

namespace FormatConverter.Xml;

/// <summary>
/// Bidirectional XML adapter using <c>System.Xml</c> only — no Newtonsoft dependency.
/// Format ID: <c>"xml"</c> (case-insensitive).
/// </summary>
/// <remarks>
/// Attribute names are stored in <see cref="NodeMetadata"/> as <c>@{name}</c>.
/// Namespace declarations are stored as <c>xmlns:{prefix}</c> or <c>xmlns</c>.
/// CDATA sections produce a <c>#cdata = "true"</c> metadata key unless <see cref="ConversionSettings.CdataAsText"/> is set.
/// </remarks>
public sealed class XmlFormatAdapter : IFormatAdapter
{
    /// <inheritdoc/>
    public string FormatId => "xml";

    /// <inheritdoc/>
    public IntermediateNode ToIM(string source, ConversionSettings settings)
    {
        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(source);
            var root = doc.DocumentElement!;
            return ConvertElement(root, root.Name, settings);
        }
        catch (XmlException ex)
        {
            throw new FormatParseException(FormatId, "ToIM", ex.Message, ex);
        }
    }

    /// <inheritdoc/>
    public string FromIM(IntermediateNode root, ConversionSettings settings)
    {
        try
        {
            var doc = new XmlDocument();
            // If the root is an unnamed container with a single named child and no metadata,
            // use the child directly as the XML root element (preserves element names across JSON/YAML).
            var actualRoot = root is ObjectNode { Name: null } u && u.Children.Count == 1 && u.Metadata.Count == 0
                ? u.Children[0]
                : root;
            var element = BuildElement(doc, actualRoot, settings, null);
            doc.AppendChild(element);

            var sb = new System.Text.StringBuilder();
            var writerSettings = new XmlWriterSettings { OmitXmlDeclaration = true, Indent = false };
            using var writer = XmlWriter.Create(sb, writerSettings);
            doc.Save(writer);
            return sb.ToString();
        }
        catch (Exception ex) when (ex is not FormatParseException)
        {
            throw new FormatParseException(FormatId, "FromIM", ex.Message, ex);
        }
    }

    private static IntermediateNode ConvertElement(XmlElement element, string? name, ConversionSettings settings)
    {
        var children = element.ChildNodes.OfType<XmlNode>()
            .Where(n => n is XmlElement or XmlCDataSection or XmlText { Data.Length: > 0 })
            .ToList();

        var hasChildElements = children.Any(n => n is XmlElement);
        var hasText = children.Any(n => n is XmlText or XmlCDataSection);
        var isMixed = hasChildElements && hasText;

        IntermediateNode result;

        if (isMixed)
        {
            result = BuildMixedContent(element, name, settings);
        }
        else if (hasChildElements)
        {
            result = BuildObjectWithChildren(element, name, settings);
        }
        else if (hasText)
        {
            var (text, isCdata) = GetTextContent(element);
            var type = settings.InferTypes ? InferType(text) : ScalarType.String;
            var value = type == ScalarType.Null ? null : text;
            var scalar = new ScalarNode(type, value) { Name = name };
            if (isCdata && !settings.CdataAsText)
                scalar.Metadata[NodeMetadata.CdataKey] = "true";
            result = scalar;
        }
        else
        {
            result = new ObjectNode { Name = name };
        }

        AttachAttributes(result, element, settings);
        AttachNamespaces(result, element);
        return result;
    }

    private static ObjectNode BuildObjectWithChildren(XmlElement element, string? name, ConversionSettings settings)
    {
        var childElements = element.ChildNodes.OfType<XmlElement>().ToList();
        // Group by full name (includes namespace prefix) to support ns:child
        var grouped = childElements.GroupBy(e => e.Name).ToList();

        var obj = new ObjectNode { Name = name };

        foreach (var group in grouped)
        {
            if (group.Count() > 1)
            {
                var arr = new ArrayNode { Name = group.Key };
                foreach (var child in group)
                    arr.Items.Add(ConvertElement(child, null, settings));
                obj.Children.Add(arr);
            }
            else
            {
                obj.Children.Add(ConvertElement(group.First(), group.Key, settings));
            }
        }

        return obj;
    }

    private static MixedContentNode BuildMixedContent(XmlElement element, string? name, ConversionSettings settings)
    {
        var mixed = new MixedContentNode { Name = name };
        foreach (XmlNode child in element.ChildNodes)
        {
            switch (child)
            {
                case XmlText t when !string.IsNullOrEmpty(t.Data):
                    mixed.Content.Add(new TextRun(t.Data));
                    break;
                case XmlElement e:
                    mixed.Content.Add(new ChildNode(ConvertElement(e, e.Name, settings)));
                    break;
                case XmlCDataSection cd:
                    mixed.Content.Add(new TextRun(cd.Value ?? string.Empty));
                    break;
            }
        }
        return mixed;
    }

    private static (string text, bool isCdata) GetTextContent(XmlElement element)
    {
        var hasCdata = false;
        var parts = new System.Text.StringBuilder();
        foreach (XmlNode child in element.ChildNodes)
        {
            switch (child)
            {
                case XmlCDataSection cd:
                    parts.Append(cd.Value ?? string.Empty);
                    hasCdata = true;
                    break;
                case XmlText t:
                    parts.Append(t.Data);
                    break;
            }
        }
        return (parts.ToString(), hasCdata);
    }

    private static void AttachAttributes(IntermediateNode node, XmlElement element, ConversionSettings settings)
    {
        var prefix = settings.AttributePrefix;
        foreach (XmlAttribute attr in element.Attributes)
        {
            if (attr.Name.StartsWith("xmlns", StringComparison.Ordinal)) continue;
            node.Metadata[$"{prefix}{attr.LocalName}"] = attr.Value;
        }
    }

    private static void AttachNamespaces(IntermediateNode node, XmlElement element)
    {
        foreach (XmlAttribute attr in element.Attributes)
        {
            if (attr.Name == "xmlns")
                node.Metadata[NodeMetadata.DefaultNamespaceKey] = attr.Value;
            else if (attr.Name.StartsWith("xmlns:", StringComparison.Ordinal))
                node.Metadata[attr.Name] = attr.Value;
        }
    }

    private static ScalarType InferType(string value)
    {
        if (string.Equals(value, "null", StringComparison.OrdinalIgnoreCase)) return ScalarType.Null;
        if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
            return ScalarType.Boolean;
        if (long.TryParse(value, out _)) return ScalarType.Integer;
        if (double.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out _))
            return ScalarType.Decimal;
        return ScalarType.String;
    }

    // ── FromIM helpers ───────────────────────────────────────────────────────

    private static XmlElement BuildElement(XmlDocument doc, IntermediateNode node, ConversionSettings settings,
        Dictionary<string, string>? inheritedNs)
    {
        // Build the namespace context: inherit from parent and add any declared on this node
        var nsCtx = inheritedNs is null ? new Dictionary<string, string>() : new Dictionary<string, string>(inheritedNs);
        foreach (var kv in node.Metadata)
        {
            if (kv.Key == NodeMetadata.DefaultNamespaceKey)
                nsCtx[string.Empty] = kv.Value;
            else if (kv.Key.StartsWith("xmlns:", StringComparison.Ordinal))
                nsCtx[kv.Key[6..]] = kv.Value;
        }

        var elementName = SanitizeXmlName(node.Name ?? "root");
        XmlElement element;

        // Handle namespace-prefixed names (e.g. "ns:child") using the inherited context
        var colonIndex = elementName.IndexOf(':');
        if (colonIndex > 0)
        {
            var prefix = elementName[..colonIndex];
            element = nsCtx.TryGetValue(prefix, out var nsUri)
                ? doc.CreateElement(elementName, nsUri)
                : doc.CreateElement(elementName);
        }
        else
        {
            element = doc.CreateElement(elementName);
        }

        ApplyMetadataAsAttributes(element, node.Metadata, settings);

        switch (node)
        {
            case ObjectNode obj:
                foreach (var child in obj.Children)
                {
                    if (child is ArrayNode arr)
                    {
                        foreach (var item in arr.Items)
                        {
                            var wrapper = doc.CreateElement(SanitizeXmlName(arr.Name ?? "item"));
                            ApplyMetadataAsAttributes(wrapper, item.Metadata, settings);
                            AppendChildContent(doc, wrapper, item, settings, nsCtx);
                            element.AppendChild(wrapper);
                        }
                    }
                    else
                    {
                        element.AppendChild(BuildElement(doc, child, settings, nsCtx));
                    }
                }
                break;

            case ArrayNode arr2:
                foreach (var item in arr2.Items)
                    element.AppendChild(BuildElement(doc, item, settings, nsCtx));
                break;

            case ScalarNode scalar:
                AppendScalarContent(doc, element, scalar);
                break;

            case MixedContentNode mixed:
                foreach (var item in mixed.Content)
                {
                    switch (item)
                    {
                        case TextRun tr:
                            element.AppendChild(doc.CreateTextNode(tr.Text));
                            break;
                        case ChildNode cn:
                            element.AppendChild(BuildElement(doc, cn.Node, settings, nsCtx));
                            break;
                    }
                }
                break;
        }

        return element;
    }

    private static void AppendChildContent(XmlDocument doc, XmlElement element, IntermediateNode item,
        ConversionSettings settings, Dictionary<string, string> nsCtx)
    {
        switch (item)
        {
            case ScalarNode s:
                AppendScalarContent(doc, element, s);
                break;
            case ObjectNode o:
                foreach (var c in o.Children)
                    element.AppendChild(BuildElement(doc, c, settings, nsCtx));
                break;
            case MixedContentNode m:
                foreach (var ci in m.Content)
                {
                    if (ci is TextRun tr) element.AppendChild(doc.CreateTextNode(tr.Text));
                    else if (ci is ChildNode cn) element.AppendChild(BuildElement(doc, cn.Node, settings, nsCtx));
                }
                break;
        }
    }

    private static void AppendScalarContent(XmlDocument doc, XmlElement element, ScalarNode scalar)
    {
        if (scalar.Metadata.TryGetValue(NodeMetadata.CdataKey, out var cdataFlag) && cdataFlag == "true")
            element.AppendChild(doc.CreateCDataSection(scalar.RawValue ?? string.Empty));
        else
            element.InnerText = scalar.Type == ScalarType.Null ? string.Empty : (scalar.RawValue ?? string.Empty);
    }

    private static void ApplyMetadataAsAttributes(XmlElement element, NodeMetadata metadata, ConversionSettings settings)
    {
        var attrPrefix = settings.AttributePrefix;
        foreach (var kv in metadata)
        {
            if (kv.Key.StartsWith(attrPrefix, StringComparison.Ordinal) && kv.Key.Length > attrPrefix.Length)
            {
                var attrName = kv.Key[attrPrefix.Length..];
                element.SetAttribute(attrName, kv.Value);
            }
            else if (kv.Key == NodeMetadata.DefaultNamespaceKey)
            {
                element.SetAttribute("xmlns", kv.Value);
            }
            else if (kv.Key.StartsWith("xmlns:", StringComparison.Ordinal))
            {
                element.SetAttribute(kv.Key, kv.Value);
            }
        }
    }

    private static string SanitizeXmlName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "element";
        if (char.IsDigit(name[0])) name = "_" + name;
        return name.Replace(' ', '_');
    }
}
