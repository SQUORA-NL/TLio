using System.Globalization;
using System.Xml.Linq;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Xml;

/// <summary>
/// INodeAdapter implementation for System.Xml.Linq's XElement model.
///
/// <para><b>The canonical JSON ↔ XML shape.</b> Commands are written against the JSON data
/// model (object / array / scalar / null), so the adapter has to answer "which of those is
/// this element?" for every element it is handed. It uses one mapping throughout — the same
/// one the <c>FormatConverter</c> round-trips and the one the fixtures are written in:</para>
///
/// <list type="table">
///   <listheader><term>JSON</term><description>XML</description></listheader>
///   <item><term><c>{"k": "v"}</c></term><description><c>&lt;k&gt;v&lt;/k&gt;</c></description></item>
///   <item><term><c>{"k": {…}}</c></term><description><c>&lt;k&gt;</c> with one child element per property</description></item>
///   <item><term><c>{"k": [1,2]}</c></term><description><c>&lt;k&gt;&lt;item&gt;1&lt;/item&gt;&lt;item&gt;2&lt;/item&gt;&lt;/k&gt;</c></description></item>
///   <item><term><c>{"k": null}</c></term><description><c>&lt;k/&gt;</c></description></item>
/// </list>
///
/// <para>An array is a single element whose children are the items — the wrapper convention.
/// It is the only shape that gives an array a node of its own, which
/// <see cref="INodeAdapter{TNode}.AppendToArray"/> and friends require, and it is what the
/// path syntax already assumes (<c>/order/items/item[1]</c>).</para>
///
/// <para><b>The empty element.</b> <c>&lt;k/&gt;</c> is the one shape XML cannot disambiguate:
/// it is equally the serialisation of <c>null</c>, <c>""</c>, <c>{}</c> and <c>[]</c>. Rather
/// than pick one meaning and break the others, each predicate answers the question it is
/// actually asked — <see cref="IsNull"/> and <see cref="IsPrimitive"/> say yes because the
/// element carries no value, and <see cref="IsObject"/> says yes because the element is an
/// unfilled container that a property can still be written into. That last answer is what
/// makes <c>add /order/address/city</c> create the city instead of losing it: the
/// <c>&lt;address/&gt;</c> that the path construction just created is a container, not a
/// primitive. <see cref="GetNodeKind"/> resolves the tie for the type predicates by asking
/// <see cref="IsNull"/> first, so <c>=isNull()</c> still reports an empty element as null.</para>
/// </summary>
public class XmlNodeAdapter : INodeAdapter<XElement>
{
    /// <summary>
    /// Element name given to array items that TLio creates itself. An array read out of an
    /// existing document keeps whatever name its items already use — see <see cref="ItemNameOf"/>.
    /// </summary>
    public const string DefaultItemName = "item";

    // ── Type queries ─────────────────────────────────────────────────────────

    /// <summary>
    /// True for an element that holds named properties, and for a completely empty element,
    /// which is a container that has not been filled in yet (see the class remarks).
    /// </summary>
    public bool IsObject(XElement node) =>
        node.HasElements ? !IsArray(node) : !HasTextContent(node);

    /// <summary>
    /// True when every child carries the same element name and either there is more than one
    /// of them, or that name is the canonical item name. A lone differently-named child is an
    /// object with one property — <c>&lt;address&gt;&lt;city/&gt;&lt;/address&gt;</c> is not a
    /// one-element array — while <c>&lt;items&gt;&lt;item/&gt;&lt;/items&gt;</c> is.
    /// </summary>
    public bool IsArray(XElement node)
    {
        if (!node.HasElements) return false;
        string? shared = null;
        var count = 0;
        foreach (var child in node.Elements())
        {
            var name = child.Name.LocalName;
            if (shared == null) shared = name;
            else if (shared != name) return false;
            count++;
        }
        return count > 1 || shared == DefaultItemName;
    }

    public bool IsPrimitive(XElement node) => !node.HasElements;

    public bool IsNull(XElement node) => !node.HasElements && !HasTextContent(node);

    private static bool HasTextContent(XElement node) =>
        node.Nodes().OfType<XText>().Any(t => t.Value.Length > 0);

    /// <summary>
    /// The item element name to use when adding to <paramref name="array"/>: the name its
    /// items already carry, so an array of <c>&lt;order&gt;</c> keeps growing with
    /// <c>&lt;order&gt;</c>, and <see cref="DefaultItemName"/> for an array with nothing in it.
    /// </summary>
    private static string ItemNameOf(XElement array)
    {
        var first = array.Elements().FirstOrDefault();
        return first?.Name.LocalName ?? DefaultItemName;
    }

    /// <summary>
    /// Not every path leaf is usable as an element name — a wildcard or predicate segment
    /// reaches these methods as-is. XName.Get would throw on those, taking the whole script
    /// down, so such a name is reported as simply not present and the command no-ops instead.
    /// </summary>
    private static bool IsUsableElementName(string propertyName) =>
        !string.IsNullOrEmpty(propertyName) &&
        System.Xml.XmlConvert.EncodeLocalName(propertyName) == propertyName;

