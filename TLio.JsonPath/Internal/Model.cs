#nullable disable
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TLio.JsonPath.Internal;

/// <summary>Coarse kind of a document node — the only thing the evaluators ask of a node besides its children.</summary>
internal enum NodeKind : byte
{
    Null,
    Bool,
    String,
    Number,
    Object,
    Array,
}

/// <summary>
/// The whole of what the evaluators need from a document model. <typeparamref name="TNode"/> is
/// the model's own node type; for System.Text.Json it is <see cref="JsonNode"/> and JSON null is
/// the C# null reference (so "absent" is always expressed by not producing a node at all, never by
/// a null). A JToken-based model can be added by implementing this interface; nothing outside
/// <see cref="JsonNodeModel"/> mentions System.Text.Json.Nodes.
/// </summary>
internal interface IJsonModel<TNode>
{
    NodeKind KindOf(TNode node);

    /// <summary>Element count of an array, member count of an object.</summary>
    int Count(TNode node);

    TNode ElementAt(TNode array, int index);

    void MemberAt(TNode obj, int index, out string name, out TNode value);

    /// <summary>True when the member exists — even when its value is JSON null.</summary>
    bool TryGetMember(TNode obj, string name, out TNode value);

    string GetString(TNode node);

    bool GetBool(TNode node);

    /// <summary>
    /// The number as <see cref="Prim"/>. <paramref name="bigIntegers"/> selects how an integer too big for
    /// <see cref="long"/> is read: as a <see cref="BigInteger"/> (Newtonsoft) or as a double (RFC 9535 / I-JSON).
    /// </summary>
    Prim GetNumber(TNode node, bool bigIntegers);
}

/// <summary>The <see cref="IJsonModel{TNode}"/> for System.Text.Json.Nodes.</summary>
internal readonly struct JsonNodeModel : IJsonModel<JsonNode>
{
    public NodeKind KindOf(JsonNode node)
    {
        if (node is null) return NodeKind.Null;
        if (node is JsonObject) return NodeKind.Object;
        if (node is JsonArray) return NodeKind.Array;
        return node.GetValueKind() switch
        {
            JsonValueKind.String => NodeKind.String,
            JsonValueKind.Number => NodeKind.Number,
            JsonValueKind.True or JsonValueKind.False => NodeKind.Bool,
            _ => NodeKind.Null,
        };
    }

    public int Count(JsonNode node) => node is JsonObject o ? o.Count : ((JsonArray)node).Count;

    public JsonNode ElementAt(JsonNode array, int index) => ((JsonArray)array)[index];

    public void MemberAt(JsonNode obj, int index, out string name, out JsonNode value)
    {
        var kv = ((JsonObject)obj).GetAt(index);
        name = kv.Key;
        value = kv.Value;
    }

    public bool TryGetMember(JsonNode obj, string name, out JsonNode value) =>
        ((JsonObject)obj).TryGetPropertyValue(name, out value);

    public string GetString(JsonNode node) => ((JsonValue)node).GetValue<string>();

    public bool GetBool(JsonNode node) => node.GetValueKind() == JsonValueKind.True;

    public Prim GetNumber(JsonNode node, bool bigIntegers)
    {
        var value = (JsonValue)node;

        // The common case: a number that came out of the parser is backed by a JsonElement, and
        // reading it that way allocates nothing.
        if (value.TryGetValue(out JsonElement element) && element.ValueKind == JsonValueKind.Number)
        {
            if (element.TryGetInt64(out var l)) return Prim.FromLong(l);
            var raw = element.GetRawText();
            return ClassifyNumberText(raw, bigIntegers);
        }

        // A node built in code (JsonValue.Create(5m), …) carries a CLR value instead.
        if (value.TryGetValue(out long cl)) return Prim.FromLong(cl);
        if (value.TryGetValue(out int ci)) return Prim.FromLong(ci);
        if (value.TryGetValue(out double cd)) return Prim.FromDouble(cd);
        return ClassifyNumberText(value.ToJsonString(), bigIntegers);
    }

    /// <summary>
    /// Newtonsoft reads an integer literal as long (or BigInteger when it overflows) and anything
    /// with a fraction or exponent as double — "1.0" and "1e2" are floats, whatever their value.
    /// </summary>
    internal static Prim ClassifyNumberText(string raw, bool bigIntegers)
    {
        var isFloat = raw.AsSpan().IndexOfAny('.', 'e', 'E') >= 0;
        if (!isFloat)
        {
            if (long.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var l))
                return Prim.FromLong(l);
            if (bigIntegers && BigInteger.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var big))
                return Prim.FromBig(big);
        }

        return Prim.FromDouble(double.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture));
    }
}

internal enum PrimKind : byte
{
    Null,
    Bool,
    String,
    Long,
    Double,
    BigInt,
    Date,
}

/// <summary>A JSON primitive, detached from any document: what a literal in a query is, and what a primitive node reads as.</summary>
internal readonly struct Prim
{
    public readonly PrimKind Kind;
    public readonly long L;
    public readonly double D;
    public readonly object O;

    private Prim(PrimKind kind, long l, double d, object o)
    {
        Kind = kind;
        L = l;
        D = d;
        O = o;
    }

    public static Prim Null => new(PrimKind.Null, 0, 0, null);
    public static Prim FromBool(bool b) => new(PrimKind.Bool, b ? 1 : 0, 0, null);
    public static Prim FromString(string s) => new(PrimKind.String, 0, 0, s);
    public static Prim FromLong(long l) => new(PrimKind.Long, l, 0, null);
    public static Prim FromDouble(double d) => new(PrimKind.Double, 0, d, null);
    public static Prim FromBig(BigInteger b) => new(PrimKind.BigInt, 0, 0, b);
    public static Prim FromDate(object date) => new(PrimKind.Date, 0, 0, date);

    public bool IsNumber => Kind is PrimKind.Long or PrimKind.Double or PrimKind.BigInt;
    public string Str => (string)O;
    public bool Bool => L != 0;
    public BigInteger Big => (BigInteger)O;
}

/// <summary>RFC 9535 §2.7 normalized-path text.</summary>
internal static class NormalizedPaths
{
    public static string Name(string name)
    {
        var sb = new System.Text.StringBuilder(name.Length + 4);
        AppendName(sb, name);
        return sb.ToString();
    }

    public static string Index(int index) => "[" + index.ToString(CultureInfo.InvariantCulture) + "]";

    internal static void AppendName(System.Text.StringBuilder sb, string name)
    {
        sb.Append("['");
        foreach (var c in name)
        {
            switch (c)
            {
                case '\'': sb.Append("\\'"); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.Append("\\u00").Append(((int)c).ToString("x2", CultureInfo.InvariantCulture));
                    else sb.Append(c);
                    break;
            }
        }

        sb.Append("']");
    }
}
