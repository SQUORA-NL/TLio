using System.Text.Json.Nodes;
using NUnit.Framework;
using TLio.JsonPath.Tests.Differential;

namespace TLio.JsonPath.Tests.Api;

/// <summary>Invalid queries fail at parse time, with the position; limits and timeouts behave as configured.</summary>
[TestFixture]
public class NegativeTests
{
    private static readonly JsonPathEngine Rfc = new(new JsonPathOptions { Dialect = JsonPathDialect.Rfc9535 });

    [TestCase("")]
    [TestCase("a")]
    [TestCase("$ ")]
    [TestCase(" $")]
    [TestCase("$.")]
    [TestCase("$..")]
    [TestCase("$.a b")]
    [TestCase("$[")]
    [TestCase("$[]")]
    [TestCase("$['a'")]
    [TestCase("$['a\\qb']")]
    [TestCase("$[01]")]
    [TestCase("$[-0]")]
    [TestCase("$[9007199254740992]")]
    [TestCase("$[1:2:3:4]")]
    [TestCase("$[?]")]
    [TestCase("$[?@.a ==]")]
    [TestCase("$[?@.a = 1]")]
    [TestCase("$[?1]")]
    [TestCase("$[?length(@.a)]")]
    [TestCase("$[?count(1)]")]
    [TestCase("$[?match(@.a)]")]
    [TestCase("$[?nosuchfunction(@.a)]")]
    [TestCase("$[?@.a == $..b]")]
    [TestCase("$[?!@.a == 1]")]
    [TestCase("$[?(@.a]")]
    [TestCase("$['a','b'")]
    [TestCase("$..[")]
    [TestCase("$.*a")]
    public void RFC_invalid_queries_fail_at_parse_time_with_a_position(string query)
    {
        var ex = Assert.Throws<JsonPathException>(() => Rfc.Parse(query))!;
        Assert.That(ex.Kind, Is.EqualTo(JsonPathErrorKind.Syntax));
        Assert.That(ex.Position, Is.InRange(0, query.Length), "position points into the query");
        Assert.That(ex.Message, Does.Contain("position"));
    }

    [Test]
    public void The_position_points_at_the_offending_character()
    {
        Assert.That(Assert.Throws<JsonPathException>(() => Rfc.Parse("$.a.b c"))!.Position, Is.EqualTo(5));
        Assert.That(Assert.Throws<JsonPathException>(() => Rfc.Parse("$[01]"))!.Position, Is.EqualTo(2));
        Assert.That(Assert.Throws<JsonPathException>(() => Rfc.Parse("$[?@.a == ]"))!.Position, Is.EqualTo(10));
        Assert.That(Assert.Throws<JsonPathException>(() => Rfc.Parse("$[?foo(@.a)]"))!.Position, Is.EqualTo(3));
    }

    [Test]
    public void Every_syntax_error_the_Newtonsoft_dialect_raises_carries_an_in_range_position()
    {
        var engine = new JsonPathEngine(new JsonPathOptions { Dialect = JsonPathDialect.Newtonsoft });
        var checkedCount = 0;
        var queries = QueryCorpus.Handwritten().Concat(File.ReadLines(Path.Combine(TestContext.CurrentContext.TestDirectory, "Corpus", "tlio-paths.txt")));
        foreach (var q in queries)
        {
            try
            {
                engine.Parse(q);
            }
            catch (JsonPathException ex)
            {
                checkedCount++;
                Assert.That(ex.Kind, Is.EqualTo(JsonPathErrorKind.Syntax), q);
                Assert.That(ex.Position, Is.InRange(0, q.Length), q);
            }
        }

        Assert.That(checkedCount, Is.GreaterThan(300));
    }

    [Test]
    public void A_query_longer_than_the_limit_is_refused()
    {
        var engine = new JsonPathEngine(new JsonPathOptions { MaxQueryLength = 20 });
        Assert.That(engine.Parse("$.a.b.c").Text, Is.EqualTo("$.a.b.c"));
        var ex = Assert.Throws<JsonPathException>(() => engine.Parse("$." + new string('a', 30)))!;
        Assert.That(ex.Kind, Is.EqualTo(JsonPathErrorKind.Limit));
    }

    [Test]
    public void The_default_query_length_limit_blocks_a_megabyte_query_without_parsing_it()
    {
        var ex = Assert.Throws<JsonPathException>(() => JsonPathEngine.Default.Parse("$" + string.Concat(Enumerable.Repeat(".a", 600_000))))!;
        Assert.That(ex.Kind, Is.EqualTo(JsonPathErrorKind.Limit));
    }

    [Test]
    public void Filter_nesting_beyond_MaxDepth_is_refused_not_a_stack_overflow()
    {
        var engine = new JsonPathEngine(new JsonPathOptions { Dialect = JsonPathDialect.Rfc9535, MaxDepth = 16, MaxQueryLength = 100_000 });
        var parens = new string('(', 200) + "@.a" + new string(')', 200);
        var ex = Assert.Throws<JsonPathException>(() => engine.Parse($"$[?{parens}]"))!;
        Assert.That(ex.Kind, Is.EqualTo(JsonPathErrorKind.Limit));

        var nestedFilters = string.Concat(Enumerable.Repeat("[?@", 200)) + string.Concat(Enumerable.Repeat("]", 200));
        Assert.That(Assert.Throws<JsonPathException>(() => engine.Parse("$" + nestedFilters))!.Kind, Is.EqualTo(JsonPathErrorKind.Limit));
    }

