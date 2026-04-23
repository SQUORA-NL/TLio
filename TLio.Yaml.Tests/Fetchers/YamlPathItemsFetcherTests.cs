using NUnit.Framework;
using TLio.Yaml;
using YamlDotNet.RepresentationModel;

namespace TLio.Yaml.Tests.Fetchers;

[TestFixture]
public class YamlPathItemsFetcherTests
{
    private YamlParentTracker _tracker = null!;
    private YamlPathItemsFetcher _fetcher = null!;
    private YamlMappingNode _doc = null!;

    [SetUp]
    public void SetUp()
    {
        _tracker = new YamlParentTracker();
        _fetcher = new YamlPathItemsFetcher(_tracker);

        // Build: { address: { city: Amsterdam, country: NL }, score: 42, items: [a, b, c] }
        var items = new YamlSequenceNode(
            new YamlScalarNode("a"),
            new YamlScalarNode("b"),
            new YamlScalarNode("c"));
        var address = new YamlMappingNode
        {
            { "city", new YamlScalarNode("Amsterdam") },
            { "country", new YamlScalarNode("NL") }
        };
        _doc = new YamlMappingNode
        {
            { "address", address },
            { "score", new YamlScalarNode("42") },
            { "items", items }
        };
    }

    [Test]
    public void SelectNodes_RootPath_ReturnsRoot()
    {
        var result = _fetcher.SelectNodes("$", _doc);
        Assert.That(result.ToList(), Has.Count.EqualTo(1));
        Assert.That(result.First(), Is.SameAs(_doc));
    }

    [Test]
    public void SelectNodes_EmptyPath_ReturnsRoot()
    {
        var result = _fetcher.SelectNodes(string.Empty, _doc);
        Assert.That(result.ToList(), Has.Count.EqualTo(1));
    }

    [Test]
    public void SelectNodes_SingleKey_ReturnsValue()
    {
        var result = _fetcher.SelectNodes("$.score", _doc);
        var list = result.ToList();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(((YamlScalarNode)list[0]).Value, Is.EqualTo("42"));
    }

    [Test]
    public void SelectNodes_DotNotationDeepPath_ReturnsNestedValue()
    {
        var result = _fetcher.SelectNodes("$.address.city", _doc);
        var list = result.ToList();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(((YamlScalarNode)list[0]).Value, Is.EqualTo("Amsterdam"));
    }

    [Test]
    public void SelectNodes_ArrayIndex_ReturnsCorrectElement()
    {
        var result = _fetcher.SelectNodes("$.items[1]", _doc);
        var list = result.ToList();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(((YamlScalarNode)list[0]).Value, Is.EqualTo("b"));
    }

    [Test]
    public void SelectNodes_WildcardIndex_ReturnsAllElements()
    {
        var result = _fetcher.SelectNodes("$.items[*]", _doc);
        Assert.That(result.ToList(), Has.Count.EqualTo(3));
    }

    [Test]
    public void SelectNodes_NonExistentKey_ReturnsEmptyCollection()
    {
        var result = _fetcher.SelectNodes("$.missing", _doc);
        Assert.That(result.ToList(), Is.Empty);
    }

    [Test]
    public void SelectNodes_DeepNonExistent_ReturnsEmptyCollection()
    {
        var result = _fetcher.SelectNodes("$.address.zip", _doc);
        Assert.That(result.ToList(), Is.Empty);
    }

    [Test]
    public void SelectNode_ExistingPath_ReturnsSingleNode()
    {
        var node = _fetcher.SelectNode("$.address.country", _doc);
        Assert.That(node, Is.Not.Null);
        Assert.That(((YamlScalarNode)node!).Value, Is.EqualTo("NL"));
    }

    [Test]
    public void SelectNode_NonExistent_ReturnsNull()
    {
        var node = _fetcher.SelectNode("$.nonexistent", _doc);
        Assert.That(node, Is.Null);
    }

    [Test]
    public void SelectNodes_BracketQuotedKey_ReturnsValue()
    {
        // Key with special character using bracket-quote syntax
        var doc = new YamlMappingNode { { "simple", new YamlScalarNode("value") } };
        var result = _fetcher.SelectNodes("$['simple']", doc);
        var list = result.ToList();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(((YamlScalarNode)list[0]).Value, Is.EqualTo("value"));
    }

    [Test]
    public void SelectNodes_EmptyMapping_ReturnsEmptyForAnyKey()
    {
        var emptyDoc = new YamlMappingNode();
        var result = _fetcher.SelectNodes("$.anything", emptyDoc);
        Assert.That(result.ToList(), Is.Empty);
    }
}
