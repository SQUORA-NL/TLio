using NUnit.Framework;
using TLio.Yaml;
using YamlDotNet.RepresentationModel;

namespace TLio.Yaml.Tests.Yaml;

/// <summary>
/// Tests for YamlPathItemsFetcher bracket-notation support.
/// Verifies that property names containing the dot delimiter (and other special chars)
/// can be addressed via $['key.name'] syntax.
/// </summary>
[TestFixture]
public class YamlPathEscapeTests
{
    private YamlParentTracker _tracker = null!;
    private YamlPathItemsFetcher _fetcher = null!;

    [SetUp]
    public void SetUp()
    {
        _tracker = new YamlParentTracker();
        _fetcher = new YamlPathItemsFetcher(_tracker);
    }

    private YamlMappingNode ParseMapping(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        return (YamlMappingNode)stream.Documents[0].RootNode;
    }

    // ── Bracket-quoted keys: single quotes ───────────────────────────────────

    [Test]
    public void SelectNodes_SingleQuotedBracket_PropertyWithDot_ReturnsNode()
    {
        var root = ParseMapping("\"server.host\": localhost");
        var result = _fetcher.SelectNodes("$['server.host']", root);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(((YamlScalarNode)result.First()).Value, Is.EqualTo("localhost"));
    }

    [Test]
    public void SelectNodes_SingleQuotedBracket_PropertyWithAt_ReturnsNode()
    {
        var root = ParseMapping("\"@type\": Person");
        var result = _fetcher.SelectNodes("$['@type']", root);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(((YamlScalarNode)result.First()).Value, Is.EqualTo("Person"));
    }

    [Test]
    public void SelectNodes_SingleQuotedBracket_PropertyWithDollar_ReturnsNode()
    {
        var root = ParseMapping("\"$ref\": \"#/User\"");
        var result = _fetcher.SelectNodes("$['$ref']", root);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(((YamlScalarNode)result.First()).Value, Is.EqualTo("#/User"));
    }

    // ── Bracket-quoted keys: double quotes ───────────────────────────────────

    [Test]
    public void SelectNodes_DoubleQuotedBracket_PropertyWithDot_ReturnsNode()
    {
        var root = ParseMapping("\"server.host\": localhost");
        var result = _fetcher.SelectNodes("$[\"server.host\"]", root);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(((YamlScalarNode)result.First()).Value, Is.EqualTo("localhost"));
    }

    // ── Nested bracket-notation ───────────────────────────────────────────────

    [Test]
    public void SelectNodes_NestedBracketKey_ReturnsCorrectNode()
    {
        var root = ParseMapping("config:\n  \"server.host\": localhost");
        var result = _fetcher.SelectNodes("$.config['server.host']", root);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(((YamlScalarNode)result.First()).Value, Is.EqualTo("localhost"));
    }

    // ── Regression: regular dot-notation still works ─────────────────────────

    [Test]
    public void SelectNodes_DotNotation_RegularKey_Unaffected()
    {
        var root = ParseMapping("name: Alice");
        var result = _fetcher.SelectNodes("$.name", root);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(((YamlScalarNode)result.First()).Value, Is.EqualTo("Alice"));
    }

    [Test]
    public void SelectNodes_ArrayIndex_Unaffected()
    {
        var root = ParseMapping("items:\n  - one\n  - two");
        var result = _fetcher.SelectNodes("$.items[1]", root);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(((YamlScalarNode)result.First()).Value, Is.EqualTo("two"));
    }

    [Test]
    public void SelectNodes_ArrayWildcard_Unaffected()
    {
        var root = ParseMapping("items:\n  - one\n  - two");
        var result = _fetcher.SelectNodes("$.items[*]", root);
        Assert.That(result.Count, Is.EqualTo(2));
    }
}
