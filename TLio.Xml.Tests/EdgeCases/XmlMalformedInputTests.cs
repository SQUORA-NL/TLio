using System.Xml;
using System.Xml.Linq;
using NUnit.Framework;
using TLio.Xml;

namespace TLio.Xml.Tests.EdgeCases;

[TestFixture]
public class XmlMalformedInputTests
{
    private XmlNodeAdapter _adapter = null!;
    private SlashPathItemsFetcher _fetcher = null!;

    [SetUp]
    public void SetUp()
    {
        _adapter = new XmlNodeAdapter();
        _fetcher = new SlashPathItemsFetcher();
    }

    [Test]
    public void Parse_InvalidXmlString_ThrowsXmlException()
    {
        Assert.Throws<XmlException>(() => _adapter.Parse("this is not xml"));
    }

    [Test]
    public void Parse_UnclosedTag_ThrowsXmlException()
    {
        Assert.Throws<XmlException>(() => _adapter.Parse("<root><child>text</root>"));
    }

    [Test]
    public void Parse_EmptyString_ThrowsXmlException()
    {
        Assert.Throws<XmlException>(() => _adapter.Parse(string.Empty));
    }

    [Test]
    public void Parse_NullString_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _adapter.Parse(null!));
    }

    [Test]
    public void SelectNodes_DeepPathOnEmptyRoot_ReturnsEmptyCollection()
    {
        var root = XElement.Parse("<root/>");
        var result = _fetcher.SelectNodes("/address/city", root);
        Assert.That(result.ToList(), Is.Empty);
    }

    [Test]
    public void SelectNodes_PathOnLeafElement_ReturnsEmptyCollection()
    {
        var root = XElement.Parse("<root><value>42</value></root>");
        var result = _fetcher.SelectNodes("/value/nonexistent", root);
        Assert.That(result.ToList(), Is.Empty);
    }

    [Test]
    public void GetProperty_OnNullAdapter_ReturnsNullForMissingChild()
    {
        var root = XElement.Parse("<root/>");
        var result = _adapter.GetProperty(root, "missing");
        Assert.That(result, Is.Null);
    }

    [Test]
    public void TryGetString_NilElement_ReturnsNull()
    {
        var nil = XElement.Parse("<value nil=\"true\"/>");
        Assert.That(_adapter.TryGetString(nil), Is.Null);
    }

    [Test]
    public void TryGetBoolean_NonBoolText_ReturnsNull()
    {
        var el = XElement.Parse("<flag>maybe</flag>");
        Assert.That(_adapter.TryGetBoolean(el), Is.Null);
    }

    [Test]
    public void IsNull_ElementWithNilFalseAttribute_ReturnsFalse()
    {
        var el = XElement.Parse("<value nil=\"false\">text</value>");
        Assert.That(_adapter.IsNull(el), Is.False);
    }

    [Test]
    public void GetArrayLength_NonArrayElement_ReturnsZero()
    {
        var el = XElement.Parse("<value>text</value>");
        Assert.That(_adapter.GetArrayLength(el), Is.EqualTo(0));
    }

    [Test]
    public void GetPropertyNames_LeafElement_ReturnsEmptyEnumerable()
    {
        var el = XElement.Parse("<value>text</value>");
        Assert.That(_adapter.GetPropertyNames(el).ToList(), Is.Empty);
    }
}
