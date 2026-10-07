using System.Text;
using System.Text.Json.Nodes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TLio.JsonPath.Internal;

namespace TLio.JsonPath.Tests.Differential;

/// <summary>What one engine did with one (query, document): hits, or the category of failure.</summary>
public sealed class Outcome
{
    public List<(string Path, bool IsProperty)>? Hits { get; init; }

    /// <summary>Syntax, Evaluation (includes NoMatch), Conversion, Limit, Multiple — or null on success.</summary>
    public string? Error { get; init; }

    public string? Detail { get; init; }

    public override string ToString() => Error != null
        ? $"ERROR[{Error}] {Detail}"
        : $"[{string.Join(", ", Hits!.Select(h => h.Path + (h.IsProperty ? " <prop>" : "")))}]";
}

/// <summary>
/// Newtonsoft.Json 13.0.4 as the oracle. Everything is reduced to the same comparable shape: the RFC
/// normalized path of each hit (a path identifies a node uniquely within one document, so equal
/// paths in equal order means the same nodes in the same order) plus whether Newtonsoft returned a
/// JProperty wrapper, and an error category instead of an exception type.
/// </summary>
public static class NewtonsoftOracle
{
    public static Outcome SelectTokens(string docJson, string path, bool errorWhenNoMatch = false)
    {
        var doc = JToken.Parse(docJson);
        IEnumerable<JToken> enumerable;
        try
        {
            enumerable = errorWhenNoMatch ? doc.SelectTokens(path, true) : doc.SelectTokens(path);
        }
        catch (Exception ex)
        {
            // JPath is parsed eagerly: anything thrown here is a syntax error, whatever its CLR type.
            return new Outcome { Error = "Syntax", Detail = ex.GetType().Name + ": " + ex.Message };
        }

        var hits = new List<(string, bool)>();
        try
        {
            foreach (var t in enumerable) hits.Add((NormalizedPath(t), t is JProperty));
        }
        catch (Exception ex)
        {
            return new Outcome { Error = Categorize(ex), Detail = ex.GetType().Name + ": " + ex.Message };
        }

        return new Outcome { Hits = hits };
    }

    public static Outcome SelectToken(string docJson, string path, bool errorWhenNoMatch = false)
    {
        var doc = JToken.Parse(docJson);
        JToken? token;
        try
        {
            token = errorWhenNoMatch ? doc.SelectToken(path, true) : doc.SelectToken(path);
        }
        catch (Exception ex)
        {
            // SelectToken parses and evaluates in one call: tell the phases apart by trying to parse alone.
            try
            {
                _ = doc.SelectTokens(path);
            }
            catch (Exception)
            {
                return new Outcome { Error = "Syntax", Detail = ex.GetType().Name + ": " + ex.Message };
            }

            return new Outcome { Error = Categorize(ex), Detail = ex.GetType().Name + ": " + ex.Message };
        }

        return new Outcome { Hits = token == null ? new() : [(NormalizedPath(token), token is JProperty)] };
    }

    private static string Categorize(Exception ex) => ex switch
    {
        JsonException { Message: "Path returned multiple tokens." } => "Multiple",
        JsonException => "Evaluation",
        FormatException or OverflowException or InvalidCastException => "Conversion",
        System.Text.RegularExpressions.RegexMatchTimeoutException => "Limit",
        _ => "Evaluation", // ArgumentOutOfRange (negative index), ArgumentException (bad regex), ...
    };

    /// <summary>The RFC 9535 normalized path of a Newtonsoft token (a JProperty is addressed as its value).</summary>
    public static string NormalizedPath(JToken token)
    {
        var parts = new List<string>();
        var current = token is JProperty p ? p.Value : token;
        while (current.Parent != null)
        {
            var parent = current.Parent;
            if (parent is JProperty prop)
            {
                var sb = new StringBuilder();
                Loc.AppendNormalizedName(sb, prop.Name);
                parts.Add(sb.ToString());
                current = prop.Parent!;
            }
            else if (parent is JArray arr)
            {
                parts.Add("[" + arr.IndexOf(current) + "]");
                current = parent;
            }
            else
            {
                break;
            }
        }

        parts.Reverse();
        return "$" + string.Concat(parts);
    }
}

/// <summary>Runs the engine and reduces its result to the same shape as <see cref="NewtonsoftOracle"/>.</summary>
public static class EngineRunner
{
    public static Outcome SelectTokens(JsonPathEngine engine, string docJson, string path)
    {
        var doc = JsonNode.Parse(docJson);
        JsonPathQuery query;
        try
        {
            query = engine.Parse(path);
        }
        catch (JsonPathException ex)
        {
            return new Outcome { Error = Category(ex), Detail = ex.Message };
        }

        try
        {
            return new Outcome { Hits = query.Select(doc).Select(m => (m.NormalizedPath, m.IsPropertyToken)).ToList() };
        }
        catch (JsonPathException ex)
        {
            return new Outcome { Error = Category(ex), Detail = ex.Message };
        }
    }

    public static Outcome SelectToken(JsonPathEngine engine, string docJson, string path)
    {
        var doc = JsonNode.Parse(docJson);
        JsonPathQuery query;
        try
        {
            query = engine.Parse(path);
        }
        catch (JsonPathException ex)
        {
            return new Outcome { Error = Category(ex), Detail = ex.Message };
        }

        try
        {
            var m = query.SelectSingle(doc);
            return new Outcome { Hits = m == null ? new() : [(m.Value.NormalizedPath, m.Value.IsPropertyToken)] };
        }
        catch (JsonPathException ex)
        {
            return new Outcome { Error = Category(ex), Detail = ex.Message };
        }
    }

    private static string Category(JsonPathException ex) => ex.Kind switch
    {
        JsonPathErrorKind.Syntax => "Syntax",
        JsonPathErrorKind.MultipleResults => "Multiple",
        JsonPathErrorKind.Conversion => "Conversion",
        JsonPathErrorKind.Limit => "Limit",
        _ => "Evaluation", // Evaluation and NoMatch
    };
}
