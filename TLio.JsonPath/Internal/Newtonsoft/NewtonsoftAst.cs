// The syntax tree of Newtonsoft.Json's JSONPath dialect. It mirrors Newtonsoft's own PathFilter /
// QueryExpression classes one for one (Newtonsoft.Json 13.0.4, MIT licensed, Copyright (c) 2007
// James Newton-King) because the dialect is defined by what those classes do, not by a grammar:
// "bit-for-bit parity" is only achievable by keeping the same moving parts.
#nullable disable
namespace TLio.JsonPath.Internal.Newtonsoft;

internal enum NOp
{
    None = 0,
    Equals = 1,
    NotEquals = 2,
    Exists = 3,
    LessThan = 4,
    LessThanOrEquals = 5,
    GreaterThan = 6,
    GreaterThanOrEquals = 7,
    And = 8,
    Or = 9,
    RegexEquals = 10,
    StrictEquals = 11,
    StrictNotEquals = 12,
}

internal abstract class NFilter
{
}

/// <summary><c>RootFilter</c>: the <c>$</c> that starts a path inside a query expression.</summary>
internal sealed class NRootFilter : NFilter
{
    public static readonly NRootFilter Instance = new();
}

/// <summary><c>FieldFilter</c>: <c>.name</c>, <c>['name']</c>, and (null name) <c>.*</c> — objects only.</summary>
internal sealed class NFieldFilter(string name) : NFilter
{
    public readonly string Name = name;
}

internal sealed class NFieldMultipleFilter(List<string> names) : NFilter
{
    public readonly List<string> Names = names;
}

/// <summary><c>ScanFilter</c>: <c>..name</c> / <c>..*</c>.</summary>
internal sealed class NScanFilter(string name) : NFilter
{
    public readonly string Name = name;
}

internal sealed class NScanMultipleFilter(List<string> names) : NFilter
{
    public readonly List<string> Names = names;
}

/// <summary><c>ArrayIndexFilter</c>: <c>[3]</c>, or (null index) <c>[*]</c> — arrays only.</summary>
internal sealed class NArrayIndexFilter(int? index) : NFilter
{
    public readonly int? Index = index;
}

internal sealed class NArrayMultipleIndexFilter(List<int> indexes) : NFilter
{
    public readonly List<int> Indexes = indexes;
}

internal sealed class NArraySliceFilter(int? start, int? end, int? step) : NFilter
{
    public readonly int? Start = start;
    public readonly int? End = end;
    public readonly int? Step = step;
}

internal sealed class NQueryFilter(NExpression expression) : NFilter
{
    public readonly NExpression Expression = expression;
}

internal sealed class NQueryScanFilter(NExpression expression) : NFilter
{
    public readonly NExpression Expression = expression;
}

internal abstract class NExpression(NOp op)
{
    public readonly NOp Operator = op;
}

internal sealed class NCompositeExpression(NOp op) : NExpression(op)
{
    public readonly List<NExpression> Expressions = new();
}

/// <summary>
/// <c>BooleanQueryExpression</c>. Each side is either a path (<c>List&lt;NFilter&gt;</c>, evaluated against the
/// node under test or the root) or a literal (<see cref="Prim"/>, boxed).
/// </summary>
internal sealed class NBooleanExpression(NOp op, object left, object right) : NExpression(op)
{
    public readonly object Left = left;
    public readonly object Right = right;

    /// <summary>The left side is a literal or a path that selects at most one node by plain names and non-negative indexes.</summary>
    public readonly bool LeftIsSimple = NSimplePath.IsSimple(left);

    /// <summary>As <see cref="LeftIsSimple"/>, for the right side (a missing right side — an existence test — counts as simple).</summary>
    public readonly bool RightIsSimple = right == null || NSimplePath.IsSimple(right);
}

internal static class NSimplePath
{
    /// <summary>
    /// A path made only of <c>$</c>, <c>.name</c> / <c>['name']</c> and <c>[n]</c> with n ≥ 0 selects at most one node and cannot
    /// raise an error (outside ErrorWhenNoMatch), so it can be followed directly instead of through a chain of lazy iterators.
    /// </summary>
    public static bool IsSimple(object side)
    {
        if (side is Prim) return true;
        if (side is not List<NFilter> filters) return false;
        for (var i = 0; i < filters.Count; i++)
        {
            switch (filters[i])
            {
                case NRootFilter when i == 0:
                case NFieldFilter { Name: not null }:
                case NArrayIndexFilter { Index: >= 0 }:
                    break;
                default:
                    return false;
            }
        }

        return true;
    }
}
