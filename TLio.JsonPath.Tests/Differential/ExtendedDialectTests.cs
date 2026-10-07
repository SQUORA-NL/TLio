using System.Text.Json.Nodes;
using NUnit.Framework;
using TLio.JsonPath.Tests.Compliance;

namespace TLio.JsonPath.Tests.Differential;

/// <summary>
/// The Extended dialect's contract: a query Newtonsoft accepts gives the Newtonsoft result; a query it
/// rejects is read as RFC 9535 and gives the RFC result. The single place the first half bends is the
/// negative array index, where Newtonsoft crashes (ArgumentOutOfRangeException) instead of answering.
/// </summary>
[TestFixture]
public class ExtendedDialectTests
{
    private static readonly JsonPathEngine Extended = new(new JsonPathOptions { Dialect = JsonPathDialect.Extended });
    private static readonly JsonPathEngine Rfc = new(new JsonPathOptions { Dialect = JsonPathDialect.Rfc9535 });

    private static bool IsNegativeIndexCrash(Outcome o) =>
        o.Error == "Evaluation" && o.Detail != null && o.Detail.StartsWith("ArgumentOutOfRangeException", StringComparison.Ordinal);

    private static IEnumerable<string> AllQueries()
    {
        foreach (var q in QueryCorpus.Handwritten()) yield return q;
        foreach (var l in File.ReadLines(Path.Combine(TestContext.CurrentContext.TestDirectory, "Corpus", "tlio-paths.txt")))
            if (l.Length > 0) yield return l;
        var gen = new GeneratedCorpus(77);
        for (var i = 0; i < 1500; i++) yield return gen.Query();
    }

    [Test]
    public void Extended_is_a_superset_of_Newtonsoft_and_falls_back_to_RFC_9535_for_the_rest()
    {
        var failures = new List<string>();
        var viaNewtonsoft = 0;
        var viaRfc = 0;
        var queries = AllQueries().Distinct().ToList();
        foreach (var (docName, docJson) in Documents.All)
        foreach (var q in queries)
        {
            var oracle = NewtonsoftOracle.SelectTokens(docJson, q);
            var actual = EngineRunner.SelectTokens(Extended, docJson, q);

            Outcome expected;
            if (oracle.Error == "Syntax")
            {
                // Newtonsoft cannot read it: the answer is whatever strict RFC 9535 says (or a syntax error if it cannot either).
                expected = EngineRunner.SelectTokens(Rfc, docJson, q);
                viaRfc++;
            }
            else if (IsNegativeIndexCrash(oracle))
            {
                continue; // the documented exception to "same as Newtonsoft"; covered by its own test below
            }
            else
            {
                expected = oracle;
                viaNewtonsoft++;
            }

            if (!DifferentialRunner.Same(expected, actual))
                failures.Add(DifferentialRunner.Describe("Extended", docName, q, expected, actual));
        }

        TestContext.Out.WriteLine($"{queries.Count} queries x {Documents.All.Count} documents: {viaNewtonsoft} answered as Newtonsoft, {viaRfc} fell back to RFC 9535");
        Assert.That(viaNewtonsoft, Is.GreaterThan(1000));
        Assert.That(viaRfc, Is.GreaterThan(1000));
        Assert.That(failures, Is.Empty, () => $"{failures.Count} mismatch(es):\n" + string.Join("\n", failures.Take(30)));
    }

    [TestCaseSource(typeof(Rfc9535ComplianceTests), nameof(Rfc9535ComplianceTests.Cases))]
    public void Every_RFC_9535_query_that_Newtonsoft_rejects_gets_the_RFC_result(Rfc9535ComplianceTests.Case c)
    {
        var newtonsoftReadsIt = true;
        try
        {
            NewtonsoftDialect.Parse(c.Selector);
        }
        catch (JsonPathException)
        {
            newtonsoftReadsIt = false;
        }

        if (newtonsoftReadsIt)
            Assert.Pass("Newtonsoft accepts this query, so Extended answers it the Newtonsoft way (checked against the oracle above).");

        Rfc9535ComplianceTests.AssertCase(Extended, c);
    }

    private static readonly JsonPathEngine NewtonsoftDialect = new(new JsonPathOptions { Dialect = JsonPathDialect.Newtonsoft });

    [Test]
    public void The_compliance_suite_split_is_substantial_on_both_sides()
    {
        int accepted = 0, rejected = 0;
        foreach (var args in Rfc9535ComplianceTests.Cases())
        {
            var c = (Rfc9535ComplianceTests.Case)args.Arguments[0]!;
            try
            {
                NewtonsoftDialect.Parse(c.Selector);
                accepted++;
            }
            catch (JsonPathException)
            {
                rejected++;
            }
        }

        TestContext.Out.WriteLine($"compliance suite: {accepted} selectors parse as Newtonsoft, {rejected} do not");
        Assert.That(rejected, Is.GreaterThan(200));
        Assert.That(accepted, Is.GreaterThan(100));
    }

    [TestCase("$[-1]", "[1,2,3]", "$[2]")]
    [TestCase("$[-3]", "[1,2,3]", "$[0]")]
    [TestCase("$[-4]", "[1,2,3]", null)]
    [TestCase("$[-1,0]", "[1,2,3]", "$[2],$[0]")]
    [TestCase("$.a[-2]", """{"a":[1,2,3]}""", "$['a'][1]")]
    public void Negative_indexes_count_from_the_end_in_Extended_where_Newtonsoft_throws(string query, string doc, string? expected)
    {
        var node = JsonNode.Parse(doc);
        var got = string.Join(",", Extended.Select(query, node).Select(m => m.NormalizedPath));
        Assert.That(got, Is.EqualTo(expected ?? ""));

        // …and that really is a place where Newtonsoft has no answer: it crashes.
        Assert.That(IsNegativeIndexCrash(NewtonsoftOracle.SelectTokens(doc, query)), Is.True);
    }

    [Test]
    public void Extended_reports_which_dialect_a_query_turned_out_to_be()
    {
        Assert.That(Extended.Parse("$.a.b[0]").Dialect, Is.EqualTo(JsonPathDialect.Newtonsoft));
        Assert.That(Extended.Parse("$[?@.a]").Dialect, Is.EqualTo(JsonPathDialect.Rfc9535));
        Assert.That(Extended.Parse("$[?match(@.a, 'x.*')]").Dialect, Is.EqualTo(JsonPathDialect.Rfc9535));
    }

    [Test]
    public void A_query_neither_dialect_accepts_reports_both_reasons()
    {
        var ex = Assert.Throws<JsonPathException>(() => Extended.Parse("$[?(@.a ==)"))!;
        Assert.That(ex.Kind, Is.EqualTo(JsonPathErrorKind.Syntax));
        Assert.That(ex.Message, Does.Contain("Newtonsoft").And.Contain("RFC 9535"));
    }
}