    [Test]
    public void Default_limits_survive_the_deepest_query_the_default_length_allows()
    {
        // 4096 characters of nesting must end in a clean error, never a crash.
        var q = "$[?" + new string('(', 2000) + "@.a" + new string(')', 2000) + "]";
        var ex = Assert.Throws<JsonPathException>(() => Rfc.Parse(q))!;
        Assert.That(ex.Kind, Is.EqualTo(JsonPathErrorKind.Limit).Or.EqualTo(JsonPathErrorKind.Syntax));
    }

    private static JsonNode Deep(int depth)
    {
        JsonNode node = new JsonObject { ["a"] = 1 };
        for (var i = 0; i < depth; i++) node = new JsonObject { ["a"] = node };
        return node;
    }

    [TestCase(JsonPathDialect.Rfc9535)]
    [TestCase(JsonPathDialect.Newtonsoft)]
    public void A_descendant_walk_over_a_document_deeper_than_MaxDepth_fails_with_Limit(JsonPathDialect dialect)
    {
        var engine = new JsonPathEngine(new JsonPathOptions { Dialect = dialect, MaxDepth = 50 });
        var ok = engine.Select("$..a", Deep(30));
        Assert.That(ok, Is.Not.Empty);
        var ex = Assert.Throws<JsonPathException>(() => engine.Select("$..a", Deep(100)))!;
        Assert.That(ex.Kind, Is.EqualTo(JsonPathErrorKind.Limit));
    }

    [Test]
    public void A_very_deep_document_is_walked_without_overflowing_the_stack_at_the_default_limit()
    {
        // Explicit stacks, not recursion: depth is bounded by MaxDepth (512), not by the thread's stack.
        var ex = Assert.Throws<JsonPathException>(() => new JsonPathEngine(new JsonPathOptions { Dialect = JsonPathDialect.Rfc9535 }).Select("$..a", Deep(5000)))!;
        Assert.That(ex.Kind, Is.EqualTo(JsonPathErrorKind.Limit));
    }

    private static readonly string Hostile = new string('a', 40) + "!";

    [Test]
    public void A_pathological_regex_in_the_Newtonsoft_dialect_times_out_as_configured()
    {
        var engine = new JsonPathEngine(new JsonPathOptions { Dialect = JsonPathDialect.Newtonsoft, RegexTimeout = TimeSpan.FromMilliseconds(50) });
        var doc = JsonNode.Parse($$"""[{"s": "{{Hostile}}"}]""");
        var ex = Assert.Throws<JsonPathException>(() => engine.Select("$[?(@.s =~ /^(a|aa)+$/)]", doc))!;
        Assert.That(ex.Kind, Is.EqualTo(JsonPathErrorKind.Limit));
    }

    [Test]
    public void A_pathological_regex_in_match_times_out_as_configured()
    {
        var engine = new JsonPathEngine(new JsonPathOptions { Dialect = JsonPathDialect.Rfc9535, RegexTimeout = TimeSpan.FromMilliseconds(50) });
        var doc = JsonNode.Parse($$"""[{"s": "{{Hostile}}"}]""");
        var ex = Assert.Throws<JsonPathException>(() => engine.Select("$[?match(@.s, '(a|aa)+')]", doc))!;
        Assert.That(ex.Kind, Is.EqualTo(JsonPathErrorKind.Limit));
        // …and a dynamic pattern (not known at parse time) is guarded the same way.
        var dyn = JsonNode.Parse($$"""[{"s": "{{Hostile}}", "p": "(a|aa)+"}]""");
        Assert.That(Assert.Throws<JsonPathException>(() => engine.Select("$[?match(@.s, @.p)]", dyn))!.Kind, Is.EqualTo(JsonPathErrorKind.Limit));
    }

    [Test]
    public void The_regex_timeout_can_be_turned_off()
    {
        var engine = new JsonPathEngine(new JsonPathOptions { Dialect = JsonPathDialect.Rfc9535, RegexTimeout = Timeout.InfiniteTimeSpan });
        var doc = JsonNode.Parse("""[{"s": "aaa"}]""");
        Assert.That(engine.Select("$[?match(@.s, '(a+)+')]", doc), Has.Count.EqualTo(1));
    }

    [Test]
    public void A_regex_that_is_not_valid_I_Regexp_never_matches_and_is_not_an_error()
    {
        var doc = JsonNode.Parse("""["a1", "a", "b"]""");
        foreach (var pattern in new[] { "a\\d", "(?:a)", "a*?", "(a)\\1", "[a-", "a{2,1}", "\\w", "a++" })
            Assert.That(Rfc.Select($"$[?search(@, '{pattern.Replace("\\", "\\\\")}')]", doc), Is.Empty, pattern);
    }

    [Test]
    public void Strict_I_Regexp_reads_caret_and_dollar_as_literals_as_the_RFC_9485_grammar_does()
    {
        var doc = JsonNode.Parse("""["^a", "a", "a$", "$"]""");
        var lenient = new JsonPathEngine(new JsonPathOptions { Dialect = JsonPathDialect.Rfc9535 });
        var strict = new JsonPathEngine(new JsonPathOptions { Dialect = JsonPathDialect.Rfc9535, StrictIRegexp = true });
        Assert.That(lenient.Select("$[?match(@, '^a')]", doc).Select(m => m.Index), Is.EqualTo(new[] { 1 }));
        Assert.That(strict.Select("$[?match(@, '^a')]", doc).Select(m => m.Index), Is.EqualTo(new[] { 0 }));
        Assert.That(lenient.Select("$[?match(@, 'a$')]", doc).Select(m => m.Index), Is.EqualTo(new[] { 1 }));
        Assert.That(strict.Select("$[?match(@, 'a$')]", doc).Select(m => m.Index), Is.EqualTo(new[] { 2 }));
    }
}
