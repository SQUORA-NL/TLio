using System.Globalization;
using System.Xml.Linq;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Xml;

/// <summary>
/// INodeAdapter implementation for System.Xml.Linq's XElement model.
///
/// XML representation conventions used by TLio:
///   Object  = element whose children are uniquely-named elements  (HasElements == true)
///   Array   = element whose children all share one element name   (IsArray check)
///   Primitive = element with no child elements (text content only)
///   Null    = element with nil="true" attribute, or empty element with no text
///
/// Property semantics: child elements are "properties"; attributes are not used for data.
/// </summary>
public class XmlNodeAdapter : INodeAdapter<XElement>
{
    // ── Type queries ──────────────────────────────────────────────────────────

    public bool IsObject(XElement node) => node.HasElements;

    public bool IsArray(XElement node) =>
        node.HasElements &&
        node.Elements().Select(e => e.Name.LocalName).Distinct().Count() == 1;

    public bool IsPrimitive(XElement node) => !node.HasElements;

    public bool IsNull(XElement node) =>
        node.Attribute("nil")?.Value == "true" ||
        (!node.HasElements && string.IsNullOrEmpty(node.Value));

    // ── Object operations ─────────────────────────────────────────────────────

    public bool HasProperty(XElement node, string propertyName) =>
        node.Element(propertyName) != null;

    /// <summary>Returns the first child element named <paramref name="propertyName"/>.</summary>
    public XElement? GetProperty(XElement node, string propertyName) =>
        node.Element(propertyName);

    /// <summary>
    /// Creates or replaces a child element named <paramref name="propertyName"/>.
    /// Content is taken from <paramref name="value"/>: child elements are copied if it is
    /// an object/array; otherwise the text value is used.
    /// </summary>
    public void SetProperty(XElement node, string propertyName, XElement value)
    {
        var newElement = BuildNamed(propertyName, value);
        var existing = node.Element(propertyName);
        if (existing != null)
            existing.ReplaceWith(newElement);
        else
            node.Add(newElement);
    }

    public void RemoveProperty(XElement node, string propertyName) =>
        node.Element(propertyName)?.Remove();

    public IEnumerable<string> GetPropertyNames(XElement node) =>
        node.Elements().Select(e => e.Name.LocalName);

    // ── Array operations ──────────────────────────────────────────────────────

    public void AppendToArray(XElement array, XElement value) => array.Add(value);

    public void InsertIntoArray(XElement array, int index, XElement value)
    {
        var children = array.Elements().ToList();
        if (index >= children.Count)
            array.Add(value);
        else
            children[index].AddBeforeSelf(value);
    }

    public void RemoveFromArray(XElement array, int index)
    {
        var elements = array.Elements().ToList();
        if (index >= 0 && index < elements.Count)
            elements[index].Remove();
    }

    public int GetArrayLength(XElement array) => array.Elements().Count();

    public XElement GetArrayElement(XElement array, int index) =>
        array.Elements().ElementAt(index);

    public IEnumerable<XElement> GetArrayElements(XElement array) => array.Elements();

    // ── Node creation ─────────────────────────────────────────────────────────

    public XElement CreateNull() => new("null");
    public XElement CreateObject() => new("object");
    public XElement CreateArray() => new("array");
    public XElement CreateString(string value) => new("value", value);

    public XElement CreateNumber(double value) =>
        new("value", value.ToString(CultureInfo.InvariantCulture));

    public XElement CreateBoolean(bool value) =>
        new("value", value.ToString().ToLowerInvariant());

    public XElement CreateValue(object? value) =>
        value == null ? CreateNull() : new XElement("value", value.ToString());

    // ── Value access ──────────────────────────────────────────────────────────

    public object? GetValue(XElement node) => node.Value;

    public T? GetValue<T>(XElement node)
    {
        try
        {
            return (T)Convert.ChangeType(node.Value, typeof(T), CultureInfo.InvariantCulture);
        }
        catch
        {
            return default;
        }
    }

