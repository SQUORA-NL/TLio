using System.Text.Json.Nodes;
using NUnit.Framework;

namespace TLio.JsonPath.Tests.Differential;

/// <summary>
/// The divergence table in the README, as executable fact. Every row states what the same query does in
/// each dialect; the Newtonsoft column is additionally checked against Newtonsoft.Json 13.0.4 itself, so
/// the table cannot drift from the thing it claims to describe.
/// </summary>
[TestFixture]
public class DivergenceTableTests
{
    private static readonly JsonPathEngine N = new(new JsonPathOptions { Dialect = JsonPathDialect.Newtonsoft });
    private static readonly JsonPathEngine R = new(new JsonPathOptions { Dialect = JsonPathDialect.Rfc9535 });
    private static readonly JsonPathEngine X = new(new JsonPathOptions { Dialect = JsonPathDialect.Extended });

    /// <summary>"[$['a'], $[0]]" for hits, "ERROR:Syntax" / "ERROR:Evaluation" / … for failures.</summary>
    private static string Run(JsonPathEngine engine, string query, string doc)
    {
        var o = EngineRunner.SelectTokens(engine, doc, query);
        return o.Error != null ? "ERROR:" + o.Error : "[" + string.Join(", ", o.Hits!.Select(h => h.Path)) + "]";
    }

    private static string RunOracle(string query, string doc)
    {
        var o = NewtonsoftOracle.SelectTokens(doc, query);
        return o.Error != null ? "ERROR:" + o.Error : "[" + string.Join(", ", o.Hits!.Select(h => h.Path)) + "]";
    }

    public sealed record Row(string Rule, string Query, string Document, string Newtonsoft, string Rfc9535, string Extended)
    {
        public override string ToString() => $"{Rule}: {Query}";
    }

    public static readonly Row[] Rows =
    [
        new("Unquoted names may contain '-'", "$.a-b", """{"a-b":1}""", "[$['a-b']]", "ERROR:Syntax", "[$['a-b']]"),
        new("Unquoted names may start with a digit", "$.1a", """{"1a":1}""", "[$['1a']]", "ERROR:Syntax", "[$['1a']]"),
        new("Double-quoted names", "$[\"a\"]", """{"a":1}""", "ERROR:Syntax", "[$['a']]", "[$['a']]"),
        new("Leading zeros in an index", "$[01]", "[10,20]", "[$[1]]", "ERROR:Syntax", "[$[1]]"),
        new("Filter without parentheses", "$[?@.a]", """[{"a":1},{"b":2}]""", "ERROR:Syntax", "[$[0]]", "[$[0]]"),
        new("Regex operator =~", "$[?(@.s =~ /^a/)]", """[{"s":"ab"},{"s":"ba"}]""", "[$[0]]", "ERROR:Syntax", "[$[0]]"),
        new("match() / search() functions", "$[?match(@.s, 'a.*')]", """[{"s":"ab"},{"s":"ba"}]""", "ERROR:Syntax", "[$[0]]", "[$[0]]"),
        new("length() / count() / value() functions", "$[?length(@.a) > 1]", """[{"a":[1,2]},{"a":[1]}]""", "ERROR:Syntax", "[$[0]]", "[$[0]]"),
        new("=== (strict equality)", "$[?(@.n === 1)]", """[{"n":1},{"n":"1"}]""", "[$[0]]", "ERROR:Syntax", "[$[0]]"),
        new("Mixed selector kinds in one bracket", "$['a',0]", """{"a":1}""", "ERROR:Syntax", "[$['a']]", "[$['a']]"),
        new("Tab/newline/CR as whitespace", "$[\t'a']", """{"a":1}""", "ERROR:Syntax", "[$['a']]", "[$['a']]"),
        new("Filter applies to object members", "$.o[?(@.a)]", """{"o":{"x":{"a":1},"y":{"b":2}}}""", "[]", "[$['o']['x']]", "[]"),
        new("'.*' on an array", "$.arr.*", """{"arr":[1,2]}""", "[]", "[$['arr'][0], $['arr'][1]]", "[]"),
        new("'[*]' on an object", "$.o[*]", """{"o":{"x":1}}""", "[]", "[$['o']['x']]", "[]"),
        new("'$..*' includes the root", "$..*", """{"a":{"b":1}}""", "[$, $['a'], $['a']['b']]", "[$['a'], $['a']['b']]", "[$, $['a'], $['a']['b']]"),
        new("Order of '..' results", "$..a", """{"x":{"a":1},"a":2}""", "[$['x']['a'], $['a']]", "[$['a'], $['x']['a']]", "[$['x']['a'], $['a']]"),
        new("'..' before an index", "$..[0]", "[[1,2],[3]]", "[$[0]]", "[$[0], $[0][0], $[1][0]]", "[$[0]]"),
        new("Negative index", "$[-1]", "[1,2,3]", "ERROR:Evaluation", "[$[2]]", "[$[2]]"),
        new("Index past the end of the array", "$[5]", "[1,2,3]", "[]", "[]", "[]"),
        new("Slice with step 0", "$[::0]", "[1,2]", "ERROR:Evaluation", "[]", "ERROR:Evaluation"),
        new("!= on a missing member", "$[?(@.x != 1)]", """[{"x":2},{"y":1}]""", "[$[0]]", "[$[0], $[1]]", "[$[0]]"),
        new("< with a numeric string", "$[?(@.v < 10)]", """[{"v":5},{"v":"7"},{"v":12}]""", "[$[0], $[1]]", "[$[0]]", "[$[0], $[1]]"),
        new("< with a non-numeric string", "$[?(@.v < 10)]", """[{"v":5},{"v":"abc"}]""", "ERROR:Conversion", "[$[0]]", "ERROR:Conversion"),
        new("== with a date-like string", "$[?(@.d == '2020-01-01T00:00:00.50Z')]", """[{"d":"2020-01-01T00:00:00.50Z"}]""", "[]", "[$[0]]", "[]"),
        new("&& / || chains", "$[?(@.a == 1 && @.b == 2 || @.c == 3)]", """[{"a":2,"b":2,"c":3}]""", "[]", "[$[0]]", "[]"),
        new("Strings are compared by UTF-16 unit", "$[?(@ > '￿')]", "[\"😀\"]", "[]", "[$[0]]", "[]"),
    ];

