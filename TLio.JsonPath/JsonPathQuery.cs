using System.Text.Json.Nodes;
using TLio.JsonPath.Internal;

namespace TLio.JsonPath;

/// <summary>
/// A parsed query. Immutable and thread-safe: it holds no per-evaluation state, so one instance can
/// be cached and used from any number of threads at once.
/// </summary>
public sealed class JsonPathQuery
{
    private readonly QueryPlan _plan;

    internal JsonPathQuery(string text, JsonPathDialect dialect, QueryPlan plan)
    {
        Text = text;
        Dialect = dialect;
        _plan = plan;
    }

    /// <summary>The query text as given.</summary>
    public string Text { get; }

    /// <summary>The dialect the query was parsed in. For <see cref="JsonPathDialect.Extended"/> this is the dialect the text actually turned out to be (Newtonsoft or RFC 9535).</summary>
    public JsonPathDialect Dialect { get; }

    /// <summary>Every node the query selects, in order. An empty list is a valid result.</summary>
    public IReadOnlyList<JsonPathMatch> Select(JsonNode? root)
    {
        var output = new List<JsonPathMatch>();
        _plan.Execute(root, output, int.MaxValue);
        return output;
    }

    /// <summary>The first selected node, or null when the query matches nothing. Does not fail when there are several.</summary>
    public JsonPathMatch? SelectFirst(JsonNode? root)
    {
        var output = new List<JsonPathMatch>(1);
        _plan.Execute(root, output, 1);
        return output.Count == 0 ? null : output[0];
    }

    /// <summary>
    /// The one selected node, or null when there is none — Newtonsoft's <c>SelectToken</c>: more than one match throws
    /// <see cref="JsonPathException"/> of kind <see cref="JsonPathErrorKind.MultipleResults"/>.
    /// </summary>
    public JsonPathMatch? SelectSingle(JsonNode? root)
    {
        var output = new List<JsonPathMatch>(2);
        _plan.Execute(root, output, 2);
        if (output.Count > 1)
            throw new JsonPathException("Path returned multiple tokens.", JsonPathErrorKind.MultipleResults);
        return output.Count == 0 ? null : output[0];
    }

    /// <summary>True when the query selects at least one node.</summary>
    public bool Exists(JsonNode? root) => SelectFirst(root) != null;

    /// <inheritdoc />
    public override string ToString() => Text;
}
