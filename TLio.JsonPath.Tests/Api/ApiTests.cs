using System.Text.Json.Nodes;
using NUnit.Framework;

namespace TLio.JsonPath.Tests.Api;

/// <summary>The public surface, used the way a consumer with nothing but System.Text.Json would use it.</summary>
[TestFixture]
public class ApiTests
{
    private static readonly JsonNode Store = JsonNode.Parse("""
        { "store": { "book": [ { "price": 8.95, "isbn": null }, { "price": 12.99 } ], "bicycle": { "price": 19.95 } }, "n": null }
        """)!;

    [Test]
    public void The_default_engine_reads_the_Newtonsoft_dialect()
    {
        Assert.That(new JsonPathOptions().Dialect, Is.EqualTo(JsonPathDialect.Newtonsoft));
        Assert.That(JsonPathEngine.Default.Options.Dialect, Is.EqualTo(JsonPathDialect.Newtonsoft));
    }

    [Test]
    public void Select_returns_matches_with_location_and_normalized_path()
    {
        var matches = JsonPathEngine.Default.Select("$.store.book[*].price", Store);
        Assert.That(matches.Select(m => m.NormalizedPath), Is.EqualTo(["$['store']['book'][0]['price']", "$['store']['book'][1]['price']"]));
        Assert.That(matches[0].Name, Is.EqualTo("price"));
        Assert.That(matches[0].Index, Is.EqualTo(-1));
        Assert.That(matches[0].Parent, Is.SameAs(Store["store"]!["book"]![0]));
        Assert.That(matches[0].Node!.GetValue<double>(), Is.EqualTo(8.95));
    }

    [Test]
    public void Array_element_matches_know_their_index_and_parent()
    {
        var m = JsonPathEngine.Default.Select("$.store.book[1]", Store).Single();
        Assert.That(m.Index, Is.EqualTo(1));
        Assert.That(m.Name, Is.Null);
        Assert.That(m.Parent, Is.SameAs(Store["store"]!["book"]));
        Assert.That(m.IsRoot, Is.False);
    }

    [Test]
    public void The_root_is_a_match_of_dollar()
    {
        var m = JsonPathEngine.Default.Select("$", Store).Single();
        Assert.That(m.IsRoot, Is.True);
        Assert.That(m.Node, Is.SameAs(Store));
        Assert.That(m.Parent, Is.Null);
        Assert.That(m.NormalizedPath, Is.EqualTo("$"));
    }

    [TestCase(JsonPathDialect.Newtonsoft)]
    [TestCase(JsonPathDialect.Rfc9535)]
    [TestCase(JsonPathDialect.Extended)]
    public void A_member_with_a_JSON_null_is_a_match_with_a_null_node_unlike_an_absent_member(JsonPathDialect dialect)
    {
        // System.Text.Json stores JSON null as a C# null: only the match list can tell "null" from "missing".
        var engine = new JsonPathEngine(new JsonPathOptions { Dialect = dialect });

        var present = engine.Select("$.n", Store);
        Assert.That(present, Has.Count.EqualTo(1));
        Assert.That(present[0].Node, Is.Null);
        Assert.That(present[0].Parent, Is.SameAs(Store));
        Assert.That(present[0].Name, Is.EqualTo("n"));

        Assert.That(engine.Select("$.missing", Store), Is.Empty);
        Assert.That(engine.Select("$.store.book[0].isbn", Store), Has.Count.EqualTo(1));
        Assert.That(engine.Select("$.store.book[1].isbn", Store), Is.Empty);
    }

    [Test]
    public void A_null_element_of_an_array_is_a_match_too()
    {
        var doc = JsonNode.Parse("[1, null, 3]");
        var m = JsonPathEngine.Default.Select("$[1]", doc).Single();
        Assert.That(m.Node, Is.Null);
        Assert.That(m.Index, Is.EqualTo(1));
        Assert.That(m.Parent, Is.SameAs(doc));
    }

    [Test]
    public void A_whole_document_that_is_JSON_null_can_be_queried()
    {
        Assert.That(JsonPathEngine.Default.Select("$", null), Has.Count.EqualTo(1));
        Assert.That(JsonPathEngine.Default.Select("$.a", null), Is.Empty);
        Assert.That(new JsonPathEngine(new JsonPathOptions { Dialect = JsonPathDialect.Rfc9535 }).Select("$[?@]", null), Is.Empty);
    }

    [Test]
    public void SelectFirst_returns_the_first_match_or_null()
    {
        Assert.That(JsonPathEngine.Default.SelectFirst("$.store.book[*]", Store)!.Value.Index, Is.EqualTo(0));
        Assert.That(JsonPathEngine.Default.SelectFirst("$.nope", Store), Is.Null);
    }

