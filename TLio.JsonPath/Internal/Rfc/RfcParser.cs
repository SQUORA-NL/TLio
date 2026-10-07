#nullable disable
using System.Globalization;
using System.Text;

namespace TLio.JsonPath.Internal.Rfc;

/// <summary>
/// A recursive-descent parser for the complete RFC 9535 grammar (§2, Figure 1), including the
/// well-typedness rules for function extensions (§2.4.3) — a query that is not well-typed is
/// rejected here, with the position of the offending token, rather than at evaluation.
/// </summary>
internal sealed class RfcParser
{
    /// <summary>I-JSON range limit for integers in a query (RFC 9535 §2.1): ±(2^53 − 1).</summary>
    private const long MaxExactInteger = 9007199254740991;

    private readonly string _s;
    private readonly int _maxDepth;
    private readonly IReadOnlyDictionary<string, JsonPathFunction> _custom;
    private readonly TimeSpan _regexTimeout;
    private readonly bool _strictRegexp;
    private int _p;
    private int _depth;

    private RfcParser(string text, int maxDepth, IReadOnlyDictionary<string, JsonPathFunction> custom, TimeSpan regexTimeout, bool strictRegexp)
    {
        _s = text;
        _strictRegexp = strictRegexp;
        _maxDepth = maxDepth;
        _custom = custom;
        _regexTimeout = regexTimeout;
    }

    public static QueryAst Parse(string text, int maxDepth, IReadOnlyDictionary<string, JsonPathFunction> custom, TimeSpan regexTimeout, bool strictRegexp)
    {
        var parser = new RfcParser(text, maxDepth, custom, regexTimeout, strictRegexp);
        if (text.Length == 0 || text[0] != '$')
            throw parser.Error("a JSONPath query must start with '$'", 0);

        parser._p = 1;
        var segments = parser.ParseSegments();
        if (parser._p != text.Length)
            throw parser.Error("unexpected character", parser._p);

        return new QueryAst(true, segments);
    }

    // ── errors, cursor ────────────────────────────────────────────────────────────────────

    private JsonPathException Error(string message, int? at = null) =>
        new(message, JsonPathErrorKind.Syntax, at ?? _p);

    private bool AtEnd => _p >= _s.Length;

    private char Peek => _p < _s.Length ? _s[_p] : '\0';

    private bool PeekIs(char c) => _p < _s.Length && _s[_p] == c;

    private void SkipWs()
    {
        // RFC 9535 B = %x20 / %x09 / %x0A / %x0D — and nothing else (not NBSP, not VT).
        while (_p < _s.Length && _s[_p] is ' ' or '\t' or '\n' or '\r') _p++;
    }

    private void Enter()
    {
        if (++_depth > _maxDepth)
            throw new JsonPathException($"query is nested deeper than the configured limit of {_maxDepth}", JsonPathErrorKind.Limit, _p);
    }

    private void Leave() => _depth--;

    private static bool IsDigit(char c) => c is >= '0' and <= '9';

    // ── segments and selectors ────────────────────────────────────────────────────────────

    private Segment[] ParseSegments()
    {
        var list = new List<Segment>();
        while (true)
        {
            // segments = *(S segment): whitespace is only legal when a segment follows it, so look
            // ahead and give the whitespace back if none does ("$.a " has a trailing blank and is invalid).
            var save = _p;
            SkipWs();
            if (_p < _s.Length && (_s[_p] == '.' || _s[_p] == '['))
                list.Add(ParseSegment());
            else
            {
                _p = save;
                break;
            }
        }

        return list.ToArray();
    }

    private Segment ParseSegment()
    {
        if (_s[_p] == '[')
            return new Segment(false, ParseBracketed());

        // '.'
        if (_p + 1 < _s.Length && _s[_p + 1] == '.')
        {
            // descendant-segment = ".." (bracketed-selection / wildcard-selector / member-name-shorthand)
            _p += 2;
            if (PeekIs('[')) return new Segment(true, ParseBracketed());
            if (PeekIs('*'))
            {
                _p++;
                return new Segment(true, [WildcardSelector.Instance]);
            }

            return new Segment(true, [new NameSelector(ParseMemberNameShorthand())]);
        }

        // child-segment = "." (wildcard-selector / member-name-shorthand) — no whitespace after the dot.
        _p++;
        if (PeekIs('*'))
        {
            _p++;
            return new Segment(false, [WildcardSelector.Instance]);
        }

        return new Segment(false, [new NameSelector(ParseMemberNameShorthand())]);
    }