    // ── Object operations ─────────────────────────────────────────────────────

    public bool HasProperty(XElement node, string propertyName) =>
        IsUsableElementName(propertyName) && node.Element(propertyName) != null;

    public XElement? GetProperty(XElement node, string propertyName) =>
        IsUsableElementName(propertyName) ? node.Element(propertyName) : null;

    /// <summary>
    /// Writes <paramref name="value"/> under <paramref name="propertyName"/>. The value node
    /// carries whatever name its producer gave it (<c>&lt;value&gt;</c>, <c>&lt;array&gt;</c>,
    /// the source element's own name for a copy); in XML the property name is the element
    /// name, so the content is re-hung under <paramref name="propertyName"/>.
    /// </summary>
    public void SetProperty(XElement node, string propertyName, XElement value)
    {
        if (!IsUsableElementName(propertyName))
            return;

        var newEl = Rename(value, propertyName);
        var existing = node.Element(propertyName);
        if (existing != null)
            existing.ReplaceWith(newEl);
        else
            node.Add(newEl);
    }

    public void RemoveProperty(XElement node, string propertyName)
    {
        if (IsUsableElementName(propertyName))
            node.Element(propertyName)?.Remove();
    }

    public IEnumerable<string> GetPropertyNames(XElement node) =>
        node.Elements().Select(e => e.Name.LocalName);

    // ── Array operations ──────────────────────────────────────────────────────

    /// <summary>
    /// Appends <paramref name="value"/> as a new item. The item is renamed to the array's item
    /// name so the array stays an array: appending a <c>&lt;value&gt;</c> or a copied
    /// <c>&lt;customer&gt;</c> verbatim would give the wrapper children of mixed names, and
    /// <see cref="IsArray"/> would stop recognising it.
    /// </summary>
    public void AppendToArray(XElement array, XElement value) =>
        array.Add(Rename(value, ItemNameOf(array)));

    public void InsertIntoArray(XElement array, int index, XElement value)
    {
        var item = Rename(value, ItemNameOf(array));
        var elements = array.Elements().ToList();
        if (index >= elements.Count)
            array.Add(item);
        else
            elements[index].AddBeforeSelf(item);
    }

    public void RemoveFromArray(XElement array, int index) =>
        array.Elements().ElementAtOrDefault(index)?.Remove();

    public int GetArrayLength(XElement array) => array.Elements().Count();

    public XElement GetArrayElement(XElement array, int index) =>
        array.Elements().ElementAt(index);

    public IEnumerable<XElement> GetArrayElements(XElement array) => array.Elements();

    // ── Node creation ─────────────────────────────────────────────────────────
    //
    // Created nodes are placeholders: SetProperty, AppendToArray and Replace all re-hang the
    // content under the name the destination requires, so the name used here never survives
    // into the document.

    public XElement CreateNull() => new("value");
    public XElement CreateObject() => new("object");
    public XElement CreateArray() => new("array");
    public XElement CreateString(string value) => new("value", value);

    public XElement CreateNumber(double value) =>
        new("value", value.ToString(CultureInfo.InvariantCulture));

    public XElement CreateBoolean(bool value) =>
        new("value", value ? "true" : "false");

    public XElement CreateValue(object? value) => value switch
    {
        null      => CreateNull(),
        bool b    => CreateBoolean(b),
        double d  => CreateNumber(d),
        float f   => new XElement("value", f.ToString(CultureInfo.InvariantCulture)),
        decimal m => new XElement("value", m.ToString(CultureInfo.InvariantCulture)),
        XElement e => new XElement(e),
        _         => new XElement("value", Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty)
    };

    // ── Value access ──────────────────────────────────────────────────────────

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

    // ── Type coercion ─────────────────────────────────────────────────────────

    public bool? TryGetBoolean(XElement node)
    {
        if (IsNull(node)) return null;
        return bool.TryParse(node.Value, out var b) ? b : null;
    }

