// The evaluator for Newtonsoft.Json 13.0.4's JSONPath dialect: PathFilter.ExecuteFilter and
// QueryExpression.IsMatch of Newtonsoft's Linq/JsonPath folder (MIT licensed, Copyright (c) 2007
// James Newton-King), re-expressed over IJsonModel instead of JToken. It keeps Newtonsoft's shape —
// a chain of lazy iterators, one per filter — because the observable behaviour (which results appear
// before an error, what SelectToken sees before "multiple tokens") follows from that laziness.
//
// JToken has a node Newtonsoft calls JProperty between an object and each member's value, and its
// query filters walk those wrappers. System.Text.Json has no such node, so a token here is
// (node, location, isProperty): a "property token" is a member value that still remembers it is
// being seen as its JProperty. That is what makes `$.obj[?(@ != 1)]` select properties — and
// `$.obj[?(@.a)]` select nothing — exactly as Newtonsoft does.
#nullable disable
using System.Text.RegularExpressions;
using TLio.JsonPath.Internal.Rfc;

namespace TLio.JsonPath.Internal.Newtonsoft;

/// <summary>A JToken: a document node, where it was found, and whether it is being seen as its owning JProperty.</summary>
internal readonly struct NToken<TNode>(TNode node, Loc loc, bool isProperty, string propertyName)
{
    public readonly TNode Node = node;
    public readonly Loc Loc = loc;
    public readonly bool IsProperty = isProperty;

    /// <summary>The JProperty's name when <see cref="IsProperty"/>; carried here because nested query paths run without location tracking.</summary>
    public readonly string PropertyName = propertyName;
}

/// <summary>One side of a comparison: a literal from the query, or a token selected by a path.</summary>
internal readonly struct NOperand<TNode>
{
    public readonly bool IsLiteral;
    public readonly Prim Literal;
    public readonly NToken<TNode> Token;

    public NOperand(Prim literal)
    {
        IsLiteral = true;
        Literal = literal;
        Token = default;
    }

    public NOperand(NToken<TNode> token)
    {
        IsLiteral = false;
        Literal = default;
        Token = token;
    }
}

internal sealed class NewtonsoftEvaluator<TNode, TModel> where TModel : struct, IJsonModel<TNode>
{
    private readonly TModel _m;
    private readonly EvalSettings _settings;

    public NewtonsoftEvaluator(EvalSettings settings)
    {
        _m = default;
        _settings = settings;
    }

    // ── entry points ──────────────────────────────────────────────────────────────────────

    /// <summary><c>JPath.Evaluate(root, t, settings)</c> for the top-level path (honours ErrorWhenNoMatch).</summary>
    public IEnumerable<NToken<TNode>> Evaluate(List<NFilter> filters, NToken<TNode> root, NToken<TNode> t) =>
        Evaluate(filters, root, t, _settings.ErrorWhenNoMatch);

    private IEnumerable<NToken<TNode>> Evaluate(List<NFilter> filters, NToken<TNode> root, NToken<TNode> t, bool errorWhenNoMatch)
    {
        IEnumerable<NToken<TNode>> current = new[] { t };
        foreach (var filter in filters)
            current = ExecuteFilter(filter, root, current, errorWhenNoMatch);
        return current;
    }

    private IEnumerable<NToken<TNode>> ExecuteFilter(NFilter filter, NToken<TNode> root, IEnumerable<NToken<TNode>> current, bool err)
    {
        switch (filter)
        {
            case NRootFilter: return new[] { root };
            case NFieldFilter f: return Field(f.Name, current, err);
            case NFieldMultipleFilter f: return FieldMultiple(f.Names, current, err);
            case NScanFilter f: return Scan(f.Name, current);
            case NScanMultipleFilter f: return ScanMultiple(f.Names, current);
            case NArrayIndexFilter f: return ArrayIndex(f.Index, current, err);
            case NArrayMultipleIndexFilter f: return ArrayMultipleIndex(f.Indexes, current, err);
            case NArraySliceFilter f: return ArraySlice(f, current, err);
            case NQueryFilter f: return Query(f.Expression, root, current);
            case NQueryScanFilter f: return QueryScan(f.Expression, root, current);
            default: throw new InvalidOperationException("unknown filter");
        }
    }

