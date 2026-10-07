using System.Text;

namespace TLio.JsonPath.Tests.Differential;

/// <summary>Runs (query × document) through the oracle and the engine and reports every disagreement.</summary>
public static class DifferentialRunner
{
    public static readonly JsonPathEngine Newtonsoft = new(new JsonPathOptions { Dialect = JsonPathDialect.Newtonsoft });
    public static readonly JsonPathEngine NewtonsoftErrors = new(new JsonPathOptions { Dialect = JsonPathDialect.Newtonsoft, ErrorWhenNoMatch = true });

    /// <summary>
    /// A deliberate, documented divergence: the oracle and the engine are allowed to differ for this
    /// (document, query), and the test asserts exactly the expected difference — so it can neither grow
    /// silently nor be fixed without the entry being removed.
    /// </summary>
    public sealed record Divergence(string Document, string Query, string Why);

    public static IEnumerable<string> Compare(
        JsonPathEngine engine,
        IEnumerable<string> queries,
        IReadOnlyDictionary<string, string> documents,
        bool errorWhenNoMatch,
        IReadOnlyCollection<Divergence>? known = null)
    {
        foreach (var (docName, docJson) in documents)
        {
            foreach (var query in queries)
            {
                if (known?.Any(k => k.Document == docName && k.Query == query) == true) continue;

                var oracle = NewtonsoftOracle.SelectTokens(docJson, query, errorWhenNoMatch);
                var actual = EngineRunner.SelectTokens(engine, docJson, query);
                if (!Same(oracle, actual))
                    yield return Describe("SelectTokens", docName, query, oracle, actual);

                var oracle1 = NewtonsoftOracle.SelectToken(docJson, query, errorWhenNoMatch);
                var actual1 = EngineRunner.SelectToken(engine, docJson, query);
                if (!Same(oracle1, actual1))
                    yield return Describe("SelectToken", docName, query, oracle1, actual1);
            }
        }
    }

    public static bool Same(Outcome a, Outcome b)
    {
        if (a.Error != null || b.Error != null) return a.Error == b.Error;
        if (a.Hits!.Count != b.Hits!.Count) return false;
        for (var i = 0; i < a.Hits.Count; i++)
            if (a.Hits[i] != b.Hits[i]) return false;
        return true;
    }

    public static string Describe(string api, string doc, string query, Outcome oracle, Outcome actual)
    {
        var sb = new StringBuilder();
        sb.Append(api).Append(" doc=").Append(doc).Append(" query=`").Append(query).AppendLine("`");
        sb.Append("    newtonsoft: ").AppendLine(Trim(oracle.ToString()) + (oracle.Detail != null ? "  // " + Trim(oracle.Detail) : ""));
        sb.Append("    engine:     ").AppendLine(Trim(actual.ToString()) + (actual.Detail != null ? "  // " + Trim(actual.Detail) : ""));
        return sb.ToString();
    }

    private static string Trim(string s) => s.Length > 400 ? s[..400] + "…" : s;
}
