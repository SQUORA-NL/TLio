#nullable disable
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace TLio.JsonPath.Internal.Rfc;

/// <summary>A node found by a query, with where it was found (<see cref="Loc"/> is null when location tracking is off).</summary>
internal readonly struct Hit<TNode>(TNode node, Loc loc)
{
    public readonly TNode Node = node;
    public readonly Loc Loc = loc;
}

/// <summary>Per-evaluation limits taken from <see cref="JsonPathOptions"/>.</summary>
internal readonly struct EvalSettings(TimeSpan regexTimeout, int maxDepth, bool errorWhenNoMatch, bool emulateDates, bool strictRegexp)
{
    public readonly bool StrictRegexp = strictRegexp;
    public readonly TimeSpan RegexTimeout = regexTimeout;
    public readonly int MaxDepth = maxDepth;
    public readonly bool ErrorWhenNoMatch = errorWhenNoMatch;
    public readonly bool EmulateDates = emulateDates;
}

/// <summary>ValueType result: Nothing, a node of the document, or a detached primitive (a literal, a computed length).</summary>
internal readonly struct Val<TNode>
{
    public readonly byte Kind; // 0 Nothing, 1 Node, 2 Prim
    public readonly TNode Node;
    public readonly Prim Prim;

    private Val(byte kind, TNode node, Prim prim)
    {
        Kind = kind;
        Node = node;
        Prim = prim;
    }

    public static Val<TNode> Nothing => new(0, default, default);
    public static Val<TNode> OfNode(TNode n) => new(1, n, default);
    public static Val<TNode> OfPrim(Prim p) => new(2, default, p);
    public bool IsNothing => Kind == 0;
}

/// <summary>
/// Evaluates an RFC 9535 <see cref="QueryAst"/> over a document model (§2.1–§2.5): nodelists are
/// ordered, may contain duplicates, and an empty one is a result, not an error. Instances hold no
/// state beyond their settings, so one evaluator can serve any number of concurrent queries.
/// </summary>
internal sealed class RfcEvaluator<TNode, TModel> where TModel : struct, IJsonModel<TNode>
{
    private readonly TModel _m;
    private readonly EvalSettings _settings;

    public RfcEvaluator(EvalSettings settings)
    {
        _m = default;
        _settings = settings;
    }

    // ── queries ───────────────────────────────────────────────────────────────────────────

    public List<Hit<TNode>> Select(QueryAst query, TNode root, bool track)
    {
        var current = new List<Hit<TNode>>(1) { new(root, track ? Loc.Root : null) };
        return Run(query, current, root);
    }

    private List<Hit<TNode>> Run(QueryAst query, List<Hit<TNode>> current, TNode root)
    {
        var segments = query.Segments;
        for (var i = 0; i < segments.Length; i++)
        {
            if (current.Count == 0) break;
            var seg = segments[i];
            var next = new List<Hit<TNode>>();
            if (seg.Descendant)
                for (var j = 0; j < current.Count; j++) Descend(current[j], seg, next, root);
            else
                for (var j = 0; j < current.Count; j++) ApplySelectors(current[j], seg.Selectors, next, root);
            current = next;
        }

        return current;
    }

    /// <summary>Runs a (sub-)query from the root or the current node without tracking locations.</summary>
    private List<Hit<TNode>> RunQuery(QueryAst q, TNode root, TNode current)
    {
        var start = new List<Hit<TNode>>(1) { new(q.Absolute ? root : current, null) };
        return Run(q, start, root);
    }