    // ── token helpers ─────────────────────────────────────────────────────────────────────

    private NodeKind KindOf(in NToken<TNode> t) => _m.KindOf(t.Node);

    private bool IsObject(in NToken<TNode> t) => !t.IsProperty && _m.KindOf(t.Node) == NodeKind.Object;

    private bool IsArray(in NToken<TNode> t) => !t.IsProperty && _m.KindOf(t.Node) == NodeKind.Array;

    private string TypeName(in NToken<TNode> t)
    {
        if (t.IsProperty) return "JProperty";
        return _m.KindOf(t.Node) switch
        {
            NodeKind.Object => "JObject",
            NodeKind.Array => "JArray",
            _ => "JValue",
        };
    }

    private static NToken<TNode> MemberToken(in NToken<TNode> parent, string name, TNode value, bool asProperty) =>
        new(value, parent.Loc == null ? null : Loc.Member(parent.Loc, name, parent.Node), asProperty, asProperty ? name : null);

    private static NToken<TNode> ElementToken(in NToken<TNode> parent, int index, TNode value) =>
        new(value, parent.Loc == null ? null : Loc.Element(parent.Loc, index, parent.Node), false, null);

    /// <summary>The same token without a location: sub-queries inside a filter only need values, not where they are.</summary>
    private static NToken<TNode> Untracked(in NToken<TNode> t) => t.Loc == null ? t : new NToken<TNode>(t.Node, null, t.IsProperty, t.PropertyName);

    /// <summary><c>foreach (JToken v in t)</c>: an object's JProperty wrappers, an array's elements, a JProperty's value, nothing for a JValue.</summary>
    private IEnumerable<NToken<TNode>> Children(NToken<TNode> t)
    {
        if (t.IsProperty)
        {
            yield return new NToken<TNode>(t.Node, t.Loc, false, null);
            yield break;
        }

        switch (_m.KindOf(t.Node))
        {
            case NodeKind.Object:
                var n = _m.Count(t.Node);
                for (var i = 0; i < n; i++)
                {
                    _m.MemberAt(t.Node, i, out var name, out var value);
                    yield return MemberToken(t, name, value, true);
                }

                break;
            case NodeKind.Array:
                var c = _m.Count(t.Node);
                for (var i = 0; i < c; i++)
                    yield return ElementToken(t, i, _m.ElementAt(t.Node, i));
                break;
        }
    }

    /// <summary>JContainer.Descendants(): pre-order, children before siblings, JProperty wrappers included.</summary>
    private IEnumerable<NToken<TNode>> Descendants(NToken<TNode> start)
    {
        var stack = new Stack<(IEnumerator<NToken<TNode>> Children, int Depth)>();
        stack.Push((Children(start).GetEnumerator(), 1));
        try
        {
            while (stack.Count > 0)
            {
                var (e, depth) = stack.Peek();
                if (!e.MoveNext())
                {
                    stack.Pop().Children.Dispose();
                    continue;
                }

                var token = e.Current;
                yield return token;

                // Depth counts containers entered. A JProperty wrapper and its value are one level, not two.
                var nextDepth = token.IsProperty ? depth : depth + 1;
                if (nextDepth > _settings.MaxDepth && (token.IsProperty || _m.KindOf(token.Node) is NodeKind.Object or NodeKind.Array))
                    throw new JsonPathException($"document is nested deeper than the configured limit of {_settings.MaxDepth}", JsonPathErrorKind.Limit);
                stack.Push((Children(token).GetEnumerator(), nextDepth));
            }
        }
        finally
        {
            while (stack.Count > 0) stack.Pop().Children.Dispose();
        }
    }