    [Test]
    public void SelectSingle_follows_Newtonsoft_SelectToken()
    {
        Assert.That(JsonPathEngine.Default.SelectSingle("$.store.book[0]", Store), Is.Not.Null);
        Assert.That(JsonPathEngine.Default.SelectSingle("$.nope", Store), Is.Null);
        var ex = Assert.Throws<JsonPathException>(() => JsonPathEngine.Default.SelectSingle("$.store.book[*]", Store))!;
        Assert.That(ex.Kind, Is.EqualTo(JsonPathErrorKind.MultipleResults));
    }

    [Test]
    public void ErrorWhenNoMatch_turns_an_empty_step_into_an_error_in_the_Newtonsoft_dialect()
    {
        var strict = new JsonPathEngine(new JsonPathOptions { ErrorWhenNoMatch = true });
        Assert.That(strict.Select("$.store", Store), Has.Count.EqualTo(1));
        var ex = Assert.Throws<JsonPathException>(() => strict.Select("$.nope", Store))!;
        Assert.That(ex.Kind, Is.EqualTo(JsonPathErrorKind.NoMatch));
    }

    [Test]
    public void An_empty_result_is_never_an_error_by_default_in_either_dialect()
    {
        foreach (var dialect in new[] { JsonPathDialect.Newtonsoft, JsonPathDialect.Rfc9535, JsonPathDialect.Extended })
        {
            var engine = new JsonPathEngine(new JsonPathOptions { Dialect = dialect });
            Assert.That(engine.Select("$.a.b.c", Store), Is.Empty, dialect.ToString());
            Assert.That(engine.Exists("$.a.b.c", Store), Is.False);
        }
    }

    [Test]
    public void Parsed_queries_are_cached_and_the_cache_is_bounded()
    {
        var engine = new JsonPathEngine(new JsonPathOptions { QueryCacheSize = 3 });
        var first = engine.Parse("$.a");
        Assert.That(engine.Parse("$.a"), Is.SameAs(first));
        for (var i = 0; i < 10; i++) engine.Parse("$.q" + i);
        // Bounded: after overflowing, the cache was reset, so the oldest entry was parsed again.
        Assert.That(engine.Parse("$.a"), Is.Not.SameAs(first));
    }

    [Test]
    public void The_cache_can_be_switched_off()
    {
        var engine = new JsonPathEngine(new JsonPathOptions { QueryCacheSize = 0 });
        Assert.That(engine.Parse("$.a"), Is.Not.SameAs(engine.Parse("$.a")));
    }

    [Test]
    public void A_parsed_query_can_be_used_from_many_threads_at_once()
    {
        foreach (var dialect in new[] { JsonPathDialect.Newtonsoft, JsonPathDialect.Rfc9535 })
        {
            var engine = new JsonPathEngine(new JsonPathOptions { Dialect = dialect });
            var query = engine.Parse(dialect == JsonPathDialect.Rfc9535 ? "$.store.book[?@.price < 10].price" : "$.store.book[?(@.price < 10)].price");
            var errors = 0;
            Parallel.For(0, 2000, _ =>
            {
                var r = query.Select(Store);
                if (r.Count != 1 || r[0].NormalizedPath != "$['store']['book'][0]['price']") Interlocked.Increment(ref errors);
            });
            Assert.That(errors, Is.Zero, dialect.ToString());
        }
    }

    [Test]
    public void Options_are_validated()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new JsonPathEngine(new JsonPathOptions { MaxQueryLength = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new JsonPathEngine(new JsonPathOptions { MaxDepth = 0 }));
        Assert.Throws<ArgumentNullException>(() => JsonPathEngine.Default.Parse(null!));
    }

    [TestCase("$['a\\'b']", "{\"a'b\":1}", "$['a\\'b']")]
    [TestCase("$['a\\\\b']", "{\"a\\\\b\":1}", "$['a\\\\b']")]
    [TestCase("$['a\\nb']", "{\"a\\nb\":1}", "$['a\\nb']")]
    [TestCase("$['\\u001f']", "{\"\\u001f\":1}", "$['\\u001f']")]
    [TestCase("$['é']", "{\"é\":1}", "$['é']")]
    [TestCase("$[\"a\\\"b\"]", "{\"a\\\"b\":1}", "$['a\"b']")]
    public void Normalized_paths_escape_names_the_way_RFC_9535_section_2_7_says(string query, string doc, string expectedPath)
    {
        var engine = new JsonPathEngine(new JsonPathOptions { Dialect = JsonPathDialect.Rfc9535 });
        Assert.That(engine.Select(query, JsonNode.Parse(doc)).Single().NormalizedPath, Is.EqualTo(expectedPath));
    }

    [Test]
    public void The_library_has_no_dependency_on_TLio_or_Newtonsoft()
    {
        var references = typeof(JsonPathEngine).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();
        Assert.That(references, Has.None.StartsWith("TLio"));
        Assert.That(references, Has.None.StartsWith("Newtonsoft"));
        Assert.That(references, Has.None.StartsWith("JsonCons"));
    }
}
