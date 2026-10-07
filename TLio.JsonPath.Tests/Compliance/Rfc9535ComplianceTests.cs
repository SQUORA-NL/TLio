using System.Text.Json;
using System.Text.Json.Nodes;
using NUnit.Framework;

namespace TLio.JsonPath.Tests.Compliance;

/// <summary>
/// Runs the pinned JSONPath Compliance Test Suite (see PINNED.md) against the RFC 9535 dialect.
/// Every entry must pass.
/// </summary>
[TestFixture]
public class Rfc9535ComplianceTests
{
    /// <summary>
    /// Entries that are allowed to fail, with the reason. The target is for this to stay empty:
    /// a failing compliance test is a bug in the engine, not something to list here.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> KnownExceptions = new Dictionary<string, string>();

    private static readonly JsonPathEngine Engine = new(new JsonPathOptions { Dialect = JsonPathDialect.Rfc9535 });

    public sealed record Case(string Name, string Selector, string? DocumentJson, bool Invalid, string[] Results, string[][] ResultPaths)
    {
        public override string ToString() => Name;
    }

    public static IEnumerable<TestCaseData> Cases()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Compliance", "cts.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var t in doc.RootElement.GetProperty("tests").EnumerateArray())
        {
            var name = t.GetProperty("name").GetString()!;
            var selector = t.GetProperty("selector").GetString()!;
            var invalid = t.TryGetProperty("invalid_selector", out var inv) && inv.GetBoolean();
            string? document = t.TryGetProperty("document", out var d) ? d.GetRawText() : null;

            // A test lists one expected result, or several when the order of object members is unspecified.
            var results = new List<string>();
            var paths = new List<string[]>();
            if (t.TryGetProperty("result", out var r))
            {
                results.Add(r.GetRawText());
                paths.Add(t.GetProperty("result_paths").EnumerateArray().Select(p => p.GetString()!).ToArray());
            }

            if (t.TryGetProperty("results", out var rs))
            {
                foreach (var x in rs.EnumerateArray()) results.Add(x.GetRawText());
                foreach (var x in t.GetProperty("results_paths").EnumerateArray())
                    paths.Add(x.EnumerateArray().Select(p => p.GetString()!).ToArray());
            }

            yield return new TestCaseData(new Case(name, selector, document, invalid, results.ToArray(), paths.ToArray())).SetName(name);
        }
    }

    [TestCaseSource(nameof(Cases))]
    public void Case_conforms_to_RFC_9535(Case c)
    {
        if (KnownExceptions.ContainsKey(c.Name))
            Assert.Ignore(KnownExceptions[c.Name]);

        if (c.Invalid)
        {
            var ex = Assert.Throws<JsonPathException>(() => Engine.Parse(c.Selector), $"'{c.Selector}' must be rejected");
            Assert.That(ex!.Kind, Is.EqualTo(JsonPathErrorKind.Syntax));
            Assert.That(ex.Position, Is.GreaterThanOrEqualTo(0), "a parse error carries the position of the offending character");
            return;
        }

        var root = JsonNode.Parse(c.DocumentJson!);
        var matches = Engine.Select(c.Selector, root);

        // Any one of the listed alternatives may be the answer (member order is implementation-defined for some entries).
        var ok = false;
        for (var i = 0; i < c.Results.Length && !ok; i++)
        {
            var expected = (JsonArray)JsonNode.Parse(c.Results[i])!;
            if (expected.Count != matches.Count) continue;
            var same = true;
            for (var j = 0; j < expected.Count && same; j++)
                same = JsonNode.DeepEquals(expected[j], matches[j].Node) && matches[j].NormalizedPath == c.ResultPaths[i][j];
            ok = same;
        }

        Assert.That(ok, Is.True,
            $"selector: {c.Selector}\ngot:      [{string.Join(", ", matches.Select(m => m.Node?.ToJsonString() ?? "null"))}]\n" +
            $"got paths: [{string.Join(", ", matches.Select(m => m.NormalizedPath))}]\nexpected: {string.Join("  OR  ", c.Results)}");
    }
}