    [TestCaseSource(nameof(Rows))]
    public void Each_dialect_behaves_as_the_table_says(Row row)
    {
        Assert.That(Run(N, row.Query, row.Document), Is.EqualTo(row.Newtonsoft), "Newtonsoft dialect");
        Assert.That(Run(R, row.Query, row.Document), Is.EqualTo(row.Rfc9535), "RFC 9535 dialect");
        Assert.That(Run(X, row.Query, row.Document), Is.EqualTo(row.Extended), "Extended dialect");
    }

    [TestCaseSource(nameof(Rows))]
    public void The_Newtonsoft_column_is_what_Newtonsoft_does(Row row)
    {
        Assert.That(RunOracle(row.Query, row.Document), Is.EqualTo(row.Newtonsoft),
            "the table claims Newtonsoft.Json 13.0.4 does this, and it does not");
    }

    [Test]
    public void With_date_emulation_off_the_Newtonsoft_dialect_compares_date_like_strings_as_text()
    {
        var plain = new JsonPathEngine(new JsonPathOptions { Dialect = JsonPathDialect.Newtonsoft, EmulateNewtonsoftDates = false });
        Assert.That(Run(plain, "$[?(@.d == '2020-01-01T00:00:00.50Z')]", """[{"d":"2020-01-01T00:00:00.50Z"}]"""), Is.EqualTo("[$[0]]"));
        Assert.That(Run(plain, "$[?(@.d == '2020-01-01T00:00:00Z')]", """[{"d":"2020-01-01T00:00:00Z"}]"""), Is.EqualTo("[$[0]]"));
    }

    [Test]
    public void Single_node_API_is_the_same_in_every_dialect()
    {
        var doc = JsonNode.Parse("""{"a":[1,2]}""");
        foreach (var engine in new[] { N, R, X })
        {
            var ex = Assert.Throws<JsonPathException>(() => engine.SelectSingle("$.a[*]", doc))!;
            Assert.That(ex.Kind, Is.EqualTo(JsonPathErrorKind.MultipleResults));
            Assert.That(engine.SelectFirst("$.a[*]", doc)!.Value.Index, Is.EqualTo(0));
        }
    }

    [Test]
    public void The_slice_semantics_of_Newtonsoft_and_RFC_9535_are_identical_for_every_nonzero_step()
    {
        // Exhaustive over small arrays and start/end/step in [-4, 4] ∪ {omitted}, steps ≠ 0.
        var starts = new[] { "", "-4", "-3", "-2", "-1", "0", "1", "2", "3", "4" };
        var steps = new[] { "", "-4", "-3", "-2", "-1", "1", "2", "3", "4" };
        var differing = new List<string>();
        var compared = 0;
        for (var len = 0; len <= 5; len++)
        {
            var doc = "[" + string.Join(",", Enumerable.Range(0, len)) + "]";
            foreach (var s in starts)
            foreach (var e in starts)
            foreach (var st in steps)
            {
                var q = $"$[{s}:{e}" + (st == "" ? "" : ":" + st) + "]";
                compared++;
                var n = Run(N, q, doc);
                var r = Run(R, q, doc);
                if (n != r) differing.Add($"{q} on length {len}: newtonsoft {n} vs rfc {r}");
            }
        }

        TestContext.Out.WriteLine($"{compared} slice queries compared; {differing.Count} differ");
        foreach (var d in differing.Take(20)) TestContext.Out.WriteLine("  " + d);
        Assert.That(compared, Is.GreaterThan(3000));
        // The README states the outcome of this exhaustive comparison (identical for every step except 0); if it changes, the README must change with it.
        Assert.That(differing, Is.Empty);
    }
}
