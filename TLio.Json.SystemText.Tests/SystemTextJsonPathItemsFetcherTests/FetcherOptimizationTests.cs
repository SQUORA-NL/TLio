using System.Text.Json.Nodes;
using NUnit.Framework;
using TLio.Json.SystemText;
using TLio.JsonPath;

namespace TLio.Json.SystemText.Tests.SystemTextJsonPathItemsFetcherTests;

/// <summary>
/// The fetcher evaluates paths directly on the document's own nodes. These tests pin the properties
/// that follow from that — the properties the previous design (serialize to a string, parse a
/// JsonDocument, select on the copy, navigate back) had to build a cache to approximate:
/// selection never works from a stale snapshot, and it hands back the live nodes.
/// </summary>
[TestFixture]
public class FetcherOptimizationTests
{
    [Test]
    public void SelectedNodesAreTheLiveNodesOfTheDocument()
    {
        var fetcher = new SystemTextJsonPathItemsFetcher();
        var root    = JsonNode.Parse("""{"a":{"b":[1,2,3]}}""")!;

        var selected = fetcher.SelectNodes("$.a.b", root).Single();

        Assert.That(selected, Is.SameAs(root["a"]!["b"]), "no copy: the very node of the document");
        Assert.That(selected.Parent, Is.SameAs(root["a"]), "parent references are intact");
    }

    [Test]
    public void SelectionAfterMutationSeesTheMutationImmediately()
    {
        var fetcher = new SystemTextJsonPathItemsFetcher();
        var root    = JsonNode.Parse("""{"items":[1,2,3]}""")!;

        Assert.That(fetcher.SelectNodes("$.items[*]", root), Has.Count.EqualTo(3));

        root.AsObject()["items"]!.AsArray().Add(4);
        root.AsObject()["extra"] = 1;

        Assert.That(fetcher.SelectNodes("$.items[*]", root), Has.Count.EqualTo(4));
        Assert.That(fetcher.SelectNodes("$.extra", root), Has.Count.EqualTo(1));
    }

    [Test]
    public void SelectionNeverSerializesTheDocument()
    {
        // A node that cannot be serialized would have failed the old snapshot design; the engine only reads.
        var fetcher = new SystemTextJsonPathItemsFetcher();
        var root    = new JsonObject { ["deep"] = Nest(2000) };

        // Nesting beyond JsonSerializerOptions.MaxDepth (64) cannot be written out, but can be walked.
        Assert.DoesNotThrow(() => fetcher.SelectNodes("$.deep.a", root));
        Assert.That(fetcher.SelectNodes("$.deep.a", root), Has.Count.EqualTo(1));
    }

    private static JsonNode Nest(int depth)
    {
        JsonNode node = new JsonObject { ["a"] = 1 };
        for (var i = 0; i < depth; i++) node = new JsonObject { ["a"] = node };
        return node;
    }

    [Test]
    public void SelectNodeAndSelectNodesAgree()
    {
        var fetcher = new SystemTextJsonPathItemsFetcher();
        var node    = JsonNode.Parse("""{"val":"hello"}""")!;

        Assert.That(fetcher.SelectNode("$.val", node), Is.SameAs(fetcher.SelectNodes("$.val", node).Single()));
    }

    [Test]
    public void ParsedQueriesAreSharedBetweenFetchers()
    {
        // A fetcher is created per execution; the engine (and its parsed-query cache) is shared per dialect.
        Assert.That(new SystemTextJsonPathItemsFetcher().Engine, Is.SameAs(new SystemTextJsonPathItemsFetcher().Engine));
        Assert.That(new SystemTextJsonPathItemsFetcher().Engine.Options.Dialect, Is.EqualTo(JsonPathDialect.Newtonsoft));
    }

    [Test]
    public void TheDialectIsAnAdapterOption()
    {
        var doc = JsonNode.Parse("""{"a":[1,2,3]}""")!;

        // Newtonsoft's dialect does not know RFC 9535's bare filter or negative index…
        Assert.Throws<JsonPathException>(() => new SystemTextJsonPathItemsFetcher().SelectNodes("$.a[?@ > 1]", doc));

        // …the opt-in dialects do.
        Assert.That(new SystemTextJsonPathItemsFetcher(JsonPathDialect.Rfc9535).SelectNodes("$.a[?@ > 1]", doc), Has.Count.EqualTo(2));
        Assert.That(new SystemTextJsonPathItemsFetcher(JsonPathDialect.Extended).SelectNodes("$.a[-1]", doc).Single().GetValue<int>(), Is.EqualTo(3));
    }

    [Test]
    public void ANullValuedMemberIsStillSelectable()
    {
        var fetcher = new SystemTextJsonPathItemsFetcher();
        var root    = JsonNode.Parse("""{"gone":null,"here":1}""")!;

        Assert.That(fetcher.SelectNodes("$.gone", root), Has.Count.EqualTo(1), "present with a null value is not absent");
        Assert.That(fetcher.SelectNodes("$.nope", root), Is.Empty);
    }
}
