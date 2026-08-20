using System.Xml.Linq;
using NUnit.Framework;
using TLio.Commands;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Xml.Tests.Adapters;

/// <summary>
/// Writing through an array subscript in XML.
///
/// XPath disagrees with JSONPath on both halves of a subscript: it writes the position on the
/// item step rather than on the array (<c>/order/items/item[2]</c>, not <c>items[2]</c>) and it
/// counts from one. The fetcher normalises both so a command sees the same array path and the
/// same zero-based position it would see in JSON.
/// </summary>
[TestFixture]
public class XmlArrayIndexTests
{
    private IExecutionContext<XElement> _context = null!;

    [SetUp]
    public void SetUp() => _context = XmlExecutionContext.CreateWithSlashPaths();

    private XElement Doc(string xml) => _context.NodeAdapter.Parse(xml);

    private static string Xml(XElement e) => e.ToString(SaveOptions.DisableFormatting);

    private IFunctionSupportedValue<XElement> Value(string text) =>
        new FixedValue<XElement>(_context.NodeAdapter.CreateString(text));

    [Test]
    public void ThePositionIsNormalisedFromXPathsOneBasedCounting()
    {
        Assert.That(_context.ItemsFetcher.TrySplitArrayIndex(
            "/order/items/item[2]", out var arrayPath, out var index), Is.True);

        // The array is the step above the item, and item[2] is position 1.
        Assert.That(arrayPath, Is.EqualTo("/order/items"));
        Assert.That(index, Is.EqualTo(1));
    }

    [Test]
    public void APredicateIsNotAPosition()
    {
        Assert.That(_context.ItemsFetcher.IsLeafArrayIndex("/order/item[@id='1']"), Is.False);
    }

    [Test]
    public void SetWritesToTheElementAtThatPosition()
    {
        var data = Doc("<order><items><item>a</item><item>b</item></items></order>");

        new Set<XElement>("/order/items/item[2]", Value("Z")).Execute(data, _context);

        Assert.That(Xml(data),
            Is.EqualTo("<order><items><item>a</item><item>Z</item></items></order>"));
    }

    [Test]
    public void PutWritesToTheElementAtThatPosition()
    {
        var data = Doc("<order><items><item>a</item><item>b</item></items></order>");

        new Put<XElement>("/order/items/item[1]", Value("Z")).Execute(data, _context);

        Assert.That(Xml(data),
            Is.EqualTo("<order><items><item>Z</item><item>b</item></items></order>"));
    }

    [Test]
    public void AddAtTheNextFreePositionAppends()
    {
        var data = Doc("<order><items><item>a</item></items></order>");

        new Add<XElement>("/order/items/item[2]", Value("b")).Execute(data, _context);

        Assert.That(Xml(data),
            Is.EqualTo("<order><items><item>a</item><item>b</item></items></order>"));
    }

    [Test]
    public void AddAtAnOccupiedPositionIsSkipped()
    {
        var data = Doc("<order><items><item>a</item><item>b</item></items></order>");

        new Add<XElement>("/order/items/item[1]", Value("z")).Execute(data, _context);

        Assert.That(Xml(data),
            Is.EqualTo("<order><items><item>a</item><item>b</item></items></order>"));
    }

    [Test]
    public void AddCreatesAMissingArrayAndItsFirstItem()
    {
        // The created wrapper is empty, and an empty element is not yet distinguishable from an
        // empty object in XML — the item still has to land inside it.
        var data = Doc("<order><a>1</a></order>");

        new Add<XElement>("/order/tags/item[1]", Value("first")).Execute(data, _context);

        Assert.That(Xml(data),
            Is.EqualTo("<order><a>1</a><tags><item>first</item></tags></order>"));
    }

    [Test]
    public void FillingAnArrayInOrderWorksOneCommandAtATime()
    {
        var data = Doc("<order><a>1</a></order>");

        new Add<XElement>("/order/tags/item[1]", Value("frontend")).Execute(data, _context);
        new Add<XElement>("/order/tags/item[2]", Value("safari")).Execute(data, _context);
        new Add<XElement>("/order/tags/item[3]", Value("auth")).Execute(data, _context);

        Assert.That(Xml(data), Is.EqualTo(
            "<order><a>1</a><tags><item>frontend</item><item>safari</item><item>auth</item></tags></order>"));
    }
}
