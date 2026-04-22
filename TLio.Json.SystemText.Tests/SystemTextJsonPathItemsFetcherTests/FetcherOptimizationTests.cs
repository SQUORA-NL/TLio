using System.Text.Json.Nodes;
using NUnit.Framework;
using TLio.Json.SystemText;

namespace TLio.Json.SystemText.Tests.SystemTextJsonPathItemsFetcherTests;

/// <summary>
/// Verifies that the fetcher does not call JsonDocument.Parse more than once when the same
/// unmodified node is passed to consecutive SelectNodes calls (SC-003).
/// Uses the internal ParseCount counter added for test observability.
/// </summary>
[TestFixture]
public class FetcherOptimizationTests
{
    [Test]
    public void ConsecutiveSelectionsOnUnchangedNode_ParseDocumentOnce()
    {
        var fetcher = new SystemTextJsonPathItemsFetcher();
        var node    = JsonNode.Parse("""{"a":1,"b":2,"c":3}""")!;

        for (var i = 0; i < 20; i++)
            fetcher.SelectNodes("$.a", node);

        Assert.That(fetcher.ParseCount, Is.EqualTo(1),
            "JsonDocument.Parse should be called exactly once for 20 consecutive selections on an unchanged node.");
    }

    [Test]
    public void SelectionAfterMutation_RebuildsCachedDocument()
    {
        var fetcher = new SystemTextJsonPathItemsFetcher();
        var root    = JsonNode.Parse("""{"items":[1,2,3]}""")!;

        fetcher.SelectNodes("$.items", root);
        Assert.That(fetcher.ParseCount, Is.EqualTo(1), "First selection should parse once.");

        // Simulate a mutation: add an element
        root.AsObject()["items"]!.AsArray().Add(4);

        fetcher.SelectNodes("$.items", root);
        Assert.That(fetcher.ParseCount, Is.EqualTo(2),
            "Mutation changes serialized content → cache miss → second parse required.");
    }

    [Test]
    public void SelectionWithDifferentRoot_ParsesForEachUniqueContent()
    {
        var fetcher = new SystemTextJsonPathItemsFetcher();
        var nodeA   = JsonNode.Parse("""{"x":1}""")!;
        var nodeB   = JsonNode.Parse("""{"x":2}""")!;

        fetcher.SelectNodes("$.x", nodeA);
        fetcher.SelectNodes("$.x", nodeA);  // same content → reuse
        fetcher.SelectNodes("$.x", nodeB);  // different content → rebuild

        Assert.That(fetcher.ParseCount, Is.EqualTo(2),
            "Two distinct node contents should result in exactly two parses.");
    }

    [Test]
    public void SelectNodeSingleResult_AlsoUsesCache()
    {
        var fetcher = new SystemTextJsonPathItemsFetcher();
        var node    = JsonNode.Parse("""{"val":"hello"}""")!;

        fetcher.SelectNode("$.val", node);
        fetcher.SelectNode("$.val", node);
        fetcher.SelectNodes("$.val", node);

        Assert.That(fetcher.ParseCount, Is.EqualTo(1),
            "SelectNode and SelectNodes should share the same document cache.");
    }
}