    private Selector[] ParseBracketed()
    {
        _p++; // '['
        var list = new List<Selector>();
        SkipWs();
        while (true)
        {
            list.Add(ParseSelector());
            SkipWs();
            if (PeekIs(',')) { _p++; SkipWs(); continue; }
            if (PeekIs(']')) { _p++; break; }
            throw Error(AtEnd ? "unterminated '[': expected ']'" : "expected ',' or ']'");
        }

        return list.ToArray();
    }

    private Selector ParseSelector()
    {
        var c = Peek;
        if (c == '\'' || c == '"') return new NameSelector(ParseStringLiteral());
        if (c == '*') { _p++; return WildcardSelector.Instance; }
        if (c == '?')
        {
            _p++;
            SkipWs();
            Enter();
            var pos = _p;
            var e = ParseOr(allowBare: false);
            Leave();
            return new FilterSelector(ToLogical(e, pos));
        }

        if (IsDigit(c) || c == '-' || c == ':') return ParseIndexOrSlice();
        throw Error(AtEnd ? "unterminated selector" : "expected a selector");
    }

    private Selector ParseIndexOrSlice()
    {
        long? start = null;
        if (IsDigit(Peek) || Peek == '-') start = ParseInt();
        SkipWs();
        if (!PeekIs(':'))
        {
            if (start == null) throw Error("expected a selector");
            return new IndexSelector(start.Value);
        }

        // slice-selector = [start S] ":" S [end S] [":" [S step ]]
        _p++;
        SkipWs();
        long? end = null;
        if (IsDigit(Peek) || Peek == '-')
        {
            end = ParseInt();
            SkipWs();
        }

        long? step = null;
        if (PeekIs(':'))
        {
            _p++;
            SkipWs();
            if (IsDigit(Peek) || Peek == '-') step = ParseInt();
        }

        return new SliceSelector(start, end, step);
    }

