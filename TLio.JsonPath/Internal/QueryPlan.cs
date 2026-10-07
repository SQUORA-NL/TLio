#nullable disable
using System.Text.Json.Nodes;
using TLio.JsonPath.Internal.Newtonsoft;
using TLio.JsonPath.Internal.Rfc;

namespace TLio.JsonPath.Internal;

/// <summary>A compiled, immutable query ready to run against a document.</summary>
internal abstract class QueryPlan
{
    /// <summary>Appends matches to <paramref name="output"/>, stopping once it holds <paramref name="limit"/> of them.</summary>
    public abstract void Execute(JsonNode root, List<JsonPathMatch> output, int limit);
}

internal sealed class RfcPlan : QueryPlan
{
    private readonly QueryAst _ast;
    private readonly EvalSettings _settings;

    public RfcPlan(QueryAst ast, EvalSettings settings)
    {
        _ast = ast;
        _settings = settings;
    }

    public override void Execute(JsonNode root, List<JsonPathMatch> output, int limit)
    {
        var evaluator = new RfcEvaluator<JsonNode, JsonNodeModel>(_settings);
        var hits = evaluator.Select(_ast, root);
        var resolver = new PathResolver<JsonNode, JsonNodeModel>(root);
        var n = Math.Min(hits.Count, limit);
        for (var i = 0; i < n; i++)
            output.Add(new JsonPathMatch(hits[i].Node, hits[i].Parent, hits[i].Name, hits[i].Index, resolver));
        resolver.SetMatches(output);
    }
}

internal sealed class NewtonsoftPlan : QueryPlan
{
    private readonly List<NFilter> _filters;
    private readonly EvalSettings _settings;

    // A top-level query made only of names and non-negative indexes — `$.store.book[0].title`, by far the most common
    // shape — selects at most one node and cannot fail, so it is followed directly. (With ErrorWhenNoMatch a missing
    // step is an error, which the general path reports.)
    private readonly bool _simple;

    public NewtonsoftPlan(List<NFilter> filters, EvalSettings settings)
    {
        _filters = filters;
        _settings = settings;
        _simple = !settings.ErrorWhenNoMatch && NSimplePath.IsSimple(filters) && !filters.Any(f => f is NRootFilter);
    }

    public override void Execute(JsonNode root, List<JsonPathMatch> output, int limit)
    {
        var evaluator = new NewtonsoftEvaluator<JsonNode, JsonNodeModel>(_settings);
        var rootToken = new NToken<JsonNode>(root, null, null, -1, false);
        var resolver = new PathResolver<JsonNode, JsonNodeModel>(root);

        if (_simple)
        {
            if (evaluator.TryFollowPlain(_filters, rootToken, out var found))
            {
                output.Add(new JsonPathMatch(found.Node, found.Parent, found.Name, found.Index, resolver));
                resolver.SetMatches(output);
            }

            return;
        }

        // Lazy, like Newtonsoft's: stopping at `limit` leaves later filters (and their errors) unevaluated,
        // which is what makes SelectToken report "multiple tokens" before it could hit a conversion error.
        foreach (var t in evaluator.Evaluate(_filters, rootToken, rootToken))
        {
            output.Add(new JsonPathMatch(t.Node, t.Parent, t.Name, t.Index, resolver, t.IsProperty));
            if (output.Count >= limit) break;
        }

        resolver.SetMatches(output);
    }
}
