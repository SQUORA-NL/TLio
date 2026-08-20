using System.Xml.Linq;
using System.Xml.XPath;
using NUnit.Framework;
using TLio.Mcp.Services;

namespace TLio.Mcp.Tests;

/// <summary>
/// ParseXml must hand back an element still attached to its XDocument. A detached element
/// (XElement.Parse) has no document node above it, so absolute paths like /order/customer —
/// the way every TLio XML path is written — silently match nothing.
/// </summary>
[TestFixture]
public sealed class DocumentServiceParseTests
{
    private DocumentService _sut = null!;

    [SetUp]
    public void SetUp() => _sut = new DocumentService();

    [Test]
    public void ParseXml_ReturnsElementAttachedToItsDocument()
    {
        var element = _sut.ParseXml("<order><customer>Ada</customer></order>");

        Assert.That(element.Document, Is.Not.Null,
            "the parsed element must keep the document node above it");
        Assert.That(element.Document!.Root, Is.SameAs(element));
    }

    [Test]
    public void ParseXml_AbsolutePath_ResolvesAgainstTheParsedElement()
    {
        var element = _sut.ParseXml("<order><customer>Ada</customer></order>");

        var matches = element.XPathSelectElements("/order/customer").ToList();

        Assert.That(matches, Has.Count.EqualTo(1),
            "/order/customer anchors on the document node, which only exists when the element is attached");
        Assert.That(matches[0].Value, Is.EqualTo("Ada"));
    }

    [Test]
    public void ParseXml_InvalidXml_ThrowsWithInvalidXmlMessage()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => _sut.ParseXml("<order>"));

        Assert.That(ex!.Message, Does.Contain("Invalid XML"));
    }
}