    // ── FieldFilter / FieldMultipleFilter ─────────────────────────────────────────────────

    private IEnumerable<NToken<TNode>> Field(string name, IEnumerable<NToken<TNode>> current, bool err)
    {
        foreach (var t in current)
        {
            if (IsObject(t))
            {
                if (name != null)
                {
                    if (_m.TryGetMember(t.Node, name, out var v)) yield return MemberToken(t, name, v, false);
                    else if (err) throw new JsonPathException($"Property '{name}' does not exist on JObject.", JsonPathErrorKind.NoMatch);
                }
                else
                {
                    var n = _m.Count(t.Node);
                    for (var i = 0; i < n; i++)
                    {
                        _m.MemberAt(t.Node, i, out var k, out var v);
                        yield return MemberToken(t, k, v, false);
                    }
                }
            }
            else if (err)
            {
                throw new JsonPathException($"Property '{name ?? "*"}' not valid on {TypeName(t)}.", JsonPathErrorKind.NoMatch);
            }
        }
    }

    private IEnumerable<NToken<TNode>> FieldMultiple(List<string> names, IEnumerable<NToken<TNode>> current, bool err)
    {
        foreach (var t in current)
        {
            if (IsObject(t))
            {
                foreach (var name in names)
                {
                    if (_m.TryGetMember(t.Node, name, out var v)) yield return MemberToken(t, name, v, false);

                    // Newtonsoft 13.0.4 raises this after *every* name once ErrorWhenNoMatch is on — even
                    // when the name was found. Preserved: it is the observable behaviour.
                    if (err) throw new JsonPathException($"Property '{name}' does not exist on JObject.", JsonPathErrorKind.NoMatch);
                }
            }
            else if (err)
            {
                throw new JsonPathException($"Properties {string.Join(", ", names.Select(n => "'" + n + "'"))} not valid on {TypeName(t)}.", JsonPathErrorKind.NoMatch);
            }
        }
    }

    // ── ScanFilter / ScanMultipleFilter ───────────────────────────────────────────────────

    private IEnumerable<NToken<TNode>> Scan(string name, IEnumerable<NToken<TNode>> current)
    {
        foreach (var c in current)
        {
            if (name == null) yield return c;

            foreach (var d in Descendants(c))
            {
                if (d.IsProperty)
                {
                    if (d.PropertyName == name)
                        yield return new NToken<TNode>(d.Node, d.Loc, false, null);
                }
                else if (name == null)
                {
                    yield return d;
                }
            }
        }
    }

    private IEnumerable<NToken<TNode>> ScanMultiple(List<string> names, IEnumerable<NToken<TNode>> current)
    {
        foreach (var c in current)
        {
            foreach (var d in Descendants(c))
            {
                if (!d.IsProperty) continue;
                foreach (var name in names)
                    if (d.PropertyName == name) yield return new NToken<TNode>(d.Node, d.Loc, false, null);
            }
        }
    }

    // ── ArrayIndexFilter / ArrayMultipleIndexFilter ───────────────────────────────────────

    private bool TryGetTokenIndex(in NToken<TNode> t, bool err, int index, out NToken<TNode> result)
    {
        if (IsArray(t))
        {
            if (index < 0 && _settings.NegativeIndexesFromEnd)
            {
                // Extended dialect: RFC 9535 §2.3.3.2 — a negative index counts from the end; before the start there is nothing.
                var fromEnd = _m.Count(t.Node) + index;
                if (fromEnd >= 0)
                {
                    result = ElementToken(t, fromEnd, _m.ElementAt(t.Node, fromEnd));
                    return true;
                }

                if (err) throw new JsonPathException($"Index {index} outside the bounds of JArray.", JsonPathErrorKind.NoMatch);
                result = default;
                return false;
            }

            if (_m.Count(t.Node) <= index)
            {
                if (err) throw new JsonPathException($"Index {index} outside the bounds of JArray.", JsonPathErrorKind.NoMatch);
                result = default;
                return false;
            }

            // Newtonsoft only guards the upper bound; a negative index reaches List<T>'s indexer and throws.
            if (index < 0)
                throw new JsonPathException("Index was out of range. Must be non-negative and less than the size of the collection. (Parameter 'index')", JsonPathErrorKind.Evaluation);

            result = ElementToken(t, index, _m.ElementAt(t.Node, index));
            return true;
        }

        if (err) throw new JsonPathException($"Index {index} not valid on {TypeName(t)}.", JsonPathErrorKind.NoMatch);
        result = default;
        return false;
    }

