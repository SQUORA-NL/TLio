#nullable disable
using System.Text.Json.Nodes;
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
        var hits = evaluator.Select(_ast, root, track: true);
        var n = Math.Min(hits.Count, limit);
        for (var i = 0; i < n; i++)
            output.Add(new JsonPathMatch(hits[i].Node, hits[i].Loc));
    }
}
