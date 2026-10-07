using System.Text;
using System.Text.Json.Nodes;
using BenchmarkDotNet.Attributes;
using Newtonsoft.Json.Linq;

namespace TLio.JsonPath.Benchmarks;

/// <summary>
/// The same logical query through four implementations:
/// <list type="bullet">
/// <item><b>Newtonsoft</b> — <c>JToken.SelectTokens</c>, the reference.</item>
/// <item><b>OldJsonCons</b> — the previous TLio.Json.SystemText strategy (see <see cref="JsonConsBaseline"/>).</item>
/// <item><b>EngineNewtonsoft</b> — TLio.JsonPath, Newtonsoft dialect (the TLio.Json.SystemText default).</item>
/// <item><b>EngineRfc9535</b> — TLio.JsonPath, RFC 9535 dialect.</item>
/// </list>
/// Each call selects from a document that has not changed, which is the old design's best case.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 12)]
public class PathBenchmarks
{
    private JToken _newtonsoftDoc = null!;
    private JsonNode _nodeDoc = null!;
    private JsonConsBaseline _old = null!;
    private JsonPathEngine _newtonsoftEngine = null!;
    private JsonPathEngine _rfcEngine = null!;

    // Newtonsoft/JsonCons syntax and RFC syntax for the same query.
    private string _legacyPath = "";
    private string _rfcPath = "";

    [Params("small", "large")]
    public string Document { get; set; } = "small";

    [Params("simple", "indexed", "wildcard", "filter", "descendant")]
    public string Query { get; set; } = "simple";

    [GlobalSetup]
    public void Setup()
    {
        var json = Document == "small" ? Bookstore : BuildLarge(20_000);
        _newtonsoftDoc = JToken.Parse(json);
        _nodeDoc = JsonNode.Parse(json)!;
        _old = new JsonConsBaseline();
        _newtonsoftEngine = new JsonPathEngine(new JsonPathOptions { Dialect = JsonPathDialect.Newtonsoft });
        _rfcEngine = new JsonPathEngine(new JsonPathOptions { Dialect = JsonPathDialect.Rfc9535 });

        (_legacyPath, _rfcPath) = (Document, Query) switch
        {
            ("small", "simple") => ("$.store.bicycle.color", "$.store.bicycle.color"),
            ("small", "indexed") => ("$.store.book[2].title", "$.store.book[2].title"),
            ("small", "wildcard") => ("$.store.book[*].author", "$.store.book[*].author"),
            ("small", "filter") => ("$.store.book[?(@.price < 10)].title", "$.store.book[?@.price < 10].title"),
            ("small", _) => ("$..price", "$..price"),
            ("large", "simple") => ("$[0].owner.name", "$[0].owner.name"),
            ("large", "indexed") => ("$[15000].name", "$[15000].name"),
            ("large", "wildcard") => ("$[*].owner.name", "$[*].owner.name"),
            ("large", "filter") => ("$[?(@.price < 10)].id", "$[?@.price < 10].id"),
            _ => ("$..id", "$..id"),
        };

        // Fail loudly if the implementations disagree about the answer: a benchmark of different work is meaningless.
        var expected = Newtonsoft();
        if (EngineNewtonsoft() != expected || OldJsonCons() != expected || EngineRfc9535() != expected)
            throw new InvalidOperationException($"implementations disagree on {Document}/{Query}: newtonsoft={expected} old={OldJsonCons()} engine={EngineNewtonsoft()} rfc={EngineRfc9535()}");
    }

    [GlobalCleanup]
    public void Cleanup() => _old.Dispose();

    [Benchmark(Baseline = true)]
    public int Newtonsoft()
    {
        var n = 0;
        foreach (var _ in _newtonsoftDoc.SelectTokens(_legacyPath)) n++;
        return n;
    }

    [Benchmark]
    public int OldJsonCons() => _old.SelectNodes(_legacyPath, _nodeDoc).Count;

    [Benchmark]
    public int EngineNewtonsoft() => _newtonsoftEngine.Select(_legacyPath, _nodeDoc).Count;

    [Benchmark]
    public int EngineRfc9535() => _rfcEngine.Select(_rfcPath, _nodeDoc).Count;

    private const string Bookstore = """
        {
          "store": {
            "book": [
              { "category": "reference", "author": "Nigel Rees", "title": "Sayings of the Century", "price": 8.95 },
              { "category": "fiction", "author": "Evelyn Waugh", "title": "Sword of Honour", "price": 12.99 },
              { "category": "fiction", "author": "Herman Melville", "title": "Moby Dick", "isbn": "0-553-21311-3", "price": 8.99 },
              { "category": "fiction", "author": "J. R. R. Tolkien", "title": "The Lord of the Rings", "isbn": "0-395-19395-8", "price": 22.99 }
            ],
            "bicycle": { "color": "red", "price": 19.95 }
          },
          "expensive": 10
        }
        """;

    private static string BuildLarge(int count)
    {
        var sb = new StringBuilder("[");
        for (var i = 0; i < count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append("{\"id\":").Append(i)
              .Append(",\"name\":\"item-").Append(i)
              .Append("\",\"price\":").Append(((i % 40) + 0.5).ToString(System.Globalization.CultureInfo.InvariantCulture))
              .Append(",\"tags\":[\"a\",\"b\",\"c\"],\"owner\":{\"name\":\"owner-").Append(i % 100)
              .Append("\",\"age\":").Append(20 + i % 50).Append("}}");
        }

        return sb.Append(']').ToString();
    }
}
