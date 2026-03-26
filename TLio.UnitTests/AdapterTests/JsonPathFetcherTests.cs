using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Json;

namespace TLio.UnitTests.AdapterTests;

/// <summary>
/// Tests for JsonPathItemsFetcher — all IItemsFetcher&lt;JToken&gt; members.
/// Results are cross-checked against JLio's existing JsonPathMethodsTests behaviour.
/// Written post-implementation as an Article VI correction (Phase 2B).
/// </summary>
[TestFixture]
public class JsonPathFetcherTests
{
    private JsonPathItemsFetcher _fetcher = null!;
    private JsonNodeAdapter _adapter = null!;

    [SetUp]
    public void SetUp()
    {
        _fetcher = new JsonPathItemsFetcher();
        _adapter = new JsonNodeAdapter();
    }

    // ── Constants ─────────────────────────────────────────────────────────────

    [Test]
    public void RootPathIndicator_IsDollarSign() => Assert.That(_fetcher.RootPathIndicator, Is.EqualTo("$"));

    [Test]
    public void PathDelimiter_IsDot() => Assert.That(_fetcher.PathDelimiter, Is.EqualTo("."));

    [Test]
    public void CurrentItemPathIndicator_IsAt() => Assert.That(_fetcher.CurrentItemPathIndicator, Is.EqualTo("@"));

    [Test]
    public void ParentPathIndicator_IsArrowLeft() => Assert.That(_fetcher.ParentPathIndicator, Is.EqualTo("<--"));

    // ── SelectNodes ───────────────────────────────────────────────────────────

    [Test]
    public void SelectNodes_RootPath_ReturnsSingleRootNode()
    {
        var data = JToken.Parse("{\"a\": 1}");
        var result = _fetcher.SelectNodes("$", data);
        Assert.That(result.Count, Is.EqualTo(1));
    }

