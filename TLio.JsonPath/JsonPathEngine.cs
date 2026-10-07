using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using TLio.JsonPath.Internal;
using TLio.JsonPath.Internal.Newtonsoft;
using TLio.JsonPath.Internal.Rfc;

namespace TLio.JsonPath;

/// <summary>
/// Parses and evaluates JSONPath queries over <see cref="JsonNode"/> documents. An engine is
/// configured once with <see cref="JsonPathOptions"/> (dialect, limits, custom functions), is
/// thread-safe, and caches the queries it has parsed, so create one and keep it.
/// </summary>
/// <example>
/// <code>
/// var engine = new JsonPathEngine(new JsonPathOptions { Dialect = JsonPathDialect.Rfc9535 });
/// var doc = JsonNode.Parse("""{ "store": { "book": [ { "price": 8.95 }, { "price": 12.99 } ] } }""");
/// foreach (var match in engine.Select("$.store.book[?@.price &lt; 10]", doc))
///     Console.WriteLine(match.NormalizedPath);   // $['store']['book'][0]
/// </code>
/// </example>
public sealed class JsonPathEngine
{
    private readonly ConcurrentDictionary<string, JsonPathQuery> _cache = new(StringComparer.Ordinal);
    private readonly IReadOnlyDictionary<string, JsonPathFunction> _functions;
    private readonly EvalSettings _settings;

    /// <summary>An engine with default options: the Newtonsoft dialect.</summary>
    public static JsonPathEngine Default { get; } = new();

    /// <summary>Creates an engine. <paramref name="options"/> are read now and not observed afterwards.</summary>
    public JsonPathEngine(JsonPathOptions? options = null)
    {
        Options = options ?? new JsonPathOptions();
        if (Options.MaxQueryLength < 1) throw new ArgumentOutOfRangeException(nameof(options), "MaxQueryLength must be at least 1.");
        if (Options.MaxDepth < 1) throw new ArgumentOutOfRangeException(nameof(options), "MaxDepth must be at least 1.");
        _functions = Options.Functions?.Snapshot() ?? new Dictionary<string, JsonPathFunction>();
        _settings = new EvalSettings(Options.RegexTimeout, Options.MaxDepth, Options.ErrorWhenNoMatch, Options.EmulateNewtonsoftDates, Options.StrictIRegexp);
    }

    /// <summary>The options this engine was created with.</summary>
    public JsonPathOptions Options { get; }

    /// <summary>Parses a query, or returns the cached parse of the same text. Throws <see cref="JsonPathException"/> (kind Syntax, with a position) when invalid.</summary>
    public JsonPathQuery Parse(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (_cache.TryGetValue(path, out var cached)) return cached;

        var query = Compile(path);
        if (Options.QueryCacheSize > 0)
        {
            // Bounded: when full, start over rather than track recency — a hot working set refills in a few calls,
            // and a stream of one-off queries cannot grow the cache without limit.
            if (_cache.Count >= Options.QueryCacheSize) _cache.Clear();
            _cache[path] = query;
        }

        return query;
    }

    /// <summary>Parses (cached) and runs a query: every selected node, in order.</summary>
    public IReadOnlyList<JsonPathMatch> Select(string path, JsonNode? root) => Parse(path).Select(root);

    /// <summary>Parses (cached) and runs a query, returning the first match or null.</summary>
    public JsonPathMatch? SelectFirst(string path, JsonNode? root) => Parse(path).SelectFirst(root);

    /// <summary>Parses (cached) and runs a query that must select at most one node (Newtonsoft's <c>SelectToken</c>).</summary>
    public JsonPathMatch? SelectSingle(string path, JsonNode? root) => Parse(path).SelectSingle(root);

    private JsonPathQuery Compile(string path)
    {
        if (path.Length > Options.MaxQueryLength)
            throw new JsonPathException($"query is {path.Length} characters long; the configured limit is {Options.MaxQueryLength}", JsonPathErrorKind.Limit);

        switch (Options.Dialect)
        {
            case JsonPathDialect.Rfc9535:
                return CompileRfc(path, JsonPathDialect.Rfc9535);

            case JsonPathDialect.Extended:
                // Newtonsoft first: anything it accepts keeps its Newtonsoft meaning (a strict superset).
                // Only text it rejects is read as RFC 9535.
                JsonPathException newtonsoftError;
                try
                {
                    return CompileNewtonsoft(path);
                }
                catch (JsonPathException ex) when (ex.Kind == JsonPathErrorKind.Syntax)
                {
                    newtonsoftError = ex;
                }

                try
                {
                    return CompileRfc(path, JsonPathDialect.Rfc9535);
                }
                catch (JsonPathException rfcError) when (rfcError.Kind == JsonPathErrorKind.Syntax)
                {
                    throw new JsonPathException(
                        $"not valid in the Newtonsoft dialect ({newtonsoftError.Message}) and not valid RFC 9535 ({rfcError.Message})",
                        JsonPathErrorKind.Syntax,
                        rfcError.Position);
                }

            default:
                return CompileNewtonsoft(path);
        }
    }

    private JsonPathQuery CompileNewtonsoft(string path)
    {
        var filters = NewtonsoftParser.Parse(path);
        var settings = Options.Dialect == JsonPathDialect.Extended
            ? new EvalSettings(Options.RegexTimeout, Options.MaxDepth, Options.ErrorWhenNoMatch, Options.EmulateNewtonsoftDates, Options.StrictIRegexp, negativeIndexesFromEnd: true)
            : _settings;
        return new JsonPathQuery(path, JsonPathDialect.Newtonsoft, new NewtonsoftPlan(filters, settings));
    }

    private JsonPathQuery CompileRfc(string path, JsonPathDialect reported)
    {
        var ast = RfcParser.Parse(path, Options.MaxDepth, _functions, Options.RegexTimeout, Options.StrictIRegexp);
        return new JsonPathQuery(path, reported, new RfcPlan(ast, _settings));
    }
}
