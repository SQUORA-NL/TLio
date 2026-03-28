using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Json;

namespace TLio.Json.Tests.AdapterTests;

/// <summary>
/// Ported from JLio.UnitTests.PathTests, JLio.UnitTests.JsonPathMethodsTests,
/// and JLio.UnitTests.JsonPathMethodsEdgeCasesTests.
/// Adaptations:
///   - NUnit classic API → Assert.That() constraint model (NUnit 4)
///
/// Covers JSONPath patterns and edge cases not already in JsonPathFetcherTests,
/// including bracket notation, deep wildcard, complex filter expressions,
/// multi-level parent navigation, and unusual input shapes.
/// </summary>
[TestFixture]
public class JsonPathMethodsTests
{
    private JsonPathItemsFetcher _fetcher = null!;
    private JsonNodeAdapter      _adapter = null!;

    [SetUp]
    public void SetUp()
    {
        _fetcher = new JsonPathItemsFetcher();
        _adapter = new JsonNodeAdapter();
    }

    // ── Bracket notation ──────────────────────────────────────────────────────

    [Test]
    public void SelectNodes_BracketNotation_ReturnsProperty()
    {
        var data = JToken.Parse(@"{ ""my-key"": ""val"" }");
        var result = _fetcher.SelectNodes("$['my-key']", data);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].Value<string>(), Is.EqualTo("val"));
    }

    [Test]
    public void SelectNodes_BracketNotationNested_ReturnsNestedProperty()
    {
        var data = JToken.Parse(@"{ ""a"": { ""b"": 99 } }");
        var result = _fetcher.SelectNodes("$['a']['b']", data);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].Value<int>(), Is.EqualTo(99));
    }

    // ── Deep wildcard ─────────────────────────────────────────────────────────

    [Test]
    public void SelectNodes_DeepWildcard_MatchesAllValues()
    {
        var data = JToken.Parse(@"{ ""a"": 1, ""b"": 2 }");
        var result = _fetcher.SelectNodes("$.*", data);
        Assert.That(result.Count, Is.EqualTo(2));
    }

    [Test]
    public void SelectNodes_RecursiveDescent_FindsDeepNested()
    {
        var data = JToken.Parse(@"{
            ""level1"": {
                ""level2"": {
                    ""target"": 42
                }
            }
        }");
        var result = _fetcher.SelectNodes("$..target", data);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].Value<int>(), Is.EqualTo(42));
    }

    [Test]
    public void SelectNodes_RecursiveDescent_ReturnsAllOccurrences()
    {
        var data = JToken.Parse(@"{
            ""x"": 1,
            ""nested"": { ""x"": 2, ""deep"": { ""x"": 3 } }
        }");
        var result = _fetcher.SelectNodes("$..x", data);
        Assert.That(result.Count, Is.EqualTo(3));
    }

    // ── Array slice / multi-index ─────────────────────────────────────────────

    [Test]
    public void SelectNodes_ArrayMultipleIndices_ReturnsBothElements()
    {
        var data = JToken.Parse(@"{ ""arr"": [10, 20, 30, 40] }");
        var result = _fetcher.SelectNodes("$.arr[0, 2]", data);
        // Newtonsoft SelectTokens with comma-separated indices
        Assert.That(result.Count, Is.EqualTo(2));
    }

    [Test]
    public void SelectNodes_LastArrayElement_ReturnsLast()
    {
        var data = JToken.Parse(@"{ ""arr"": [10, 20, 30] }");
        var result = _fetcher.SelectNodes("$.arr[2]", data);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].Value<int>(), Is.EqualTo(30));
    }

    // ── Complex filter expressions ────────────────────────────────────────────

    [Test]
    public void SelectNodes_FilterStringEquality_MatchesCorrectItems()
    {
        var data = JToken.Parse(@"{
            ""items"": [
                { ""type"": ""A"", ""val"": 1 },
                { ""type"": ""B"", ""val"": 2 },
                { ""type"": ""A"", ""val"": 3 }
            ]
        }");
        var result = _fetcher.SelectNodes("$.items[?(@.type == 'A')]", data);
        Assert.That(result.Count, Is.EqualTo(2));
    }

    [Test]
    public void SelectNodes_FilterNegativeNumeric_ReturnsMatchingItems()
    {
        var data = JToken.Parse(@"{
            ""scores"": [
                { ""v"": 10 },
                { ""v"": -5 },
                { ""v"": 20 }
            ]
        }");
        var result = _fetcher.SelectNodes("$.scores[?(@.v < 0)]", data);
        Assert.That(result.Count, Is.EqualTo(1));
    }

    // ── Multi-level parent navigation ─────────────────────────────────────────

    [Test]
    public void ResolveRelativePath_TwoParentSteps_ReturnsGrandparentPath()
    {
        var data = JObject.Parse(@"{ ""a"": { ""b"": { ""c"": 1 } } }");
        var node = _fetcher.SelectNode("$.a.b.c", data)!;
        var resolved = _fetcher.ResolveRelativePath("@.<--.<--", node, data);
        Assert.That(resolved, Is.EqualTo("$.a"));
    }

    [Test]
    public void ResolveRelativePath_ParentThenChild_ReturnsSiblingPath()
    {
        var data = JObject.Parse(@"{ ""a"": { ""x"": 1, ""y"": 2 } }");
        var node = _fetcher.SelectNode("$.a.x", data)!;
        var resolved = _fetcher.ResolveRelativePath("@.<--.y", node, data);
        Assert.That(resolved, Is.EqualTo("$.a.y"));
    }

    // ── SplitParentAndLeaf edge cases ─────────────────────────────────────────

    [Test]
    public void SplitParentAndLeaf_WithWildcard_SplitsBeforeWildcard()
    {
        // "$.items[*]" is 2 path elements: ["$", "items[*]"] — bracket notation is a single segment
        var (parent, leaf) = _fetcher.SplitParentAndLeaf("$.items[*]");
        Assert.That(parent, Is.EqualTo("$"));
        Assert.That(leaf, Is.EqualTo("items[*]"));
    }

    [Test]
    public void SplitParentAndLeaf_WithBracketIndex_SplitsCorrectly()
    {
        // "$.arr[0]" is 2 path elements: ["$", "arr[0]"] — bracket notation is a single segment
        var (parent, leaf) = _fetcher.SplitParentAndLeaf("$.arr[0]");
        Assert.That(parent, Is.EqualTo("$"));
        Assert.That(leaf, Is.EqualTo("arr[0]"));
    }

    // ── Edge cases ────────────────────────────────────────────────────────────

    [Test]
    public void SelectNodes_EmptyArray_ReturnsNoItems()
    {
        var data = JToken.Parse(@"{ ""items"": [] }");
        var result = _fetcher.SelectNodes("$.items[*]", data);
        Assert.That(result.Count, Is.EqualTo(0));
    }

    [Test]
    public void SelectNodes_PathOnNullValue_ReturnsNoItems()
    {
        var data = JToken.Parse(@"{ ""x"": null }");
        var result = _fetcher.SelectNodes("$.x.missing", data);
        Assert.That(result.Count, Is.EqualTo(0));
    }

    [Test]
    public void EnsurePath_DeepArrayPath_CreatesObjectChain()
    {
        var data = JObject.Parse("{}");
        _fetcher.EnsurePath("$.a.b.c.d", data, _adapter);
        Assert.That(data.SelectToken("$.a.b.c"), Is.Not.Null);
    }

    [Test]
    public void GetPath_ArrayElementHasIndexedPath()
    {
        var data = JToken.Parse(@"{ ""list"": [{ ""id"": 1 }, { ""id"": 2 }] }");
        var second = _fetcher.SelectNode("$.list[1]", data)!;
        var path = _fetcher.GetPath(second);
        Assert.That(path, Does.Contain("[1]").Or.EndWith(".1"));
    }
}
