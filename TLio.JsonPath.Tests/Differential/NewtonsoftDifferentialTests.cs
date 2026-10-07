using NUnit.Framework;

namespace TLio.JsonPath.Tests.Differential;

/// <summary>
/// The Newtonsoft dialect against Newtonsoft.Json 13.0.4 itself: values, order, normalized paths,
/// errors (and their category), no-match and multiple-match behaviour — for both SelectTokens and
/// SelectToken, with and without ErrorWhenNoMatch.
/// </summary>
[TestFixture]
public class NewtonsoftDifferentialTests
{
    private static void AssertNoMismatches(IEnumerable<string> mismatches)
    {
        var list = mismatches.ToList();
        Assert.That(list, Is.Empty, () => $"{list.Count} mismatch(es):\n" + string.Join("\n", list.Take(40)));
    }

    [Test]
    public void Handwritten_corpus_matches_oracle()
    {
        var queries = QueryCorpus.Handwritten().Distinct().ToList();
        AssertNoMismatches(DifferentialRunner.Compare(DifferentialRunner.Newtonsoft, queries, Documents.All, false));
    }

    [Test]
    public void The_harness_is_sensitive_to_dialect_differences()
    {
        // Guards against a comparator that passes vacuously: the RFC 9535 dialect genuinely differs from
        // Newtonsoft (descendant order, `$..*` including the root, filters on objects, ...), so running
        // it against the oracle must produce mismatches — and plenty of them.
        var rfc = new JsonPathEngine(new JsonPathOptions { Dialect = JsonPathDialect.Rfc9535 });
        var queries = QueryCorpus.Handwritten().Distinct().ToList();
        var mismatches = DifferentialRunner.Compare(rfc, queries, Documents.All, false).Count();
        Assert.That(mismatches, Is.GreaterThan(500));
    }

    [Test]
    public void Corpus_exercises_hits_and_every_error_category()
    {
        var queries = QueryCorpus.Handwritten().Distinct().ToList();
        int hits = 0, empty = 0;
        var errors = new Dictionary<string, int>();
        var queriesThatHit = new HashSet<string>();
        foreach (var doc in Documents.All.Values)
        foreach (var q in queries)
        {
            var o = NewtonsoftOracle.SelectTokens(doc, q);
            if (o.Error != null) errors[o.Error] = errors.GetValueOrDefault(o.Error) + 1;
            else if (o.Hits!.Count == 0) empty++;
            else
            {
                hits++;
                queriesThatHit.Add(q);
            }
        }

        TestContext.Out.WriteLine($"queries={queries.Count} documents={Documents.All.Count} pairs={queries.Count * Documents.All.Count} hits={hits} empty={empty} queriesThatHit={queriesThatHit.Count} errors=[{string.Join(", ", errors.Select(kv => kv.Key + ":" + kv.Value))}]");
        Assert.That(hits, Is.GreaterThan(800));
        Assert.That(queriesThatHit.Count, Is.GreaterThan(500));
        Assert.That(errors.Keys, Is.SupersetOf(new[] { "Syntax", "Evaluation", "Conversion" }));
    }

    public sealed record JpcEntry(string Id, string Selector, string Document);

    private static List<JpcEntry> LoadJpc()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Corpus", "jpc-regression.json");
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.EnumerateArray()
            .Select(e => new JpcEntry(e.GetProperty("id").GetString()!, e.GetProperty("selector").GetString()!, e.GetProperty("document").GetString()!))
            .ToList();
    }

    [Test]
    public void The_json_path_comparison_regression_suite_matches_oracle()
    {
        // 258 queries from cburgmer/json-path-comparison (pinned, see Corpus/PINNED.md), each against its own
        // document and against the shared documents.
        var entries = LoadJpc();
        Assert.That(entries.Count, Is.GreaterThan(250));
        var mismatches = new List<string>();
        foreach (var e in entries)
        {
            mismatches.AddRange(DifferentialRunner.Compare(DifferentialRunner.Newtonsoft, [e.Selector],
                new Dictionary<string, string> { ["jpc:" + e.Id] = e.Document }, false));
            mismatches.AddRange(DifferentialRunner.Compare(DifferentialRunner.NewtonsoftErrors, [e.Selector],
                new Dictionary<string, string> { ["jpc:" + e.Id] = e.Document }, true));
        }

        mismatches.AddRange(DifferentialRunner.Compare(DifferentialRunner.Newtonsoft, entries.Select(e => e.Selector).Distinct(), Documents.All, false));
        AssertNoMismatches(mismatches);
    }

    [Test]
    public void Every_path_used_in_TLio_tests_samples_and_docs_matches_oracle()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Corpus", "tlio-paths.txt");
        var queries = File.ReadAllLines(path).Where(l => l.Length > 0).Distinct().ToList();
        Assert.That(queries.Count, Is.GreaterThan(500));
        AssertNoMismatches(DifferentialRunner.Compare(DifferentialRunner.Newtonsoft, queries, Documents.All, false));
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    [TestCase(7)]
    [TestCase(8)]
    public void Generated_queries_on_generated_documents_match_oracle(int seed)
    {
        // 1500 random queries (a third of them damaged by a random edit) per seed, each against 4 random documents.
        var gen = new GeneratedCorpus(seed);
        var mismatches = new List<string>();
        var total = 0;
        for (var i = 0; i < 1500 && mismatches.Count < 40; i++)
        {
            var query = gen.Query();
            for (var d = 0; d < 4; d++)
            {
                var doc = gen.Document();
                total++;
                mismatches.AddRange(DifferentialRunner.Compare(DifferentialRunner.Newtonsoft, [query], new Dictionary<string, string> { ["gen"] = doc }, false)
                    .Select(m => m + "    document: " + doc));
            }
        }

        TestContext.Out.WriteLine($"seed {seed}: {total} (query, document) pairs compared");
        AssertNoMismatches(mismatches);
    }

    [Test]
    [Explicit("Long soak: 300 seeds x 1500 queries x 4 documents. Run before a release: dotnet test TLio.JsonPath.Tests --filter Name~Soak")]
    public void Generated_queries_soak()
    {
        var mismatches = new List<string>();
        for (var seed = 1000; seed < 1300 && mismatches.Count < 40; seed++)
        {
            var gen = new GeneratedCorpus(seed);
            for (var i = 0; i < 1500 && mismatches.Count < 40; i++)
            {
                var query = gen.Query();
                for (var d = 0; d < 4; d++)
                {
                    var doc = gen.Document();
                    mismatches.AddRange(DifferentialRunner.Compare(DifferentialRunner.Newtonsoft, [query], new Dictionary<string, string> { ["gen"] = doc }, false)
                        .Select(m => m + "    document: " + doc));
                    mismatches.AddRange(DifferentialRunner.Compare(DifferentialRunner.NewtonsoftErrors, [query], new Dictionary<string, string> { ["gen"] = doc }, true)
                        .Select(m => "[ErrorWhenNoMatch] " + m + "    document: " + doc));
                }
            }
        }

        AssertNoMismatches(mismatches);
    }

    [Test]
    public void Handwritten_corpus_matches_oracle_when_errors_are_requested_for_no_match()
    {
        var queries = QueryCorpus.Handwritten().Distinct().ToList();
        AssertNoMismatches(DifferentialRunner.Compare(DifferentialRunner.NewtonsoftErrors, queries, Documents.All, true));
    }
}