    public double? TryGetDouble(XElement node)
    {
        if (IsNull(node)) return null;
        return double.TryParse(node.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
            ? d : null;
    }

    public string? TryGetString(XElement node) => IsNull(node) ? null : node.Value;

    // ── Cloning & replacement ─────────────────────────────────────────────────

    public XElement DeepClone(XElement node) => new(node);

    /// <summary>
    /// Replaces the <i>value</i> of <paramref name="target"/> with the value of
    /// <paramref name="replacement"/>, keeping the target's own element name — in XML the name
    /// is the property name, and <c>set</c> changes a property's value, not its name.
    /// </summary>
    public void Replace(XElement target, XElement replacement)
    {
        var content = ContentOf(replacement);

        // The document element has an XDocument above it, so ReplaceWith would succeed and swap
        // it out — orphaning the reference the engine hands back to the caller. Rebuild its
        // contents in place instead so that reference stays the live document.
        if (target.Parent == null)
        {
            target.RemoveAll();
            target.Add(content);
            return;
        }

        target.ReplaceWith(new XElement(target.Name, content));
    }

    public bool RemoveFromParent(XElement node)
    {
        if (node.Parent == null) return false;
        node.Remove();
        return true;
    }

    /// <summary>
    /// An XElement carries its own name, so this is a true in-place rename: attributes,
    /// children and document position all survive, and no parent is required. That is what
    /// makes the document element renameable where a JSON root object is not.
    /// </summary>
    public bool RenameNode(XElement node, string newName)
    {
        if (!IsUsableElementName(newName)) return false;
        node.Name = node.Name.Namespace + newName;
        return true;
    }

    /// <summary>A detached copy of <paramref name="source"/>'s content under <paramref name="name"/>.</summary>
    private static XElement Rename(XElement source, string name) =>
        source.Name.LocalName == name && source.Parent == null
            ? source
            : new XElement(name, ContentOf(source));

    /// <summary>
    /// The child nodes of <paramref name="element"/>, detached from it. Attached nodes are
    /// cloned by the XElement constructor, so the source is never disturbed.
    /// </summary>
    private static object[] ContentOf(XElement element) => element.Nodes().Cast<object>().ToArray();

    // ── Deep merge ────────────────────────────────────────────────────────────

    /// <summary>
    /// Merges <paramref name="source"/> into <paramref name="target"/> with the JSON semantics:
    /// arrays combine as wholes, objects merge property by property, and a scalar overwrites.
    /// Arrays are handled before the property walk — matching array items by element name would
    /// fold every <c>&lt;item&gt;</c> onto the first one and lose the rest.
    /// </summary>
    public void DeepMergeInto(XElement source, XElement target,
        ArrayMergeMode arrayMergeMode = ArrayMergeMode.Concat)
    {
        if (IsArray(source) && IsArray(target))
        {
            MergeArrays(source, target, arrayMergeMode);
            return;
        }

        if (!source.HasElements)
        {
            // A scalar source replaces whatever the target held, structure included.
            target.RemoveAll();
            target.Add(ContentOf(source));
            return;
        }

        if (!IsObject(target))
        {
            // Object over a scalar (or over an array of a different shape): the source wins.
            target.RemoveAll();
            target.Add(ContentOf(source));
            return;
        }

        foreach (var srcChild in source.Elements())
        {
            var tgtChild = target.Element(srcChild.Name);
            if (tgtChild == null)
                target.Add(new XElement(srcChild));
            else
                DeepMergeInto(srcChild, tgtChild, arrayMergeMode);
        }
    }

    private static void MergeArrays(XElement source, XElement target, ArrayMergeMode arrayMergeMode)
    {
        if (arrayMergeMode == ArrayMergeMode.Replace)
            target.RemoveNodes();

        var itemName = target.Elements().Any() ? ItemNameOf(target) : ItemNameOf(source);
        foreach (var item in source.Elements().ToList())
            target.Add(Rename(item, itemName));
    }

    // ── Parent access ─────────────────────────────────────────────────────────

    public XElement? GetParentNode(XElement node) => node.Parent;

    /// <summary>
    /// The name a node is known by in its parent. An array item is addressed by position
    /// rather than by name, so it reports none — the same answer a JSON array element gives.
    /// </summary>
    public string? GetParentPropertyName(XElement node)
    {
        var parent = node.Parent;
        if (parent != null && IsArray(parent)) return null;
        return node.Name.LocalName;
    }

    // ── Equality ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Structural equality. <see cref="XNode.DeepEquals"/> compares the literal node trees, so
    /// it separates documents that differ only in insignificant whitespace or in whether an
    /// empty element was written <c>&lt;k/&gt;</c> or <c>&lt;k&gt;&lt;/k&gt;</c>.
    /// </summary>
    public bool DeepEquals(XElement a, XElement b)
    {
        if (a.Name != b.Name) return false;

        var aChildren = a.Elements().ToList();
        var bChildren = b.Elements().ToList();
        if (aChildren.Count != bChildren.Count) return false;

        if (aChildren.Count == 0)
            return string.Equals(a.Value.Trim(), b.Value.Trim(), StringComparison.Ordinal);

        for (var i = 0; i < aChildren.Count; i++)
            if (!DeepEquals(aChildren[i], bChildren[i])) return false;

        return true;
    }

    // ── Serialisation ─────────────────────────────────────────────────────────

    /// <summary>
    /// Parses into an element that is still attached to its XDocument, so the document node
    /// above it exists and absolute XPath (<c>/order/customer</c>) resolves the way XPath
    /// defines it. XElement.Parse would hand back a detached element whose only reachable
    /// context is itself, which is what made bare <c>customer</c> match the root's children.
    /// </summary>
    public XElement Parse(string content) => XDocument.Parse(content).Root!;

    public string Serialize(XElement node, bool pretty = false) =>
        pretty ? node.ToString(SaveOptions.None) : node.ToString(SaveOptions.DisableFormatting);
}
