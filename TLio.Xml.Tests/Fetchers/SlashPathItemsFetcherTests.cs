using System.Xml.Linq;
using NUnit.Framework;
using TLio.Xml;

namespace TLio.Xml.Tests.Fetchers;

[TestFixture]
public class SlashPathItemsFetcherTests
{
    private SlashPathItemsFetcher _fetcher = null!;
    private XElement _doc = null!;

    [SetUp]
    public void SetUp()
    {
        _fetcher = new SlashPathItemsFetcher();
        _doc = XElement.Parse(
            """
            <root>
              <address>
                <city>Amsterdam</city>
                <country>NL</country>
              </address>
              <items>
                <item>alpha</item>
                <item>beta</item>
                <item>gamma</item>
              </items>
              <score>42</score>
            </root>
            """);
    }

    [Test]
    public void SelectNodes_RootPath_ReturnsRootElement()
    {
        var result = _fetcher.SelectNodes("/", _doc);
        Assert.That(result.ToList(), Has.Count.EqualTo(1));
        Assert.That(result.First().Name.LocalName, Is.EqualTo("root"));
    }

    [Test]
    public void SelectNodes_EmptyPath_ReturnsRootElement()
    {
        var result = _fetcher.SelectNodes("", _doc);
        Assert.That(result.ToList(), Has.Count.EqualTo(1));
    }

    [Test]
    public void SelectNodes_SingleSegment_ReturnsMatchingChild()
    {
        var result = _fetcher.SelectNodes("/address", _doc);
        var list = result.ToList();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].Name.LocalName, Is.EqualTo("address"));
    }

    [Test]
    public void SelectNodes_DeepPath_ReturnsDeepChild()
    {
        var result = _fetcher.SelectNodes("/address/city", _doc);
        var list = result.ToList();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].Value, Is.EqualTo("Amsterdam"));
    }

    [Test]
    public void SelectNodes_RepeatedChildName_ReturnsAllMatches()
    {
        var result = _fetcher.SelectNodes("/items/item", _doc);
        var list = result.ToList();
        Assert.That(list, Has.Count.EqualTo(3));
        Assert.That(list.Select(e => e.Value), Is.EquivalentTo(new[] { "alpha", "beta", "gamma" }));
    }

    [Test]
    public void SelectNodes_NonExistentPath_ReturnsEmptyCollection()
    {
        var result = _fetcher.SelectNodes("/nonexistent/child", _doc);
        Assert.That(result.ToList(), Is.Empty);
    }

    [Test]
    public void SelectNodes_WildcardStar_ReturnsAllChildren()
    {
        var result = _fetcher.SelectNodes("/items/*", _doc);
        Assert.That(result.ToList(), Has.Count.EqualTo(3));
    }

    [Test]
    public void SelectNode_SingleResult_ReturnsSingleElement()
    {
        var node = _fetcher.SelectNode("/score", _doc);
        Assert.That(node, Is.Not.Null);
        Assert.That(node!.Value, Is.EqualTo("42"));
    }

    [Test]
    public void SelectNode_NonExistent_ReturnsNull()
    {
        var node = _fetcher.SelectNode("/missing", _doc);
        Assert.That(node, Is.Null);
    }

    [Test]
    public void GetPath_NestedElement_ReturnsCorrctSlashPath()
    {
        var city = _doc.Element("address")!.Element("city")!;
        Assert.That(_fetcher.GetPath(city), Is.EqualTo("/address/city"));
    }

    [Test]
    public void SelectNodes_PathWithoutLeadingSlash_TreatedAsRelativeXPath()
    {
        var result = _fetcher.SelectNodes("score", _doc);
        var list = result.ToList();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].Value, Is.EqualTo("42"));
    }
}
