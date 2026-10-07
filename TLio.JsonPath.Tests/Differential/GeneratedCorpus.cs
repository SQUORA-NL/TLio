using System.Text;

namespace TLio.JsonPath.Tests.Differential;

/// <summary>
/// A seeded generator of queries and documents for differential fuzzing. Queries are assembled from
/// grammar fragments and then — some of them — damaged by a random edit, so the corpus covers the
/// error paths as densely as the success paths. Fixed seeds keep every run reproducible.
/// </summary>
public sealed class GeneratedCorpus
{
    private static readonly string[] Names = ["store", "book", "a", "b", "x", "y", "z", "price", "title", "isbn", "tags", "owner", "name", "age", "id", "a-b", "name with space", "v", "s", "d", "e", "n", "k", "0", "1", "$x", "é", "o'q", "*"];
    private static readonly string[] Ops = ["==", "!=", "<>", "<", "<=", ">", ">=", "===", "!==", "=~"];
    private static readonly string[] Literals =
    [
        "0", "1", "2", "-1", "10", "1.5", "-0.5", "1e2", "100", "9007199254740993", "'a'", "'b'", "'x'", "'1'", "'10'", "''", "'apple'", "'2020-01-01T00:00:00Z'", "'2020-06-15T12:30:45'",
        "true", "false", "null", "/a/", "/^a/i", "/\\d/", "/[a-c]+/", "'abc'", "5", "3",
    ];

    private static readonly string DamageChars = "[]()'\".,:*@$?!=<>&| -0123456789\\/{}a";

    private readonly Random _rng;

    public GeneratedCorpus(int seed) => _rng = new Random(seed);

    private T Pick<T>(IReadOnlyList<T> items) => items[_rng.Next(items.Count)];

    private bool Chance(double p) => _rng.NextDouble() < p;

    // ── queries ───────────────────────────────────────────────────────────────────────────

    public string Query()
    {
        var sb = new StringBuilder("$");
        var segments = _rng.Next(0, 6);
        for (var i = 0; i < segments; i++) sb.Append(Segment(0));
        var q = sb.ToString();
        if (Chance(0.35)) q = Damage(q);
        return q;
    }

    private string Segment(int depth)
    {
        switch (_rng.Next(14))
        {
            case 0: return "." + Pick(Names);
            case 1: return "." + Pick(Names);
            case 2: return ".*";
            case 3: return ".." + Pick(Names);
            case 4: return "..*";
            case 5: return "['" + Pick(Names) + "']";
            case 6: return "['" + Pick(Names) + "','" + Pick(Names) + "']";
            case 7: return "[" + Int() + "]";
            case 8: return "[" + Int() + "," + Int() + "]";
            case 9: return "[" + OptInt() + ":" + OptInt() + (Chance(0.4) ? ":" + OptInt() : "") + "]";
            case 10: return "[*]";
            case 11: return "[?(" + Filter(depth) + ")]";
            case 12: return "..[?(" + Filter(depth) + ")]";
            default: return Chance(0.5) ? "[" + Int() + "]" : "..[" + Int() + "]";
        }
    }

    private string Int() => _rng.Next(-3, 8).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private string OptInt() => Chance(0.35) ? "" : Int();

    private string Path(int depth)
    {
        var sb = new StringBuilder(Chance(0.15) ? "$" : "@");
        var n = _rng.Next(0, 3);
        for (var i = 0; i < n; i++)
        {
            switch (_rng.Next(6))
            {
                case 0: sb.Append('.').Append(Pick(Names)); break;
                case 1: sb.Append('.').Append(Pick(Names)); break;
                case 2: sb.Append("['").Append(Pick(Names)).Append("']"); break;
                case 3: sb.Append('[').Append(Int()).Append(']'); break;
                case 4: sb.Append(".*"); break;
                default: sb.Append("..").Append(Pick(Names)); break;
            }
        }

        if (depth < 1 && Chance(0.06)) sb.Append("[?(").Append(Filter(depth + 1)).Append(")]");
        return sb.ToString();
    }

    private string Filter(int depth)
    {
        var first = Basic(depth);
        if (!Chance(0.3)) return first;
        var sb = new StringBuilder(first);
        var more = _rng.Next(1, 3);
        for (var i = 0; i < more; i++) sb.Append(Chance(0.5) ? " && " : " || ").Append(Basic(depth));
        return sb.ToString();
    }

    private string Basic(int depth)
    {
        switch (_rng.Next(6))
        {
            case 0: return Path(depth);
            case 1: return Path(depth) + " " + Pick(Ops) + " " + Pick(Literals);
            case 2: return Path(depth) + " " + Pick(Ops) + " " + Pick(Literals);
            case 3: return Path(depth) + " " + Pick(Ops) + " " + Path(depth);
            case 4: return Pick(Literals) + " " + Pick(Ops) + " " + Path(depth);
            default: return Path(depth) + Pick(Ops) + Pick(Literals);
        }
    }

    private string Damage(string q)
    {
        if (q.Length == 0) return q;
        var edits = _rng.Next(1, 3);
        for (var e = 0; e < edits; e++)
        {
            var pos = _rng.Next(q.Length + 1);
            switch (_rng.Next(3))
            {
                case 0 when q.Length > 0 && pos < q.Length: q = q.Remove(pos, 1); break;
                case 1: q = q.Insert(pos, DamageChars[_rng.Next(DamageChars.Length)].ToString()); break;
                default:
                    if (pos < q.Length) q = q.Remove(pos, 1).Insert(pos, DamageChars[_rng.Next(DamageChars.Length)].ToString());
                    break;
            }

            if (q.Length == 0) break;
        }

        return q;
    }

    // ── documents ─────────────────────────────────────────────────────────────────────────

    public string Document()
    {
        var sb = new StringBuilder();
        WriteValue(sb, 0);
        return sb.ToString();
    }

    private void WriteValue(StringBuilder sb, int depth)
    {
        var k = depth >= 4 ? _rng.Next(6, 14) : _rng.Next(14);
        switch (k)
        {
            case 0:
            case 1:
            case 2:
                sb.Append('{');
                var keys = new HashSet<string>();
                var n = _rng.Next(0, 6);
                for (var i = 0; i < n; i++)
                {
                    var key = Pick(Names);
                    if (!keys.Add(key)) continue;
                    if (keys.Count > 1) sb.Append(',');
                    sb.Append('"').Append(key.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append("\":");
                    WriteValue(sb, depth + 1);
                }

                sb.Append('}');
                break;
            case 3:
            case 4:
            case 5:
                sb.Append('[');
                var m = _rng.Next(0, 7);
                for (var i = 0; i < m; i++)
                {
                    if (i > 0) sb.Append(',');
                    WriteValue(sb, depth + 1);
                }

                sb.Append(']');
                break;
            case 6: sb.Append(_rng.Next(-3, 12)); break;
            case 7: sb.Append((_rng.Next(-30, 120) / 10.0).ToString("0.0###", System.Globalization.CultureInfo.InvariantCulture)); break;
            case 8: sb.Append(Pick(new[] { "1e2", "100", "9007199254740993", "-0.0", "0", "1", "2", "5", "10", "3" })); break;
            case 9: sb.Append('"').Append(Pick(new[] { "a", "b", "x", "1", "10", "abc", "apple", "", "true", "null", "2020-01-01T00:00:00Z", "2020-06-15T12:30:45", "é" })).Append('"'); break;
            case 10: sb.Append("true"); break;
            case 11: sb.Append("false"); break;
            default: sb.Append("null"); break;
        }
    }
}
