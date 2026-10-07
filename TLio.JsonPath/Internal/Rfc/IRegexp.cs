#nullable disable
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TLio.JsonPath.Internal.Rfc;

/// <summary>
/// I-Regexp (RFC 9485): a deliberately small regular-expression dialect that every engine can
/// implement the same way. <c>match()</c> and <c>search()</c> are defined in terms of it, so a
/// pattern is first <em>validated</em> against the I-Regexp grammar — a .NET regex that is not an
/// I-Regexp (lazy quantifiers, back-references, <c>\d</c>, anchors…) must not quietly work — and
/// then translated to the equivalent .NET pattern.
/// </summary>
/// <remarks>
/// .NET regular expressions work on UTF-16 code units while I-Regexp works on Unicode scalar
/// values, so the translation keeps a supplementary-plane character one unit: <c>.</c> and negated
/// classes consume a surrogate pair at a time, and astral literals and ranges are grouped and
/// expanded into surrogate-pair alternations. The one place this cannot reach is a <c>\p{…}</c>
/// category applied to a supplementary-plane character, which .NET does not classify.
/// </remarks>
internal static class IRegexp
{
    private static readonly ConcurrentDictionary<(string Pattern, bool Full, long Timeout, bool Strict), Regex> Cache = new();
    private const int CacheLimit = 512;
    private const string Pair = "[\\uD800-\\uDBFF][\\uDC00-\\uDFFF]";

    /// <summary>The translated regex, or null when <paramref name="pattern"/> is not a valid I-Regexp.</summary>
    /// <param name="pattern">The I-Regexp text.</param>
    /// <param name="full">True for <c>match()</c> (the whole string must match), false for <c>search()</c> (a substring may).</param>
    /// <param name="timeout">Match timeout baked into the returned regex.</param>
    /// <param name="strict">True to read <c>^</c> and <c>$</c> as the literal characters the RFC 9485 grammar says they are.</param>
    public static Regex TryCompile(string pattern, bool full, TimeSpan timeout, bool strict)
    {
        var key = (pattern, full, timeout == Timeout.InfiniteTimeSpan ? -1L : (long)timeout.TotalMilliseconds, strict);
        if (Cache.TryGetValue(key, out var cached)) return cached;

        var regex = Compile(pattern, full, timeout, strict);
        if (Cache.Count >= CacheLimit) Cache.Clear();
        Cache[key] = regex;
        return regex;
    }

    private static Regex Compile(string pattern, bool full, TimeSpan timeout, bool strict)
    {
        var translator = new Translator(pattern, strict);
        string body;
        try
        {
            body = translator.Translate();
        }
        catch (FormatException)
        {
            return null;
        }

        var text = full ? "\\A(?:" + body + ")\\z" : "(?:" + body + ")";
        try
        {
            return new Regex(text, RegexOptions.CultureInvariant, timeout);
        }
        catch (ArgumentException)
        {
            // e.g. a quantifier like {5,2} that the grammar allows but no engine can honour.
            return null;
        }
    }

    private sealed class Translator
    {
        private readonly string _s;
        private readonly bool _strict;
        private int _p;

        public Translator(string s, bool strict)
        {
            _s = s;
            _strict = strict;
        }

        public string Translate()
        {
            var sb = new StringBuilder();
            ParseRegexp(sb, 0);
            if (_p != _s.Length) throw new FormatException();
            return sb.ToString();
        }

        private bool More => _p < _s.Length;

        private void ParseRegexp(StringBuilder sb, int depth)
        {
            if (depth > 200) throw new FormatException();
            ParseBranch(sb, depth);
            while (More && _s[_p] == '|')
            {
                _p++;
                sb.Append('|');
                ParseBranch(sb, depth);
            }
        }

        private void ParseBranch(StringBuilder sb, int depth)
        {
            var first = true;
            while (More && _s[_p] != '|' && _s[_p] != ')')
            {
                if (!_strict && TryAnchor(sb, first)) { first = false; continue; }
                ParsePiece(sb, depth);
                first = false;
            }
        }

        /// <summary>
        /// A '^' that opens a branch, or a '$' that closes one, is an anchor (see JsonPathOptions.StrictIRegexp);
        /// anywhere else — or with a quantifier after it — it is the literal character the grammar says it is.
        /// </summary>
        private bool TryAnchor(StringBuilder sb, bool first)
        {
            var c = _s[_p];
            var next = _p + 1 < _s.Length ? _s[_p + 1] : '\0';
            if (c == '^' && first && next is not ('*' or '+' or '?' or '{'))
            {
                _p++;
                sb.Append("\\A");
                return true;
            }

            if (c == '$' && (next is '\0' or '|' or ')') && (_p + 1 == _s.Length || next != '\0'))
            {
                _p++;
                sb.Append("\\z");
                return true;
            }

            return false;
        }