    private IEnumerable<NToken<TNode>> ArrayIndex(int? index, IEnumerable<NToken<TNode>> current, bool err)
    {
        foreach (var t in current)
        {
            if (index != null)
            {
                if (TryGetTokenIndex(t, err, index.Value, out var v)) yield return v;
            }
            else if (IsArray(t))
            {
                var n = _m.Count(t.Node);
                for (var i = 0; i < n; i++)
                    yield return ElementToken(t, i, _m.ElementAt(t.Node, i));
            }
            else if (err)
            {
                throw new JsonPathException($"Index * not valid on {TypeName(t)}.", JsonPathErrorKind.NoMatch);
            }
        }
    }

    private IEnumerable<NToken<TNode>> ArrayMultipleIndex(List<int> indexes, IEnumerable<NToken<TNode>> current, bool err)
    {
        foreach (var t in current)
        {
            foreach (var i in indexes)
                if (TryGetTokenIndex(t, err, i, out var v)) yield return v;
        }
    }

    // ── ArraySliceFilter ──────────────────────────────────────────────────────────────────

    private IEnumerable<NToken<TNode>> ArraySlice(NArraySliceFilter f, IEnumerable<NToken<TNode>> current, bool err)
    {
        if (f.Step == 0) throw new JsonPathException("Step cannot be zero.", JsonPathErrorKind.Evaluation);

        foreach (var t in current)
        {
            if (IsArray(t))
            {
                var count = _m.Count(t.Node);
                var stepCount = f.Step ?? 1;
                var startIndex = f.Start ?? (stepCount > 0 ? 0 : count - 1);
                var stopIndex = f.End ?? (stepCount > 0 ? count : -1);

                if (f.Start < 0) startIndex = count + startIndex;
                if (f.End < 0) stopIndex = count + stopIndex;

                startIndex = Math.Max(startIndex, stepCount > 0 ? 0 : int.MinValue);
                startIndex = Math.Min(startIndex, stepCount > 0 ? count : count - 1);
                stopIndex = Math.Max(stopIndex, -1);
                stopIndex = Math.Min(stopIndex, count);

                var positiveStep = stepCount > 0;
                if (IsValid(startIndex, stopIndex, positiveStep))
                {
                    // unchecked, as in Newtonsoft: a huge step wraps around and then indexes out of range.
                    for (var i = startIndex; IsValid(i, stopIndex, positiveStep); i = unchecked(i + stepCount))
                    {
                        if (i < 0 || i >= count)
                            throw new JsonPathException("Index was out of range. Must be non-negative and less than the size of the collection. (Parameter 'index')", JsonPathErrorKind.Evaluation);
                        yield return ElementToken(t, i, _m.ElementAt(t.Node, i));
                    }
                }
                else if (err)
                {
                    throw new JsonPathException($"Array slice of {(f.Start != null ? f.Start.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "*")} to {(f.End != null ? f.End.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "*")} returned no results.", JsonPathErrorKind.NoMatch);
                }
            }
            else if (err)
            {
                throw new JsonPathException($"Array slice is not valid on {TypeName(t)}.", JsonPathErrorKind.NoMatch);
            }
        }
    }

    private static bool IsValid(int index, int stopIndex, bool positiveStep) =>
        positiveStep ? index < stopIndex : index > stopIndex;

