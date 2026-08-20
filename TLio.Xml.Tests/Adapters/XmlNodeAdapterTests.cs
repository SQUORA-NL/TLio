using System.Xml.Linq;
using NUnit.Framework;
using TLio.Xml;

namespace TLio.Xml.Tests.Adapters;

[TestFixture]
public class XmlNodeAdapterTests
{
    private XmlNodeAdapter _adapter = null!;

    [SetUp]
    public void SetUp() => _adapter = new XmlNodeAdapter();

    // ── Type queries ──────────────────────────────────────────────────────────

    [Test]
    public void IsObject_ElementWithChildren_ReturnsTrue()
    {
        var el = XElement.Parse("<root><child>x</child></root>");
        Assert.That(_adapter.IsObject(el), Is.True);
    }

    [Test]
    public void IsObject_LeafElement_ReturnsFalse()
    {
        var el = XElement.Parse("<value>42</value>");
        Assert.That(_adapter.IsObject(el), Is.False);
    }

    [Test]
    public void IsArray_AllChildrenSameName_ReturnsTrue()
    {
        var el = XElement.Parse("<items><item>a</item><item>b</item></items>");
        Assert.That(_adapter.IsArray(el), Is.True);
    }

    [Test]
    public void IsArray_ChildrenWithDifferentNames_ReturnsFalse()
    {
        var el = XElement.Parse("<root><name>x</name><age>1</age></root>");
        Assert.That(_adapter.IsArray(el), Is.False);
    }

    [Test]
    public void IsPrimitive_LeafElement_ReturnsTrue()
    {
        var el = XElement.Parse("<value>hello</value>");
        Assert.That(_adapter.IsPrimitive(el), Is.True);
    }

    [Test]
    public void IsNull_ElementWithNilAttribute_ReturnsTrue()
    {
        // Not because of the attribute — attributes are outside the JSON data model and the
        // adapter ignores them. The element is null because it carries no content at all.
        var el = XElement.Parse("<value nil=\"true\"/>");
        Assert.That(_adapter.IsNull(el), Is.True);
    }

    [Test]
    public void IsNull_EmptyElement_ReturnsTrue()
    {
        var el = XElement.Parse("<value/>");
        Assert.That(_adapter.IsNull(el), Is.True);
    }

    [Test]
    public void IsNull_ElementWithText_ReturnsFalse()
    {
        var el = XElement.Parse("<value>hello</value>");
        Assert.That(_adapter.IsNull(el), Is.False);
    }

    // ── Property access ───────────────────────────────────────────────────────

    [Test]
    public void GetProperty_ExistingChild_ReturnsChild()
    {
        var el = XElement.Parse("<root><city>Amsterdam</city></root>");
        var city = _adapter.GetProperty(el, "city");
        Assert.That(city, Is.Not.Null);
        Assert.That(city!.Value, Is.EqualTo("Amsterdam"));
    }

    [Test]
    public void GetProperty_MissingChild_ReturnsNull()
    {
        var el = XElement.Parse("<root><city>Amsterdam</city></root>");
        var result = _adapter.GetProperty(el, "country");
        Assert.That(result, Is.Null);
    }

    [Test]
    public void HasProperty_ExistingChild_ReturnsTrue()
    {
        var el = XElement.Parse("<root><name>Alice</name></root>");
        Assert.That(_adapter.HasProperty(el, "name"), Is.True);
    }

    [Test]
    public void HasProperty_MissingChild_ReturnsFalse()
    {
        var el = XElement.Parse("<root><name>Alice</name></root>");
        Assert.That(_adapter.HasProperty(el, "age"), Is.False);
    }

    [Test]
    public void GetPropertyNames_ReturnsAllChildNames()
    {
        var el = XElement.Parse("<root><a>1</a><b>2</b><c>3</c></root>");
        var names = _adapter.GetPropertyNames(el).ToList();
        Assert.That(names, Is.EquivalentTo(new[] { "a", "b", "c" }));
    }

    // ── Value access ──────────────────────────────────────────────────────────

    [Test]
    public void TryGetString_TextNode_ReturnsText()
    {
        var el = XElement.Parse("<value>hello</value>");
        Assert.That(_adapter.TryGetString(el), Is.EqualTo("hello"));
    }

    [Test]
    public void TryGetBoolean_TrueText_ReturnsTrue()
    {
        var el = XElement.Parse("<flag>true</flag>");
        Assert.That(_adapter.TryGetBoolean(el), Is.True);
    }

    [Test]
    public void TryGetBoolean_FalseText_ReturnsFalse()
    {
        var el = XElement.Parse("<flag>false</flag>");
        Assert.That(_adapter.TryGetBoolean(el), Is.False);
    }

    [Test]
    public void TryGetDouble_NumericText_ReturnsParsedValue()
    {
        var el = XElement.Parse("<price>3.14</price>");
        Assert.That(_adapter.TryGetDouble(el), Is.EqualTo(3.14).Within(0.0001));
    }

    [Test]
    public void TryGetDouble_NonNumericText_ReturnsNull()
    {
        var el = XElement.Parse("<value>notanumber</value>");
        Assert.That(_adapter.TryGetDouble(el), Is.Null);
    }

    // ── Clone & equality ──────────────────────────────────────────────────────

    [Test]
    public void DeepClone_ProducesEqualButDistinctNode()
    {
        var original = XElement.Parse("<root><name>Alice</name></root>");
        var clone = _adapter.DeepClone(original);
        Assert.That(clone, Is.Not.SameAs(original));
        Assert.That(_adapter.DeepEquals(original, clone), Is.True);
    }

    [Test]
    public void DeepClone_MutatingCloneDoesNotAffectOriginal()
    {
        var original = XElement.Parse("<root><name>Alice</name></root>");
        var clone = _adapter.DeepClone(original);
        clone.Element("name")!.Value = "Bob";
        Assert.That(original.Element("name")!.Value, Is.EqualTo("Alice"));
    }

    // ── Array operations ──────────────────────────────────────────────────────

    [Test]
    public void GetArrayLength_CountsChildElements()
    {
        var el = XElement.Parse("<items><item>a</item><item>b</item><item>c</item></items>");
        Assert.That(_adapter.GetArrayLength(el), Is.EqualTo(3));
    }

    [Test]
    public void GetArrayElement_ReturnsCorrectElement()
    {
        var el = XElement.Parse("<items><item>first</item><item>second</item></items>");
        var second = _adapter.GetArrayElement(el, 1);
        Assert.That(second.Value, Is.EqualTo("second"));
    }

    // ── Serialisation ─────────────────────────────────────────────────────────

    [Test]
    public void Parse_ValidXml_ReturnsParsedElement()
    {
        var el = _adapter.Parse("<root><city>Paris</city></root>");
        Assert.That(el.Name.LocalName, Is.EqualTo("root"));
        Assert.That(el.Element("city")!.Value, Is.EqualTo("Paris"));
    }

    [Test]
    public void Parse_InvalidXml_ThrowsException()
    {
        Assert.Throws<System.Xml.XmlException>(() => _adapter.Parse("not xml at all"));
    }
}