    private void Descend(Hit<TNode> start, Segment seg, List<Hit<TNode>> output, TNode root)
    {
        // Pre-order: a node, then its children left to right — RFC 9535 §2.5.2.2. Explicit stack, so a deep
        // document cannot overflow the call stack; depth is still bounded by MaxDepth.
        var stack = new Stack<(Hit<TNode> Hit, int Depth)>();
        stack.Push((start, 0));
        while (stack.Count > 0)
        {
            var (hit, depth) = stack.Pop();
            ApplySelectors(hit, seg.Selectors, output, root);
            var kind = _m.KindOf(hit.Node);
            if (kind != NodeKind.Object && kind != NodeKind.Array) continue;
            if (depth + 1 > _settings.MaxDepth)
                throw new JsonPathException($"document is nested deeper than the configured limit of {_settings.MaxDepth}", JsonPathErrorKind.Limit);
            var count = _m.Count(hit.Node);
            for (var i = count - 1; i >= 0; i--)
            {
                if (kind == NodeKind.Object)
                {
                    _m.MemberAt(hit.Node, i, out var name, out var value);
                    stack.Push((new Hit<TNode>(value, hit.Loc == null ? null : Loc.Member(hit.Loc, name, hit.Node)), depth + 1));
                }
                else
                {
                    stack.Push((new Hit<TNode>(_m.ElementAt(hit.Node, i), hit.Loc == null ? null : Loc.Element(hit.Loc, i, hit.Node)), depth + 1));
                }
            }
        }
    }

    private void ApplySelectors(Hit<TNode> hit, Selector[] selectors, List<Hit<TNode>> output, TNode root)
    {
        for (var i = 0; i < selectors.Length; i++)
            ApplySelector(hit, selectors[i], output, root);
    }

    private void ApplySelector(Hit<TNode> hit, Selector selector, List<Hit<TNode>> output, TNode root)
    {
        var node = hit.Node;
        var kind = _m.KindOf(node);
        switch (selector)
        {
            case NameSelector name:
                if (kind == NodeKind.Object && _m.TryGetMember(node, name.Name, out var v))
                    output.Add(new Hit<TNode>(v, hit.Loc == null ? null : Loc.Member(hit.Loc, name.Name, node)));
                break;

            case WildcardSelector:
                if (kind == NodeKind.Object)
                {
                    var n = _m.Count(node);
                    for (var i = 0; i < n; i++)
                    {
                        _m.MemberAt(node, i, out var k, out var mv);
                        output.Add(new Hit<TNode>(mv, hit.Loc == null ? null : Loc.Member(hit.Loc, k, node)));
                    }
                }
                else if (kind == NodeKind.Array)
                {
                    var n = _m.Count(node);
                    for (var i = 0; i < n; i++)
                        output.Add(new Hit<TNode>(_m.ElementAt(node, i), hit.Loc == null ? null : Loc.Element(hit.Loc, i, node)));
                }

                break;

            case IndexSelector index:
                if (kind == NodeKind.Array)
                {
                    var len = _m.Count(node);
                    var i = index.Index >= 0 ? index.Index : len + index.Index;
                    if (i >= 0 && i < len)
                        output.Add(new Hit<TNode>(_m.ElementAt(node, (int)i), hit.Loc == null ? null : Loc.Element(hit.Loc, (int)i, node)));
                }

                break;

            case SliceSelector slice:
                if (kind == NodeKind.Array) ApplySlice(hit, slice, output);
                break;

            case FilterSelector filter:
                if (kind == NodeKind.Object)
                {
                    var n = _m.Count(node);
                    for (var i = 0; i < n; i++)
                    {
                        _m.MemberAt(node, i, out var k, out var mv);
                        if (EvalLogical(filter.Expression, root, mv))
                            output.Add(new Hit<TNode>(mv, hit.Loc == null ? null : Loc.Member(hit.Loc, k, node)));
                    }
                }
                else if (kind == NodeKind.Array)
                {
                    var n = _m.Count(node);
                    for (var i = 0; i < n; i++)
                    {
                        var el = _m.ElementAt(node, i);
                        if (EvalLogical(filter.Expression, root, el))
                            output.Add(new Hit<TNode>(el, hit.Loc == null ? null : Loc.Element(hit.Loc, i, node)));
                    }
                }

                break;
        }
    }

