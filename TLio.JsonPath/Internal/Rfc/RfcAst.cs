#nullable disable
namespace TLio.JsonPath.Internal.Rfc;

// The RFC 9535 syntax tree. It knows nothing about any document model: it is built from query
// text alone and is immutable afterwards, which is what makes a parsed query safe to cache and to
// share between threads.

internal abstract class Selector
{
}

internal sealed class NameSelector(string name) : Selector
{
    public readonly string Name = name;
}

internal sealed class WildcardSelector : Selector
{
    public static readonly WildcardSelector Instance = new();
}

internal sealed class IndexSelector(long index) : Selector
{
    public readonly long Index = index;
}

internal sealed class SliceSelector(long? start, long? end, long? step) : Selector
{
    public readonly long? Start = start;
    public readonly long? End = end;
    public readonly long? Step = step;
}

internal sealed class FilterSelector(Expr expression) : Selector
{
    public readonly Expr Expression = expression;
}

internal sealed class Segment(bool descendant, Selector[] selectors)
{
    public readonly bool Descendant = descendant;
    public readonly Selector[] Selectors = selectors;
}

/// <summary>A query: <c>$…</c> (absolute, evaluated from the document root) or <c>@…</c> (relative, from the node under test).</summary>
internal sealed class QueryAst(bool absolute, Segment[] segments)
{
    public readonly bool Absolute = absolute;
    public readonly Segment[] Segments = segments;

    /// <summary>RFC 9535 §2.3.5.1: only child segments, each with exactly one name or index selector — so it yields at most one node.</summary>
    public bool IsSingular
    {
        get
        {
            foreach (var s in Segments)
            {
                if (s.Descendant || s.Selectors.Length != 1) return false;
                if (s.Selectors[0] is not (NameSelector or IndexSelector)) return false;
            }

            return true;
        }
    }
}

/// <summary>The RFC 9535 §2.4.1 types an expression can have.</summary>
internal enum ExprType : byte
{
    Value,
    Logical,
    Nodes,
}

internal abstract class Expr
{
    /// <summary>Zero-based index in the query text where the expression starts; used only in error messages.</summary>
    public int Pos;
}

internal sealed class LiteralExpr(Prim value) : Expr
{
    public readonly Prim Value = value;
}

internal sealed class QueryExpr(QueryAst query) : Expr
{
    public readonly QueryAst Query = query;
    public readonly bool IsSingular = query.IsSingular;
}

internal sealed class FunctionExpr(string name, Expr[] args, BuiltIn builtIn, JsonPathFunction custom, JsonPathFunctionType[] parameterTypes, JsonPathFunctionType returnType) : Expr
{
    public readonly string Name = name;
    public readonly Expr[] Args = args;
    public readonly BuiltIn BuiltIn = builtIn;
    public readonly JsonPathFunction Custom = custom;
    public readonly JsonPathFunctionType[] ParameterTypes = parameterTypes;
    public readonly JsonPathFunctionType ReturnType = returnType;

    /// <summary>For match()/search() whose pattern is a string literal: translated once at parse time (null when the pattern is not valid I-Regexp).</summary>
    public System.Text.RegularExpressions.Regex PrecompiledPattern;
    public bool HasPrecompiledPattern;
}

internal enum BuiltIn : byte
{
    None,
    Length,
    Count,
    Match,
    Search,
    Value,
}

internal sealed class OrExpr(Expr[] operands) : Expr
{
    public readonly Expr[] Operands = operands;
}

internal sealed class AndExpr(Expr[] operands) : Expr
{
    public readonly Expr[] Operands = operands;
}

internal sealed class NotExpr(Expr operand) : Expr
{
    public readonly Expr Operand = operand;
}

internal enum CompareOp : byte
{
    Eq,
    Ne,
    Lt,
    Le,
    Gt,
    Ge,
}

internal sealed class CompareExpr(Expr left, CompareOp op, Expr right) : Expr
{
    public readonly Expr Left = left;
    public readonly CompareOp Op = op;
    public readonly Expr Right = right;
}

/// <summary>A query or a LogicalType/NodesType function used where a logical is expected: true when it is non-empty (or true).</summary>
internal sealed class TestExpr(Expr operand) : Expr
{
    public readonly Expr Operand = operand;
}