    [Test]
    public void SelectNodes_NestedPath_ReturnsMatchedNode()
    {
        var data = JToken.Parse("{\"person\": {\"name\": \"Alice\"}}");
        var result = _fetcher.SelectNodes("$.person.name", data);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].Value<string>(), Is.EqualTo("Alice"));
    }

    [Test]
    public void SelectNodes_ArrayIndex_ReturnsElement()
    {
        var data = JToken.Parse("{\"items\": [10, 20, 30]}");
        var result = _fetcher.SelectNodes("$.items[1]", data);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].Value<int>(), Is.EqualTo(20));
    }

    [Test]
    public void SelectNodes_Wildcard_ReturnsAllArrayElements()
    {
        var data = JToken.Parse("{\"items\": [1, 2, 3]}");
        var result = _fetcher.SelectNodes("$.items[*]", data);
        Assert.That(result.Count, Is.EqualTo(3));
    }

    [Test]
    public void SelectNodes_RecursiveDescent_FindsAllMatchingKeys()
    {
        var data = JToken.Parse("{\"a\": {\"id\": 1}, \"b\": {\"id\": 2}}");
        var result = _fetcher.SelectNodes("$..id", data);
        Assert.That(result.Count, Is.EqualTo(2));
    }

    [Test]
    public void SelectNodes_FilterExpression_FiltersCorrectly()
    {
        var data = JToken.Parse("{\"items\": [{\"v\": 1}, {\"v\": 5}, {\"v\": 3}]}");
        var result = _fetcher.SelectNodes("$.items[?(@.v > 2)]", data);
        Assert.That(result.Count, Is.EqualTo(2));
    }

    [Test]
    public void SelectNodes_NoMatch_ReturnsEmptyList()
    {
        var data = JToken.Parse("{\"a\": 1}");
        var result = _fetcher.SelectNodes("$.nonexistent", data);
        Assert.That(result.Count, Is.EqualTo(0));
    }

    // ── SelectNode ────────────────────────────────────────────────────────────

    [Test]
    public void SelectNode_ReturnsFirstMatch()
    {
        var data = JToken.Parse("{\"x\": 42}");
        var node = _fetcher.SelectNode("$.x", data);
        Assert.That(node?.Value<int>(), Is.EqualTo(42));
    }

    [Test]
    public void SelectNode_ReturnsNullForNoMatch()
    {
        var data = JToken.Parse("{\"x\": 42}");
        Assert.That(_fetcher.SelectNode("$.missing", data), Is.Null);
    }

    // ── GetPath ───────────────────────────────────────────────────────────────

    [Test]
    public void GetPath_RootNodeReturnsDollar()
    {
        var data = JToken.Parse("{\"a\": 1}");
        Assert.That(_fetcher.GetPath(data), Is.EqualTo("$"));
    }

    [Test]
    public void GetPath_NestedNodeReturnsFullPath()
    {
        var data = JToken.Parse("{\"person\": {\"name\": \"Bob\"}}");
        var node = _fetcher.SelectNode("$.person.name", data)!;
        Assert.That(_fetcher.GetPath(node), Is.EqualTo("$.person.name"));
    }

    [Test]
    public void GetPath_ArrayElementReturnsIndexedPath()
    {
        var data = JToken.Parse("{\"items\": [\"a\", \"b\"]}");
        var node = _fetcher.SelectNode("$.items[0]", data)!;
        Assert.That(_fetcher.GetPath(node), Does.Contain("items"));
    }

    // ── GetParent ─────────────────────────────────────────────────────────────

    [Test]
    public void GetParent_OneLevelUp_ReturnsImmediateSemanticParent()
    {
        var data = JObject.Parse("{\"parent\": {\"child\": 1}}");
        var child = _fetcher.SelectNode("$.parent.child", data)!;
        var parent = _fetcher.GetParent(child, 1);
        Assert.That(JToken.DeepEquals(parent, data["parent"]), Is.True);
    }

    [Test]
    public void GetParent_TwoLevelsUp_SkipsIntermediate()
    {
        var data = JObject.Parse("{\"a\": {\"b\": {\"c\": 1}}}");
        var node = _fetcher.SelectNode("$.a.b.c", data)!;
        var grandParent = _fetcher.GetParent(node, 2);
        Assert.That(JToken.DeepEquals(grandParent, data["a"]), Is.True);
    }

    [Test]
    public void GetParent_ArrayElement_ReturnsContainingObject()
    {
        var data = JObject.Parse("{\"items\": [1, 2, 3]}");
        var element = _fetcher.SelectNode("$.items[0]", data)!;
        var parent = _fetcher.GetParent(element, 1);
        // array element → skip JArray + JProperty → owning JObject
        Assert.That(JToken.DeepEquals(parent, data), Is.True);
    }

    [Test]
    public void GetParent_ZeroLevels_ReturnsSameNode()
    {
        var data = JObject.Parse("{\"a\": 1}");
        var node = _fetcher.SelectNode("$.a", data)!;
        var result = _fetcher.GetParent(node, 0);
        Assert.That(result, Is.SameAs(node));
    }

    // ── ResolveRelativePath ───────────────────────────────────────────────────

    [Test]
    public void ResolveRelativePath_AtOnly_ReturnsNodePath()
    {
        var data = JObject.Parse("{\"x\": {\"y\": 1}}");
        var node = _fetcher.SelectNode("$.x.y", data)!;
        var resolved = _fetcher.ResolveRelativePath("@", node, data);
        Assert.That(resolved, Is.EqualTo("$.x.y"));
    }

    [Test]
    public void ResolveRelativePath_AtDotProp_AppendsToCurrentPath()
    {
        var data = JObject.Parse("{\"x\": {\"y\": 1}}");
        var node = _fetcher.SelectNode("$.x", data)!;
        var resolved = _fetcher.ResolveRelativePath("@.y", node, data);
        Assert.That(resolved, Is.EqualTo("$.x.y"));
    }

    [Test]
    public void ResolveRelativePath_AtParent_ReturnsParentPath()
    {
        var data = JObject.Parse("{\"a\": {\"b\": 1}}");
        var node = _fetcher.SelectNode("$.a.b", data)!;
        var resolved = _fetcher.ResolveRelativePath("@.<--", node, data);
        Assert.That(resolved, Is.EqualTo("$.a"));
    }

    [Test]
    public void ResolveRelativePath_AbsolutePath_ReturnedAsIs()
    {
        var data = JObject.Parse("{\"a\": 1}");
        var node = _fetcher.SelectNode("$.a", data)!;
        var resolved = _fetcher.ResolveRelativePath("$.other.path", node, data);
        Assert.That(resolved, Is.EqualTo("$.other.path"));
    }

    // ── EnsurePath ────────────────────────────────────────────────────────────

    [Test]
    public void EnsurePath_CreatesIntermediateObjects()
    {
        var data = JObject.Parse("{}");
        _fetcher.EnsurePath("$.a.b.c", data, _adapter);
        Assert.That(data.SelectToken("$.a.b"), Is.Not.Null);
    }

    [Test]
    public void EnsurePath_DoesNotOverwriteExistingObject()
    {
        var data = JObject.Parse("{\"a\": {\"existing\": 99}}");
        _fetcher.EnsurePath("$.a.b", data, _adapter);
        Assert.That(data["a"]!["existing"]!.Value<int>(), Is.EqualTo(99));
    }

    // ── SplitParentAndLeaf ────────────────────────────────────────────────────

    [Test]
    public void SplitParentAndLeaf_SimpleThreeLevel()
    {
        var (parent, leaf) = _fetcher.SplitParentAndLeaf("$.a.b.c");
        Assert.That(leaf, Is.EqualTo("c"));
        Assert.That(parent, Does.Contain("a"));
        Assert.That(parent, Does.Contain("b"));
    }

    [Test]
    public void SplitParentAndLeaf_TwoLevel()
    {
        var (parent, leaf) = _fetcher.SplitParentAndLeaf("$.person.name");
        Assert.That(leaf, Is.EqualTo("name"));
        Assert.That(parent, Is.EqualTo("$.person"));
    }

    [Test]
    public void SplitParentAndLeaf_SingleSegment_ReturnsRootAndSegment()
    {
        var (parent, leaf) = _fetcher.SplitParentAndLeaf("$.name");
        Assert.That(parent, Is.EqualTo("$"));
        Assert.That(leaf, Is.EqualTo("name"));
    }

    // ── ProcessIndirectPath ───────────────────────────────────────────────────

    [Test]
    public void ProcessIndirectPath_NoIndirect_ReturnsOriginalPath()
    {
        var data = JToken.Parse("{}");
        var result = _fetcher.ProcessIndirectPath("$.a.b", data);
        Assert.That(result, Is.EqualTo("$.a.b"));
    }

    [Test]
    public void ProcessIndirectPath_ResolvesIndirectToStoredPath()
    {
        var data = JToken.Parse("{\"pathRef\": \"$.target.value\"}");
        var result = _fetcher.ProcessIndirectPath("=indirect($.pathRef)", data);
        Assert.That(result, Is.EqualTo("$.target.value"));
    }

    [Test]
    public void ProcessIndirectPath_ReturnsNullForMissingReference()
    {
        var data = JToken.Parse("{}");
        var result = _fetcher.ProcessIndirectPath("=indirect($.missing)", data);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ProcessIndirectPath_ResolvesQuotedPath()
    {
        var data = JToken.Parse("{\"ref\": \"$.x\"}");
        var result = _fetcher.ProcessIndirectPath("=indirect('$.ref')", data);
        Assert.That(result, Is.EqualTo("$.x"));
    }

    // ── GetIntellisense ───────────────────────────────────────────────────────

    [Test]
    public void GetIntellisense_RootObject_ReturnsDotProperties()
    {
        var data = JToken.Parse("{\"name\": \"Alice\", \"age\": 30}");
        var suggestions = _fetcher.GetIntellisense("$", data).ToList();
        Assert.That(suggestions, Has.Some.Contains("name"));
        Assert.That(suggestions, Has.Some.Contains("age"));
    }

    [Test]
    public void GetIntellisense_Array_ReturnsWildcardPath()
    {
        var data = JToken.Parse("{\"items\": [1, 2, 3]}");
        var suggestions = _fetcher.GetIntellisense("$.items", data).ToList();
        Assert.That(suggestions, Has.Some.Contains("[*]"));
    }

    [Test]
    public void GetIntellisense_EmptyPath_ReturnsRootChildren()
    {
        var data = JToken.Parse("{\"a\": 1, \"b\": 2}");
        var suggestions = _fetcher.GetIntellisense("", data).ToList();
        Assert.That(suggestions.Count, Is.GreaterThan(0));
    }
}