    private void ApplySlice(Hit<TNode> hit, SliceSelector slice, List<Hit<TNode>> output)
    {
        // RFC 9535 §2.3.4.2.2
        var node = hit.Node;
        long len = _m.Count(node);
        var step = slice.Step ?? 1;
        if (step == 0) return;
        var start = slice.Start ?? (step >= 0 ? 0 : len - 1);
        var end = slice.End ?? (step >= 0 ? len : -len - 1);
        var nStart = start >= 0 ? start : len + start;
        var nEnd = end >= 0 ? end : len + end;
        long lower, upper;
        if (step >= 0)
        {
            lower = Math.Min(Math.Max(nStart, 0), len);
            upper = Math.Min(Math.Max(nEnd, 0), len);
            for (var i = lower; i < upper; i += step)
                output.Add(new Hit<TNode>(_m.ElementAt(node, (int)i), hit.Loc == null ? null : Loc.Element(hit.Loc, (int)i, node)));
        }
        else
        {
            upper = Math.Min(Math.Max(nStart, -1), len - 1);
            lower = Math.Min(Math.Max(nEnd, -1), len - 1);
            for (var i = upper; lower < i; i += step)
                output.Add(new Hit<TNode>(_m.ElementAt(node, (int)i), hit.Loc == null ? null : Loc.Element(hit.Loc, (int)i, node)));
        }
    }

    // ── logical expressions ───────────────────────────────────────────────────────────────

    private bool EvalLogical(Expr e, TNode root, TNode current)
    {
        switch (e)
        {
            case OrExpr or:
                foreach (var o in or.Operands)
                    if (EvalLogical(o, root, current)) return true;
                return false;
            case AndExpr and:
                foreach (var o in and.Operands)
                    if (!EvalLogical(o, root, current)) return false;
                return true;
            case NotExpr not:
                return !EvalLogical(not.Operand, root, current);
            case CompareExpr cmp:
                return Compare(cmp, root, current);
            case TestExpr test:
                return EvalLogical(test.Operand, root, current);
            case QueryExpr q:
                // A query used as a test: true when it selects anything (§2.3.5.2.1).
                return q.IsSingular ? TrySingular(q.Query, root, current, out _) : RunQuery(q.Query, root, current).Count > 0;
            case FunctionExpr f:
                return f.ReturnType == JsonPathFunctionType.Logical ? CallLogical(f, root, current) : CallNodes(f, root, current).Count > 0;
            default:
                throw new InvalidOperationException("not a logical expression");
        }
    }

    private bool Compare(CompareExpr cmp, TNode root, TNode current)
    {
        var l = EvalValue(cmp.Left, root, current);
        var r = EvalValue(cmp.Right, root, current);
        switch (cmp.Op)
        {
            case CompareOp.Eq: return ValEquals(l, r);
            case CompareOp.Ne: return !ValEquals(l, r);
            case CompareOp.Lt: return ValLess(l, r);
            case CompareOp.Gt: return ValLess(r, l);
            case CompareOp.Le: return ValLess(l, r) || ValEquals(l, r);
            default: return ValLess(r, l) || ValEquals(l, r);
        }
    }

    // ── values ────────────────────────────────────────────────────────────────────────────

    private Val<TNode> EvalValue(Expr e, TNode root, TNode current)
    {
        switch (e)
        {
            case LiteralExpr lit:
                return Val<TNode>.OfPrim(lit.Value);
            case QueryExpr q:
                return TrySingular(q.Query, root, current, out var n) ? Val<TNode>.OfNode(n) : Val<TNode>.Nothing;
            case FunctionExpr f:
                return CallValue(f, root, current);
            default:
                throw new InvalidOperationException("not a value expression");
        }
    }

