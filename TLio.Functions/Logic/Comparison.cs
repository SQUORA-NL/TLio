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