    /// <summary>int = "0" / (["-"] DIGIT1 *DIGIT) — no "-0", no leading zeros, within the I-JSON range.</summary>
    private long ParseInt()
    {
        var start = _p;
        if (PeekIs('-')) _p++;
        if (!IsDigit(Peek)) throw Error("expected a digit");
        if (_s[_p] == '0')
        {
            _p++;
            if (_p - start == 2) throw Error("'-0' is not a valid integer here", start);
            if (IsDigit(Peek)) throw Error("leading zeros are not allowed", start);
        }
        else
        {
            while (IsDigit(Peek)) _p++;
        }

        var text = _s.AsSpan(start, _p - start);
        if (!long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v) || v > MaxExactInteger || v < -MaxExactInteger)
            throw Error("integer is outside the I-JSON range ±(2^53−1)", start);
        return v;
    }

    /// <summary>member-name-shorthand = name-first *name-char (RFC 9535 §2.5.1.1, Figure 3).</summary>
    private string ParseMemberNameShorthand()
    {
        var start = _p;
        if (!IsNameFirstAt(_p, out var len))
            throw Error(AtEnd ? "expected a member name" : "invalid member name shorthand", _p);
        _p += len;
        while (_p < _s.Length)
        {
            if (IsNameFirstAt(_p, out len)) _p += len;
            else if (IsDigit(_s[_p])) _p++;
            else break;
        }

        return _s.Substring(start, _p - start);
    }

    /// <summary>name-first = ALPHA / "_" / %x80-D7FF / %xE000-10FFFF</summary>
    private bool IsNameFirstAt(int i, out int length)
    {
        length = 1;
        if (i >= _s.Length) return false;
        var c = _s[i];
        if (c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or '_') return true;
        if (c < 0x80) return false;
        if (char.IsHighSurrogate(c))
        {
            if (i + 1 < _s.Length && char.IsLowSurrogate(_s[i + 1])) { length = 2; return true; }
            return false;
        }

        return !char.IsLowSurrogate(c);
    }

    // ── string literals (§2.3.1.1) ────────────────────────────────────────────────────────

    private string ParseStringLiteral()
    {
        var quote = _s[_p];
        var open = _p;
        _p++;
        var sb = new StringBuilder();
        while (_p < _s.Length)
        {
            var c = _s[_p];
            if (c == quote) { _p++; return sb.ToString(); }
            if (c == '\\')
            {
                _p++;
                if (AtEnd) break;
                var e = _s[_p];
                switch (e)
                {
                    case 'b': sb.Append('\b'); _p++; break;
                    case 'f': sb.Append('\f'); _p++; break;
                    case 'n': sb.Append('\n'); _p++; break;
                    case 'r': sb.Append('\r'); _p++; break;
                    case 't': sb.Append('\t'); _p++; break;
                    case '/': sb.Append('/'); _p++; break;
                    case '\\': sb.Append('\\'); _p++; break;
                    case '\'' when quote == '\'': sb.Append('\''); _p++; break;
                    case '"' when quote == '"': sb.Append('"'); _p++; break;
                    case 'u': ParseUnicodeEscape(sb); break;
                    default: throw Error("invalid escape sequence", _p - 1);
                }

                continue;
            }

            if (c < 0x20) throw Error("control characters must be escaped in a string literal");
            if (char.IsHighSurrogate(c))
            {
                if (_p + 1 < _s.Length && char.IsLowSurrogate(_s[_p + 1])) { sb.Append(c).Append(_s[_p + 1]); _p += 2; continue; }
                throw Error("unpaired surrogate in string literal");
            }

            if (char.IsLowSurrogate(c)) throw Error("unpaired surrogate in string literal");
            sb.Append(c);
            _p++;
        }

        throw Error("unterminated string literal", open);
    }

    private void ParseUnicodeEscape(StringBuilder sb)
    {
        var escapeStart = _p - 1; // at the backslash
        _p++; // 'u'
        var hi = ReadHex4();
        if (hi is >= 0xD800 and <= 0xDBFF)
        {
            // A high surrogate is only legal as the first half of an escaped pair: 😀
            if (_p + 1 < _s.Length && _s[_p] == '\\' && _s[_p + 1] == 'u')
            {
                _p += 2;
                var lo = ReadHex4();
                if (lo is >= 0xDC00 and <= 0xDFFF)
                {
                    sb.Append((char)hi).Append((char)lo);
                    return;
                }
            }

            throw Error("unpaired surrogate in \\u escape", escapeStart);
        }

        if (hi is >= 0xDC00 and <= 0xDFFF) throw Error("unpaired surrogate in \\u escape", escapeStart);
        sb.Append((char)hi);
    }

    private int ReadHex4()
    {
        if (_p + 4 > _s.Length) throw Error("expected four hexadecimal digits after \\u");
        var v = 0;
        for (var i = 0; i < 4; i++)
        {
            var c = _s[_p + i];
            int d;
            if (c is >= '0' and <= '9') d = c - '0';
            else if (c is >= 'a' and <= 'f') d = c - 'a' + 10;
            else if (c is >= 'A' and <= 'F') d = c - 'A' + 10;
            else throw Error("expected four hexadecimal digits after \\u", _p + i);
            v = v * 16 + d;
        }

        _p += 4;
        return v;
    }

    // ── filter expressions (§2.3.5) ───────────────────────────────────────────────────────

    /// <summary>
    /// logical-or-expr. With <paramref name="allowBare"/> (inside a function argument) a lone literal,
    /// query or function call is returned as it is, because there it may be a ValueType or NodesType
    /// argument; everywhere else it has to be a logical expression and the caller converts it.
    /// </summary>
    private Expr ParseOr(bool allowBare)
    {
        Enter();
        var first = ParseAnd(allowBare);
        List<Expr> list = null;
        while (true)
        {
            var save = _p;
            SkipWs();
            if (_p + 1 < _s.Length && _s[_p] == '|' && _s[_p + 1] == '|')
            {
                _p += 2;
                SkipWs();
                list ??= [ToLogical(first, first.Pos)];
                var pos = _p;
                list.Add(ToLogical(ParseAnd(false), pos));
            }
            else
            {
                _p = save;
                break;
            }
        }

        Leave();
        return list == null ? first : new OrExpr(list.ToArray()) { Pos = first.Pos };
    }

    private Expr ParseAnd(bool allowBare)
    {
        var first = ParseBasic(allowBare);
        List<Expr> list = null;
        while (true)
        {
            var save = _p;
            SkipWs();
            if (_p + 1 < _s.Length && _s[_p] == '&' && _s[_p + 1] == '&')
            {
                _p += 2;
                SkipWs();
                list ??= [ToLogical(first, first.Pos)];
                var pos = _p;
                list.Add(ToLogical(ParseBasic(false), pos));
            }
            else
            {
                _p = save;
                break;
            }
        }

        return list == null ? first : new AndExpr(list.ToArray()) { Pos = first.Pos };
    }

    private Expr ParseBasic(bool allowBare)
    {
        var start = _p;
        var negated = false;
        if (PeekIs('!'))
        {
            negated = true;
            _p++;
            SkipWs();
        }

        if (PeekIs('('))
        {
            // paren-expr = [logical-not-op S] "(" S logical-expr S ")"
            _p++;
            SkipWs();
            var pos = _p;
            var inner = ToLogical(ParseOr(false), pos);
            SkipWs();
            if (!PeekIs(')')) throw Error(AtEnd ? "unterminated '(': expected ')'" : "expected ')'");
            _p++;
            return negated ? new NotExpr(inner) { Pos = start } : inner;
        }

        var operand = ParseOperand();
        var save = _p;
        SkipWs();
        if (TryReadComparisonOp(out var op))
        {
            if (negated) throw Error("'!' cannot be applied to a comparison; use parentheses", start);
            SkipWs();
            var rightStart = _p;
            var right = ParseOperand();
            CheckComparable(operand, operand.Pos);
            CheckComparable(right, rightStart);
            return new CompareExpr(operand, op, right) { Pos = start };
        }

        _p = save;
        if (!negated && allowBare) return operand;
        var test = ToLogical(operand, operand.Pos);
        return negated ? new NotExpr(test) { Pos = start } : test;
    }

    private bool TryReadComparisonOp(out CompareOp op)
    {
        op = CompareOp.Eq;
        if (_p >= _s.Length) return false;
        var c = _s[_p];
        var n = _p + 1 < _s.Length ? _s[_p + 1] : '\0';
        switch (c)
        {
            case '=' when n == '=': op = CompareOp.Eq; _p += 2; return true;
            case '!' when n == '=': op = CompareOp.Ne; _p += 2; return true;
            case '<' when n == '=': op = CompareOp.Le; _p += 2; return true;
            case '>' when n == '=': op = CompareOp.Ge; _p += 2; return true;
            case '<': op = CompareOp.Lt; _p++; return true;
            case '>': op = CompareOp.Gt; _p++; return true;
            default: return false;
        }
    }

    private Expr ParseOperand()
    {
        var start = _p;
        var c = Peek;
        Expr result;
        if (c == '@' || c == '$')
        {
            _p++;
            var segments = ParseSegments();
            result = new QueryExpr(new QueryAst(c == '$', segments));
        }
        else if (c == '\'' || c == '"')
        {
            result = new LiteralExpr(Prim.FromString(ParseStringLiteral()));
        }
        else if (c == '-' || IsDigit(c))
        {
            result = new LiteralExpr(ParseNumberLiteral());
        }
        else if (c is >= 'a' and <= 'z')
        {
            var id = _p;
            while (_p < _s.Length && (_s[_p] is >= 'a' and <= 'z' or >= '0' and <= '9' or '_')) _p++;
            var name = _s.Substring(id, _p - id);
            if (PeekIs('(')) result = ParseFunctionCall(name, id);
            else
                result = name switch
                {
                    "true" => new LiteralExpr(Prim.FromBool(true)),
                    "false" => new LiteralExpr(Prim.FromBool(false)),
                    "null" => new LiteralExpr(Prim.Null),
                    _ => throw Error($"unexpected identifier '{name}'", id),
                };
        }
        else
        {
            throw Error(AtEnd ? "unexpected end of query" : "expected a value, query or function call");
        }

        result.Pos = start;
        return result;
    }

    /// <summary>number = (int / "-0") [ frac ] [ exp ]</summary>
    private Prim ParseNumberLiteral()
    {
        var start = _p;
        if (PeekIs('-')) _p++;
        if (!IsDigit(Peek)) throw Error("expected a digit");
        if (_s[_p] == '0')
        {
            _p++;
            if (IsDigit(Peek)) throw Error("leading zeros are not allowed", start);
        }
        else
        {
            while (IsDigit(Peek)) _p++;
        }

        var isFloat = false;
        if (PeekIs('.'))
        {
            _p++;
            if (!IsDigit(Peek)) throw Error("expected a digit after '.'");
            while (IsDigit(Peek)) _p++;
            isFloat = true;
        }

        if (Peek is 'e' or 'E')
        {
            _p++;
            if (Peek is '+' or '-') _p++;
            if (!IsDigit(Peek)) throw Error("expected a digit in the exponent");
            while (IsDigit(Peek)) _p++;
            isFloat = true;
        }

        var text = _s.AsSpan(start, _p - start);
        if (!isFloat && long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var l))
            return Prim.FromLong(l);
        return Prim.FromDouble(double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture));
    }

    private Expr ParseFunctionCall(string name, int namePos)
    {
        _p++; // '('
        SkipWs();
        var args = new List<Expr>();
        if (!PeekIs(')'))
        {
            while (true)
            {
                args.Add(ParseOr(allowBare: true));
                SkipWs();
                if (PeekIs(',')) { _p++; SkipWs(); continue; }
                if (PeekIs(')')) break;
                throw Error(AtEnd ? "unterminated function call: expected ')'" : "expected ',' or ')'");
            }
        }

        _p++; // ')'

        BuiltIn builtIn = BuiltIn.None;
        JsonPathFunction custom = null;
        JsonPathFunctionType[] paramTypes;
        JsonPathFunctionType returnType;
        switch (name)
        {
            case "length": builtIn = BuiltIn.Length; paramTypes = [JsonPathFunctionType.Value]; returnType = JsonPathFunctionType.Value; break;
            case "count": builtIn = BuiltIn.Count; paramTypes = [JsonPathFunctionType.Nodes]; returnType = JsonPathFunctionType.Value; break;
            case "match": builtIn = BuiltIn.Match; paramTypes = [JsonPathFunctionType.Value, JsonPathFunctionType.Value]; returnType = JsonPathFunctionType.Logical; break;
            case "search": builtIn = BuiltIn.Search; paramTypes = [JsonPathFunctionType.Value, JsonPathFunctionType.Value]; returnType = JsonPathFunctionType.Logical; break;
            case "value": builtIn = BuiltIn.Value; paramTypes = [JsonPathFunctionType.Nodes]; returnType = JsonPathFunctionType.Value; break;
            default:
                if (!_custom.TryGetValue(name, out custom))
                    throw Error($"unknown function '{name}'", namePos);
                paramTypes = custom.ParameterTypes.ToArray();
                returnType = custom.ReturnType;
                break;
        }

        if (args.Count != paramTypes.Length)
            throw Error($"function '{name}' expects {paramTypes.Length} argument(s) but {args.Count} were given", namePos);

        for (var i = 0; i < args.Count; i++)
            CheckArgument(name, i, paramTypes[i], args[i]);

        var call = new FunctionExpr(name, args.ToArray(), builtIn, custom, paramTypes, returnType);
        if ((builtIn == BuiltIn.Match || builtIn == BuiltIn.Search) && args[1] is LiteralExpr { Value.Kind: PrimKind.String } pattern)
        {
            call.HasPrecompiledPattern = true;
            call.PrecompiledPattern = IRegexp.TryCompile(pattern.Value.Str, builtIn == BuiltIn.Match, _regexTimeout, _strictRegexp);
        }

        return call;
    }

    // ── static typing (§2.4.3) ────────────────────────────────────────────────────────────

    private void CheckArgument(string function, int index, JsonPathFunctionType expected, Expr arg)
    {
        var ok = expected switch
        {
            // A ValueType parameter takes a literal, a singular query or a ValueType function — never a node list.
            JsonPathFunctionType.Value => arg is LiteralExpr || arg is QueryExpr { IsSingular: true } || arg is FunctionExpr { ReturnType: JsonPathFunctionType.Value },
            // A NodesType parameter takes a query of any shape, or a NodesType function.
            JsonPathFunctionType.Nodes => arg is QueryExpr || arg is FunctionExpr { ReturnType: JsonPathFunctionType.Nodes },
            // A LogicalType parameter takes a logical expression; a query or NodesType function is converted by testing it for non-emptiness.
            _ => arg is OrExpr or AndExpr or NotExpr or CompareExpr or TestExpr || arg is QueryExpr || arg is FunctionExpr { ReturnType: JsonPathFunctionType.Logical or JsonPathFunctionType.Nodes },
        };
        if (!ok)
            throw Error($"argument {index + 1} of '{function}' must be a {expected}Type expression", arg.Pos);
    }

    private void CheckComparable(Expr e, int pos)
    {
        var ok = e is LiteralExpr || e is QueryExpr { IsSingular: true } || e is FunctionExpr { ReturnType: JsonPathFunctionType.Value };
        if (!ok)
            throw Error(e is QueryExpr
                ? "a query used in a comparison must be a singular query"
                : "this expression cannot be compared (a function used in a comparison must return a value)", pos);
    }

    /// <summary>Turns a lone operand into a logical expression (test-expr), rejecting those that are not logical.</summary>
    private Expr ToLogical(Expr e, int pos)
    {
        switch (e)
        {
            case OrExpr or AndExpr or NotExpr or CompareExpr or TestExpr:
                return e;
            case QueryExpr:
                return new TestExpr(e) { Pos = e.Pos };
            case FunctionExpr { ReturnType: JsonPathFunctionType.Logical or JsonPathFunctionType.Nodes }:
                return new TestExpr(e) { Pos = e.Pos };
            case FunctionExpr f:
                throw Error($"function '{f.Name}' returns a value and cannot be used as a test; compare its result", pos);
            default:
                throw Error("a literal cannot be used as a test; compare it with something", pos);
        }
    }
}