    /// <summary>Evaluates a singular query without building node lists: follows its one name/index per segment.</summary>
    private bool TrySingular(QueryAst q, TNode root, TNode current, out TNode result)
    {
        var node = q.Absolute ? root : current;
        foreach (var seg in q.Segments)
        {
            var sel = seg.Selectors[0];
            var kind = _m.KindOf(node);
            if (sel is NameSelector name)
            {
                if (kind != NodeKind.Object || !_m.TryGetMember(node, name.Name, out node)) { result = default; return false; }
            }
            else
            {
                var idx = ((IndexSelector)sel).Index;
                if (kind != NodeKind.Array) { result = default; return false; }
                var len = _m.Count(node);
                var i = idx >= 0 ? idx : len + idx;
                if (i < 0 || i >= len) { result = default; return false; }
                node = _m.ElementAt(node, (int)i);
            }
        }

        result = node;
        return true;
    }

    private bool TryPrim(in Val<TNode> v, out Prim p)
    {
        if (v.Kind == 2) { p = v.Prim; return true; }
        var node = v.Node;
        switch (_m.KindOf(node))
        {
            case NodeKind.Null: p = Prim.Null; return true;
            case NodeKind.Bool: p = Prim.FromBool(_m.GetBool(node)); return true;
            case NodeKind.String: p = Prim.FromString(_m.GetString(node)); return true;
            case NodeKind.Number: p = _m.GetNumber(node, bigIntegers: false); return true;
            default: p = default; return false;
        }
    }

    private bool ValEquals(in Val<TNode> a, in Val<TNode> b)
    {
        if (a.IsNothing || b.IsNothing) return a.IsNothing && b.IsNothing;
        var pa = TryPrim(a, out var xa);
        var pb = TryPrim(b, out var xb);
        if (pa && pb) return PrimEquals(xa, xb);
        if (!pa && !pb) return DeepEquals(a.Node, b.Node);
        return false;
    }

    private static bool PrimEquals(in Prim a, in Prim b)
    {
        if (a.IsNumber && b.IsNumber) return CompareNumbers(a, b) == 0;
        if (a.Kind != b.Kind) return false;
        return a.Kind switch
        {
            PrimKind.Null => true,
            PrimKind.Bool => a.L == b.L,
            PrimKind.String => string.Equals(a.Str, b.Str, StringComparison.Ordinal),
            _ => false,
        };
    }

    private bool ValLess(in Val<TNode> a, in Val<TNode> b)
    {
        if (a.IsNothing || b.IsNothing) return false;
        if (!TryPrim(a, out var xa) || !TryPrim(b, out var xb)) return false;
        if (xa.IsNumber && xb.IsNumber) return CompareNumbers(xa, xb) < 0;
        if (xa.Kind == PrimKind.String && xb.Kind == PrimKind.String) return CompareCodePoints(xa.Str, xb.Str) < 0;
        return false;
    }

    private bool DeepEquals(TNode a, TNode b)
    {
        var ka = _m.KindOf(a);
        if (ka != _m.KindOf(b)) return false;
        switch (ka)
        {
            case NodeKind.Null: return true;
            case NodeKind.Bool: return _m.GetBool(a) == _m.GetBool(b);
            case NodeKind.String: return string.Equals(_m.GetString(a), _m.GetString(b), StringComparison.Ordinal);
            case NodeKind.Number: return CompareNumbers(_m.GetNumber(a, false), _m.GetNumber(b, false)) == 0;
            case NodeKind.Array:
                var n = _m.Count(a);
                if (n != _m.Count(b)) return false;
                for (var i = 0; i < n; i++)
                    if (!DeepEquals(_m.ElementAt(a, i), _m.ElementAt(b, i))) return false;
                return true;
            default:
                var c = _m.Count(a);
                if (c != _m.Count(b)) return false;
                for (var i = 0; i < c; i++)
                {
                    _m.MemberAt(a, i, out var name, out var va);
                    if (!_m.TryGetMember(b, name, out var vb) || !DeepEquals(va, vb)) return false;
                }

                return true;
        }
    }

