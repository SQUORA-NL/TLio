using System.Xml.Linq;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Xml;

/// <summary>
/// INodeAdapter implementation for System.Xml.Linq's XElement model.
/// Stub — all members throw NotImplementedException until the XML adapter is built.
/// See specs/001-tlio-core-architecture/tasks.md for the implementation backlog.
/// </summary>
public class XmlNodeAdapter : INodeAdapter<XElement>
{
    public bool IsObject(XElement node) => node.HasElements;
    public bool IsArray(XElement node) =>
        node.Elements().Select(e => e.Name.LocalName).Distinct().Count() == 1;
    public bool IsPrimitive(XElement node) => !node.HasElements;
    public bool IsNull(XElement node) =>
        node.Attribute("nil")?.Value == "true" || (!node.HasElements && string.IsNullOrEmpty(node.Value));

    public bool HasProperty(XElement node, string propertyName) =>
        node.Element(propertyName) != null || node.Attribute(propertyName) != null;

    public XElement? GetProperty(XElement node, string propertyName)
    {
        // TODO: decide attribute vs element semantics
        throw new NotImplementedException();
    }

    public void SetProperty(XElement node, string propertyName, XElement value) =>
        throw new NotImplementedException();

    public void RemoveProperty(XElement node, string propertyName) =>
        throw new NotImplementedException();

    public IEnumerable<string> GetPropertyNames(XElement node) =>
        node.Elements().Select(e => e.Name.LocalName);

    public void AppendToArray(XElement array, XElement value) => array.Add(value);
    public void InsertIntoArray(XElement array, int index, XElement value) =>
        throw new NotImplementedException();
    public void RemoveFromArray(XElement array, int index) =>
        throw new NotImplementedException();
    public int GetArrayLength(XElement array) => array.Elements().Count();
    public XElement GetArrayElement(XElement array, int index) =>
        array.Elements().ElementAt(index);
    public IEnumerable<XElement> GetArrayElements(XElement array) => array.Elements();

    public XElement CreateNull() => new("null");
    public XElement CreateObject() => new("object");
    public XElement CreateArray() => new("array");
    public XElement CreateString(string value) => new("value", value);
    public XElement CreateNumber(double value) => new("value", value);
    public XElement CreateBoolean(bool value) => new("value", value);
    public XElement CreateValue(object? value) =>
        value == null ? CreateNull() : new XElement("value", value);

    public object? GetValue(XElement node) => node.Value;
    public T? GetValue<T>(XElement node) => throw new NotImplementedException();

    public XElement DeepClone(XElement node) => new(node);
    public void Replace(XElement target, XElement replacement) =>
        target.ReplaceWith(replacement);

    public XElement Parse(string content) => XElement.Parse(content);
    public string Serialize(XElement node, bool pretty = false) =>
        pretty ? node.ToString(SaveOptions.None) : node.ToString(SaveOptions.DisableFormatting);
}
