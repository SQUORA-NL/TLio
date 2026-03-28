using System.Globalization;
using System.Xml.Linq;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Xml;

/// <summary>
/// INodeAdapter implementation for System.Xml.Linq's XElement model.
/// Elements with child elements are treated as objects; elements with only
/// text content are treated as primitives.
/// </summary>
public class XmlNodeAdapter : INodeAdapter<XElement>
{
    public bool IsObject(XElement node) => node.HasElements;
    public bool IsArray(XElement node) =>
        node.HasElements &&
        node.Elements().Select(e => e.Name.LocalName).Distinct().Count() == 1;
    public bool IsPrimitive(XElement node) => !node.HasElements;
    public bool IsNull(XElement node) =>
        node.Attribute("nil")?.Value == "true" ||
        (!node.HasElements && string.IsNullOrEmpty(node.Value));

    public bool HasProperty(XElement node, string propertyName) =>
        node.Element(propertyName) != null;

    public XElement? GetProperty(XElement node, string propertyName) =>
        node.Element(propertyName);

    public void SetProperty(XElement node, string propertyName, XElement value)
    {
        var existing = node.Element(propertyName);
        var newEl = new XElement(propertyName,
            value.Attributes().Where(a => a.Name.LocalName != "nil"),
            value.Nodes());
        if (existing != null)
            existing.ReplaceWith(newEl);
        else
            node.Add(newEl);
    }

    public void RemoveProperty(XElement node, string propertyName) =>
        node.Element(propertyName)?.Remove();

    public IEnumerable<string> GetPropertyNames(XElement node) =>
        node.Elements().Select(e => e.Name.LocalName);

    public void AppendToArray(XElement array, XElement value) => array.Add(value);

    public void InsertIntoArray(XElement array, int index, XElement value)
    {
        var elements = array.Elements().ToList();
        if (index >= elements.Count)
            array.Add(value);
        else
            elements[index].AddBeforeSelf(value);
    }

    public void RemoveFromArray(XElement array, int index)
    {
        var el = array.Elements().ElementAtOrDefault(index);
        el?.Remove();
    }

    public int GetArrayLength(XElement array) => array.Elements().Count();
    public XElement GetArrayElement(XElement array, int index) =>
        array.Elements().ElementAt(index);
    public IEnumerable<XElement> GetArrayElements(XElement array) => array.Elements();

    public XElement CreateNull() => new XElement("value", new XAttribute("nil", "true"));
    public XElement CreateObject() => new XElement("object");
    public XElement CreateArray() => new XElement("array");
    public XElement CreateString(string value) => new XElement("value", value);
    public XElement CreateNumber(double value) =>
        new XElement("value", value.ToString(CultureInfo.InvariantCulture));
    public XElement CreateBoolean(bool value) =>
        new XElement("value", value.ToString().ToLowerInvariant());
    public XElement CreateValue(object? value) =>
        value == null ? CreateNull() : new XElement("value", value);

    public object? GetValue(XElement node) => node.Value;

    public T? GetValue<T>(XElement node)
    {
        try
        {
            if (!string.IsNullOrEmpty(node.Value))
                return (T)Convert.ChangeType(node.Value, typeof(T), CultureInfo.InvariantCulture);
        }
        catch { }
        return default;
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
        if (double.TryParse(node.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            return d;
        return null;
    }

    public string? TryGetString(XElement node)
    {
        if (IsNull(node)) return null;
        return node.Value;
    }

    public XElement DeepClone(XElement node) => new XElement(node);

    public void Replace(XElement target, XElement replacement)
    {
        var renamed = new XElement(target.Name,
            replacement.Attributes().Where(a => a.Name.LocalName != "nil"),
            replacement.Nodes());
        target.ReplaceWith(renamed);
    }

    public bool RemoveFromParent(XElement node)
    {
        if (node.Parent == null) return false;
        node.Remove();
        return true;
    }

    public void DeepMergeInto(XElement source, XElement target,
        ArrayMergeMode arrayMergeMode = ArrayMergeMode.Concat)
    {
        if (!source.HasElements)
        {
            target.Value = source.Value;
            return;
        }

        foreach (var srcChild in source.Elements())
        {
            var tgtChild = target.Element(srcChild.Name);
            if (tgtChild == null)
            {
                target.Add(new XElement(srcChild));
            }
            else if (IsArray(tgtChild) && IsArray(srcChild))
            {
                if (arrayMergeMode == ArrayMergeMode.Replace)
                {
                    tgtChild.RemoveNodes();
                    foreach (var item in srcChild.Elements())
                        tgtChild.Add(new XElement(item));
                }
                else
                {
                    foreach (var item in srcChild.Elements())
                        tgtChild.Add(new XElement(item));
                }
            }
            else
            {
                DeepMergeInto(srcChild, tgtChild, arrayMergeMode);
            }
        }
    }

    public XElement? GetParentNode(XElement node) => node.Parent;
    public string? GetParentPropertyName(XElement node) => node.Name.LocalName;

    public bool DeepEquals(XElement a, XElement b) => XNode.DeepEquals(a, b);

    public XElement Parse(string content) => XElement.Parse(content);

    public string Serialize(XElement node, bool pretty = false) =>
        pretty ? node.ToString(SaveOptions.None) : node.ToString(SaveOptions.DisableFormatting);
}