    internal static int CompareNumbers(in Prim a, in Prim b)
    {
        if (a.Kind == PrimKind.Long && b.Kind == PrimKind.Long) return a.L.CompareTo(b.L);
        if (a.Kind == PrimKind.Long) return CompareLongDouble(a.L, b.D);
        if (b.Kind == PrimKind.Long) return -CompareLongDouble(b.L, a.D);
        return a.D.CompareTo(b.D);
    }

    /// <summary>Exact comparison of an integer with a double — no precision is lost above 2^53.</summary>
    private static int CompareLongDouble(long l, double d)
    {
        if (double.IsNaN(d)) return 1;
        if (d >= 9.2233720368547758E+18) return -1;
        if (d < -9.2233720368547758E+18) return 1;
        var floor = Math.Floor(d);
        var fl = (long)floor;
        if (l != fl) return l < fl ? -1 : 1;
        return d > floor ? -1 : 0;
    }

    /// <summary>Compares by Unicode scalar value, as RFC 9535 §2.3.5.2.2 requires — not by UTF-16 unit, which orders astral characters before U+E000..U+FFFF.</summary>
    internal static int CompareCodePoints(string a, string b)
    {
        var n = Math.Min(a.Length, b.Length);
        for (var i = 0; i < n; i++)
        {
            var ca = a[i];
            var cb = b[i];
            if (ca == cb) continue;
            var sa = char.IsSurrogate(ca);
            var sb = char.IsSurrogate(cb);
            if (sa != sb) return sa ? 1 : -1;
            return ca < cb ? -1 : 1;
        }

        return a.Length.CompareTo(b.Length);
    }

    // ── functions ─────────────────────────────────────────────────────────────────────────

    private Val<TNode> CallValue(FunctionExpr f, TNode root, TNode current)
    {
        switch (f.BuiltIn)
        {
            case BuiltIn.Length:
            {
                var v = EvalValue(f.Args[0], root, current);
                if (v.IsNothing) return Val<TNode>.Nothing;
                if (v.Kind == 2)
                    return v.Prim.Kind == PrimKind.String ? Val<TNode>.OfPrim(Prim.FromLong(CountScalars(v.Prim.Str))) : Val<TNode>.Nothing;
                switch (_m.KindOf(v.Node))
                {
                    case NodeKind.String: return Val<TNode>.OfPrim(Prim.FromLong(CountScalars(_m.GetString(v.Node))));
                    case NodeKind.Array:
                    case NodeKind.Object: return Val<TNode>.OfPrim(Prim.FromLong(_m.Count(v.Node)));
                    default: return Val<TNode>.Nothing;
                }
            }
            case BuiltIn.Count:
                return Val<TNode>.OfPrim(Prim.FromLong(EvalNodes(f.Args[0], root, current).Count));
            case BuiltIn.Value:
            {
                var nodes = EvalNodes(f.Args[0], root, current);
                return nodes.Count == 1 ? Val<TNode>.OfNode(nodes[0]) : Val<TNode>.Nothing;
            }
            default:
            {
                var result = InvokeCustom(f, root, current);
                var jv = (JsonPathValue)result;
                if (jv.IsNothing) return Val<TNode>.Nothing;
                return Val<TNode>.OfNode((TNode)(object)jv.Node);
            }
        }
    }

    private static long CountScalars(string s)
    {
        long n = s.Length;
        for (var i = 1; i < s.Length; i++)
            if (char.IsLowSurrogate(s[i]) && char.IsHighSurrogate(s[i - 1])) n--;
        return n;
    }