        private void ParsePiece(StringBuilder sb, int depth)
        {
            var atom = new StringBuilder();
            ParseAtom(atom, depth);
            sb.Append(atom);
            if (!More) return;
            switch (_s[_p])
            {
                case '*': case '+': case '?':
                    sb.Append(_s[_p]);
                    _p++;
                    break;
                case '{':
                    _p++;
                    var lo = ReadNumber();
                    sb.Append('{').Append(lo);
                    if (More && _s[_p] == ',')
                    {
                        _p++;
                        sb.Append(',');
                        if (More && char.IsAsciiDigit(_s[_p])) sb.Append(ReadNumber());
                    }

                    if (!More || _s[_p] != '}') throw new FormatException();
                    _p++;
                    sb.Append('}');
                    break;
            }
        }

        private string ReadNumber()
        {
            var start = _p;
            while (More && _s[_p] is >= '0' and <= '9') _p++;
            if (_p == start || _p - start > 9) throw new FormatException();
            return _s.Substring(start, _p - start);
        }

        private void ParseAtom(StringBuilder sb, int depth)
        {
            var c = _s[_p];
            switch (c)
            {
                case '(':
                    _p++;
                    sb.Append("(?:");
                    ParseRegexp(sb, depth + 1);
                    if (!More || _s[_p] != ')') throw new FormatException();
                    _p++;
                    sb.Append(')');
                    return;
                case '.':
                    _p++;
                    sb.Append("(?:").Append(Pair).Append("|[^\\n\\r])");
                    return;
                case '[':
                    ParseClass(sb);
                    return;
                case '\\':
                    ParseEscape(sb);
                    return;
                case ')': case '*': case '+': case '?': case ']': case '{': case '}': case '|':
                    throw new FormatException();
                default:
                    var cp = ReadCodePoint();
                    AppendLiteral(sb, cp);
                    return;
            }
        }

        private int ReadCodePoint()
        {
            var c = _s[_p];
            if (char.IsHighSurrogate(c))
            {
                if (_p + 1 < _s.Length && char.IsLowSurrogate(_s[_p + 1]))
                {
                    var cp = char.ConvertToUtf32(c, _s[_p + 1]);
                    _p += 2;
                    return cp;
                }

                throw new FormatException();
            }

            if (char.IsLowSurrogate(c)) throw new FormatException();
            _p++;
            return c;
        }

        private static void AppendLiteral(StringBuilder sb, int cp)
        {
            if (cp >= 0x10000)
            {
                // Group the pair so a following quantifier applies to the whole character.
                sb.Append("(?:").Append(char.ConvertFromUtf32(cp)).Append(')');
                return;
            }

            sb.Append(Regex.Escape(((char)cp).ToString()));
        }

        /// <summary>SingleCharEsc / CatEsc / ComplCatEsc outside a class.</summary>
        private void ParseEscape(StringBuilder sb)
        {
            _p++; // '\'
            if (!More) throw new FormatException();
            var c = _s[_p];
            switch (c)
            {
                case 'n': sb.Append("\\n"); _p++; return;
                case 'r': sb.Append("\\r"); _p++; return;
                case 't': sb.Append("\\t"); _p++; return;
                case 'p': case 'P':
                    sb.Append(ReadCategory());
                    return;
                default:
                    if (IsSingleCharEscTarget(c))
                    {
                        sb.Append('\\').Append(c);
                        _p++;
                        return;
                    }

                    throw new FormatException();
            }
        }

        // SingleCharEsc = "\" ( %x28-2B / "-" / "." / "?" / %x5B-5E / n / r / t / %x7B-7D )
        private static bool IsSingleCharEscTarget(char c) =>
            c is >= '(' and <= '+' or '-' or '.' or '?' or >= '[' and <= '^' or >= '{' and <= '}';

        private string ReadCategory()
        {
            var negate = _s[_p] == 'P';
            _p++;
            if (!More || _s[_p] != '{') throw new FormatException();
            _p++;
            var close = _s.IndexOf('}', _p);
            if (close < 0) throw new FormatException();
            var prop = _s.Substring(_p, close - _p);
            if (!IsCategory(prop)) throw new FormatException();
            _p = close + 1;
            return (negate ? "\\P{" : "\\p{") + prop + "}";
        }

        private static bool IsCategory(string p) => p switch
        {
            "L" or "Ll" or "Lm" or "Lo" or "Lt" or "Lu" => true,
            "M" or "Mc" or "Me" or "Mn" => true,
            "N" or "Nd" or "Nl" or "No" => true,
            "P" or "Pc" or "Pd" or "Pe" or "Pf" or "Pi" or "Po" or "Ps" => true,
            "Z" or "Zl" or "Zp" or "Zs" => true,
            "S" or "Sc" or "Sk" or "Sm" or "So" => true,
            "C" or "Cc" or "Cf" or "Cn" or "Co" => true,
            _ => false,
        };

