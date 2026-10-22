using System.Xml;
using TLio.Core.Internal;
using TLio.FormatConverter.Core;
using TLio.FormatConverter.Core.Exceptions;
using TLio.FormatConverter.Core.Model;

namespace TLio.FormatConverter.Xml;

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
            return ConvertElement(root, ReadName(root.Name, settings), settings);
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
            // If the root is an unnamed container with a single named child and no metadata,
            // use the child directly as the XML root element (preserves element names across JSON/YAML).
            var actualRoot = root is ObjectNode { Name: null } u && u.Children.Count == 1 && u.Metadata.Count == 0
                ? u.Children[0]
                : root;

            // Without namespaces the document can be written as it is read, with no DOM in between.
            // Namespaces stay on the DOM path below: which declarations an XmlDocument emits, and
            // where, is behaviour the output depends on and a hand-written writer would have to
            // reproduce byte for byte.
            if (!NeedsDocument(actualRoot, settings, null))
            {
                try { return WriteStreaming(actualRoot, settings); }
                catch (Exception)
                {
                    // Whatever went wrong, say it the way the DOM does: it words an illegal name
                    // differently from XmlWriter, and its message is what callers have seen.
                    // Nothing but the failure path pays for the second walk.
                }
            }

            var doc = new XmlDocument();
            var element = BuildElement(doc, actualRoot, settings, null);
            doc.AppendChild(element);

            using var text = new PooledStringWriter();
            var writerSettings = new XmlWriterSettings { OmitXmlDeclaration = true, Indent = false };
            using var writer = XmlWriter.Create(text, writerSettings);
            doc.Save(writer);
            writer.Flush();
            return text.ToString();
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
            result = BuildContainer(element, name, settings);
        }
        else if (hasText && !IsNilled(element))
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
            // An element with nothing in it carries no value. TLio's own adapter answers Null for
            // this, so the converter does too — the alternative was {} arriving where the source
            // document said null, which is the one answer nobody wrote.
            result = new ScalarNode(ScalarType.Null, null) { Name = name };
            if (IsNilled(element))
                result.Metadata[NodeMetadata.NilKey] = "true";
        }

        AttachAttributes(result, element, settings);
        AttachNamespaces(result, element, settings);
        return result;
    }

    /// <summary>
    /// Decide whether an element with children is an array or an object, and build it.
    /// </summary>
    private static IntermediateNode BuildContainer(XmlElement element, string? name, ConversionSettings settings)
    {
        var childElements = element.ChildNodes.OfType<XmlElement>().ToList();

        if (settings.ArrayHandling == ArrayHandling.Wrapped && IsWrappedArray(childElements, settings))
            return BuildWrappedArray(element, childElements, name, settings);

        return BuildObjectWithChildren(childElements, name, settings);
    }

    /// <summary>
    /// The canonical array test, matching <c>XmlNodeAdapter.IsArray</c>: every child shares one
    /// element name, and there is either more than one of them or that name is the item name.
    /// </summary>
    /// <remarks>
    /// The second clause is what separates the two documents XML cannot otherwise tell apart —
    /// <c>&lt;items&gt;&lt;item&gt;A&lt;/item&gt;&lt;/items&gt;</c> is an array of one, while
    /// <c>&lt;address&gt;&lt;city&gt;A&lt;/city&gt;&lt;/address&gt;</c> is an object.
    /// </remarks>
    private static bool IsWrappedArray(List<XmlElement> children, ConversionSettings settings)
    {
        if (children.Count == 0) return false;

        var shared = children[0].Name;
        if (children.Any(c => c.Name != shared)) return false;

        return children.Count > 1 || shared == settings.ArrayItemName;
    }

    private static ArrayNode BuildWrappedArray(
        XmlElement element, List<XmlElement> children, string? name, ConversionSettings settings)
    {
        var arr = new ArrayNode { Name = name };
        foreach (var child in children)
            arr.Items.Add(ConvertElement(child, null, settings));

        // Keep the name the items were written with, so an array of <order> stays an array of
        // <order> on the way back out. JSON and YAML have no room for it, so it survives only an
        // XML-to-XML trip — which is exactly what TLio documents.
        var itemName = children[0].Name;
        if (itemName != settings.ArrayItemName)
            arr.Metadata[NodeMetadata.ItemNameKey] = itemName;

        return arr;
    }

    private static ObjectNode BuildObjectWithChildren(
        List<XmlElement> childElements, string? name, ConversionSettings settings)
    {
        // Group by full name (includes namespace prefix) to support ns:child
        var grouped = childElements.GroupBy(e => e.Name).ToList();

        var obj = new ObjectNode { Name = name };

        foreach (var group in grouped)
        {
            if (group.Count() > 1)
            {
                var arr = new ArrayNode { Name = ReadName(group.Key, settings) };
                foreach (var child in group)
                    arr.Items.Add(ConvertElement(child, null, settings));
                obj.Children.Add(arr);
            }
            else
            {
                obj.Children.Add(ConvertElement(group.First(), ReadName(group.Key, settings), settings));
            }
        }

        return obj;
    }

    /// <summary>
    /// <c>xsi:nil="true"</c> says null and nothing else, so it is honoured whichever
    /// <see cref="NullRepresentation"/> is configured for writing.
    /// </summary>
    private static bool IsNilled(XmlElement element)
    {
        foreach (XmlAttribute attr in element.Attributes)
        {
            if (attr.LocalName == "nil"
                && attr.NamespaceURI == XsiNamespace
                && string.Equals(attr.Value, "true", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private const string XsiNamespace = "http://www.w3.org/2001/XMLSchema-instance";

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
                    mixed.Content.Add(new ChildNode(ConvertElement(e, ReadName(e.Name, settings), settings)));
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
        // One text node is the common case, and the one that matters for size: a StringBuilder
        // would copy it twice (into its chunks, then into the result) for nothing.
        if (element.ChildNodes.Count == 1)
        {
            switch (element.FirstChild)
            {
                case XmlCDataSection cdata: return (cdata.Value ?? string.Empty, true);
                case XmlText text: return (text.Data, false);
            }
        }

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
            if (attr.Name.StartsWith(NodeMetadata.DefaultNamespaceKey, StringComparison.Ordinal)) continue;
            // xsi:nil is how the document spells null, not a property of the data.
            if (attr.NamespaceURI == XsiNamespace && attr.LocalName == "nil") continue;
            node.Metadata[$"{prefix}{attr.LocalName}"] = attr.Value;
        }
    }

    /// <summary>
    /// Namespace declarations become metadata keyed by
    /// <see cref="ConversionSettings.NamespacePrefix"/>. The setting names the key the neutral
    /// formats see; the XML attribute written back out is always the literal <c>xmlns:</c>.
    /// </summary>
    private static void AttachNamespaces(IntermediateNode node, XmlElement element, ConversionSettings settings)
    {
        foreach (XmlAttribute attr in element.Attributes)
        {
            if (attr.Name == NodeMetadata.DefaultNamespaceKey)
                node.Metadata[NodeMetadata.DefaultNamespaceKey] = attr.Value;
            else if (attr.Value == XsiNamespace)
                continue; // re-declared on write, and only where a nil actually needs it
            else if (attr.Name.StartsWith(NodeMetadata.NamespacePrefix, StringComparison.Ordinal))
                node.Metadata[settings.NamespacePrefix + attr.LocalName] = attr.Value;
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

    // ── FromIM, written straight to a writer ─────────────────────────────────

    /// <summary>
    /// Whether anything in this tree involves a namespace: a declaration, a prefixed name, or
    /// <c>xsi:nil</c>. Conservative on purpose — a name that merely contains a colon counts, even
    /// where sanitising would remove it — because a false "yes" only costs the DOM path.
    /// </summary>
    private static bool NeedsDocument(IntermediateNode node, ConversionSettings settings, string? fallbackName)
    {
        if (settings.NullRepresentation == NullRepresentation.XsiNil) return true;
        if ((node.Name ?? fallbackName ?? "root").Contains(':')) return true;

        foreach (var key in node.Metadata.Keys)
        {
            if (MetadataConvention.IsNamespaceKey(key, settings)) return true;
            if (MetadataConvention.IsAttributeKey(key, settings) && key.Contains(':')) return true;
        }

        switch (node)
        {
            case ObjectNode obj:
                foreach (var child in obj.Children)
                {
                    if (child is ArrayNode repeated && settings.ArrayHandling == ArrayHandling.Repeated)
                    {
                        if ((repeated.Name ?? settings.ArrayItemName).Contains(':')) return true;
                        foreach (var item in repeated.Items)
                            if (ItemNeedsDocument(item, settings)) return true;
                    }
                    else if (NeedsDocument(child, settings, null)) return true;
                }
                return false;

            case ArrayNode arr:
                var itemName = ItemNameFor(arr, settings);
                if (itemName.Contains(':')) return true;
                foreach (var item in arr.Items)
                    if (NeedsDocument(item, settings, itemName)) return true;
                return false;

            case MixedContentNode mixed:
                foreach (var part in mixed.Content)
                    if (part is ChildNode cn && NeedsDocument(cn.Node, settings, null)) return true;
                return false;

            default:
                return false;
        }
    }

    /// <summary>An item written under a repeated-array wrapper: its attributes, then its content.</summary>
    private static bool ItemNeedsDocument(IntermediateNode item, ConversionSettings settings)
    {
        foreach (var key in item.Metadata.Keys)
        {
            if (MetadataConvention.IsNamespaceKey(key, settings)) return true;
            if (MetadataConvention.IsAttributeKey(key, settings) && key.Contains(':')) return true;
        }

        switch (item)
        {
            case ObjectNode o:
                return o.Children.Any(c => NeedsDocument(c, settings, null));
            case ArrayNode a:
                var itemName = ItemNameFor(a, settings);
                return itemName.Contains(':') || a.Items.Any(n => NeedsDocument(n, settings, itemName));
            case MixedContentNode m:
                return m.Content.Any(c => c is ChildNode cn && NeedsDocument(cn.Node, settings, null));
            default:
                return false;
        }
    }

    private static string WriteStreaming(IntermediateNode root, ConversionSettings settings)
    {
        using var text = new PooledStringWriter();
        var writerSettings = new XmlWriterSettings { OmitXmlDeclaration = true, Indent = false };
        using var writer = XmlWriter.Create(text, writerSettings);
        WriteElement(writer, root, settings, null);
        writer.Flush();
        return text.ToString();
    }

    /// <summary>The same walk as <see cref="BuildElement"/>, in the same order, writing instead of building.</summary>
    private static void WriteElement(XmlWriter writer, IntermediateNode node, ConversionSettings settings, string? fallbackName)
    {
        writer.WriteStartElement(XmlName(node.Name ?? fallbackName ?? "root", settings));
        WriteAttributes(writer, node.Metadata, settings);

        switch (node)
        {
            case ObjectNode obj:
                foreach (var child in obj.Children)
                {
                    if (settings.ArrayHandling == ArrayHandling.Repeated && child is ArrayNode repeated)
                    {
                        foreach (var item in repeated.Items)
                        {
                            writer.WriteStartElement(XmlName(repeated.Name ?? settings.ArrayItemName, settings));
                            WriteAttributes(writer, item.Metadata, settings);
                            WriteChildContent(writer, item, settings);
                            writer.WriteEndElement();
                        }
                    }
                    else
                    {
                        WriteElement(writer, child, settings, null);
                    }
                }
                break;

            case ArrayNode arr:
                var itemName = ItemNameFor(arr, settings);
                foreach (var item in arr.Items)
                    WriteElement(writer, item, settings, itemName);
                break;

            case ScalarNode scalar:
                WriteScalarContent(writer, scalar);
                break;

            case MixedContentNode mixed:
                foreach (var item in mixed.Content)
                {
                    switch (item)
                    {
                        case TextRun tr:
                            writer.WriteString(tr.Text);
                            break;
                        case ChildNode cn:
                            WriteElement(writer, cn.Node, settings, null);
                            break;
                    }
                }
                break;
        }

        writer.WriteEndElement();
    }

    private static void WriteChildContent(XmlWriter writer, IntermediateNode item, ConversionSettings settings)
    {
        switch (item)
        {
            case ScalarNode s:
                WriteScalarContent(writer, s);
                break;
            case ObjectNode o:
                foreach (var c in o.Children)
                    WriteElement(writer, c, settings, null);
                break;
            case ArrayNode a:
                var itemName = ItemNameFor(a, settings);
                foreach (var nested in a.Items)
                    WriteElement(writer, nested, settings, itemName);
                break;
            case MixedContentNode m:
                foreach (var ci in m.Content)
                {
                    if (ci is TextRun tr) writer.WriteString(tr.Text);
                    else if (ci is ChildNode cn) WriteElement(writer, cn.Node, settings, null);
                }
                break;
        }
    }

    private static void WriteScalarContent(XmlWriter writer, ScalarNode scalar)
    {
        if (scalar.Type == ScalarType.Null) return;

        if (scalar.Metadata.TryGetValue(NodeMetadata.CdataKey, out var cdataFlag) && cdataFlag == "true")
            writer.WriteCData(scalar.RawValue ?? string.Empty);
        else
            writer.WriteString(scalar.RawValue ?? string.Empty);
    }

    private static void WriteAttributes(XmlWriter writer, NodeMetadata metadata, ConversionSettings settings)
    {
        foreach (var kv in metadata)
        {
            if (MetadataConvention.IsAttributeKey(kv.Key, settings))
                writer.WriteAttributeString(kv.Key[settings.AttributePrefix.Length..], kv.Value);
        }
    }

    // ── FromIM helpers ───────────────────────────────────────────────────────

    private static XmlElement BuildElement(XmlDocument doc, IntermediateNode node, ConversionSettings settings,
        Dictionary<string, string>? inheritedNs, string? fallbackName = null)
    {
        // Build the namespace context: inherit from parent and add any declared on this node
        var nsCtx = inheritedNs is null ? new Dictionary<string, string>() : new Dictionary<string, string>(inheritedNs);
        foreach (var kv in node.Metadata)
        {
            if (kv.Key == NodeMetadata.DefaultNamespaceKey)
                nsCtx[string.Empty] = kv.Value;
            else if (MetadataConvention.IsNamespaceKey(kv.Key, settings))
                nsCtx[kv.Key[settings.NamespacePrefix.Length..]] = kv.Value;
        }

        var element = CreateElement(doc, node.Name ?? fallbackName ?? "root", nsCtx, settings);
        ApplyMetadataAsAttributes(element, node.Metadata, settings);

        switch (node)
        {
            case ObjectNode obj:
                foreach (var child in obj.Children)
                {
                    if (settings.ArrayHandling == ArrayHandling.Repeated && child is ArrayNode repeated)
                    {
                        // Legacy shape: the array has no element of its own, its items repeat
                        // under this one.
                        foreach (var item in repeated.Items)
                        {
                            var wrapper = CreateElement(doc, repeated.Name ?? settings.ArrayItemName, nsCtx, settings);
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

            case ArrayNode arr:
                // Canonical shape: this element is the array, and each item is one child of it.
                var itemName = ItemNameFor(arr, settings);
                foreach (var item in arr.Items)
                    element.AppendChild(BuildElement(doc, item, settings, nsCtx, itemName));
                break;

            case ScalarNode scalar:
                AppendScalarContent(doc, element, scalar, settings);
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
                AppendScalarContent(doc, element, s, settings);
                break;
            case ObjectNode o:
                foreach (var c in o.Children)
                    element.AppendChild(BuildElement(doc, c, settings, nsCtx));
                break;
            case ArrayNode a:
                var itemName = ItemNameFor(a, settings);
                foreach (var nested in a.Items)
                    element.AppendChild(BuildElement(doc, nested, settings, nsCtx, itemName));
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

    /// <summary>The element name this array's items take — the one they arrived with, or the default.</summary>
    private static string ItemNameFor(ArrayNode array, ConversionSettings settings) =>
        array.Metadata.TryGetValue(NodeMetadata.ItemNameKey, out var remembered) && remembered.Length > 0
            ? remembered
            : settings.ArrayItemName;

    private static void AppendScalarContent(XmlDocument doc, XmlElement element, ScalarNode scalar,
        ConversionSettings settings)
    {
        if (scalar.Type == ScalarType.Null)
        {
            // An empty element is equally null, "", {} and []; xsi:nil says null and only null,
            // at the cost of a namespace declaration.
            if (settings.NullRepresentation == NullRepresentation.XsiNil)
            {
                var nil = doc.CreateAttribute("xsi", "nil", XsiNamespace);
                nil.Value = "true";
                element.Attributes.Append(nil);
            }
            return;
        }

        if (scalar.Metadata.TryGetValue(NodeMetadata.CdataKey, out var cdataFlag) && cdataFlag == "true")
            element.AppendChild(doc.CreateCDataSection(scalar.RawValue ?? string.Empty));
        else
            element.InnerText = scalar.RawValue ?? string.Empty;
    }

    /// <summary>
    /// Create an element in whatever namespace the context puts it in: the one its own prefix
    /// names, or the default declaration in force.
    /// </summary>
    /// <remarks>
    /// The default namespace has to be part of the element rather than an <c>xmlns</c> attribute
    /// set afterwards — the writer rejects redefining the empty prefix on an element that is not
    /// in that namespace, and children of a defaulted element belong to it too, so building them
    /// bare would make the writer emit <c>xmlns=""</c> to take them back out again.
    /// </remarks>
    private static XmlElement CreateElement(XmlDocument doc, string rawName, Dictionary<string, string> nsCtx,
        ConversionSettings settings)
    {
        var name = XmlName(rawName, settings);
        var colonIndex = name.IndexOf(':');

        if (colonIndex > 0)
        {
            return nsCtx.TryGetValue(name[..colonIndex], out var prefixedNs)
                ? doc.CreateElement(name, prefixedNs)
                : doc.CreateElement(name);
        }

        return nsCtx.TryGetValue(string.Empty, out var defaultNs) && defaultNs.Length > 0
            ? doc.CreateElement(name, defaultNs)
            : doc.CreateElement(name);
    }

    private static void ApplyMetadataAsAttributes(XmlElement element, NodeMetadata metadata, ConversionSettings settings)
    {
        foreach (var kv in metadata)
        {
            // Namespaces are checked first: a custom attribute prefix could otherwise claim a
            // namespace key, and the declaration would be written out as an ordinary attribute.
            if (kv.Key == NodeMetadata.DefaultNamespaceKey)
            {
                // Already carried by the element itself — see CreateElement.
            }
            else if (MetadataConvention.IsNamespaceKey(kv.Key, settings))
            {
                // The metadata key is spelled with the configured prefix; the XML attribute is
                // always xmlns: — that part is the format, not a convention.
                element.SetAttribute(NodeMetadata.NamespacePrefix + kv.Key[settings.NamespacePrefix.Length..], kv.Value);
            }
            else if (MetadataConvention.IsAttributeKey(kv.Key, settings))
            {
                element.SetAttribute(kv.Key[settings.AttributePrefix.Length..], kv.Value);
            }
        }
    }

    /// <summary>
    /// Turn a property name into an XML element name, the way
    /// <see cref="ConversionSettings.NameSanitization"/> asks for.
    /// </summary>
    /// <exception cref="FormatParseException">
    /// Under <see cref="NameSanitization.Error"/>, when the name is not a legal element name.
    /// </exception>
    private static string XmlName(string name, ConversionSettings settings)
    {
        if (string.IsNullOrEmpty(name)) name = "element";
        if (IsLegalXmlName(name)) return name;

        switch (settings.NameSanitization)
        {
            case NameSanitization.Error:
                throw new FormatParseException("xml", "FromIM",
                    $"'{name}' is not a legal XML element name. Rename the property, or set " +
                    $"nameSanitization to 'sanitize' or 'escape'.");

            case NameSanitization.Escape:
                // The XML Schema _xHHHH_ convention, which DecodeName reverses on the way back.
                return XmlConvert.EncodeLocalName(name);

            default:
                if (char.IsDigit(name[0])) name = "_" + name;
                return name.Replace(' ', '_');
        }
    }

    /// <summary>Reverse <see cref="NameSanitization.Escape"/> when reading.</summary>
    private static string ReadName(string name, ConversionSettings settings) =>
        settings.NameSanitization == NameSanitization.Escape ? XmlConvert.DecodeName(name) : name;

    private static bool IsLegalXmlName(string name)
    {
        try
        {
            XmlConvert.VerifyName(name);
            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }
}