    // ── QueryFilter / QueryScanFilter ─────────────────────────────────────────────────────

    private IEnumerable<NToken<TNode>> Query(NExpression expression, NToken<TNode> root, IEnumerable<NToken<TNode>> current)
    {
        foreach (var t in current)
        {
            foreach (var v in Children(t))
            {
                if (IsMatch(expression, root, v)) yield return v;
            }
        }
    }

    private IEnumerable<NToken<TNode>> QueryScan(NExpression expression, NToken<TNode> root, IEnumerable<NToken<TNode>> current)
    {
        foreach (var t in current)
        {
            // JContainer: JObject, JArray and JProperty. DescendantsAndSelf() includes the JProperty wrappers.
            if (t.IsProperty || KindOf(t) is NodeKind.Object or NodeKind.Array)
            {
                if (IsMatch(expression, root, t)) yield return t;
                foreach (var d in Descendants(t))
                    if (IsMatch(expression, root, d)) yield return d;
            }
            else if (IsMatch(expression, root, t))
            {
                yield return t;
            }
        }
    }

    // ── QueryExpression.IsMatch ───────────────────────────────────────────────────────────

    private bool IsMatch(NExpression expression, NToken<TNode> root, NToken<TNode> t)
    {
        switch (expression)
        {
            case NCompositeExpression c:
                switch (c.Operator)
                {
                    case NOp.And:
                        foreach (var e in c.Expressions)
                            if (!IsMatch(e, root, t)) return false;
                        return true;
                    case NOp.Or:
                        foreach (var e in c.Expressions)
                            if (IsMatch(e, root, t)) return true;
                        return false;
                    default:
                        throw new InvalidOperationException();
                }

            case NBooleanExpression b:
                return IsBooleanMatch(b, root, t);
            default:
                throw new InvalidOperationException();
        }
    }

    private IEnumerable<NOperand<TNode>> GetResult(NToken<TNode> root, NToken<TNode> t, object o)
    {
        if (o is Prim p) return new[] { new NOperand<TNode>(p) };
        if (o is List<NFilter> filters) return Wrap(Evaluate(filters, Untracked(root), Untracked(t), errorWhenNoMatch: false));
        return Array.Empty<NOperand<TNode>>();
    }

    private static IEnumerable<NOperand<TNode>> Wrap(IEnumerable<NToken<TNode>> tokens)
    {
        foreach (var token in tokens) yield return new NOperand<TNode>(token);
    }

    private bool IsBooleanMatch(NBooleanExpression b, NToken<TNode> root, NToken<TNode> t)
    {
        if (b.Operator == NOp.Exists)
        {
            using var any = GetResult(root, t, b.Left).GetEnumerator();
            return any.MoveNext();
        }

        using var leftResults = GetResult(root, t, b.Left).GetEnumerator();
        if (leftResults.MoveNext())
        {
            var rightResultsEn = GetResult(root, t, b.Right);
            var rightResults = rightResultsEn as ICollection<NOperand<TNode>> ?? rightResultsEn.ToList();
            do
            {
                var leftResult = leftResults.Current;
                foreach (var rightResult in rightResults)
                {
                    if (MatchTokens(b.Operator, leftResult, rightResult)) return true;
                }
            }
            while (leftResults.MoveNext());
        }

        return false;
    }

    /// <summary>The primitive a JValue token holds, or false when the token is a container or a JProperty (not a JValue).</summary>
    private bool TryPrimitive(in NOperand<TNode> o, out Prim p)
    {
        if (o.IsLiteral)
        {
            p = o.Literal;
            return true;
        }

        var t = o.Token;
        if (t.IsProperty)
        {
            p = default;
            return false;
        }

        switch (_m.KindOf(t.Node))
        {
            case NodeKind.Null: p = Prim.Null; return true;
            case NodeKind.Bool: p = Prim.FromBool(_m.GetBool(t.Node)); return true;
            case NodeKind.Number: p = _m.GetNumber(t.Node, bigIntegers: true); return true;
            case NodeKind.String:
                var s = _m.GetString(t.Node);
                // JsonTextReader's DateParseHandling.DateTime: a date-looking string is a Date value, not a String.
                p = _settings.EmulateDates && NewtonsoftDates.TryParse(s, out var dt) ? Prim.FromDate(dt) : Prim.FromString(s);
                return true;
            default:
                p = default;
                return false;
        }
    }