    private bool CallLogical(FunctionExpr f, TNode root, TNode current)
    {
        if (f.BuiltIn is BuiltIn.Match or BuiltIn.Search)
        {
            var subject = EvalValue(f.Args[0], root, current);
            var pattern = EvalValue(f.Args[1], root, current);
            if (!TryString(subject, out var s) || !TryString(pattern, out var p)) return false;
            var regex = f.HasPrecompiledPattern ? f.PrecompiledPattern : IRegexp.TryCompile(p, f.BuiltIn == BuiltIn.Match, _settings.RegexTimeout, _settings.StrictRegexp);
            if (regex == null) return false;
            try
            {
                return regex.IsMatch(s);
            }
            catch (RegexMatchTimeoutException ex)
            {
                throw new JsonPathException($"regular expression evaluation exceeded the configured timeout of {_settings.RegexTimeout}", JsonPathErrorKind.Limit, -1, ex);
            }
        }

        return (bool)InvokeCustom(f, root, current);
    }

    private bool TryString(in Val<TNode> v, out string s)
    {
        if (v.Kind == 2 && v.Prim.Kind == PrimKind.String) { s = v.Prim.Str; return true; }
        if (v.Kind == 1 && _m.KindOf(v.Node) == NodeKind.String) { s = _m.GetString(v.Node); return true; }
        s = null;
        return false;
    }

    private List<TNode> CallNodes(FunctionExpr f, TNode root, TNode current)
    {
        var result = (IReadOnlyList<JsonNode>)InvokeCustom(f, root, current);
        var list = new List<TNode>(result.Count);
        foreach (var n in result) list.Add((TNode)(object)n);
        return list;
    }

    private List<TNode> EvalNodes(Expr e, TNode root, TNode current)
    {
        switch (e)
        {
            case QueryExpr q:
                var hits = RunQuery(q.Query, root, current);
                var list = new List<TNode>(hits.Count);
                foreach (var h in hits) list.Add(h.Node);
                return list;
            case FunctionExpr f:
                return CallNodes(f, root, current);
            default:
                throw new InvalidOperationException("not a node-list expression");
        }
    }

    /// <summary>Calls a custom function, converting arguments to its declared parameter types; returns a JsonPathValue, bool or list depending on its declared result.</summary>
    private object InvokeCustom(FunctionExpr f, TNode root, TNode current)
    {
        var args = new object[f.Args.Length];
        for (var i = 0; i < args.Length; i++)
        {
            switch (f.ParameterTypes[i])
            {
                case JsonPathFunctionType.Value:
                    var v = EvalValue(f.Args[i], root, current);
                    args[i] = v.IsNothing ? JsonPathValue.Nothing : JsonPathValue.Of(ToJsonNode(v));
                    break;
                case JsonPathFunctionType.Logical:
                    args[i] = EvalLogical(f.Args[i], root, current);
                    break;
                default:
                    var nodes = EvalNodes(f.Args[i], root, current);
                    var arr = new JsonNode[nodes.Count];
                    for (var j = 0; j < arr.Length; j++) arr[j] = (JsonNode)(object)nodes[j];
                    args[i] = arr;
                    break;
            }
        }

        var result = f.Custom.Implementation(new JsonPathFunctionArguments(args)).Payload;
        var ok = f.ReturnType switch
        {
            JsonPathFunctionType.Value => result is JsonPathValue,
            JsonPathFunctionType.Logical => result is bool,
            _ => result is IReadOnlyList<JsonNode>,
        };
        if (!ok)
            throw new JsonPathException($"function '{f.Name}' declared a {f.ReturnType} result but returned a different kind", JsonPathErrorKind.Evaluation);
        return result;
    }

    private JsonNode ToJsonNode(in Val<TNode> v)
    {
        if (v.Kind == 1) return (JsonNode)(object)v.Node;
        var p = v.Prim;
        return p.Kind switch
        {
            PrimKind.Null => null,
            PrimKind.Bool => JsonValue.Create(p.Bool),
            PrimKind.String => JsonValue.Create(p.Str),
            PrimKind.Long => JsonValue.Create(p.L),
            _ => JsonValue.Create(p.D),
        };
    }
}
