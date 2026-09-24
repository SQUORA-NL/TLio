using System.Text.Json.Nodes;
using NUnit.Framework;
using TLio.Json.SystemText;

namespace TLio.Json.SystemText.Tests.SystemTextJsonPathItemsFetcherTests;

/// <summary>
/// GetPath computes each array-index / object-key step through a per-container cache
/// (SystemTextJsonPathItemsFetcher's FastPath / IndexOfCached / KeyOfCached) instead of
/// JsonNode.GetPath()'s own per-call scan, for performance at large array sizes — the
/// System.Text.Json sibling of TLio.Json.Tests' JsonPathFetcherTests.GetPath_* cache tests,
/// same reasoning, same shape of tests, adapted to a node model where a value's Parent is the
/// JsonObject directly (no intermediate JProperty).
/// </summary>
[TestFixture]
public class GetPathCacheTests
{
    private SystemTextJsonPathItemsFetcher _fetcher = null!;

    [SetUp]
    public void SetUp() => _fetcher = new SystemTextJsonPathItemsFetcher();

    [Test]
    public void GetPath_RootNodeReturnsDollar()
    {
        var data = JsonNode.Parse("""{"a":1}""")!;
        Assert.That(_fetcher.GetPath(data), Is.EqualTo("$"));
    }

    [Test]
    public void GetPath_ArrayElement_ExactIndexAtSeveralPositions()
    {
        var data = (JsonObject)JsonNode.Parse("""{"items":[10,20,30,40,50]}""")!;
        var items = data["items"]!.AsArray();
        for (var i = 0; i < 5; i++)
            Assert.That(_fetcher.GetPath(items[i]!), Is.EqualTo($"$.items[{i}]"));
    }

    [Test]
    public void GetPath_NestedPropertyInsideAnArrayElement()
    {
        var data = (JsonObject)JsonNode.Parse("""{"items":[{"name":"a"},{"name":"b"}]}""")!;
        var node = data["items"]![1]!["name"]!;
        Assert.That(_fetcher.GetPath(node), Is.EqualTo("$.items[1].name"));
    }

    [Test]
    public void GetPath_AfterArrayShrinks_ReflectsTheNewPositions_NotTheCachedOnes()
    {
        var data = (JsonObject)JsonNode.Parse("""{"items":[10,20,30,40,50]}""")!;
        var items = data["items"]!.AsArray();
        var last = items[4]!;
        Assert.That(_fetcher.GetPath(last), Is.EqualTo("$.items[4]"),
            "sanity check: the cache is now populated for this array's original shape");

        items.RemoveAt(0); // [20, 30, 40, 50] — every remaining element shifts down by one
        Assert.That(_fetcher.GetPath(last), Is.EqualTo("$.items[3]"),
            "the array's Count changed, so the cached index map must be rebuilt, not reused");
    }

    [Test]
    public void GetPath_AfterArrayGrows_NewElementGetsItsOwnIndex()
    {
        var data = (JsonObject)JsonNode.Parse("""{"items":[10,20,30]}""")!;
        var items = data["items"]!.AsArray();
        _ = _fetcher.GetPath(items[1]!); // populate the cache for the 3-element shape

        items.Add(40);
        Assert.That(_fetcher.GetPath(items[3]!), Is.EqualTo("$.items[3]"));
    }

    [Test]
    public void GetPath_AfterObjectGainsAKey_NewPropertyGetsItsOwnPath()
    {
        var data = (JsonObject)JsonNode.Parse("""{"a":1,"b":2}""")!;
        _ = _fetcher.GetPath(data["a"]!); // populate the key cache for the 2-property shape

        data["c"] = 3;
        Assert.That(_fetcher.GetPath(data["c"]!), Is.EqualTo("$.c"));
    }

    [Test]
    public void GetPath_ArrayWithNullElements_IndexesTheNonNullOnesCorrectly()
    {
        // System.Text.Json has no node for JSON null (see Internal/NullSlots.cs) — a raw C# null
        // sits in the array, and the index count still has to walk past it correctly.
        var data = (JsonObject)JsonNode.Parse("""{"items":[10,null,30,null,50]}""")!;
        var items = data["items"]!.AsArray();
        Assert.That(_fetcher.GetPath(items[2]!), Is.EqualTo("$.items[2]"));
        Assert.That(_fetcher.GetPath(items[4]!), Is.EqualTo("$.items[4]"));
    }

    [Test]
    public void GetPath_DetachedNode_DoesNotThrow()
    {
        var outer = (JsonObject)JsonNode.Parse("""{"myObject":{"inner":1}}""")!;
        var innerNode = outer["myObject"]!["inner"]!;
        outer.Remove("myObject"); // detaches "myObject" from `outer`

        Assert.That(() => _fetcher.GetPath(innerNode), Throws.Nothing);
    }
}