    private bool MatchTokens(NOp op, NOperand<TNode> left, NOperand<TNode> right)
    {
        if (TryPrimitive(left, out var l) && TryPrimitive(right, out var r))
        {
            switch (op)
            {
                case NOp.RegexEquals:
                    if (RegexEquals(l, r)) return true;
                    break;
                case NOp.Equals:
                    if (NewtonsoftValues.EqualsWithStringCoercion(l, r)) return true;
                    break;
                case NOp.StrictEquals:
                    if (NewtonsoftValues.EqualsWithStrictMatch(l, r)) return true;
                    break;
                case NOp.NotEquals:
                    if (!NewtonsoftValues.EqualsWithStringCoercion(l, r)) return true;
                    break;
                case NOp.StrictNotEquals:
                    if (!NewtonsoftValues.EqualsWithStrictMatch(l, r)) return true;
                    break;
                case NOp.GreaterThan:
                    if (NewtonsoftValues.CompareTo(l, r) > 0) return true;
                    break;
                case NOp.GreaterThanOrEquals:
                    if (NewtonsoftValues.CompareTo(l, r) >= 0) return true;
                    break;
                case NOp.LessThan:
                    if (NewtonsoftValues.CompareTo(l, r) < 0) return true;
                    break;
                case NOp.LessThanOrEquals:
                    if (NewtonsoftValues.CompareTo(l, r) <= 0) return true;
                    break;
                case NOp.Exists:
                    return true;
            }
        }
        else
        {
            switch (op)
            {
                case NOp.Exists:
                // you can only specify primitive types in a comparison
                // notequals will always be true
                case NOp.NotEquals:
                    return true;
            }
        }

        return false;
    }

    private bool RegexEquals(in Prim input, in Prim pattern)
    {
        if (input.Kind != PrimKind.String || pattern.Kind != PrimKind.String) return false;

        var regexText = pattern.Str;
        var patternOptionDelimiterIndex = regexText.LastIndexOf('/');
        if (patternOptionDelimiterIndex < 1)
            throw new JsonPathException("Length cannot be less than zero. (Parameter 'length')", JsonPathErrorKind.Evaluation);
        var patternText = regexText.Substring(1, patternOptionDelimiterIndex - 1);
        var optionsText = regexText.Substring(patternOptionDelimiterIndex + 1);

        try
        {
            return Regex.IsMatch(input.Str, patternText, GetRegexOptions(optionsText), _settings.RegexTimeout);
        }
        catch (RegexMatchTimeoutException ex)
        {
            throw new JsonPathException($"regular expression evaluation exceeded the configured timeout of {_settings.RegexTimeout}", JsonPathErrorKind.Limit, -1, ex);
        }
        catch (ArgumentException ex)
        {
            throw new JsonPathException(ex.Message, JsonPathErrorKind.Evaluation, -1, ex);
        }
    }

    /// <summary><c>MiscellaneousUtils.GetRegexOptions</c>: i, m, s and x only — and 'x' means ExplicitCapture, not IgnorePatternWhitespace.</summary>
    private static RegexOptions GetRegexOptions(string optionsText)
    {
        var options = RegexOptions.None;
        foreach (var c in optionsText)
        {
            switch (c)
            {
                case 'i': options |= RegexOptions.IgnoreCase; break;
                case 'm': options |= RegexOptions.Multiline; break;
                case 's': options |= RegexOptions.Singleline; break;
                case 'x': options |= RegexOptions.ExplicitCapture; break;
            }
        }

        return options;
    }
}
