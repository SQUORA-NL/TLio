using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Functions.Logic;

/// <summary>
/// Shared implementation of the two-argument comparison predicates.
/// Comparison semantics come from <see cref="NodeComparison"/>, so
/// <c>=greaterThan($.a, 10)</c> means the same thing as a <c>decisionTable</c> cell of <c>&gt; 10</c>.
/// </summary>
public abstract class ComparisonFunctionBase<TNode> : PredicateFunctionBase<TNode>
{
    /// <summary>Decide the outcome from CompareTo-style ordering.</summary>
    protected abstract bool FromComparison(int comparison);

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (!RequireArguments(2, currentNode, context, "two arguments required (left, right)", out var failure))
            return failure;

        // A missing operand cannot be ordered against anything — say so rather than
        // silently answering false, which would read as "the values are ordered".
        if (!TryResolveNode(Arguments[0], currentNode, dataContext, context, out var left) ||
            !TryResolveNode(Arguments[1], currentNode, dataContext, context, out var right))
        {
            context.LogWarning(FunctionName, $"{FunctionName}: an argument path matched nothing.");
            return Boolean(false, context);
        }

        var comparison = NodeComparison.Compare(left, right, context.NodeAdapter);
        if (comparison == null)
        {
            context.LogWarning(FunctionName,
                $"{FunctionName}: values are not comparable by ordering (null, object or array).");
            return Boolean(false, context);
        }

        return Boolean(FromComparison(comparison.Value), context);
    }
}

/// <summary>=greaterThan(a, b) — true when a &gt; b. Numbers compare numerically, text ordinally.</summary>
public class GreaterThanFunction<TNode> : ComparisonFunctionBase<TNode>
{
    public override string FunctionName => "greaterThan";
    protected override bool FromComparison(int comparison) => comparison > 0;
}

/// <summary>=greaterOrEqual(a, b) — true when a &gt;= b.</summary>
public class GreaterOrEqualFunction<TNode> : ComparisonFunctionBase<TNode>
{
    public override string FunctionName => "greaterOrEqual";
    protected override bool FromComparison(int comparison) => comparison >= 0;
}

/// <summary>=lessThan(a, b) — true when a &lt; b.</summary>
public class LessThanFunction<TNode> : ComparisonFunctionBase<TNode>
{
    public override string FunctionName => "lessThan";
    protected override bool FromComparison(int comparison) => comparison < 0;
}

/// <summary>=lessOrEqual(a, b) — true when a &lt;= b.</summary>
public class LessOrEqualFunction<TNode> : ComparisonFunctionBase<TNode>
{
    public override string FunctionName => "lessOrEqual";
    protected override bool FromComparison(int comparison) => comparison <= 0;
}

/// <summary>
/// =between(value, low, high) — true when low &lt;= value &lt;= high. Inclusive on both bounds.
///
/// The numeric sibling <c>isDateBetween</c> never got: same argument order (value first, then
/// the two bounds) and the same inclusivity, so the two read alike. It replaces
/// <c>=and(=greaterOrEqual(v,lo),=lessOrEqual(v,hi))</c>, which is how every band check in the
/// rate tables is written today.
///
/// It differs from <c>isDateBetween</c> in exactly one place, and has to: <c>isDateBetween</c>
/// lives in the TimeDate pack, whose contract is "a path that does not resolve is a failure".
/// This is a core predicate, and a predicate answers rather than aborts — an operand that
/// matched nothing, or that cannot be ordered, is a warning and false, which is precisely what
/// the <c>greaterOrEqual</c>/<c>lessOrEqual</c> pair it replaces already does.
///
/// Bounds are not sorted. <c>=between(5, 10, 1)</c> is false, not true.
/// </summary>
public class BetweenFunction<TNode> : PredicateFunctionBase<TNode>
{
    public override string FunctionName => "between";

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (!RequireArguments(3, currentNode, context, "three arguments required (value, low, high)", out var failure))
            return failure;

        if (!TryResolveNode(Arguments[0], currentNode, dataContext, context, out var value) ||
            !TryResolveNode(Arguments[1], currentNode, dataContext, context, out var low) ||
            !TryResolveNode(Arguments[2], currentNode, dataContext, context, out var high))
        {
            context.LogWarning(FunctionName, $"{FunctionName}: an argument path matched nothing.");
            return Boolean(false, context);
        }

        var fromLow = NodeComparison.Compare(value, low, context.NodeAdapter);
        var toHigh = NodeComparison.Compare(value, high, context.NodeAdapter);
        if (fromLow == null || toHigh == null)
        {
            context.LogWarning(FunctionName,
                $"{FunctionName}: values are not comparable by ordering (null, object or array).");
            return Boolean(false, context);
        }

        return Boolean(fromLow.Value >= 0 && toHigh.Value <= 0, context);
    }
}