    public bool? TryGetBoolean(XElement node)
    {
        if (IsNull(node)) return null;
        if (bool.TryParse(node.Value, out var b)) return b;
        return null;
    }

    public double? TryGetDouble(XElement node)
    {
        if (IsNull(node)) return null;
        if (double.TryParse(node.Value, NumberStyles.Any,
                CultureInfo.InvariantCulture, out var d)) return d;
        return null;
    }

    public string? TryGetString(XElement node)
    {
        if (IsNull(node)) return null;
        return node.HasElements ? null : node.Value;
    }

    // ── Cloning & replacement ─────────────────────────────────────────────────

    /// <summary>Deep clone via XElement copy constructor.</summary>
    public XElement DeepClone(XElement node) => new(node);

    /// <summary>
    /// Replace <paramref name="target"/> with the content of <paramref name="replacement"/>,
    /// preserving <paramref name="target"/>'s element name in the document tree.
    /// </summary>
    public void Replace(XElement target, XElement replacement)
    {
        var renamed = BuildNamed(target.Name.LocalName, replacement);
        target.ReplaceWith(renamed);
    }

    public bool RemoveFromParent(XElement node)
    {
        if (node.Parent == null) return false;
        node.Remove();
        return true;
    }

    // ── Deep merge ────────────────────────────────────────────────────────────

    public void DeepMergeInto(XElement source, XElement target,
        ArrayMergeMode arrayMergeMode = ArrayMergeMode.Concat)
    {
        if (!source.HasElements)
        {
            // Source is primitive — replace target text
            target.RemoveNodes();
            target.Value = source.Value;
            return;
        }

        foreach (var sourceChild in source.Elements())
        {
            var targetChild = target.Element(sourceChild.Name);
            if (targetChild == null)
            {
                target.Add(new XElement(sourceChild));
            }
            else if (IsArray(targetChild) && IsArray(sourceChild))
            {
                if (arrayMergeMode == ArrayMergeMode.Replace)
                {
                    targetChild.RemoveNodes();
                    foreach (var item in sourceChild.Elements())
                        targetChild.Add(new XElement(item));
                }
                else
                {
                    // Concat / MergeByKey → append
                    foreach (var item in sourceChild.Elements())
                        targetChild.Add(new XElement(item));
                }
            }
            else
            {
                DeepMergeInto(sourceChild, targetChild, arrayMergeMode);
            }
        }
    }

    // ── Parent access ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the XElement parent, or null if the node is the document root
    /// or its parent is an XDocument rather than another XElement.
    /// </summary>
    public XElement? GetParentNode(XElement node) => node.Parent as XElement;

    /// <summary>
    /// Returns the element's own local name, which is also the property name
    /// under which it lives in its parent (XML child-element semantics).
    /// Returns null when the node has no parent.
    /// </summary>
    public string? GetParentPropertyName(XElement node) =>
        node.Parent != null ? node.Name.LocalName : null;

    // ── Equality ─────────────────────────────────────────────────────────────

    public bool DeepEquals(XElement a, XElement b) => XNode.DeepEquals(a, b);

    // ── Serialisation ─────────────────────────────────────────────────────────

    public XElement Parse(string content) => XElement.Parse(content);

    public string Serialize(XElement node, bool pretty = false) =>
        pretty
            ? node.ToString(SaveOptions.None)
            : node.ToString(SaveOptions.DisableFormatting);

    // ── Internal helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Create a new element named <paramref name="name"/> whose content mirrors
    /// <paramref name="source"/>: child elements if it is an object/array,
    /// or text value if it is a primitive.
    /// </summary>
    private static XElement BuildNamed(string name, XElement source)
    {
        var el = new XElement(name);
        if (source.HasElements)
        {
            foreach (var child in source.Elements())
                el.Add(new XElement(child));
        }
        else
        {
            el.Value = source.Value;
        }
        return el;
    }
}
