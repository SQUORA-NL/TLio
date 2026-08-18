using System.Globalization;
using TLio.Core.Contracts;

namespace TLio.Core.Models;

/// <summary>
/// The single definition of "is this true?" and "how do two values compare?" for the whole
/// library. The IfElse command, the DecisionTable command and the predicate functions all
/// route through here so a condition means the same thing wherever it is written.
///
/// Comparison order (matches the DecisionTable operator semantics):
///   1. both values read as numbers  → numeric comparison
///   2. both values read as booleans → boolean comparison (equality only)
///   3. either value is null         → null equality
///   4. otherwise                    → ordinal string comparison
/// </summary>
public static class NodeComparison
{
    /// <summary>
    /// Truthiness of a condition node: a boolean value, or the text "true"/"false"
    /// (case-insensitive). Anything else — including numbers — is false.
    /// </summary>
    public static bool IsTruthy<TNode>(TNode node, INodeAdapter<TNode> adapter)
    {
        if (node == null) return false;
        if (adapter.IsNull(node)) return false;

        if (adapter.GetNodeKind(node) == NodeKind.Boolean)
        {
            var boolVal = adapter.TryGetBoolean(node);
            if (boolVal.HasValue) return boolVal.Value;
        }

        var str = adapter.TryGetString(node);
        return str != null && string.Equals(str, "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Compare two nodes. Returns null when they are not comparable by ordering
    /// (e.g. two booleans, or an object) — equality can still be answered via <see cref="AreEqual"/>.
    /// Negative / zero / positive follow the usual CompareTo convention.
    /// </summary>
    public static int? Compare<TNode>(TNode left, TNode right, INodeAdapter<TNode> adapter)
    {
        if (adapter.IsNull(left) || adapter.IsNull(right)) return null;
        if (adapter.IsObject(left) || adapter.IsArray(left)) return null;
        if (adapter.IsObject(right) || adapter.IsArray(right)) return null;

        var leftNum = adapter.TryGetDouble(left);
        var rightNum = adapter.TryGetDouble(right);
        if (leftNum.HasValue && rightNum.HasValue)
            return leftNum.Value.CompareTo(rightNum.Value);

        var leftStr = adapter.TryGetString(left);
        var rightStr = adapter.TryGetString(right);
        if (leftStr == null || rightStr == null) return null;

        return string.Compare(leftStr, rightStr, StringComparison.Ordinal);
    }

    /// <summary>
    /// Equality across types: numbers compare numerically ("1" equals 1), booleans compare
    /// as booleans, nulls are equal to each other, and everything else compares as text.
    /// Objects and arrays compare structurally via the adapter.
    /// </summary>
    public static bool AreEqual<TNode>(TNode left, TNode right, INodeAdapter<TNode> adapter)
    {
        var leftNull = adapter.IsNull(left);
        var rightNull = adapter.IsNull(right);
        if (leftNull || rightNull) return leftNull && rightNull;

        if (adapter.IsObject(left) || adapter.IsArray(left) ||
            adapter.IsObject(right) || adapter.IsArray(right))
            return adapter.DeepEquals(left, right);

        var leftNum = adapter.TryGetDouble(left);
        var rightNum = adapter.TryGetDouble(right);
        if (leftNum.HasValue && rightNum.HasValue)
            return leftNum.Value.Equals(rightNum.Value);

        var leftBool = adapter.TryGetBoolean(left);
        var rightBool = adapter.TryGetBoolean(right);
        if (leftBool.HasValue && rightBool.HasValue)
            return leftBool.Value == rightBool.Value;

        return string.Equals(adapter.TryGetString(left), adapter.TryGetString(right),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Invariant-culture fixed-decimal formatting, shared by the string-producing functions.
    /// Rounds half away from zero (14.5 → "15"), which is what money formatting expects —
    /// .NET's own "F" specifier rounds half to even and would give "14".
    /// </summary>
    public static string FormatNumber(double value, int decimals)
    {
        var rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        return rounded.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture);
    }
}
