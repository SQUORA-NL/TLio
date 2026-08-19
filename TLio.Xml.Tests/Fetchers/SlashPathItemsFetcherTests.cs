using System.Xml.Linq;
using NUnit.Framework;
using TLio.Xml;

namespace TLio.Xml.Tests.Fetchers;

/// <summary>
/// The slash fetcher is the simple-hierarchy subset of XPath and anchors identically:
/// <c>/</c> is the document node and <c>/root</c> the document element, so every path
/// starts with the document element's own name.
/// </summary>
[TestFixture]
public class SlashPathItemsFetcherTests
{
    private SlashPathItemsFetcher _fetcher = null!;
    private XElement _doc = null!;

    [SetUp]
    public void SetUp()
    {
        _fetcher = new SlashPathItemsFetcher();
        // Parsed through the adapter so the document node above the element exists.
        _doc = new XmlNodeAdapter().Parse(
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
    public void SelectNodes_RootPath_SelectsNothingBecauseTheDocumentNodeIsNotAnElement()
    {
        // "/" names the document node, which has no element to hand back. Addressing the
        // document element means naming it: "/root".
        Assert.That(_fetcher.SelectNodes("/", _doc).ToList(), Is.Empty);
    }

    [Test]
    public void SelectNodes_EmptyPath_ReturnsRootElement()
    {
        var result = _fetcher.SelectNodes("", _doc);
        Assert.That(result.ToList(), Has.Count.EqualTo(1));
    }

    [Test]
    public void SelectNodes_DocumentElement_ReturnsRoot()
    {
        var result = _fetcher.SelectNodes("/root", _doc);
        var list = result.ToList();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].Name.LocalName, Is.EqualTo("root"));
    }

    [Test]
    public void SelectNodes_SingleSegment_ReturnsMatchingChild()
    {
        var result = _fetcher.SelectNodes("/root/address", _doc);
        var list = result.ToList();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].Name.LocalName, Is.EqualTo("address"));
    }

    [Test]
    public void SelectNodes_DeepPath_ReturnsDeepChild()
    {
        var result = _fetcher.SelectNodes("/root/address/city", _doc);
        var list = result.ToList();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].Value, Is.EqualTo("Amsterdam"));
    }

    [Test]
    public void SelectNodes_RepeatedChildName_ReturnsAllMatches()
    {
        var result = _fetcher.SelectNodes("/root/items/item", _doc);
        var list = result.ToList();
        Assert.That(list, Has.Count.EqualTo(3));
        Assert.That(list.Select(e => e.Value), Is.EquivalentTo(new[] { "alpha", "beta", "gamma" }));
    }

    [Test]
    public void SelectNodes_NonExistentPath_ReturnsEmptyCollection()
    {
        var result = _fetcher.SelectNodes("/root/nonexistent/child", _doc);
        Assert.That(result.ToList(), Is.Empty);
    }

    [Test]
    public void SelectNodes_WildcardStar_ReturnsAllChildren()
    {
        var result = _fetcher.SelectNodes("/root/items/*", _doc);
        Assert.That(result.ToList(), Has.Count.EqualTo(3));
    }

    [Test]
    public void SelectNode_SingleResult_ReturnsSingleElement()
    {
        var node = _fetcher.SelectNode("/root/score", _doc);
        Assert.That(node, Is.Not.Null);
        Assert.That(node!.Value, Is.EqualTo("42"));
    }

    [Test]
    public void SelectNode_NonExistent_ReturnsNull()
    {
        var node = _fetcher.SelectNode("/root/missing", _doc);
        Assert.That(node, Is.Null);
    }

    [Test]
    public void GetPath_NestedElement_IncludesTheDocumentElement()
    {
        var city = _doc.Element("address")!.Element("city")!;
        Assert.That(_fetcher.GetPath(city), Is.EqualTo("/root/address/city"));
    }

    [Test]
    public void SelectNodes_PathThatSkipsTheDocumentElement_MatchesNothing()
    {
        // '/score' used to work, because the leading slash was stripped and the path
        // evaluated against <root>. It now means "a score child of the document node".
        Assert.That(_fetcher.SelectNodes("/score", _doc).ToList(), Is.Empty);
        Assert.That(_fetcher.SelectNodes("score", _doc).ToList(), Is.Empty);
    }
}