        // charClassExpr = "[" [ "^" ] ( "-" / CCE1 ) *CCE1 [ "-" ] "]"
        private void ParseClass(StringBuilder sb)
        {
            _p++; // '['
            var negated = false;
            if (More && _s[_p] == '^') { negated = true; _p++; }

            var bmp = new StringBuilder();
            var astral = new List<string>();
            var first = true;
            while (true)
            {
                if (!More) throw new FormatException();
                var c = _s[_p];
                if (c == ']' && !first) { _p++; break; }
                if (c == ']') throw new FormatException();

                if (c == '-')
                {
                    // A '-' is a literal only as the very first or the very last element of the class.
                    if (first) { _p++; bmp.Append("\\-"); first = false; continue; }
                    if (_p + 1 < _s.Length && _s[_p + 1] == ']') { _p++; bmp.Append("\\-"); continue; }
                    throw new FormatException();
                }

                first = false;
                if (c == '\\' && _p + 1 < _s.Length && _s[_p + 1] is 'p' or 'P')
                {
                    _p++;
                    bmp.Append(ReadCategory());
                    continue;
                }

                var lo = ReadClassChar();
                if (More && _s[_p] == '-' && _p + 1 < _s.Length && _s[_p + 1] != ']')
                {
                    _p++; // '-'
                    var hi = ReadClassChar();
                    if (hi < lo) throw new FormatException();
                    AddRange(bmp, astral, lo, hi);
                }
                else
                {
                    AddRange(bmp, astral, lo, lo);
                }
            }

            var hasBmp = bmp.Length > 0;
            if (!negated)
            {
                if (astral.Count == 0)
                {
                    sb.Append('[').Append(bmp).Append(']');
                    return;
                }

                sb.Append("(?:");
                var parts = new List<string>(astral);
                if (hasBmp) parts.Insert(0, "[" + bmp + "]");
                sb.Append(string.Join("|", parts)).Append(')');
                return;
            }

            // Negated: any one character that is not in the set — an astral character not named in the set is one unit.
            if (astral.Count == 0)
            {
                sb.Append("(?:").Append(Pair).Append("|[^").Append(bmp).Append("])");
                return;
            }

            var excluded = new List<string>(astral);
            if (hasBmp) excluded.Add("[" + bmp + "]");
            sb.Append("(?:(?!").Append(string.Join("|", excluded)).Append(")(?:").Append(Pair).Append("|[\\s\\S]))");
        }

        private int ReadClassChar()
        {
            if (!More) throw new FormatException();
            var c = _s[_p];
            if (c == '\\')
            {
                _p++;
                if (!More) throw new FormatException();
                var e = _s[_p];
                _p++;
                return e switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    _ when IsSingleCharEscTarget(e) => e,
                    _ => throw new FormatException(),
                };
            }

            // CCchar = %x00-2C / %x2E-5A / %x5E-D7FF / %xE000-10FFFF — '-', '[', '\' and ']' are not allowed bare.
            if (c is '-' or '[' or ']') throw new FormatException();
            return ReadCodePoint();
        }

        private static void AddRange(StringBuilder bmp, List<string> astral, int lo, int hi)
        {
            if (lo < 0x10000)
            {
                var bmpHi = Math.Min(hi, 0xFFFF);
                AppendClassChar(bmp, lo);
                if (bmpHi > lo)
                {
                    bmp.Append('-');
                    AppendClassChar(bmp, bmpHi);
                }

                if (hi < 0x10000) return;
                lo = 0x10000;
            }

            AddAstralRange(astral, lo, hi);
        }

        private static void AppendClassChar(StringBuilder sb, int cp)
        {
            // Everything that could be structural inside a .NET class is written as an escape.
            if (cp is '\\' or ']' or '[' or '^' or '-') sb.Append('\\').Append((char)cp);
            else if (cp < 0x20 || cp is >= 0xD800 and <= 0xDFFF) sb.Append("\\u").Append(cp.ToString("X4", CultureInfo.InvariantCulture));
            else sb.Append((char)cp);
        }

        private static void AddAstralRange(List<string> parts, int lo, int hi)
        {
            var loStr = char.ConvertFromUtf32(lo);
            var hiStr = char.ConvertFromUtf32(hi);
            int hsLo = loStr[0], lsLo = loStr[1], hsHi = hiStr[0], lsHi = hiStr[1];
            if (hsLo == hsHi)
            {
                parts.Add(U(hsLo) + "[" + U(lsLo) + "-" + U(lsHi) + "]");
                return;
            }

            parts.Add(U(hsLo) + "[" + U(lsLo) + "-\\uDFFF]");
            if (hsHi - hsLo > 1) parts.Add("[" + U(hsLo + 1) + "-" + U(hsHi - 1) + "][\\uDC00-\\uDFFF]");
            parts.Add(U(hsHi) + "[\\uDC00-" + U(lsHi) + "]");
        }

        private static string U(int unit) => "\\u" + unit.ToString("X4", CultureInfo.InvariantCulture);
    }
}
