using System.Globalization;
using System.Text.RegularExpressions;

namespace TLio.Extensions.Math;

/// <summary>
/// Evaluates JLio-style criteria strings against a value.
///
/// Supported operators: =, !=, &lt;&gt;, &gt;=, &lt;=, &gt;, &lt;
/// Wildcards (equality/inequality only): * (any chars), ? (single char)
///   ~* and ~? are escaped literals.
/// When no operator prefix is present, equality is assumed.
/// Numeric comparison is tried first; string comparison used as fallback.
///
/// Examples:
///   "=active"   → value == "active"
///   ">=5"       → value >= 5
///   "*john*"    → value contains "john" (case-insensitive)
///   "'text'"    → single quotes stripped, value == "text"
///
/// Ported from JLio's ConditionEvaluator.
/// </summary>
public static class ConditionEvaluator
{
    public static bool EvaluateCondition(object? value, string criteria)
    {
        if (string.IsNullOrEmpty(criteria)) return false;

        // Strip surrounding single quotes
        if (criteria.StartsWith("'") && criteria.EndsWith("'") && criteria.Length >= 2)
            criteria = criteria[1..^1];

        // Extract operator prefix
        string op;
        string condValue;

        if (criteria.StartsWith(">="))      { op = ">="; condValue = criteria[2..]; }
        else if (criteria.StartsWith("<=")) { op = "<="; condValue = criteria[2..]; }
        else if (criteria.StartsWith("<>")) { op = "<>"; condValue = criteria[2..]; }
        else if (criteria.StartsWith("!=")) { op = "!="; condValue = criteria[2..]; }
        else if (criteria.StartsWith(">"))  { op = ">";  condValue = criteria[1..]; }
        else if (criteria.StartsWith("<"))  { op = "<";  condValue = criteria[1..]; }
        else if (criteria.StartsWith("="))  { op = "=";  condValue = criteria[1..]; }
        else                                { op = "=";  condValue = criteria; }

        // For equality / inequality, check wildcard first
        if (op is "=" or "<>" or "!=")
        {
            if (condValue.Contains('*') || condValue.Contains('?'))
            {
                var valueStr = value?.ToString() ?? string.Empty;
                bool matches = MatchesWildcard(valueStr, condValue);
                return op == "=" ? matches : !matches;
            }
        }

        var valueString = value?.ToString() ?? string.Empty;

        // Try numeric comparison
        if (double.TryParse(condValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var condNum)
            && double.TryParse(valueString, NumberStyles.Float, CultureInfo.InvariantCulture, out var valueNum))
        {
            return op switch
            {
                ">=" => valueNum >= condNum,
                "<=" => valueNum <= condNum,
                ">"  => valueNum > condNum,
                "<"  => valueNum < condNum,
                "<>" or "!=" => valueNum != condNum,
                _    => valueNum == condNum
            };
        }

        // String comparison (case-insensitive)
        int cmp = string.Compare(valueString, condValue, StringComparison.OrdinalIgnoreCase);
        return op switch
        {
            ">=" => cmp >= 0,
            "<=" => cmp <= 0,
            ">"  => cmp > 0,
            "<"  => cmp < 0,
            "<>" or "!=" => cmp != 0,
            _    => cmp == 0
        };
    }

    private static bool MatchesWildcard(string input, string pattern)
    {
        // Escape regex metacharacters, then restore * → .* and ? → .
        // ~* and ~? are literal * and ? respectively
        var escaped = Regex.Escape(pattern);

        // Temporarily replace escaped ~\* and ~\? sequences
        escaped = escaped
            .Replace(@"\~\*", "\x00STAR\x00")
            .Replace(@"\~\?", "\x00QMARK\x00");

        // Convert wildcards
        escaped = escaped.Replace(@"\*", ".*").Replace(@"\?", ".");

        // Restore literal * and ?
        escaped = escaped
            .Replace("\x00STAR\x00", @"\*")
            .Replace("\x00QMARK\x00", @"\?");

        return Regex.IsMatch(input, "^" + escaped + "$", RegexOptions.IgnoreCase);
    }
}
